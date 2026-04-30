using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.WPF.Controls;
using LenovoLegionToolkit.WPF.Controls.Custom;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using static LenovoLegionToolkit.Lib.Settings.GodModeSettings;

namespace LenovoLegionToolkit.WPF.Windows.Settings;

public partial class WindowsPowerModesWindow
{
    private readonly PowerModeFeature _powerModeFeature = IoCContainer.Resolve<PowerModeFeature>();
    private readonly ApplicationSettings _settings = IoCContainer.Resolve<ApplicationSettings>();
    private readonly GodModeController _godModeController = IoCContainer.Resolve<GodModeController>();
    private readonly GodModeSettings _godModeSettings = IoCContainer.Resolve<GodModeSettings>();

    private bool IsRefreshing => _loader.IsLoading;

    public WindowsPowerModesWindow()
    {
        InitializeComponent();

        IsVisibleChanged += PowerModesWindow_IsVisibleChanged;
    }

    private async void PowerModesWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _loader.IsLoading = true;

        var loadingTask = Task.Delay(500);

        var powerModes = Enum.GetValues<WindowsPowerMode>();
        RefreshMode(_quietCardControl, _quietAcDcRow, powerModes, PowerModeState.Quiet);
        RefreshMode(_balanceCardControl, _balanceAcDcRow, powerModes, PowerModeState.Balance);
        RefreshMode(_performanceCardControl, _performanceAcDcRow, powerModes, PowerModeState.Performance);

        var allStates = await _powerModeFeature.GetAllStatesAsync();
        if (allStates.Contains(PowerModeState.Extreme))
            RefreshMode(_extremeCardControl, _extremeAcDcRow, powerModes, PowerModeState.Extreme);
        else
            _extremeCardControl.Visibility = Visibility.Collapsed;

        if (allStates.Contains(PowerModeState.GodMode))
        {
            _godModeCardControl.Visibility = Visibility.Visible;

            var controller = await _godModeController.GetControllerAsync().ConfigureAwait(false);
            var presets = await controller.GetGodModePresetsAsync().ConfigureAwait(false);

            if (presets.Count > 1)
            {
                _godModeSinglePresetContainer.Visibility = Visibility.Collapsed;
                _godModePresetsContainer.Visibility = Visibility.Visible;
                _godModePresetsContainer.Children.Clear();

                foreach (var preset in presets)
                {
                    var cardControl = new CardControl
                    {
                        Margin = new Thickness(0, 8, 0, 8)
                    };

                    var headerControl = new CardHeaderControl
                    {
                        Title = preset.Value.Name
                    };
                    cardControl.Header = headerControl;

                    var savedAc = preset.Value.Overrides.TryGetEnum<WindowsPowerMode>(PowerOverrideKey.PowerModeOnAc) ?? _settings.Store.PowerModes.GetValueOrDefault(PowerModeState.GodMode, WindowsPowerMode.Balanced);
                    var savedDc = preset.Value.Overrides.TryGetEnum<WindowsPowerMode>(PowerOverrideKey.PowerModeOnDc) ?? _settings.Store.PowerModes.GetValueOrDefault(PowerModeState.GodMode, WindowsPowerMode.Balanced);

                    var (row, acCombo, dcCombo) = BuildAcDcRow(powerModes, savedAc, savedDc);

                    acCombo.SelectionChanged += async (_, _) =>
                    {
                        if (acCombo.TryGetSelectedItem(out WindowsPowerMode mode))
                            await GodModePresetPowerModeChangedAsync(preset.Key.ToString(), mode, isAc: true);
                    };
                    dcCombo.SelectionChanged += async (_, _) =>
                    {
                        if (dcCombo.TryGetSelectedItem(out WindowsPowerMode mode))
                            await GodModePresetPowerModeChangedAsync(preset.Key.ToString(), mode, isAc: false);
                    };

                    cardControl.Content = row;
                    _godModePresetsContainer.Children.Add(cardControl);
                }
            }
            else
            {
                _godModePresetsContainer.Visibility = Visibility.Collapsed;
                _godModeSinglePresetContainer.Visibility = Visibility.Visible;

                var singlePreset = presets.FirstOrDefault();
                var savedAc = singlePreset.Value?.Overrides.TryGetEnum<WindowsPowerMode>(PowerOverrideKey.PowerModeOnAc) ?? _settings.Store.PowerModes.GetValueOrDefault(PowerModeState.GodMode, WindowsPowerMode.Balanced);
                var savedDc = singlePreset.Value?.Overrides.TryGetEnum<WindowsPowerMode>(PowerOverrideKey.PowerModeOnDc) ?? _settings.Store.PowerModes.GetValueOrDefault(PowerModeState.GodMode, WindowsPowerMode.Balanced);

                RefreshModeRow(_godModeSingleAcDcRow, powerModes, savedAc, savedDc, singlePreset.Key.ToString());
            }
        }
        else
        {
            _godModeCardControl.Visibility = Visibility.Collapsed;
        }

        await loadingTask;

