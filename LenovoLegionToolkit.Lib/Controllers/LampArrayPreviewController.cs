using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Lights;
using Windows.UI;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.Lib.Extensions;
using NeoSmart.AsyncLock;

namespace LenovoLegionToolkit.Lib.Controllers;

public class LampArrayPreviewController : IDisposable
{
    private class LampArrayDevice
    {
        public LampArray Device { get; }
        public Dictionary<int, int> ScanCodeToIndex { get; } = [];

        public LampArrayDevice(LampArray device)
        {
            Device = device;
            Log.Instance.Trace($"[Diagnostics] Initializing LampArrayDevice with {device.LampCount} lamps.");
            
            for (var i = 0; i < device.LampCount; i++)
            {
                var lampInfo = device.GetLampInfo(i);
                
                var purposes = lampInfo.Purposes;
                var position = lampInfo.Position;

                Log.Instance.Trace($"[Diagnostics] Lamp {i}: Index={lampInfo.Index}, Purposes={purposes}, Pos=({position.X:F2},{position.Y:F2},{position.Z:F2})");
            }
        }

        public void SetLayout(int width, int height, IEnumerable<(ushort Code, int X, int Y)> keys)
        {
            if (Device == null) return;
            ScanCodeToIndex.Clear();
            
            var lamps = new List<(int Index, double X, double Y)>();
            for (int i = 0; i < Device.LampCount; i++)
            {
                var lamp = Device.GetLampInfo(i);
                if (lamp.Purposes.HasFlag(LampPurposes.Control))
                    lamps.Add((lamp.Index, lamp.Position.X, lamp.Position.Y));
            }

            if (lamps.Count == 0) return;
            
            double minLX = lamps.Min(l => l.X);
            double maxLX = lamps.Max(l => l.X);
            double minLY = lamps.Min(l => l.Y);
            double maxLY = lamps.Max(l => l.Y);
            double rangeLX = maxLX - minLX;
            double rangeLY = maxLY - minLY;

            foreach (var key in keys)
            {
                double kx = (double)key.X / width;
                double ky = (double)key.Y / height;

                var bestLamp = -1;
                var bestDist = double.MaxValue;

                foreach (var lamp in lamps)
                {
                    double lx = (lamp.X - minLX) / (rangeLX > 0 ? rangeLX : 1);
                    double ly = (lamp.Y - minLY) / (rangeLY > 0 ? rangeLY : 1);
                    double dist = GetDistance(lx, ly, kx, ky);
                    
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestLamp = lamp.Index;
                    }
                }

                if (bestLamp != -1 && bestDist < 0.05)
                {
                    if (!ScanCodeToIndex.ContainsKey(key.Code))
                        ScanCodeToIndex[key.Code] = bestLamp;
                }
            }
            
        }

