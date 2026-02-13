using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Lights;
using Windows.System;
using Windows.UI;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.Lib.Utils.LampEffects;
using NeoSmart.AsyncLock;

namespace LenovoLegionToolkit.Lib.Controllers;

public class LampArrayController : IDisposable
{
    private readonly AsyncLock _lock = new();
    private readonly Dictionary<string, LampArrayDevice> _lampArrays = [];
    private DeviceWatcher? _watcher;
    private bool _isDisposed;

    private double _brightness = 1.0;
    private double _speed = 1.0;
    private bool _smoothTransition = true;

    private ILampEffect? _currentEffect;
    private ILampEffect? _targetEffect;
    private double _transitionStartTime;
    private double _transitionDuration;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private readonly global::System.Collections.Concurrent.ConcurrentDictionary<int, ILampEffect> _effectOverrides = new();
    private readonly global::System.Collections.Concurrent.ConcurrentDictionary<int, Color> _lastFrameColors = new();

    private class LampArrayDevice
    {
        public LampArray Device { get; }
        private Dictionary<VirtualKey, List<int>> VirtualKeyToIndex { get; } = new();

        public LampArrayDevice(LampArray device)
        {
            Device = device;
            Log.Instance.Trace($"Initializing device: {device.DeviceId}, Lamp count: {device.LampCount}");
        }

        public void SetLayout(int width, int height, IEnumerable<(ushort Code, int X, int Y)> keys)
        {
        }
    }

    public double Brightness
    {
        get => _brightness;
        set => _brightness = Math.Clamp(value, 0.0, 1.0);
    }

    public double Speed
    {
        get => _speed;
        set => _speed = Math.Clamp(value, 0.1, 5.0);
    }

    public bool SmoothTransition
    {
        get => _smoothTransition;
        set => _smoothTransition = value;
    }

    public bool IsAvailable
    {
        get
        {
            lock (_lampArrays)
            {
                foreach (var kvp in _lampArrays)
                    if (kvp.Value.Device.IsAvailable)
                        return true;
                return false;
            }
        }
    }

