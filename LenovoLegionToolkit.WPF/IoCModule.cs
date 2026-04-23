using Autofac;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.CLI;
using LenovoLegionToolkit.WPF.Settings;
using LenovoLegionToolkit.WPF.Utils;

namespace LenovoLegionToolkit.WPF;

public class IoCModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register<MainThreadDispatcher>();

        builder.Register<SpectrumScreenCapture>();

        builder.Register<ThemeManager>().AutoActivate();
        builder.Register<NotificationsManager>().AutoActivate();

        builder.Register<DashboardSettings>();
        builder.Register<SensorsControlSettings>();
        builder.Register<HardwareSensorSettings>();

        builder.Register<IpcServer>();

        builder.RegisterType<Extensions.NavigationService>().As<INavigationService>().SingleInstance();
        builder.RegisterType<Extensions.ExtensionManager>().SingleInstance();
        builder.RegisterType<Extensions.ExtensionContextFactory>().SingleInstance();
        builder.RegisterType<Extensions.ExtensionLogger>().As<IExtensionLogger>();
        builder.RegisterType<Extensions.UiDispatcher>().As<IUiDispatcher>().SingleInstance();
    }
}
