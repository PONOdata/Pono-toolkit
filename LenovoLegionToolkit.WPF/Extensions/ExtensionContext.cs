using System;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.WPF.Extensions;

public sealed class ExtensionContext : IExtensionContext
{
    private readonly FanCurveSettings _fanCurveSettings = new();

    public ExtensionContext(INavigationService navigation, IUiDispatcher uiDispatcher, IExtensionLogger logger)
    {
        Navigation = navigation;
        UiDispatcher = uiDispatcher;
        Logger = logger;

        _fanCurveSettings.EnsureFileExists();
        _fanCurveSettings.SynchronizeStore();
    }

    public INavigationService Navigation { get; }
    public IUiDispatcher UiDispatcher { get; }
    public IExtensionLogger Logger { get; }

    public bool TryGetSetting<T>(string key, out T value)
    {
        var property = typeof(FanCurveSettings.FanCurveSettingsStore).GetProperty(key);
        if (property?.GetValue(_fanCurveSettings.Store) is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default!;
        return false;
    }

    public bool TrySetSetting<T>(string key, T value)
    {
        var property = typeof(FanCurveSettings.FanCurveSettingsStore).GetProperty(key);
        if (property is null || !property.CanWrite)
        {
            return false;
        }

        try
        {
            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var converted = value is null || targetType.IsInstanceOfType(value)
                ? value
                : Convert.ChangeType(value, targetType);

            property.SetValue(_fanCurveSettings.Store, converted);
            _fanCurveSettings.SynchronizeStore();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
