using System;
using Windows.Devices.Lights;
using Windows.UI;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Utils.LampEffects;

// System-state-driven indicator effects. Each effect samples a Windows or Lenovo
// platform signal at most once per second to keep the per-frame cost negligible
// while the effect runs at the controller's render cadence.

public class BatteryLowEffect : BaseLampEffect
{
    public override string Name => "Battery Low";

    private const double DefaultThreshold = 0.15;
    private const double DefaultPeriod = 1.5;

    private double _lastSampleTime = -1;
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

        SampleIfDue(time, threshold);

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

    private void SampleIfDue(double time, double threshold)
    {
        if (_lastSampleTime >= 0 && time - _lastSampleTime < 1.0)
            return;

        _lastSampleTime = time;
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
        _lastSampleTime = -1;
        _sampledLow = false;
    }
}

public class ChargingEffect : BaseLampEffect
{
    public override string Name => "Charging";

    private const double DefaultPeriod = 4.0;

    private double _lastSampleTime = -1;
    private bool _sampledCharging;
    private double _sampledFraction;

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

        SampleIfDue(time);

        if (!_sampledCharging)
            return Color.FromArgb(0, 0, 0, 0);

        var sweep = Math.Sin(time / period * Math.PI * 2) * 0.5 + 0.5;
        sweep = EaseInOut(sweep);

        var blend = Math.Clamp(_sampledFraction + (sweep - 0.5) * 0.2, 0.0, 1.0);
        return LerpColor(startColor, endColor, blend);
    }

    private void SampleIfDue(double time)
    {
        if (_lastSampleTime >= 0 && time - _lastSampleTime < 1.0)
            return;

        _lastSampleTime = time;
        try
        {
            var info = Battery.GetBatteryInformation();
            _sampledCharging = info.IsCharging;
            _sampledFraction = Math.Clamp(info.BatteryPercentage / 100.0, 0.0, 1.0);
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"ChargingEffect sample failed: {ex.Message}");
            _sampledCharging = false;
            _sampledFraction = 0;
        }
    }

    public override void Reset()
    {
        _lastSampleTime = -1;
        _sampledCharging = false;
        _sampledFraction = 0;
    }
}

public class CapsLockIndicatorEffect : BaseLampEffect
{
    public override string Name => "Caps Lock";

    public CapsLockIndicatorEffect(Color color)
    {
        Parameters["Color"] = color;
    }

    public override Color GetColorForLamp(int lampIndex, double time, LampInfo lampInfo, int totalLamps)
    {
        bool isOn;
        try
        {
            // ANCHOR: PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_CAPITAL) & 0x1, mirrors NativeWindowsMessageListener.cs:353.
            isOn = (PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_CAPITAL) & 0x1) != 0;
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"CapsLockIndicatorEffect query failed: {ex.Message}");
            isOn = false;
        }

        if (!isOn)
            return Color.FromArgb(0, 0, 0, 0);

        return (Color)Parameters["Color"];
    }
}
