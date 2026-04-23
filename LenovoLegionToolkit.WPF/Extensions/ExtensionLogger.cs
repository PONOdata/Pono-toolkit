using System;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.WPF.Extensions;

public sealed class ExtensionLogger : IExtensionLogger
{
    public void Trace(string message) => Log.Instance.Trace($"[Extension] {message}");

    public void Error(string message, Exception exception)
    {
        Log.Instance.ErrorReport($"[Extension] {message}", exception);
        Log.Instance.Trace($"[Extension] {message}", exception);
    }
}
