using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.View;

namespace LenovoLegionToolkit.Lib.Utils;

public class FanCurveManager : IDisposable
{
    private readonly PowerModeListener _powerModeListener;
    private readonly PowerModeFeature _powerModeFeature;
    private readonly FanCurveSettings _fanCurveSettings;

    private IExtensionProvider? _extension;
    private bool _pluginLoaded;
    private bool _isThinkBook;
    private bool _isInitialized;
    private bool _isFullSpeedActive;


    private readonly Dictionary<FanType, IFanControlView> _activeViewModels = new();

    public bool IsEnabled { get; private set; }
    public bool IsFanCurveManagerActive { get; private set; }
    public bool IsInGodMode { get; private set; }
    public double? PluginMaxPwm => _extension?.GetData("MaxPwm") is double d ? d : (_extension?.GetData("MaxPwm") is int i ? (double)i : null);

    public FanCurveManager(
        PowerModeListener powerModeListener,
        PowerModeFeature powerModeFeature,
        FanCurveSettings fanCurveSettings)
    {
        Log.Instance.Trace($"FanCurveManager instance created.");
        _powerModeListener = powerModeListener;
        _powerModeFeature = powerModeFeature;
        _fanCurveSettings = fanCurveSettings;
    }

    public async Task<bool> IsSupportedAsync()
    {
        if (_extension != null) return true;

        await Task.Run(LoadPlugin).ConfigureAwait(false);

        IsEnabled = _extension != null;
        Log.Instance.Trace($"State: {IsEnabled}");

        return IsEnabled;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        Log.Instance.Trace($"InitializeAsync called.");

        bool isSupported = await IsSupportedAsync().ConfigureAwait(false);
        if (!isSupported)
        {
            Log.Instance.Trace($"No extension found during Initialize.");
            return;
        }

        _extension!.Initialize(this);
        Log.Instance.Trace($"FanCurveManager initialized with extension.");

        if (PluginMaxPwm is { } pluginMax)
        {
            foreach (FanType fanType in Enum.GetValues(typeof(FanType)))
            {
                if (GetEntry(fanType) is { } entry && !entry.IsMaxPwmUserModified)
                {
                    entry.MaxPwm = pluginMax;
                    entry.IsMaxPwmUserModified = false;
                }
            }
        }

        var entries = await EnsureEntriesAsync().ConfigureAwait(false);
        foreach (var entry in entries)
        {
            AddEntry(entry);
            UpdateConfig(entry.Type, entry);
        }

        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        _isThinkBook = mi.LegionSeries == LegionSeries.ThinkBook;

        if (!_isThinkBook)
        {
            _powerModeListener.Changed += OnPowerModeChanged;
            var currentState = await _powerModeFeature.GetStateAsync().ConfigureAwait(false);
            await ApplyStateLogicAsync(currentState).ConfigureAwait(false);
        }

        _isInitialized = true;
    }

    private Task<List<FanCurveEntry>> EnsureEntriesAsync()
    {
        var entries = _fanCurveSettings.Store.Entries;
        if (entries.Count > 0)
        {
            Log.Instance.Trace($"Using {entries.Count} existing fan curve entr{(entries.Count == 1 ? "y" : "ies")} from settings and filling missing fan types with defaults if needed.");
        }

        foreach (FanType fanType in Enum.GetValues(typeof(FanType)))
        {
            try
            {
                EnsureEntryEx(fanType, fanTableInfo: null, syncExtension: false);
            }
            catch (Exception ex)
            {
                Log.Instance.Trace($"Failed to ensure fan curve entry for {fanType}: {ex}");
            }
        }

        return Task.FromResult(entries);
    }

    private async void OnPowerModeChanged(object? sender, PowerModeListener.ChangedEventArgs e)
    {
        if (_isThinkBook) return;

        try
        {
            await ApplyStateLogicAsync(e.State).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Error handling PowerMode change: {ex}");
        }
    }

