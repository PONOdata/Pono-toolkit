using System.Diagnostics;
using System.Windows;

namespace LenovoLegionToolkit.WPF.Windows.Utils;

public partial class UpstreamShoutoutWindow
{
    private const string DonateUrl = "https://www.paypal.com/donate/?hosted_button_id=22AZE2NBP3HTL";

    public UpstreamShoutoutWindow()
    {
        InitializeComponent();
    }

    private void DonateUpstream_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = DonateUrl,
            UseShellExecute = true,
        });
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
