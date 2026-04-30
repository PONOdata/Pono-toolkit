using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Controls;
using LenovoLegionToolkit.WPF.Controls.Custom;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using static LenovoLegionToolkit.Lib.Settings.GodModeSettings;

namespace LenovoLegionToolkit.WPF.Windows.Settings;

public partial class WindowsPowerPlansWindow
{
    private static readonly WindowsPowerPlan DefaultValue = new(Guid.Empty, Resource.WindowsPowerPlansWindow_DefaultPowerPlan, false);
    private static readonly Guid BalancedPowerPlanGuid = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");

    private readonly WindowsPowerPlanController _windowsPowerPlanController = IoCContainer.Resolve<WindowsPowerPlanController>();
    private readonly PowerModeFeature _powerModeFeature = IoCContainer.Resolve<PowerModeFeature>();
    private readonly ApplicationSettings _settings = IoCContainer.Resolve<ApplicationSettings>();
    private readonly GodModeController _godModeController = IoCContainer.Resolve<GodModeController>();
    private readonly GodModeSettings _godModeSettings = IoCContainer.Resolve<GodModeSettings>();

    private bool IsRefreshing => _loader.IsLoading;
    private Guid? _singlePresetGuid;

    public WindowsPowerPlansWindow()
    {
        InitializeComponent();
        IsVisibleChanged += PowerPlansWindow_IsVisibleChanged;
    }

    private async void PowerPlansWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _loader.IsLoading = true;

        var loadingTask = Task.Delay(500);

        var compatibility = await Compatibility.GetMachineInformationAsync();
        _aoAcWarningCard.Visibility = compatibility.Properties.SupportsAlwaysOnAc.status
            ? Visibility.Visible
            : Visibility.Collapsed;

        var powerPlans = _windowsPowerPlanController.GetPowerPlans().OrderBy(x => x.Name).Prepend(DefaultValue).ToArray();
        var powerModes = Enum.GetValues<WindowsPowerMode>();

        RefreshMode(_quietModeComboBox, _quietOverlayContainer, powerPlans, powerModes, PowerModeState.Quiet);
        RefreshMode(_balanceModeComboBox, _balanceOverlayContainer, powerPlans, powerModes, PowerModeState.Balance);
        RefreshMode(_performanceModeComboBox, _performanceOverlayContainer, powerPlans, powerModes, PowerModeState.Performance);

        var allStates = await _powerModeFeature.GetAllStatesAsync();
        if (allStates.Contains(PowerModeState.Extreme))
            RefreshMode(_extremeModeComboBox, _extremeOverlayContainer, powerPlans, powerModes, PowerModeState.Extreme);
        else
            _extremeModeComboBox.Visibility = Visibility.Collapsed;

        if (allStates.Contains(PowerModeState.GodMode))
        {
            _godModeCardControl.Visibility = Visibility.Visible;

            var controller = await _godModeController.GetControllerAsync().ConfigureAwait(false);
            var presets = await controller.GetGodModePresetsAsync().ConfigureAwait(false);

            if (presets.Count > 1)
            {
                _godModeComboBox.Visibility = Visibility.Collapsed;
                _godModeOverlayContainer.Visibility = Visibility.Collapsed;
                _godModePresetsContainer.Visibility = Visibility.Visible;
                _godModePresetsContainer.Children.Clear();
                _singlePresetGuid = null;

                foreach (var preset in presets)
                    BuildGodModePresetCard(powerPlans, powerModes, preset);
            }
            else
            {
                _godModePresetsContainer.Visibility = Visibility.Collapsed;
                _godModeComboBox.Visibility = Visibility.Visible;

                var singlePreset = presets.FirstOrDefault();
                _singlePresetGuid = singlePreset.Key;

                RefreshMode(_godModeComboBox, _godModeOverlayContainer, powerPlans, powerModes, PowerModeState.GodMode,
                    savedPlan: singlePreset.Value?.Overrides.TryGetGuid(PowerOverrideKey.PowerPlan),
                    savedAc: singlePreset.Value?.Overrides.TryGetEnum<WindowsPowerMode>(PowerOverrideKey.PowerPlanBalanceOnAc),
                    savedDc: singlePreset.Value?.Overrides.TryGetEnum<WindowsPowerMode>(PowerOverrideKey.PowerPlanBalanceOnDc),
                    saveOverlay: async (mode, isAc) => await GodModePresetBalanceOverlayChangedAsync(_singlePresetGuid.Value.ToString(), mode, isAc));
            }
        }
        else
        {
            _godModeCardControl.Visibility = Visibility.Collapsed;
        }

