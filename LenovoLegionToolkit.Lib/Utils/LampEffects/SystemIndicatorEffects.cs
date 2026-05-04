using System;
using System.Diagnostics;
using Windows.Devices.Lights;
using Windows.UI;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Utils.LampEffects;

// System-state-driven indicator effects. Each effect samples a Windows or Lenovo
// platform signal at most once per second of wall-clock time to keep the per-frame
// cost negligible while the effect runs at the controller's render cadence. The
// shared Stopwatch is intentionally separate from the controller's animation time
// (which is scaled by the speed slider) so sampling cadence stays at one second
// regardless of how fast or slow the visual animation runs.
internal static class IndicatorSampleClock
{
    public static readonly Stopwatch Wall = Stopwatch.StartNew();
}

public class BatteryLowEffect : BaseLampEffect
{
    public override string Name => "Battery Low";

    private const double DefaultThreshold = 0.15;
    private const double DefaultPeriod = 1.5;

    private long _lastSampleMs = -1;
    private bool _sampledLow;

    public BatteryLowEffect(Color color, double threshold = DefaultThreshold, double period = DefaultPeriod)
    {
        Parameters["Color"] = color;
        Parameters["Threshold"] = threshold;
        Parameters["Period"] = period;
    }

    public override Color GetColorForLamp(int lampIndex, double time, LampInfo lampInfo, int totalLamps)
    {
        var color = (Color)Parameters["Color"];
        var threshold = (double)Parameters["Threshold"];
        var period = (double)Parameters["Period"];

        SampleIfDue(threshold);

        if (!_sampledLow)
            return Color.FromArgb(0, 0, 0, 0);

        var t = time % period / period;
        var pulse = Math.Sin(t * Math.PI * 2) * 0.5 + 0.5;
        pulse = EaseInOut(pulse);
        pulse = 0.25 + pulse * 0.75;

        return Color.FromArgb(255,
            (byte)(color.R * pulse),
            (byte)(color.G * pulse),
            (byte)(color.B * pulse));
    }

    private void SampleIfDue(double threshold)
    {
        var nowMs = IndicatorSampleClock.Wall.ElapsedMilliseconds;
        if (_lastSampleMs >= 0 && nowMs - _lastSampleMs < 1000)
            return;

        _lastSampleMs = nowMs;
        try
        {
            var info = Battery.GetBatteryInformation();
            var fraction = info.BatteryPercentage / 100.0;
            _sampledLow = !info.IsCharging && (fraction <= threshold || info.IsLowBattery);
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"BatteryLowEffect sample failed: {ex.Message}");
            _sampledLow = false;
        }
    }

    public override void Reset()
    {
        _lastSampleMs = -1;
        _sampledLow = false;
    }
}

public class ChargingEffect : BaseLampEffect
{
    public override string Name => "Charging";

    private const double DefaultPeriod = 4.0;

    private long _lastSampleMs = -1;
    private bool _sampledCharging;

    public ChargingEffect(Color startColor, Color endColor, double period = DefaultPeriod)
    {
        Parameters["StartColor"] = startColor;
        Parameters["EndColor"] = endColor;
        Parameters["Period"] = period;
    }

    public override Color GetColorForLamp(int lampIndex, double time, LampInfo lampInfo, int totalLamps)
    {
        var startColor = (Color)Parameters["StartColor"];
        var endColor = (Color)Parameters["EndColor"];
        var period = (double)Parameters["Period"];

        SampleIfDue();

        if (!_sampledCharging)
            return Color.FromArgb(0, 0, 0, 0);

        var sweep = Math.Sin(time / period * Math.PI * 2) * 0.5 + 0.5;
        sweep = EaseInOut(sweep);

        return LerpColor(startColor, endColor, sweep);
    }

    private void SampleIfDue()
    {
        var nowMs = IndicatorSampleClock.Wall.ElapsedMilliseconds;
        if (_lastSampleMs >= 0 && nowMs - _lastSampleMs < 1000)
            return;

        _lastSampleMs = nowMs;
        try
        {
            var info = Battery.GetBatteryInformation();
            _sampledCharging = info.IsCharging;
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"ChargingEffect sample failed: {ex.Message}");
            _sampledCharging = false;
        }
    }

    public override void Reset()
    {
        _lastSampleMs = -1;
        _sampledCharging = false;
    }
}

// Borg mode: zero-configuration adaptive effect that "just works" on any
// LampArray-conformant device. Status and Branding lamps are held at white
// so system feedback channels are preserved; everything else runs a spatial
// rainbow wave whose period scales with the lamp count, so a 5-lamp ambient
// strip and a 100-key keyboard both look intentional. Resistance is futile.
public class BorgEffect : BaseLampEffect
{
    public override string Name => "Borg";

    public override Color GetColorForLamp(int lampIndex, double time, LampInfo lampInfo, int totalLamps)
    {
        if (lampInfo.Purposes.HasFlag(LampPurposes.Status) || lampInfo.Purposes.HasFlag(LampPurposes.Branding))
            return Color.FromArgb(255, 255, 255, 255);

        var period = totalLamps > 30 ? 6.0 : 3.0;
        var hue = time / period * 360.0 + lampInfo.Position.X * 200.0 + lampInfo.Position.Y * 100.0;
        hue %= 360.0;
        if (hue < 0) hue += 360.0;
        return HsvToRgb(hue, 0.85, 0.9);
    }
}

public class CapsLockIndicatorEffect : BaseLampEffect
{
    public override string Name => "Caps Lock";

    private double _lastQueryTime = double.NaN;
    private bool _isOn;

    public CapsLockIndicatorEffect(Color color)
    {
        Parameters["Color"] = color;
    }

    public override Color GetColorForLamp(int lampIndex, double time, LampInfo lampInfo, int totalLamps)
    {
        // The controller passes the same time value to every lamp on a single
        // frame. Cache the GetKeyState result on the time-key so a lamp array
        // with N lamps does one Win32 call per frame rather than N.
        if (time != _lastQueryTime)
        {
            _lastQueryTime = time;
            try
            {
                // ANCHOR: PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_CAPITAL) & 0x1, mirrors NativeWindowsMessageListener.cs:353.
                _isOn = (PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_CAPITAL) & 0x1) != 0;
            }
            catch (Exception ex)
            {
                Log.Instance.Trace($"CapsLockIndicatorEffect query failed: {ex.Message}");
                _isOn = false;
            }
        }

        if (!_isOn)
            return Color.FromArgb(0, 0, 0, 0);

        return (Color)Parameters["Color"];
    }

    public override void Reset()
    {
        _lastQueryTime = double.NaN;
        _isOn = false;
    }
}