    private async Task ApplyStateLogicAsync(PowerModeState state)
    {
        IsInGodMode = state == PowerModeState.GodMode;
        Log.Instance.Trace($"FanCurveManager power mode state changed. [IsInGodMode={IsInGodMode}, IsFanCurveManagerActive={IsFanCurveManagerActive}, powerMode={state}]");

        if (IsInGodMode)
        {
            Log.Instance.Trace($"PowerMode is GodMode. Enabling custom fan control.");
            await SetRegisterAsync(true).ConfigureAwait(false);
        }
        else
        {
            Log.Instance.Trace($"PowerMode is {state}. Disabling custom fan control.");
            await SetRegisterAsync(false).ConfigureAwait(false);
        }
    }

    private void LoadPlugin()
    {
        if (_pluginLoaded)
        {
            Log.Instance.Trace($"Plugin loaded.");
            return;
        }

        _pluginLoaded = true;

        try
        {
            var pluginDir = Path.Combine(Folders.AppData, "Plugins");
            Log.Instance.Trace($"Scanning for plugins in: {pluginDir} (Full: {Path.GetFullPath(pluginDir)})");

            if (!Directory.Exists(pluginDir))
            {
                Log.Instance.Trace($"Plugin directory does not exist.");
                return;
            }

            var dlls = Directory.GetFiles(pluginDir, "*.dll");
            Log.Instance.Trace($"Found {dlls.Length} DLL(s) in plugin directory.");

            foreach (var dll in dlls)
            {
                if (TryLoadPlugin(dll))
                {
                    Log.Instance.Trace($"Successfully loaded extension from {dll}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Error during plugin scanning: {ex}");
        }
    }

    private bool TryLoadPlugin(string path)
    {
        ResolveEventHandler resolver = (_, args) =>
            args.Name.Contains(typeof(IExtensionProvider).Assembly.GetName().Name!) ? typeof(IExtensionProvider).Assembly : null;

        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += resolver;
            Log.Instance.Trace($"Attempting to load: {path}");
            var assembly = Assembly.LoadFrom(path);
            var types = assembly.GetTypes();
            Log.Instance.Trace($"Assembly loaded. Found {types.Length} types.");

            var type = types.FirstOrDefault(t => typeof(IExtensionProvider).IsAssignableFrom(t) && !t.IsInterface);
            if (type != null)
            {
                Log.Instance.Trace($"Found provider type: {type.FullName}");
                _extension = (IExtensionProvider?)Activator.CreateInstance(type);
                return _extension != null;
            }

            Log.Instance.Trace($"No valid IExtensionProvider implementation found in this assembly.");
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to load plugin assembly: {path}. Error: {ex}");
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolver;
        }
        return false;
    }

    public void RegisterViewModel(FanType type, IFanControlView vm)
    {
        if (!IsEnabled) return;
        lock (_activeViewModels)
        {
            _activeViewModels[type] = vm;
        }
    }

    public void UnregisterViewModel(FanType type, IFanControlView vm)
    {
        if (!IsEnabled) return;
        lock (_activeViewModels)
        {
            if (_activeViewModels.TryGetValue(type, out var current) && current == vm) _activeViewModels.Remove(type);
        }
    }

    public void UpdateMonitoring(FanType type, float temp, int rpm, int pwm)
    {
        lock (_activeViewModels)
        {
            if (_activeViewModels.TryGetValue(type, out var vm))
            {
                vm.UpdateMonitoring(temp, rpm, (byte)pwm);
            }
        }
    }

    public async Task LoadAndApply(List<FanCurveEntry> entries, bool isFullSpeed = false)
    {
        if (_extension == null) return;

        foreach (var entry in entries)
        {
            AddEntry(entry);
            UpdateConfig(entry.Type, entry);
        }

        if (_isThinkBook)
        {
            await SetRegisterAsync(true).ConfigureAwait(false);
        }
        else
        {
            var currentState = await _powerModeFeature.GetStateAsync().ConfigureAwait(false);
            await ApplyStateLogicAsync(currentState).ConfigureAwait(false);
        }

        if (isFullSpeed)
        {
            await SetFullSpeedAsync(true).ConfigureAwait(false);
        }
    }

    public async Task SetRegisterAsync(bool flag = false)
    {
        IsFanCurveManagerActive = flag;
        Log.Instance.Trace($"FanCurveManager register state update. [IsFanCurveManagerActive={IsFanCurveManagerActive}, IsInGodMode={IsInGodMode}, requested={flag}]");

        if (!IsEnabled) return;
        if (_extension != null)
        {
            await _extension.ExecuteAsync("SetRegister", flag).ConfigureAwait(false);
        }
    }


    public FanCurveEntry? GetEntry(FanType type) => _extension?.GetData($"Entry_{type}") as FanCurveEntry;

    public FanCurveEntry EnsureEntry(FanType type, FanTableInfo fanTableInfo) => EnsureEntryEx(type, fanTableInfo, syncExtension: true);

    private FanCurveEntry EnsureEntryEx(FanType type, FanTableInfo? fanTableInfo, bool syncExtension)
    {
        if (_fanCurveSettings.Store.Entries.FirstOrDefault(e => e.Type == type) is { } storedEntry)
        {
            if (syncExtension)
            {
                AddEntry(storedEntry);
                UpdateConfig(type, storedEntry);
            }

            return storedEntry;
        }

        if (GetEntry(type) is { } existingEntry)
        {
            _fanCurveSettings.Store.Entries.RemoveAll(e => e.Type == type);
            _fanCurveSettings.Store.Entries.Add(existingEntry);
            _fanCurveSettings.SynchronizeStore();

            if (syncExtension)
            {
                AddEntry(existingEntry);
                UpdateConfig(type, existingEntry);
            }

            Log.Instance.Trace($"Recovered missing fan curve entry for {type} from plugin state.");
            return existingEntry;
        }

        FanCurveEntry entry;
        if (fanTableInfo is { Data: { Length: > 0 } })
        {
            entry = FanCurveEntry.FromFanTableInfo(fanTableInfo.Value, (ushort)type);
            Log.Instance.Trace($"Created missing fan curve entry for {type} from provided FanTableInfo.");
        }
        else
        {
            entry = new FanCurveEntry { Type = type };
            Log.Instance.Trace($"Created missing fan curve entry for {type} from default fan_curve settings template.");
        }

        _fanCurveSettings.Store.Entries.RemoveAll(e => e.Type == type);
        _fanCurveSettings.Store.Entries.Add(entry);
        _fanCurveSettings.SynchronizeStore();

        if (syncExtension)
        {
            AddEntry(entry);
            UpdateConfig(type, entry);
        }

        return entry;
    }


    public void SaveEntries(IEnumerable<FanCurveEntry> entries, bool? isFullSpeed = null)
    {
        _fanCurveSettings.Store.Entries.Clear();
        _fanCurveSettings.Store.Entries.AddRange(entries);

        if (isFullSpeed.HasValue)
        {
            _fanCurveSettings.Store.IsFullSpeed = isFullSpeed.Value;
        }

        _fanCurveSettings.Save();
    }

    public void AddEntry(FanCurveEntry entry)
    {
        if (!entry.IsMaxPwmUserModified && PluginMaxPwm is { } pluginMax)
        {
            entry.MaxPwm = pluginMax;
            entry.IsMaxPwmUserModified = false;
        }
        _extension?.ExecuteAsync("AddEntry", entry);
    }

    public async Task SetFullSpeedAsync(bool enabled)
    {
        _isFullSpeedActive = enabled;

        if (!IsEnabled) return;
        if (_extension != null)
        {
            await _extension.ExecuteAsync("SetFullSpeed", enabled).ConfigureAwait(false);
        }
    }

    public void UpdateGlobalSettings(FanCurveEntry sourceEntry) => _extension?.ExecuteAsync("UpdateGlobal", sourceEntry);


    public void UpdateConfig(FanType type, FanCurveEntry entry) => _extension?.ExecuteAsync("UpdateConfig", type, entry);

    public void Dispose()
    {
        _extension?.Dispose();
        _powerModeListener.Changed -= OnPowerModeChanged;
    }

}