    public async Task StartAsync()
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
        {
            if (_watcher is not null)
                return;

            Log.Instance.Trace($"Starting LampArray device watcher...");

            var selector = LampArray.GetDeviceSelector();
            _watcher = DeviceInformation.CreateWatcher(selector);

            _watcher.Added += Watcher_Added;
            _watcher.Removed += Watcher_Removed;
            _watcher.EnumerationCompleted += Watcher_EnumerationCompleted;
            _watcher.Start();

            Log.Instance.Trace($"LampArray device watcher started.");
        }
    }

    private void Watcher_EnumerationCompleted(DeviceWatcher sender, object args)
    {
        lock (_lampArrays)
        {
            Log.Instance.Trace($"LampArray device enumeration completed. Devices found: {_lampArrays.Count}");
        }
    }

    public async Task StopAsync()
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
        {
            if (_watcher is null)
                return;

            Log.Instance.Trace($"Stopping LampArray device watcher...");

            _watcher.Added -= Watcher_Added;
            _watcher.Removed -= Watcher_Removed;
            _watcher.EnumerationCompleted -= Watcher_EnumerationCompleted;
            _watcher.Stop();
            _watcher = null;

            lock (_lampArrays)
            {
                foreach (var device in _lampArrays.Values)
                    device.Device.AvailabilityChanged -= LampArray_AvailabilityChanged;
                _lampArrays.Clear();
            }

            Log.Instance.Trace($"LampArray device watcher stopped.");
        }
    }

    public void SetLayout(int width, int height, IEnumerable<(ushort Code, int X, int Y)> keys)
    {
        lock (_lampArrays)
        {
            foreach (var kvp in _lampArrays) kvp.Value.SetLayout(width, height, keys);
        }
    }

    public IEnumerable<(string DeviceId, LampInfo Info)> GetLamps()
    {
        lock (_lampArrays)
        {
            foreach (var kvp in _lampArrays)
            {
                var device = kvp.Value.Device;
                if (!device.IsAvailable) continue;

                for (var i = 0; i < device.LampCount; i++) yield return (device.DeviceId, device.GetLampInfo(i));
            }
        }
    }

    public void SetAllLampsColor(Color color)
    {
        if (!IsAvailable)
        {
            Log.Instance.Trace($"SetAllLampsColor failed: Controller not available.");
            return;
        }

        lock (_lampArrays)
        {
            Log.Instance.Trace($"SetAllLampsColor: RGB({color.R},{color.G},{color.B}) on {_lampArrays.Count} devices.");
            foreach (var kvp in _lampArrays)
            {
                if (!kvp.Value.Device.IsAvailable)
                {
                    Log.Instance.Trace($"Device {kvp.Key} is not available.");
                    continue;
                }

                try
                {
                    Log.Instance.Trace($"Setting all {kvp.Value.Device.LampCount} lamps on {kvp.Key} to color.");
                    kvp.Value.Device.SetColor(color);
                }
                catch (Exception ex)
                {
                    Log.Instance.Trace($"Failed to set all lamps color on {kvp.Key}: {ex.Message}");
                }
            }
        }
    }

    public void SetLampColors(Dictionary<int, Color> lampColors)
    {
        if (!IsAvailable)
        {
            Log.Instance.Trace($"SetLampColors failed: Controller not available.");
            return;
        }

        if (lampColors.Count == 0) return;

        lock (_lampArrays)
        {
            Log.Instance.Trace($"SetLampColors: Setting {lampColors.Count} lamp colors.");
            foreach (var kvp in _lampArrays)
            {
                if (!kvp.Value.Device.IsAvailable) continue;

                try
                {
                    var indices = lampColors.Keys.ToArray();
                    var colors = lampColors.Values.ToArray();
                    Log.Instance.Trace($"Applying {indices.Length} colors to {kvp.Key}.");
                    kvp.Value.Device.SetColorsForIndices(colors, indices);
                }
                catch (Exception ex)
                {
                    Log.Instance.Trace($"Failed to set lamp colors on {kvp.Key}: {ex.Message}");
                }
            }
        }
    }

    public void UpdateEffect()
    {
        if (!IsAvailable) return;

        var currentTime = _stopwatch.Elapsed.TotalSeconds * _speed;

        if (_targetEffect != null)
        {
            var elapsed = _stopwatch.Elapsed.TotalSeconds - _transitionStartTime;
            if (elapsed >= _transitionDuration)
            {
                _currentEffect = _targetEffect;
                _targetEffect = null;
                _currentEffect.Reset();
                Log.Instance.Trace($"Transition complete to {_currentEffect.Name}.");
            }
        }

        if (_currentEffect == null && _effectOverrides.IsEmpty) return;

        lock (_lampArrays)
        {
            foreach (var kvp in _lampArrays)
            {
                if (!kvp.Value.Device.IsAvailable) continue;

                try
                {
                    var device = kvp.Value.Device;
                    var lampCount = device.LampCount;
                    var colors = new Color[lampCount];

                    for (var i = 0; i < lampCount; i++)
                    {
                        var lampInfo = device.GetLampInfo(i);
                        
                        ILampEffect? effectToUse = _currentEffect;
                        bool isOverridden = _effectOverrides.TryGetValue(i, out var overrideEffect);
                        if (isOverridden) effectToUse = overrideEffect;

                        if (effectToUse == null) 
                        {
                             colors[i] = Color.FromArgb(0,0,0,0);
                             continue;
                        }

                        var color = effectToUse.GetColorForLamp(i, currentTime, lampInfo, lampCount);

                        if (!isOverridden && _targetEffect != null)
                        {
                            var targetColor = _targetEffect.GetColorForLamp(i, currentTime, lampInfo, lampCount);
                            var elapsed = _stopwatch.Elapsed.TotalSeconds - _transitionStartTime;
                            var t = Math.Clamp(elapsed / _transitionDuration, 0, 1);
                            color = LerpColor(color, targetColor, t);
                        }

                        colors[i] = ApplyBrightness(color, _brightness);
                        _lastFrameColors[i] = colors[i];
                    }

                    device.SetColorsForIndices(colors, Enumerable.Range(0, lampCount).ToArray());
                }
                catch (Exception ex)
                {
                    Log.Instance.Trace($"Error updating lights on {kvp.Key}: {ex.Message}");
                }
            }
        }
    }

    public void SetEffectForIndices(IEnumerable<int> indices, ILampEffect? effect)
    {
        if (indices == null) return;
        foreach (var index in indices)
        {
            if (effect == null) _effectOverrides.TryRemove(index, out _);
            else _effectOverrides[index] = effect;
        }
    }

    public Color? GetCurrentColor(int index)
    {
        return _lastFrameColors.TryGetValue(index, out var color) ? color : null;
    }

    private static Color ApplyBrightness(Color color, double brightness)
    {
        return Color.FromArgb(
            color.A,
            (byte)(color.R * brightness),
            (byte)(color.G * brightness),
            (byte)(color.B * brightness)
        );
    }

    private static Color LerpColor(Color from, Color to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)(from.A + (to.A - from.A) * t),
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t)
        );
    }

    private async void Watcher_Added(DeviceWatcher sender, DeviceInformation args)
    {
        try
        {
            Log.Instance.Trace($"LampArray device added: {args.Id}");
            var lampArray = await LampArray.FromIdAsync(args.Id);

            if (lampArray is null)
            {
                Log.Instance.Trace($"Failed to create LampArray instance from {args.Id}");
                return;
            }

            var deviceWrapper = new LampArrayDevice(lampArray);

            lock (_lampArrays)
            {
                if (_lampArrays.TryGetValue(args.Id, out var oldWrapper))
                {
                    Log.Instance.Trace($"Refreshing stale LampArray instance for {args.Id}");
                    oldWrapper.Device.AvailabilityChanged -= LampArray_AvailabilityChanged;
                }

                _lampArrays[args.Id] = deviceWrapper;
                lampArray.AvailabilityChanged += LampArray_AvailabilityChanged;
            }

            Log.Instance.Trace(
                $"LampArray device registered: DeviceId={args.Id}, LampCount={lampArray.LampCount}, IsAvailable={lampArray.IsAvailable}");


        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to add LampArray device: {ex.Message}");
        }
    }

    private void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        try
        {
            Log.Instance.Trace($"LampArray device removed: {args.Id}");

            lock (_lampArrays)
            {
                if (_lampArrays.TryGetValue(args.Id, out var device))
                {
                    device.Device.AvailabilityChanged -= LampArray_AvailabilityChanged;
                    _lampArrays.Remove(args.Id);
                }
            }


        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to remove LampArray device: {ex.Message}");
        }
    }

    private void LampArray_AvailabilityChanged(LampArray sender, object args)
    {
        Log.Instance.Trace($"LampArray availability changed: IsAvailable={sender.IsAvailable}");
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        _watcher?.Stop();
        _watcher = null;

        lock (_lampArrays)
        {
            foreach (var dev in _lampArrays.Values) dev.Device.AvailabilityChanged -= LampArray_AvailabilityChanged;
            _lampArrays.Clear();
        }

        GC.SuppressFinalize(this);
    }
}