using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.WPF.Extensions;

public sealed class ExtensionContextFactory
{
    private readonly INavigationService _navigationService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IExtensionLogger _logger;

    public ExtensionContextFactory(INavigationService navigationService, IUiDispatcher uiDispatcher, IExtensionLogger logger)
    {
        _navigationService = navigationService;
        _uiDispatcher = uiDispatcher;
        _logger = logger;
    }

    public IExtensionContext Create() => new ExtensionContext(_navigationService, _uiDispatcher, _logger);
}