        await loadingTask;
        _loader.IsLoading = false;
    }

    private void RefreshMode(ComboBox modeCombo, StackPanel overlayContainer,
        WindowsPowerPlan[] powerPlans, WindowsPowerMode[] powerModes, PowerModeState state,
        Guid? savedPlan = null, WindowsPowerMode? savedAc = null, WindowsPowerMode? savedDc = null,
        Func<WindowsPowerMode, bool, Task>? saveOverlay = null)
    {
        Guid settingsPowerPlanGuid;
        if (savedPlan.HasValue)
            settingsPowerPlanGuid = savedPlan.Value;
        else if (!_settings.Store.PowerPlans.TryGetValue(state, out settingsPowerPlanGuid))
            settingsPowerPlanGuid = Guid.Empty;
        var selectedPlan = powerPlans.FirstOrDefault(pp => pp.Guid == settingsPowerPlanGuid);
        var effectivePlan = (selectedPlan == default(WindowsPowerPlan)) ? DefaultValue : selectedPlan;
        modeCombo.SetItems(powerPlans, effectivePlan, pp => pp.Name);

        var isBalanced = IsBalancedPlan(effectivePlan.Guid);

        overlayContainer.Children.Clear();

        var ac = savedAc ?? (_settings.Store.Overrides.GetPowerPlanBalanceOnAc(state) ?? WindowsPowerMode.Balanced);
        var dc = savedDc ?? (_settings.Store.Overrides.GetPowerPlanBalanceOnDc(state) ?? WindowsPowerMode.Balanced);

        var (row, acCombo, dcCombo) = BuildBalanceOverlayRow(powerModes, ac, dc);

        acCombo.SelectionChanged += async (_, _) =>
        {
            if (acCombo.TryGetSelectedItem(out WindowsPowerMode mode))
            {
                if (saveOverlay != null)
                    await saveOverlay(mode, true);
                else
                    await BalanceOverlayChangedAsync(mode, state, isAc: true);
            }
        };
        dcCombo.SelectionChanged += async (_, _) =>
        {
            if (dcCombo.TryGetSelectedItem(out WindowsPowerMode mode))
            {
                if (saveOverlay != null)
                    await saveOverlay(mode, false);
                else
                    await BalanceOverlayChangedAsync(mode, state, isAc: false);
            }
        };

        overlayContainer.Children.Add(row);

        overlayContainer.Visibility = isBalanced ? Visibility.Visible : Visibility.Collapsed;
    }
    private static (StackPanel Row, ComboBox AcCombo, ComboBox DcCombo) BuildBalanceOverlayRow(
        WindowsPowerMode[] powerModes, WindowsPowerMode savedAc, WindowsPowerMode savedDc)
    {
        var grayBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 6, 0, 0)
        };

        var title = new TextBlock
        {
            Text = Resource.WindowsPowerPlansWindow_PowerMode_Title,
            FontSize = 11,
            Foreground = grayBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };

        var acLabel = new TextBlock
        {
            Text = Resource.WindowsPowerPlansWindow_PowerMode_AC,
            FontSize = 11,
            Foreground = grayBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        var acCombo = new ComboBox { Width = 120, MaxDropDownHeight = 300 };
        acCombo.SetItems(powerModes, savedAc, pm => pm.GetDisplayName());

        var dcLabel = new TextBlock
        {
            Text = Resource.WindowsPowerPlansWindow_PowerMode_DC,
            FontSize = 11,
            Foreground = grayBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 4, 0)
        };
        var dcCombo = new ComboBox { Width = 120, MaxDropDownHeight = 300 };
        dcCombo.SetItems(powerModes, savedDc, pm => pm.GetDisplayName());

        row.Children.Add(title);
        row.Children.Add(acLabel);
        row.Children.Add(acCombo);
        row.Children.Add(dcLabel);
        row.Children.Add(dcCombo);

        return (row, acCombo, dcCombo);
    }

    private void BuildGodModePresetCard(WindowsPowerPlan[] powerPlans, WindowsPowerMode[] powerModes,
        KeyValuePair<Guid, GodModeSettingsStore.Preset> preset)
    {
        var cardControl = new CardControl { Margin = new Thickness(0, 8, 0, 8) };
        var headerControl = new CardHeaderControl { Title = preset.Value.Name };
        cardControl.Header = headerControl;

        var comboBox = new ComboBox
        {
            MinWidth = 300,
            Margin = new Thickness(0, 8, 0, 8),
            Tag = preset.Key,
            MaxDropDownHeight = 300
        };

        var currentPowerPlanGuid = GetGodModePresetPowerPlan(preset.Key.ToString());
        var selectedPlanGuid = currentPowerPlanGuid ?? Guid.Empty;
        var selectedPlan = powerPlans.FirstOrDefault(pp => pp.Guid == selectedPlanGuid);
        var effectivePlan = (selectedPlan == default(WindowsPowerPlan)) ? DefaultValue : selectedPlan;
        comboBox.SetItems(powerPlans, effectivePlan, pp => pp.Name);

        var savedAc = preset.Value.Overrides.TryGetEnum<WindowsPowerMode>(PowerOverrideKey.PowerPlanBalanceOnAc) ?? WindowsPowerMode.Balanced;
        var savedDc = preset.Value.Overrides.TryGetEnum<WindowsPowerMode>(PowerOverrideKey.PowerPlanBalanceOnDc) ?? WindowsPowerMode.Balanced;
        var (overlayRow, acCombo, dcCombo) = BuildBalanceOverlayRow(powerModes, savedAc, savedDc);
        overlayRow.Visibility = IsBalancedPlan(effectivePlan.Guid) ? Visibility.Visible : Visibility.Collapsed;

        comboBox.SelectionChanged += async (_, _) =>
        {
            if (comboBox.TryGetSelectedItem(out WindowsPowerPlan plan))
            {
                overlayRow.Visibility = IsBalancedPlan(plan.Guid) ? Visibility.Visible : Visibility.Collapsed;
                await GodModePresetPowerPlanChangedAsync(preset.Key.ToString(), plan);
            }
        };
        acCombo.SelectionChanged += async (_, _) =>
        {
            if (acCombo.TryGetSelectedItem(out WindowsPowerMode mode))
                await GodModePresetBalanceOverlayChangedAsync(preset.Key.ToString(), mode, isAc: true);
        };
        dcCombo.SelectionChanged += async (_, _) =>
        {
            if (dcCombo.TryGetSelectedItem(out WindowsPowerMode mode))
                await GodModePresetBalanceOverlayChangedAsync(preset.Key.ToString(), mode, isAc: false);
        };

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(comboBox);
        stackPanel.Children.Add(overlayRow);

        cardControl.Content = stackPanel;
        _godModePresetsContainer.Children.Add(cardControl);
    }

    private async void QuietModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_quietModeComboBox.TryGetSelectedItem(out WindowsPowerPlan plan))
        {
            _quietOverlayContainer.Visibility = IsBalancedPlan(plan.Guid) ? Visibility.Visible : Visibility.Collapsed;
            await WindowsPowerPlanChangedAsync(plan, PowerModeState.Quiet);
        }
    }

    private async void BalanceModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_balanceModeComboBox.TryGetSelectedItem(out WindowsPowerPlan plan))
        {
            _balanceOverlayContainer.Visibility = IsBalancedPlan(plan.Guid) ? Visibility.Visible : Visibility.Collapsed;
            await WindowsPowerPlanChangedAsync(plan, PowerModeState.Balance);
        }
    }

    private async void PerformanceModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_performanceModeComboBox.TryGetSelectedItem(out WindowsPowerPlan plan))
        {
            _performanceOverlayContainer.Visibility = IsBalancedPlan(plan.Guid) ? Visibility.Visible : Visibility.Collapsed;
            await WindowsPowerPlanChangedAsync(plan, PowerModeState.Performance);
        }
    }

    private async void ExtremeModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_extremeModeComboBox.TryGetSelectedItem(out WindowsPowerPlan plan))
        {
            _extremeOverlayContainer.Visibility = IsBalancedPlan(plan.Guid) ? Visibility.Visible : Visibility.Collapsed;
            await WindowsPowerPlanChangedAsync(plan, PowerModeState.Extreme);
        }
    }

    private async void GodModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_godModeComboBox.TryGetSelectedItem(out WindowsPowerPlan plan))
        {
            _godModeOverlayContainer.Visibility = IsBalancedPlan(plan.Guid) ? Visibility.Visible : Visibility.Collapsed;

            if (_singlePresetGuid.HasValue)
                await GodModePresetPowerPlanChangedAsync(_singlePresetGuid.Value.ToString(), plan);
            else
                await WindowsPowerPlanChangedAsync(plan, PowerModeState.GodMode);
        }
    }

    private static bool IsBalancedPlan(Guid guid) =>
        guid == BalancedPowerPlanGuid;

    private Guid? GetGodModePresetPowerPlan(string presetKey)
    {
        if (Guid.TryParse(presetKey, out var presetGuid) &&
            _godModeSettings.Store.Presets.TryGetValue(presetGuid, out var preset))
        {
            var powerPlanGuid = preset.Overrides.TryGetGuid(PowerOverrideKey.PowerPlan);
            if (powerPlanGuid != null)
                return powerPlanGuid;
        }
        if (!_settings.Store.PowerPlans.TryGetValue(PowerModeState.GodMode, out var globalGuid))
            return Guid.Empty;
        return globalGuid;
    }

    private async Task WindowsPowerPlanChangedAsync(WindowsPowerPlan windowsPowerPlan, PowerModeState powerModeState, GodModeSettingsStore.Preset? preset = null)
    {
        if (IsRefreshing)
            return;

        if (preset == null)
        {
            _settings.Store.PowerPlans[powerModeState] = windowsPowerPlan.Guid;
            if (windowsPowerPlan.Guid != Guid.Empty && !IsBalancedPlan(windowsPowerPlan.Guid))
            {
                _settings.Store.Overrides.SetPowerPlanBalanceOnAc(powerModeState, null);
                _settings.Store.Overrides.SetPowerPlanBalanceOnDc(powerModeState, null);
            }
        }
        else
        {
            var powerPlan = preset.Overrides.TryGetGuid(PowerOverrideKey.PowerPlan);
            if (powerPlan == null)
                return;
            _settings.Store.PowerPlans[powerModeState] = powerPlan.Value;
        }

        _settings.SynchronizeStore();
        await _powerModeFeature.EnsureCorrectWindowsPowerSettingsAreSetAsync(preset);
    }

    private async Task BalanceOverlayChangedAsync(WindowsPowerMode selectedMode, PowerModeState powerModeState, bool isAc)
    {
        if (IsRefreshing)
            return;

        if (isAc)
            _settings.Store.Overrides.SetPowerPlanBalanceOnAc(powerModeState, selectedMode);
        else
            _settings.Store.Overrides.SetPowerPlanBalanceOnDc(powerModeState, selectedMode);

        _settings.SynchronizeStore();
        await _powerModeFeature.EnsureCorrectWindowsPowerSettingsAreSetAsync();
    }

    private async Task GodModePresetPowerPlanChangedAsync(string presetKey, WindowsPowerPlan windowsPowerPlan)
    {
        if (IsRefreshing)
            return;

        var presetKvp = _godModeSettings.Store.Presets.FirstOrDefault(profile => profile.Key.ToString() == presetKey);

        if (!presetKvp.Equals(default(KeyValuePair<Guid, GodModeSettingsStore.Preset>)) && presetKvp.Value != null)
        {
            var newOv = new Dictionary<PowerOverrideKey, string>(presetKvp.Value.Overrides ?? []) { [PowerOverrideKey.PowerPlan] = windowsPowerPlan.Guid.ToString() };
            if (windowsPowerPlan.Guid != Guid.Empty && !IsBalancedPlan(windowsPowerPlan.Guid))
            {
                newOv.Remove(PowerOverrideKey.PowerPlanBalanceOnAc);
                newOv.Remove(PowerOverrideKey.PowerPlanBalanceOnDc);
            }
            var updated = presetKvp.Value with { Overrides = newOv };
            _godModeSettings.Store.Presets[presetKvp.Key] = updated;
            _godModeSettings.SynchronizeStore();

            await WindowsPowerPlanChangedAsync(windowsPowerPlan, PowerModeState.GodMode, updated);
        }
    }

    private async Task GodModePresetBalanceOverlayChangedAsync(string presetKey, WindowsPowerMode selectedMode, bool isAc)
    {
        if (IsRefreshing)
            return;

        var presetKvp = _godModeSettings.Store.Presets.FirstOrDefault(profile => profile.Key.ToString() == presetKey);

        if (!presetKvp.Equals(default(KeyValuePair<Guid, GodModeSettingsStore.Preset>)) && presetKvp.Value != null)
        {
            var newOv = new Dictionary<PowerOverrideKey, string>(presetKvp.Value.Overrides ?? []);
            if (isAc)
                newOv[PowerOverrideKey.PowerPlanBalanceOnAc] = selectedMode.ToString();
            else
                newOv[PowerOverrideKey.PowerPlanBalanceOnDc] = selectedMode.ToString();
            var updated = presetKvp.Value with { Overrides = newOv };
            _godModeSettings.Store.Presets[presetKvp.Key] = updated;
            _godModeSettings.SynchronizeStore();

            await _powerModeFeature.EnsureCorrectWindowsPowerSettingsAreSetAsync(updated);
        }
    }
}
