using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Utils;

public enum ExtensionIcon
{
    None = 0,
    Gauge = 1,
}

public sealed record ExtensionNavigationItem(
    string Id,
    string Title,
    ExtensionIcon Icon,
    string PageTag,
    Type PageType,
    bool IsFooter = false);

public interface INavigationService
{
    IReadOnlyCollection<ExtensionNavigationItem> Items { get; }
    event EventHandler? ItemsChanged;
    void Register(ExtensionNavigationItem item);
}

public interface IUiDispatcher
{
    Task InvokeAsync(Action action);
    Task<T> InvokeAsync<T>(Func<T> action);
}

public interface IExtensionLogger
{
    void Trace(string message);
    void Error(string message, Exception exception);
}

public interface IExtensionContext : IExtensionHostContext
{
    INavigationService Navigation { get; }
    IUiDispatcher UiDispatcher { get; }
    IExtensionLogger Logger { get; }
}