        _loader.IsLoading = false;
    }

    private void RefreshMode(CardControl cardControl, StackPanel acDcRow, WindowsPowerMode[] windowsPowerModes, PowerModeState powerModeState)
    {
        var defaultMode = _settings.Store.PowerModes.GetValueOrDefault(powerModeState, WindowsPowerMode.Balanced);
        var savedAc = _settings.Store.Overrides.GetPowerModeOnAc(powerModeState);
        var savedDc = _settings.Store.Overrides.GetPowerModeOnDc(powerModeState);

        RefreshModeRow(acDcRow, windowsPowerModes, savedAc ?? defaultMode, savedDc ?? defaultMode, powerModeState);
    }

    private void RefreshModeRow(StackPanel row, WindowsPowerMode[] powerModes, WindowsPowerMode savedAc, WindowsPowerMode savedDc, PowerModeState powerModeState)
    {
        row.Children.Clear();

        var (_, acCombo, dcCombo) = SetupAcDcRow(row, powerModes, savedAc, savedDc);

        acCombo.SelectionChanged += async (_, _) =>
        {
            if (acCombo.TryGetSelectedItem(out WindowsPowerMode mode))
                await WindowsPowerModeAcDcChangedAsync(mode, powerModeState, isAc: true);
        };
        dcCombo.SelectionChanged += async (_, _) =>
        {
            if (dcCombo.TryGetSelectedItem(out WindowsPowerMode mode))
                await WindowsPowerModeAcDcChangedAsync(mode, powerModeState, isAc: false);
        };
    }

    private void RefreshModeRow(StackPanel row, WindowsPowerMode[] powerModes, WindowsPowerMode savedAc, WindowsPowerMode savedDc, string presetKey)
    {
        row.Children.Clear();

        var (_, acCombo, dcCombo) = SetupAcDcRow(row, powerModes, savedAc, savedDc);

        acCombo.SelectionChanged += async (_, _) =>
        {
            if (acCombo.TryGetSelectedItem(out WindowsPowerMode mode))
                await GodModePresetPowerModeChangedAsync(presetKey, mode, isAc: true);
        };
        dcCombo.SelectionChanged += async (_, _) =>
        {
            if (dcCombo.TryGetSelectedItem(out WindowsPowerMode mode))
                await GodModePresetPowerModeChangedAsync(presetKey, mode, isAc: false);
        };
    }

    private static (StackPanel Row, ComboBox AcCombo, ComboBox DcCombo) BuildAcDcRow(
        WindowsPowerMode[] powerModes, WindowsPowerMode savedAc, WindowsPowerMode savedDc)
    {
        var row = new StackPanel();
        return SetupAcDcRow(row, powerModes, savedAc, savedDc);
    }

    private static (StackPanel Row, ComboBox AcCombo, ComboBox DcCombo) SetupAcDcRow(
        StackPanel row, WindowsPowerMode[] powerModes, WindowsPowerMode savedAc, WindowsPowerMode savedDc)
    {
        var grayBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

        var grid = new Grid
        {
            Margin = new Thickness(0, 6, 0, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "AcLabel" },
                new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "AcCombo" },
                new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "DcLabel" },
                new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "DcCombo" },
            }
        };

        var acLabel = new TextBlock
        {
            Text = Resource.WindowsPowerPlansWindow_PowerMode_AC,
            FontSize = 11,
            Foreground = grayBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        Grid.SetColumn(acLabel, 0);
        var acCombo = new ComboBox { Width = 120, MaxDropDownHeight = 300 };
        Grid.SetColumn(acCombo, 1);
        acCombo.SetItems(powerModes, savedAc, pm => pm.GetDisplayName());

        var dcLabel = new TextBlock
        {
            Text = Resource.WindowsPowerPlansWindow_PowerMode_DC,
            FontSize = 11,
            Foreground = grayBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 4, 0)
        };
        Grid.SetColumn(dcLabel, 2);
        var dcCombo = new ComboBox { Width = 120, MaxDropDownHeight = 300 };
        Grid.SetColumn(dcCombo, 3);
        dcCombo.SetItems(powerModes, savedDc, pm => pm.GetDisplayName());

        grid.Children.Add(acLabel);
        grid.Children.Add(acCombo);
        grid.Children.Add(dcLabel);
        grid.Children.Add(dcCombo);

        row.Children.Add(grid);

        return (row, acCombo, dcCombo);
    }

    private async Task WindowsPowerModeAcDcChangedAsync(WindowsPowerMode windowsPowerMode, PowerModeState powerModeState, bool isAc)
    {
        if (IsRefreshing)
            return;

        if (isAc)
            _settings.Store.Overrides.SetPowerModeOnAc(powerModeState, windowsPowerMode);
        else
            _settings.Store.Overrides.SetPowerModeOnDc(powerModeState, windowsPowerMode);

        _settings.SynchronizeStore();

        await _powerModeFeature.EnsureCorrectWindowsPowerSettingsAreSetAsync();
    }

    private async Task GodModePresetPowerModeChangedAsync(string presetKey, WindowsPowerMode windowsPowerMode, bool isAc)
    {
        if (IsRefreshing)
            return;

        var presetKvp = _godModeSettings.Store.Presets.FirstOrDefault(profile => profile.Key.ToString() == presetKey);

        if (!presetKvp.Equals(default(KeyValuePair<Guid, GodModeSettingsStore.Preset>)) && presetKvp.Value != null)
        {
            var newOv = new Dictionary<PowerOverrideKey, string>(presetKvp.Value.Overrides ?? []);
            if (isAc)
                newOv[PowerOverrideKey.PowerModeOnAc] = windowsPowerMode.ToString();
            else
                newOv[PowerOverrideKey.PowerModeOnDc] = windowsPowerMode.ToString();
            var updated = presetKvp.Value with { Overrides = newOv };
            _godModeSettings.Store.Presets[presetKvp.Key] = updated;
            _godModeSettings.SynchronizeStore();

            await _powerModeFeature.EnsureCorrectWindowsPowerSettingsAreSetAsync(updated);
        }
    }
}