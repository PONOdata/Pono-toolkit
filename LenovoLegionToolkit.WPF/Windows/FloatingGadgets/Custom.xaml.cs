using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.Sensors;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;

namespace LenovoLegionToolkit.WPF.Windows.FloatingGadgets;

public class GadgetItemGroup
{
    public string Header { get; set; } = string.Empty;
    public List<FloatingGadgetItem> Items { get; set; } = new List<FloatingGadgetItem>();
}

public partial class Custom
{
    private static Custom? _instance;

    private readonly FloatingGadgetSettings _floatingGadgetSettings = IoCContainer.Resolve<FloatingGadgetSettings>();
    private readonly SensorsGroupController _controller = IoCContainer.Resolve<SensorsGroupController>();
    private bool _isInitializing = true;

    public Custom()
    {
        InitializeComponent();
        this.Loaded += Custom_Loaded;
    }

    public static void ShowInstance()
    {
        if (_instance == null)
        {
            _instance = new Custom();
            _instance.Closed += (s, e) => _instance = null;
            _instance.Show();
        }
        else
        {
            if (_instance.WindowState == WindowState.Minimized)
                _instance.WindowState = WindowState.Normal;
            _instance.Activate();
        }
    }

    private void Custom_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeCheckboxes();
        
        // Setup Interval and Style controls
        _floatingGadgetsInterval.Value = _floatingGadgetSettings.Store.FloatingGadgetsRefreshInterval;
        _floatingGadgetsStyleComboBox.SelectedIndex = _floatingGadgetSettings.Store.SelectedStyleIndex;
        
