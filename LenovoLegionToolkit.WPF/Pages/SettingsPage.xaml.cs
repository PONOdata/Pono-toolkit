using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Controls.Settings;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Pages;

public partial class SettingsPage
{
    private SettingsAppearanceControl? _appearanceControl;
    private SettingsAppBehaviorControl? _appBehaviorControl;
    private SettingsSoftwareControlControl? _softwareControlControl;
    private SettingsSmartKeysControl? _smartKeysControl;
    private SettingsDisplayControl? _displayControl;
    private SettingsUpdateControl? _updateControl;
    private SettingsPowerControl? _powerControl;
    private SettingsIntegrationsControl? _integrationsControl;

    private bool _isInitialized;
    private bool _isInitializing;

    public SettingsPage()
    {
        InitializeComponent();

        IsVisibleChanged += SettingsPage_IsVisibleChanged;
    }

    private async void SettingsPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            if (IsVisible)
                await InitializeAsync();
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to initialize settings page.", ex);
        }
    }

    private async Task InitializeAsync()
    {
        if (!_isInitialized)
        {
            _isInitializing = true;

            _appearanceControl = new SettingsAppearanceControl();
            _appBehaviorControl = new SettingsAppBehaviorControl();
            _softwareControlControl = new SettingsSoftwareControlControl();
            _smartKeysControl = new SettingsSmartKeysControl();
            _displayControl = new SettingsDisplayControl();
            _updateControl = new SettingsUpdateControl();
            _powerControl = new SettingsPowerControl();
            _integrationsControl = new SettingsIntegrationsControl();

            _softwareControlControl.FnKeysStatusChanged += SoftwareControl_FnKeysStatusChanged;

            InitializeNavigationItems();
            _isInitialized = true;
            _isInitializing = false;
        }

        await RefreshCurrentControlAsync();
    }

    private void InitializeNavigationItems()
    {
        var navigationItems = new List<NavigationItem>
        {
            new() { Key = "AppBehavior", Title = WPF.Resources.Resource.SettingsPage_Category_AppBehavior, Icon = SymbolRegular.Apps24 },
            new() { Key = "Appearance", Title = WPF.Resources.Resource.SettingsPage_Category_Appearance, Icon = SymbolRegular.PaintBrush24 },
            new() { Key = "Power", Title = WPF.Resources.Resource.SettingsPage_Category_Power, Icon = SymbolRegular.Battery024 },
            new() { Key = "Display", Title = WPF.Resources.Resource.SettingsPage_Category_Display, Icon = SymbolRegular.Desktop24 },
            new() { Key = "SmartKeys", Title = WPF.Resources.Resource.SettingsPage_Category_SmartKeys, Icon = SymbolRegular.Keyboard24 },
            new() { Key = "SoftwareControl", Title = WPF.Resources.Resource.SettingsPage_Category_SoftwareControl, Icon = SymbolRegular.ShieldTask24 },
            new() { Key = "Updates", Title = WPF.Resources.Resource.SettingsPage_Category_Updates, Icon = SymbolRegular.ArrowSync24 },
            new() { Key = "Integrations", Title = WPF.Resources.Resource.SettingsPage_Category_Integrations, Icon = SymbolRegular.PlugConnected24 }
        };

        _navigationListBox.ItemsSource = navigationItems;
        _navigationListBox.SelectedIndex = 0;
    }

    private async void NavigationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_navigationListBox.SelectedItem is not NavigationItem selectedItem)
            return;

        UserControl? controlToShow = selectedItem.Key switch
        {
            "Appearance" => _appearanceControl,
            "AppBehavior" => _appBehaviorControl,
            "SoftwareControl" => _softwareControlControl,
            "SmartKeys" => _smartKeysControl,
            "Display" => _displayControl,
            "Updates" => _updateControl,
            "Power" => _powerControl,
            "Integrations" => _integrationsControl,
            _ => null
        };

        if (controlToShow is null)
            return;

        _contentControl.Content = controlToShow;
        PlayTransitionAnimation();

        if (_isInitializing)
            return;

        await RefreshControlAsync(selectedItem.Key);
    }

    private async Task RefreshCurrentControlAsync()
    {
        if (_navigationListBox.SelectedItem is NavigationItem selectedItem)
            await RefreshControlAsync(selectedItem.Key);
    }

    private async Task RefreshControlAsync(string key)
    {
        try
        {
            switch (key)
            {
                case "Appearance":
                    if (_appearanceControl is not null) await _appearanceControl.RefreshAsync();
                    break;
                case "AppBehavior":
                    if (_appBehaviorControl is not null) await _appBehaviorControl.RefreshAsync();
                    break;
                case "SoftwareControl":
                    if (_softwareControlControl is not null) await _softwareControlControl.RefreshAsync();
                    break;
                case "SmartKeys":
                    if (_smartKeysControl is not null) await _smartKeysControl.RefreshAsync();
                    break;
                case "Display":
                    if (_displayControl is not null) await _displayControl.RefreshAsync();
                    break;
                case "Updates":
                    if (_updateControl is not null) await _updateControl.RefreshAsync();
                    break;
                case "Power":
                    if (_powerControl is not null) await _powerControl.RefreshAsync();
                    break;
                case "Integrations":
                    if (_integrationsControl is not null) await _integrationsControl.RefreshAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to refresh settings control '{key}'.", ex);
        }
    }

    private void SoftwareControl_FnKeysStatusChanged(object? sender, SoftwareStatus fnKeysStatus)
    {
        _smartKeysControl?.UpdateVisibilityBasedOnFnKeys(fnKeysStatus);
    }

    private void PlayTransitionAnimation()
    {
        if (TryFindResource("ContentTransitionAnimation") is Storyboard storyboard)
            storyboard.Begin();
    }

    private class NavigationItem
    {
        public string Key { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public SymbolRegular Icon { get; init; }
    }
}
