using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.Sensors;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using LenovoLegionToolkit.WPF.Windows;
using LenovoLegionToolkit.WPF.Windows.Dashboard;
using LenovoLegionToolkit.WPF.Windows.Utils;

namespace LenovoLegionToolkit.WPF.Controls.Settings;

public partial class SettingsAppBehaviorControl
{
    private readonly ApplicationSettings _settings = IoCContainer.Resolve<ApplicationSettings>();

    private bool _isRefreshing;

    public SettingsAppBehaviorControl()
    {
        InitializeComponent();
    }

    public Task RefreshAsync()
    {
        _isRefreshing = true;

        _autorunComboBox.SetItems(Enum.GetValues<AutorunState>(), Autorun.State, t => t.GetDisplayName());
        _minimizeToTrayToggle.IsChecked = _settings.Store.MinimizeToTray;
        _minimizeOnCloseToggle.IsChecked = _settings.Store.MinimizeOnClose;
        _useNewSensorDashboardToggle.IsChecked = _settings.Store.UseNewSensorDashboard;
        _lockWindowSizeToggle.IsChecked = _settings.Store.LockWindowSize;
        _enableLoggingToggle.IsChecked = _settings.Store.EnableLogging;

        _autorunComboBox.Visibility = Visibility.Visible;
        _minimizeToTrayToggle.Visibility = Visibility.Visible;
        _minimizeOnCloseToggle.Visibility = Visibility.Visible;
        _enableLoggingToggle.Visibility = Visibility.Visible;
        _useNewSensorDashboardToggle.Visibility = Visibility.Visible;
        _lockWindowSizeToggle.Visibility = Visibility.Visible;

        _isRefreshing = false;

        return Task.CompletedTask;
    }

    private void AutorunComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing)
            return;

        if (!_autorunComboBox.TryGetSelectedItem(out AutorunState state))
            return;

        Autorun.Set(state);
    }

    private void MinimizeToTrayToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var state = _minimizeToTrayToggle.IsChecked;
        if (state is null)
            return;

        _settings.Store.MinimizeToTray = state.Value;
        _settings.SynchronizeStore();
    }

    private void MinimizeOnCloseToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var state = _minimizeOnCloseToggle.IsChecked;
        if (state is null)
            return;

        _settings.Store.MinimizeOnClose = state.Value;
        _settings.SynchronizeStore();
    }

    private void LockWindowSizeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var state = _lockWindowSizeToggle.IsChecked;
        if (state is null)
            return;

        _settings.Store.LockWindowSize = state.Value;
        _settings.SynchronizeStore();
    }

    private async void EnableLoggingToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        if (App.Current.MainWindow is not MainWindow mainWindow)
            return;

        var state = _enableLoggingToggle.IsChecked;
        if (state is null)
            return;

        const string logSuffix = " [LOGGING ENABLED]";

        await mainWindow.InvokeIfRequired(() =>
        {
            if (state.Value)
            {
                if (!mainWindow._title.Text.EndsWith(logSuffix))
                {
                    mainWindow._title.Text += logSuffix;
                }
            }
            else
            {
                mainWindow._title.Text = mainWindow._title.Text.Replace(logSuffix, string.Empty);
            }
        });

        Log.Instance.IsTraceEnabled = state.Value;
        AppFlags.Instance.IsTraceEnabled = state.Value;
        _settings.Store.EnableLogging = state.Value;
        _settings.SynchronizeStore();

        mainWindow._openLogIndicator.Visibility = Utils.BooleanToVisibilityConverter.Convert(_settings.Store.EnableLogging);
    }

    private void UseNewSensorDashboard_Toggle(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var state = _useNewSensorDashboardToggle.IsChecked;
        if (state is null)
            return;

        var feature = IoCContainer.Resolve<SensorsGroupController>();

        if (state.Value && !PawnIOHelper.IsPawnIOInnstalled())
        {
            var dialog = new DialogWindow
            {
                Title = Resource.MainWindow_PawnIO_Warning_Title,
                Content = Resource.MainWindow_PawnIO_Warning_Message,
                Owner = Application.Current.MainWindow
            };

            dialog.ShowDialog();

            if (dialog.Result.Item1)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://pawnio.eu/",
                    UseShellExecute = true
                });
                SnackbarHelper.Show(Resource.SettingsPage_UseNewDashboard_Switch_Title,
                                  Resource.SettingsPage_UseNewDashboard_Restart_Message,
                                  SnackbarType.Success);
                _settings.Store.UseNewSensorDashboard = state.Value;
                _settings.SynchronizeStore();
            }
            else
            {
                _useNewSensorDashboardToggle.IsChecked = false;
                _settings.Store.UseNewSensorDashboard = false;
                _settings.SynchronizeStore();
            }
        }
        else
        {
            SnackbarHelper.Show(Resource.SettingsPage_UseNewDashboard_Switch_Title,
                                  Resource.SettingsPage_UseNewDashboard_Restart_Message,
                                  SnackbarType.Success);
            _settings.Store.UseNewSensorDashboard = state.Value;
            _settings.SynchronizeStore();
        }
    }

    private void DashboardCustomButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        EditSensorGroupWindow.ShowInstance();
    }
}