        private double GetDistance(double x1, double y1, double x2, double y2)
        {
             return Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2);
        }
        
    }

    private readonly AsyncLock _lock = new();
    private readonly Dictionary<string, LampArrayDevice> _lampArrays = [];
    private DeviceWatcher? _watcher;
    private bool _isDisposed;

    public event EventHandler<LampArrayAvailabilityChangedEventArgs>? AvailabilityChanged;

    public bool IsAvailable
    {
        get
        {
            lock (_lampArrays)
            {
                foreach (var kvp in _lampArrays)
                {
                    if (kvp.Value.Device.IsAvailable)
                        return true;
                }
                return false;
            }
        }
    }

    public int LampCount
    {
        get
        {
            lock (_lampArrays)
            {
                foreach (var kvp in _lampArrays)
                {
                    if (kvp.Value.Device.IsAvailable)
                        return kvp.Value.Device.LampCount;
                }
                return 0;
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
                _lampArrays.Clear();
            }

            Log.Instance.Trace($"LampArray device watcher stopped.");
        }
    }

    public void SetLayout(int width, int height, IEnumerable<(ushort Code, int X, int Y)> keys)
    {
        lock (_lampArrays)
        {
            foreach (var kvp in _lampArrays)
            {
                kvp.Value.SetLayout(width, height, keys);
            }
        }
    }

    public void SetPreviewColor(RGBColor color)
    {
        if (!IsAvailable)
            return;

        var winColor = ToWindowsColor(color);

        lock (_lampArrays)
        {
            foreach (var kvp in _lampArrays)
            {
                if (kvp.Value.Device.IsAvailable)
                {
                    try
                    {
                        kvp.Value.Device.SetColor(winColor);
                    }
                    catch (Exception ex)
                    {
                        Log.Instance.Trace($"Failed to set preview color: {ex.Message}");
                    }
                }
            }
        }
    }

    public void SetPreviewColorsForIndices(int[] indices, RGBColor color)
    {
        if (!IsAvailable)
            return;

        var winColor = ToWindowsColor(color);

        lock (_lampArrays)
        {
            foreach (var kvp in _lampArrays)
            {
                if (kvp.Value.Device.IsAvailable)
                {
                    try
                    {
                        kvp.Value.Device.SetColorsForIndices(CreateColorArray(winColor, indices.Length), indices);
                    }
                    catch (Exception ex)
                    {
                        Log.Instance.Trace($"Failed to set preview colors for indices: {ex.Message}");
                    }
                }
            }
        }
    }

    public void SetPreviewColorsForScanCodes(IEnumerable<ushort> scanCodes, RGBColor color)
    {
        if (!IsAvailable)
            return;

        var winColor = ToWindowsColor(color);

        lock (_lampArrays)
        {
            foreach (var kvp in _lampArrays)
            {
                if (!kvp.Value.Device.IsAvailable)
                    continue;

                try
                {
                    var indices = new List<int>();
                    var device = kvp.Value;
                    
                    foreach (var scanCode in scanCodes)
                    {
                        if (device.ScanCodeToIndex.TryGetValue((int)scanCode, out var index))
                        {
                            indices.Add(index);
                        }
                    }

                    if (indices.Count > 0)
                    {
                        var indicesArray = indices.ToArray();
                        device.Device.SetColorsForIndices(CreateColorArray(winColor, indicesArray.Length), indicesArray);
                    }
                }
                catch (Exception ex)
                {
                    Log.Instance.Trace($"Failed to set preview colors for scan codes: {ex.Message}");
                }
            }
        }
    }

    public void SetPreviewZoneColors(RGBColor[] zoneColors, ILampArrayZoneMapper mapper)
    {
        if (!IsAvailable)
            return;

        lock (_lampArrays)
        {
            foreach (var kvp in _lampArrays)
            {
                if (!kvp.Value.Device.IsAvailable)
                    continue;

                try
                {
                    for (int zone = 0; zone < zoneColors.Length; zone++)
                    {
                        var indices = mapper.GetLampIndicesForZone(zone, kvp.Value.Device.LampCount);
                        if (indices.Length > 0)
                        {
                            var winColor = ToWindowsColor(zoneColors[zone]);
                            kvp.Value.Device.SetColorsForIndices(CreateColorArray(winColor, indices.Length), indices);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Instance.Trace($"Failed to set preview zone colors: {ex.Message}");
                }
            }
        }
    }
    
    public void ApplyProfile(IEnumerable<SpectrumKeyboardBacklightEffect> effects)
    {
        if (!IsAvailable)
            return;

        lock (_lampArrays)
        {
            foreach (var kvp in _lampArrays)
            {
                if (!kvp.Value.Device.IsAvailable)
                    continue;

                try
                {
                    var device = kvp.Value;
                    var colors = new Color[device.Device.LampCount];
                    Array.Fill(colors, Color.FromArgb(255, 0, 0, 0));

                    foreach (var effect in effects)
                    {
                        IEnumerable<int> indices = [];
                        
                        if (effect.Type.IsAllLightsEffect())
                        {
                            indices = Enumerable.Range(0, device.Device.LampCount);
                        }
                        else
                        {
                            var list = new List<int>();
                            if (effect.Keys != null)
                            {
                                foreach (var sc in effect.Keys)
                                {
                                    if (device.ScanCodeToIndex.TryGetValue((int)sc, out var idx))
                                        list.Add(idx);
                                }
                            }
                            indices = list;
                        }
                        
                        var c = effect.Colors?.FirstOrDefault() ?? new RGBColor(0,0,0);
                        var winColor = ToWindowsColor(c);
                        
                        foreach (var idx in indices)
                        {
                            if (idx >= 0 && idx < colors.Length)
                                colors[idx] = winColor;
                        }
                    }

                    var allIndices = Enumerable.Range(0, device.Device.LampCount).ToArray();
                    device.Device.SetColorsForIndices(colors, allIndices);
                }
                catch (Exception ex)
                {
                    Log.Instance.Trace($"Failed to apply profile: {ex.Message}");
                }
            }
        }
    }

    private async void Watcher_Added(DeviceWatcher sender, DeviceInformation args)
    {
        try
        {
            Log.Instance.Trace($"LampArray device added: {args.Id}");

            var lampArray = await LampArray.FromIdAsync(args.Id);
            if (lampArray is null)
                return;

            lock (_lampArrays)
            {
                _lampArrays[args.Id] = new LampArrayDevice(lampArray);
            }

            lampArray.AvailabilityChanged += LampArray_AvailabilityChanged;

            Log.Instance.Trace($"LampArray device registered: DeviceId={args.Id}, DeviceName={args.Name}, LampCount={lampArray.LampCount}, Kind={lampArray.LampArrayKind}, IsAvailable={lampArray.IsAvailable}");

            if (lampArray.IsAvailable)
            {
                AvailabilityChanged?.Invoke(this, new LampArrayAvailabilityChangedEventArgs(true, lampArray.LampCount));
            }
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

            AvailabilityChanged?.Invoke(this, new LampArrayAvailabilityChangedEventArgs(IsAvailable, LampCount));
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to remove LampArray device: {ex.Message}");
        }
    }

    private void LampArray_AvailabilityChanged(LampArray sender, object args)
    {
        Log.Instance.Trace($"LampArray availability changed: IsAvailable={sender.IsAvailable}");
        AvailabilityChanged?.Invoke(this, new LampArrayAvailabilityChangedEventArgs(sender.IsAvailable, sender.LampCount));
    }

    private static Color ToWindowsColor(RGBColor color) => Color.FromArgb(255, color.R, color.G, color.B);

    private static Color[] CreateColorArray(Color color, int count)
    {
        var colors = new Color[count];
        Array.Fill(colors, color);
        return colors;
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
            _lampArrays.Clear();
        }

        GC.SuppressFinalize(this);
    }
}
public class LampArrayAvailabilityChangedEventArgs : EventArgs
{
    public bool IsAvailable { get; }
    public int LampCount { get; }

    public LampArrayAvailabilityChangedEventArgs(bool isAvailable, int lampCount)
    {
        IsAvailable = isAvailable;
        LampCount = lampCount;
    }
}

public interface ILampArrayZoneMapper
{
    int ZoneCount { get; }
    int[] GetLampIndicesForZone(int zone, int totalLampCount);
}
