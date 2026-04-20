using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using LenovoLegionToolkit.Lib.Utils;
using static LenovoLegionToolkit.Lib.Settings.FanCurveSettings;

namespace LenovoLegionToolkit.Lib.Settings;


public class FanCurveSettings() : AbstractSettings<FanCurveSettingsStore>("fan_curves.json")
{
    private static readonly Dictionary<string, PropertyInfo> SettingProperties = typeof(FanCurveSettingsStore)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);

    public class FanCurveSettingsStore
    {
        public List<FanCurveEntry> Entries { get; set; } = [];
        public bool IsFullSpeed { get; set; }

        public int CalculationDelayMs { get; set; } = 500;
        public bool DebugMode { get; set; }
        public int SensorIntervalMs { get; set; } = 500;
        public int ModeSwitchRefreshDelayMs { get; set; } = 250;
        public bool AlwaysWritePwm { get; set; } = true;
        public bool ForceWriteWhenRpmZero { get; set; } = true;
        public int MinimumPwm { get; set; }
        public int MaxPwm { get; set; } = 255;
        public bool IsMaxPwmInitialized { get; set; }
        public bool SpinUpBoostEnabled { get; set; }
        public int SpinUpBoostPwm { get; set; } = 120;
        public int SpinUpBoostDurationMs { get; set; } = 300;
        public double TemperatureDeltaThreshold { get; set; } = 0.5;
        public int MinimumPwmChangeToApply { get; set; } = 2;
        public int UiUpdateIntervalMs { get; set; } = 1000;
        public bool ForceRefreshOnRegisterEnable { get; set; } = true;
        public bool ForceRefreshOnModeSwitch { get; set; } = true;
        public int ModeSwitchRefreshCount { get; set; } = 2;
        public bool ClearCachedStateWhenLeavingCustomMode { get; set; } = true;
        public bool ReapplyCurveOnEveryCalculation { get; set; } = true;
        public bool UseCachedSnapshotForForcedRefresh { get; set; } = true;
        public bool IgnoreZeroTemperature { get; set; }
        public bool EnableMaxFanWriteEachCycle { get; set; } = true;
    }

    public bool Contains(string settingName) => ResolveSettingProperty(settingName) is not null;

    public object? Read(string settingName)
    {
        if (string.IsNullOrWhiteSpace(settingName))
            return null;

        var property = ResolveSettingProperty(settingName);
        return property?.GetValue(Store);
    }

    public bool TryRead<T>(string settingName, out T value)
    {
        value = default!;

        var rawValue = Read(settingName);
        if (!TryConvertValue(rawValue, typeof(T), out var convertedValue) || convertedValue is not T typedValue)
            return false;

        value = typedValue;
        return true;
    }

    public T ReadOrDefault<T>(string settingName, T fallback = default!) =>
        TryRead<T>(settingName, out var value) ? value : fallback;

    public bool Write(string settingName, object? value)
    {
        if (string.IsNullOrWhiteSpace(settingName))
            return false;

        var property = ResolveSettingProperty(settingName);
        if (property is null || !property.CanWrite)
            return false;

        if (!TryConvertValue(value, property.PropertyType, out var convertedValue))
            return false;

        property.SetValue(Store, convertedValue);
        Save();
        return true;
    }

    public object? GetSetting(string settingName) => Read(settingName);

    public bool SetSetting(string settingName, object? value) => Write(settingName, value);

    private static PropertyInfo? ResolveSettingProperty(string settingName) =>
        string.IsNullOrWhiteSpace(settingName)
            ? null
            : SettingProperties.GetValueOrDefault(settingName);

    private static bool TryConvertValue(object? value, Type targetType, out object? convertedValue)
    {
        convertedValue = null;

        var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value is null)
        {
            if (!effectiveType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null)
            {
                return true;
            }

            return false;
        }

        if (effectiveType.IsInstanceOfType(value))
        {
            convertedValue = value;
            return true;
        }

        try
        {
            if (effectiveType == typeof(bool))
            {
                if (value is string boolString)
                {
                    if (bool.TryParse(boolString, out var parsedBool))
                    {
                        convertedValue = parsedBool;
                        return true;
                    }

                    if (int.TryParse(boolString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedBoolNumber))
                    {
                        convertedValue = parsedBoolNumber != 0;
                        return true;
                    }
                }
                else if (value is sbyte or byte or short or ushort or int or uint or long or ulong)
                {
                    convertedValue = Convert.ToUInt64(value, CultureInfo.InvariantCulture) != 0;
                    return true;
                }
            }

            if (effectiveType.IsEnum)
            {
                convertedValue = value is string enumString
                    ? Enum.Parse(effectiveType, enumString, true)
                    : Enum.ToObject(effectiveType, Convert.ChangeType(value, Enum.GetUnderlyingType(effectiveType), CultureInfo.InvariantCulture));
                return true;
            }

            if (effectiveType == typeof(Guid))
            {
                convertedValue = value is Guid guid ? guid : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!);
                return true;
            }

            convertedValue = Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
