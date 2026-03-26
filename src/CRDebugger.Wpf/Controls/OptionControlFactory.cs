using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using CRDebugger.Core.Options;
using CRDebugger.Core.ViewModels;

namespace CRDebugger.Wpf.Controls;

/// <summary>
/// OptionDescriptor の Kind に基づいて WPF コントロールを動的生成するファクトリ
/// </summary>
public static class OptionControlFactory
{
    /// <summary>
    /// OptionItemViewModel から適切な WPF コントロールを生成する
    /// </summary>
    public static FrameworkElement CreateControl(OptionItemViewModel item)
    {
        // ActionItemViewModel の場合はボタンを生成
        if (item is ActionItemViewModel actionItem)
        {
            return CreateActionButton(actionItem);
        }

        return item.Kind switch
        {
            OptionKind.Boolean => CreateBooleanControl(item),
            OptionKind.Integer => CreateIntegerControl(item),
            OptionKind.Float => CreateFloatControl(item),
            OptionKind.String => CreateStringControl(item),
            OptionKind.Enum => CreateEnumControl(item),
            OptionKind.ReadOnly => CreateReadOnlyControl(item),
            _ => CreateReadOnlyControl(item)
        };
    }

    private static FrameworkElement CreateBooleanControl(OptionItemViewModel item)
    {
        var panel = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

        var label = CreateLabel(item.DisplayName);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        var toggle = new ToggleButton
        {
            IsEnabled = !item.IsReadOnly,
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 50,
            Padding = new Thickness(8, 4, 8, 4)
        };

        var binding = new Binding(nameof(OptionItemViewModel.Value))
        {
            Source = item,
            Mode = item.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        toggle.SetBinding(ToggleButton.IsCheckedProperty, binding);
        panel.Children.Add(toggle);

        return panel;
    }

    private static FrameworkElement CreateIntegerControl(OptionItemViewModel item)
    {
        var panel = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

        var label = CreateLabel(item.DisplayName);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        if (item.Min.HasValue && item.Max.HasValue)
        {
            var sliderPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var valueText = new TextBlock
            {
                Width = 40,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            valueText.SetBinding(TextBlock.TextProperty, new Binding(nameof(OptionItemViewModel.Value))
            {
                Source = item,
                Mode = BindingMode.OneWay,
                StringFormat = "0"
            });

            var slider = new Slider
            {
                Minimum = item.Min.Value,
                Maximum = item.Max.Value,
                TickFrequency = item.Step ?? 1,
                IsSnapToTickEnabled = true,
                Width = 150,
                IsEnabled = !item.IsReadOnly,
                VerticalAlignment = VerticalAlignment.Center
            };
            slider.SetBinding(RangeBase.ValueProperty, new Binding(nameof(OptionItemViewModel.Value))
            {
                Source = item,
                Mode = item.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            sliderPanel.Children.Add(valueText);
            sliderPanel.Children.Add(slider);
            panel.Children.Add(sliderPanel);
        }
        else
        {
            var textBox = new TextBox
            {
                Width = 80,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsReadOnly = item.IsReadOnly
            };
            textBox.SetBinding(TextBox.TextProperty, new Binding(nameof(OptionItemViewModel.Value))
            {
                Source = item,
                Mode = item.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            });
            panel.Children.Add(textBox);
        }

        return panel;
    }

    private static FrameworkElement CreateFloatControl(OptionItemViewModel item)
    {
        var panel = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

        var label = CreateLabel(item.DisplayName);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        if (item.Min.HasValue && item.Max.HasValue)
        {
            var sliderPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var valueText = new TextBlock
            {
                Width = 50,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            valueText.SetBinding(TextBlock.TextProperty, new Binding(nameof(OptionItemViewModel.Value))
            {
                Source = item,
                Mode = BindingMode.OneWay,
                StringFormat = "F2"
            });

            var slider = new Slider
            {
                Minimum = item.Min.Value,
                Maximum = item.Max.Value,
                TickFrequency = item.Step ?? 0.1,
                Width = 150,
                IsEnabled = !item.IsReadOnly,
                VerticalAlignment = VerticalAlignment.Center
            };
            slider.SetBinding(RangeBase.ValueProperty, new Binding(nameof(OptionItemViewModel.Value))
            {
                Source = item,
                Mode = item.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            sliderPanel.Children.Add(valueText);
            sliderPanel.Children.Add(slider);
            panel.Children.Add(sliderPanel);
        }
        else
        {
            var textBox = new TextBox
            {
                Width = 80,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsReadOnly = item.IsReadOnly
            };
            textBox.SetBinding(TextBox.TextProperty, new Binding(nameof(OptionItemViewModel.Value))
            {
                Source = item,
                Mode = item.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            });
            panel.Children.Add(textBox);
        }

        return panel;
    }

    private static FrameworkElement CreateStringControl(OptionItemViewModel item)
    {
        var panel = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

        var label = CreateLabel(item.DisplayName);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        var textBox = new TextBox
        {
            MinWidth = 150,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsReadOnly = item.IsReadOnly
        };
        textBox.SetBinding(TextBox.TextProperty, new Binding(nameof(OptionItemViewModel.Value))
        {
            Source = item,
            Mode = item.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
        });
        panel.Children.Add(textBox);

        return panel;
    }

    private static FrameworkElement CreateEnumControl(OptionItemViewModel item)
    {
        var panel = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

        var label = CreateLabel(item.DisplayName);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        var comboBox = new ComboBox
        {
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsEnabled = !item.IsReadOnly
        };

        if (item.EnumNames != null)
        {
            foreach (var name in item.EnumNames)
                comboBox.Items.Add(name);
        }

        comboBox.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(OptionItemViewModel.Value))
        {
            Source = item,
            Mode = item.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
            Converter = new EnumToStringConverter()
        });

        panel.Children.Add(comboBox);
        return panel;
    }

    private static FrameworkElement CreateReadOnlyControl(OptionItemViewModel item)
    {
        var panel = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

        var label = CreateLabel(item.DisplayName);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        var valueText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.7
        };
        valueText.SetBinding(TextBlock.TextProperty, new Binding(nameof(OptionItemViewModel.Value))
        {
            Source = item,
            Mode = BindingMode.OneWay,
            StringFormat = "{0}"
        });
        panel.Children.Add(valueText);

        return panel;
    }

    private static FrameworkElement CreateActionButton(ActionItemViewModel actionItem)
    {
        var button = new Button
        {
            Content = actionItem.Label,
            Command = actionItem.ExecuteCommand,
            Margin = new Thickness(0, 4, 0, 4),
            Padding = new Thickness(16, 8, 16, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        return button;
    }

    private static TextBlock CreateLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
    }
}

/// <summary>
/// Enum値と文字列の相互変換用コンバーター
/// </summary>
internal sealed class EnumToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter,
        System.Globalization.CultureInfo culture)
    {
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter,
        System.Globalization.CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrEmpty(s))
            return s;
        return value ?? string.Empty;
    }
}
