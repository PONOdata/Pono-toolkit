namespace LenovoLegionToolkit.Lib.Utils;

public interface IExtensionHostContext
{
    bool TryGetSetting<T>(string key, out T value);
    bool TrySetSetting<T>(string key, T value);
}
