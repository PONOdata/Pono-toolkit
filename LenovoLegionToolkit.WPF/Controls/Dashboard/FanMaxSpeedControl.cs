using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

public class FanMaxSpeedControl : AbstractToggleFeatureCardControl<FanMaxSpeedState>
{
    protected override FanMaxSpeedState OnState => FanMaxSpeedState.On;

    protected override FanMaxSpeedState OffState => FanMaxSpeedState.Off;

    public FanMaxSpeedControl()
    {
        Icon = SymbolRegular.TopSpeed24;
        Title = Resource.FanMaxSpeedControl_Title;
        Subtitle = Resource.FanMaxSpeedControl_Message;
    }
}