        _isInitializing = false;
    }

    private void InitializeCheckboxes()
    {
        var groups = new List<GadgetItemGroup>
        {
            new GadgetItemGroup { Header = Resource.FloatingGadget_Custom_Game, Items =
                [FloatingGadgetItem.Fps, FloatingGadgetItem.LowFps, FloatingGadgetItem.FrameTime]
            },
            new GadgetItemGroup { Header = Resource.FloatingGadget_Custom_CPU, Items =
                [
                    FloatingGadgetItem.CpuUtilization, FloatingGadgetItem.CpuFrequency,
                    FloatingGadgetItem.CpuTemperature,
                    FloatingGadgetItem.CpuPower, FloatingGadgetItem.CpuFan
                ]
            },
            new GadgetItemGroup { Header = Resource.FloatingGadget_Custom_GPU, Items =
                [
                    FloatingGadgetItem.GpuUtilization, FloatingGadgetItem.GpuFrequency,
                    FloatingGadgetItem.GpuTemperature,
                    FloatingGadgetItem.GpuVramTemperature, FloatingGadgetItem.GpuPower, FloatingGadgetItem.GpuFan
                ]
            },
            new GadgetItemGroup { Header = Resource.FloatingGadget_Custom_Chipset, Items =
                [
                    FloatingGadgetItem.MemoryUtilization, FloatingGadgetItem.MemoryTemperature,
                    FloatingGadgetItem.Disk1Temperature, FloatingGadgetItem.Disk2Temperature,
                    FloatingGadgetItem.PchTemperature, FloatingGadgetItem.PchFan
                ]
            }
        };

        // Insert CPU P-Core and E-Core frequency options if the CPU is hybrid arch.
        var cpuGroup = groups[1];
        if (_controller.IsHybrid)
        {
            int baseFrequencyIndex = cpuGroup.Items.IndexOf(FloatingGadgetItem.CpuFrequency);
            if (baseFrequencyIndex >= 0)
            {
                cpuGroup.Items.Insert(baseFrequencyIndex + 1, FloatingGadgetItem.CpuPCoreFrequency);
                cpuGroup.Items.Insert(baseFrequencyIndex + 2, FloatingGadgetItem.CpuECoreFrequency);
                cpuGroup.Items.Remove(FloatingGadgetItem.CpuFrequency);
            }
        }

        var activeItems = new HashSet<FloatingGadgetItem>(_floatingGadgetSettings.Store.Items);

        if (activeItems.Count == 0)
        {
            activeItems = new HashSet<FloatingGadgetItem>(
                _floatingGadgetSettings.Store.Items.Cast<FloatingGadgetItem>()
            );
        }

        foreach (var group in groups)
        {
            var groupBox = new GroupBox
            {
                Header = group.Header,
                Padding = new Thickness(10, 5, 5, 10)
            };

            var stackPanel = new StackPanel();

            foreach (var item in group.Items)
            {
                var checkBox = new CheckBox
                {
                    Content = item.GetDisplayName(),
                    Tag = item,
                    IsChecked = activeItems.Contains(item)
                };
                checkBox.Checked += CheckBox_CheckedOrUnchecked;
                checkBox.Unchecked += CheckBox_CheckedOrUnchecked;

                stackPanel.Children.Add(checkBox);
            }

            groupBox.Content = stackPanel;
            _itemsStackPanel.Children.Add(groupBox);
        }
    }

    private void CheckBox_CheckedOrUnchecked(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var selectedItems = new List<FloatingGadgetItem>();

        foreach (var groupBox in _itemsStackPanel.Children.OfType<GroupBox>())
        {
            if (groupBox.Content is StackPanel stackPanel)
            {
                foreach (var child in stackPanel.Children.OfType<CheckBox>())
                {
                    if (child is { IsChecked: true, Tag: FloatingGadgetItem item })
                    {
                        selectedItems.Add(item);
                    }
                }
            }
        }

        _floatingGadgetSettings.Store.Items = selectedItems;
        _floatingGadgetSettings.SynchronizeStore();
        MessagingCenter.Publish(new FloatingGadgetElementChangedMessage(selectedItems));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void FloatingGadgetsInput_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || !IsLoaded)
            return;

        _floatingGadgetSettings.Store.FloatingGadgetsRefreshInterval = (int)(_floatingGadgetsInterval.Value ?? 1);
        _floatingGadgetSettings.SynchronizeStore();
    }

    private void StyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || !IsLoaded)
            return;

        try
        {
            _floatingGadgetSettings.Store.SelectedStyleIndex = _floatingGadgetsStyleComboBox.SelectedIndex;
            _floatingGadgetSettings.SynchronizeStore();

            if (_floatingGadgetSettings.Store.ShowFloatingGadgets && App.Current.FloatingGadget != null)
            {
                var styleTypeMapping = new Dictionary<int, Type>
                {
                    [0] = typeof(FloatingGadget),
                    [1] = typeof(FloatingGadgetUpper)
                };

                var constructorMapping = new Dictionary<int, Func<Window>>
                {
                    [0] = () => new FloatingGadget(),
                    [1] = () => new FloatingGadgetUpper()
                };

                int selectedStyle = _floatingGadgetSettings.Store.SelectedStyleIndex;
                if (styleTypeMapping.TryGetValue(selectedStyle, out Type? targetType) &&
                    App.Current.FloatingGadget.GetType() != targetType)
                {
                    var oldGadgetPos = new Point(App.Current.FloatingGadget.Left, App.Current.FloatingGadget.Top);
                    App.Current.FloatingGadget.Close();

                    if (constructorMapping.TryGetValue(selectedStyle, out Func<Window>? constructor))
                    {
                        App.Current.FloatingGadget = constructor();
                        App.Current.FloatingGadget.Left = oldGadgetPos.X;
                        App.Current.FloatingGadget.Top = oldGadgetPos.Y;
                        App.Current.FloatingGadget.Show();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"StyleComboBox_SelectionChanged error: {ex.Message}");

            _isInitializing = true;
            _floatingGadgetsStyleComboBox.SelectedIndex = _floatingGadgetSettings.Store.SelectedStyleIndex;
            _isInitializing = false;
        }
    }
}