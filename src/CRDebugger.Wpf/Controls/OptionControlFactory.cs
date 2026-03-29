using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using CRDebugger.Core.Options;
using CRDebugger.Core.ViewModels;

namespace CRDebugger.Wpf.Controls;

/// <summary>
/// OptionDescriptor の Kind に基づいて WPF コントロールを動的生成するファクトリクラス。
/// Boolean はトグルボタン、Integer/Float はスライダーまたはテキストボックス、
/// String はテキストボックス、Enum はコンボボックス、ReadOnly はテキストブロックとして生成する。
/// </summary>
public static class OptionControlFactory
{
    /// <summary>
    /// OptionItemViewModel の Kind に応じた WPF コントロールを生成して返す
    /// </summary>
    /// <param name="item">コントロールを生成する対象の OptionItemViewModel</param>
    /// <returns>Kind に対応した WPF FrameworkElement</returns>
    public static FrameworkElement CreateControl(OptionItemViewModel item)
    {
        // Kind に応じたコントロール生成メソッドへ振り分け
        return item.Kind switch
        {
            OptionKind.Boolean => CreateBooleanControl(item),
            OptionKind.Integer => CreateIntegerControl(item),
            OptionKind.Float => CreateFloatControl(item),
            OptionKind.String => CreateStringControl(item),
            OptionKind.Enum => CreateEnumControl(item),
            OptionKind.ReadOnly => CreateReadOnlyControl(item),
            // 未知の Kind はフォールスルーで読み取り専用表示
            _ => CreateReadOnlyControl(item)
        };
    }

    /// <summary>
    /// Boolean 値用のトグルボタンを含む DockPanel を生成する
    /// </summary>
    /// <param name="item">Boolean 値を持つ OptionItemViewModel</param>
    /// <returns>ラベルとトグルボタンを含む DockPanel</returns>
    private static FrameworkElement CreateBooleanControl(OptionItemViewModel item)
    {
        // ラベルと操作コントロールを横並びに配置するパネル
        var panel = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

        // 項目名ラベルを左ドックに配置
        var label = CreateLabel(item.DisplayName);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        // トグルボタンを作成（読み取り専用の場合は無効化）
        var toggle = new ToggleButton
        {
            IsEnabled = !item.IsReadOnly,
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 50,
            Padding = new Thickness(8, 4, 8, 4)
        };

        // Value プロパティと IsChecked を双方向バインディング（読み取り専用は一方向）
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

    /// <summary>
    /// Integer 値用のスライダーまたはテキストボックスを含む DockPanel を生成する。
    /// Min/Max が設定されている場合はスライダー、そうでなければテキストボックスを使用する。
    /// </summary>
    /// <param name="item">Integer 値を持つ OptionItemViewModel</param>
    /// <returns>ラベルとスライダー（またはテキストボックス）を含む DockPanel</returns>
    private static FrameworkElement CreateIntegerControl(OptionItemViewModel item)
    {
        // ラベルと操作コントロールを横並びに配置するパネル
        var panel = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

        // 項目名ラベルを左ドックに配置
        var label = CreateLabel(item.DisplayName);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        // Min/Max が両方設定されている場合はスライダーで表示
        if (item.Min.HasValue && item.Max.HasValue)
        {
            // 現在値テキストとスライダーを横並びにするパネル
            var sliderPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            // 現在値をテキストで表示（整数フォーマット）
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
                StringFormat = "0"  // 整数フォーマットで表示
            });

            // スライダーを作成（Min/Max/Step を適用）
            var slider = new Slider
            {
                Minimum = item.Min.Value,
                Maximum = item.Max.Value,
                TickFrequency = item.Step ?? 1,  // Step 未設定の場合は 1 刻み
                IsSnapToTickEnabled = true,        // 目盛りにスナップ
                Width = 150,
                IsEnabled = !item.IsReadOnly,
                VerticalAlignment = VerticalAlignment.Center
            };
            // Value プロパティとスライダー値を双方向バインディング
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
            // Min/Max が未設定の場合はテキストボックスで直接入力
            var textBox = new TextBox
            {
                Width = 80,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsReadOnly = item.IsReadOnly
            };
            // フォーカス離脱時に値を更新（LostFocus）
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

    /// <summary>
    /// Float 値用のスライダーまたはテキストボックスを含む DockPanel を生成する。
    /// Min/Max が設定されている場合はスライダー、そうでなければテキストボックスを使用する。
    /// </summary>
    /// <param name="item">Float 値を持つ OptionItemViewModel</param>
    /// <returns>ラベルとスライダー（またはテキストボックス）を含む DockPanel</returns>
    private static FrameworkElement CreateFloatControl(OptionItemViewModel item)
    {
        // ラベルと操作コントロールを横並びに配置するパネル
        var panel = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

        // 項目名ラベルを左ドックに配置
        var label = CreateLabel(item.DisplayName);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        // Min/Max が両方設定されている場合はスライダーで表示
        if (item.Min.HasValue && item.Max.HasValue)
        {
            // 現在値テキストとスライダーを横並びにするパネル
            var sliderPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            // 現在値を小数点2桁で表示
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
                StringFormat = "F2"  // 小数点2桁フォーマットで表示
            });

            // スライダーを作成（Float 用に Step のデフォルトは 0.1）
            var slider = new Slider
            {
                Minimum = item.Min.Value,
                Maximum = item.Max.Value,
                TickFrequency = item.Step ?? 0.1,  // Step 未設定の場合は 0.1 刻み
                Width = 150,
                IsEnabled = !item.IsReadOnly,
                VerticalAlignment = VerticalAlignment.Center
            };
            // Value プロパティとスライダー値を双方向バインディング
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
            // Min/Max が未設定の場合はテキストボックスで直接入力
            var textBox = new TextBox
            {
                Width = 80,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsReadOnly = item.IsReadOnly
            };
            // フォーカス離脱時に値を更新（LostFocus）
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

    /// <summary>
    /// String 値用のテキストボックスを含む DockPanel を生成する
    /// </summary>
    /// <param name="item">String 値を持つ OptionItemViewModel</param>
    /// <returns>ラベルとテキストボックスを含む DockPanel</returns>
    private static FrameworkElement CreateStringControl(OptionItemViewModel item)
    {
        // ラベルと操作コントロールを横並びに配置するパネル
        var panel = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

        // 項目名ラベルを左ドックに配置
        var label = CreateLabel(item.DisplayName);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        // 文字列入力用テキストボックス（読み取り専用の場合は編集不可）
        var textBox = new TextBox
        {
            MinWidth = 150,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsReadOnly = item.IsReadOnly
        };
        // フォーカス離脱時に値を更新（LostFocus）
        textBox.SetBinding(TextBox.TextProperty, new Binding(nameof(OptionItemViewModel.Value))
        {
            Source = item,
            Mode = item.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
        });
        panel.Children.Add(textBox);

        return panel;
    }

    /// <summary>
    /// Enum 値用のコンボボックスを含む DockPanel を生成する。
    /// EnumNames をドロップダウン選択肢として表示する。
    /// </summary>
    /// <param name="item">Enum 値を持つ OptionItemViewModel</param>
    /// <returns>ラベルとコンボボックスを含む DockPanel</returns>
    private static FrameworkElement CreateEnumControl(OptionItemViewModel item)
    {
        // ラベルと操作コントロールを横並びに配置するパネル
        var panel = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

        // 項目名ラベルを左ドックに配置
        var label = CreateLabel(item.DisplayName);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        // 列挙値選択用コンボボックス（読み取り専用の場合は無効化）
        var comboBox = new ComboBox
        {
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsEnabled = !item.IsReadOnly
        };

        // EnumNames の各文字列をコンボボックスの選択肢として追加
        if (item.EnumNames != null)
        {
            foreach (var name in item.EnumNames)
                comboBox.Items.Add(name);
        }

        // Value プロパティと SelectedItem を EnumToStringConverter 経由で双方向バインディング
        comboBox.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(OptionItemViewModel.Value))
        {
            Source = item,
            Mode = item.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
            Converter = new EnumToStringConverter()
        });

        panel.Children.Add(comboBox);
        return panel;
    }

    /// <summary>
    /// 読み取り専用の値を表示するテキストブロックを含む DockPanel を生成する
    /// </summary>
    /// <param name="item">読み取り専用の値を持つ OptionItemViewModel</param>
    /// <returns>ラベルとテキストブロックを含む DockPanel</returns>
    private static FrameworkElement CreateReadOnlyControl(OptionItemViewModel item)
    {
        // ラベルと値表示を横並びに配置するパネル
        var panel = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

        // 項目名ラベルを左ドックに配置
        var label = CreateLabel(item.DisplayName);
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        // 値を薄いテキストで読み取り専用表示（Opacity 0.7 で灰色調）
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

    /// <summary>
    /// アクション実行用ボタンを生成する
    /// </summary>
    /// <param name="actionItem">ボタンのラベルとコマンドを持つ ActionItemViewModel</param>
    /// <returns>コマンドにバインドされた Button</returns>
    private static FrameworkElement CreateActionButton(ActionItemViewModel actionItem)
    {
        // アクション名をラベルとし、ExecuteCommand をコマンドとして設定
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

    /// <summary>
    /// 項目名表示用の共通ラベルを生成するヘルパーメソッド
    /// </summary>
    /// <param name="text">ラベルに表示するテキスト</param>
    /// <returns>スタイルが適用された TextBlock</returns>
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
/// Enum 値と文字列の相互変換を行う WPF IValueConverter 実装。
/// Enum の ToString() で文字列化し、逆変換はそのまま文字列を返す。
/// </summary>
internal sealed class EnumToStringConverter : IValueConverter
{
    /// <summary>
    /// Enum 値を文字列に変換する
    /// </summary>
    /// <param name="value">変換する Enum 値</param>
    /// <param name="targetType">変換先の型（未使用）</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャ情報（未使用）</param>
    /// <returns>Enum の ToString() 結果、または空文字列</returns>
    public object Convert(object value, Type targetType, object parameter,
        System.Globalization.CultureInfo culture)
    {
        // null の場合は空文字列を返す
        return value?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 文字列を Enum 値に逆変換する（実際には文字列をそのまま返す）
    /// </summary>
    /// <param name="value">変換する文字列</param>
    /// <param name="targetType">変換先の型（未使用）</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャ情報（未使用）</param>
    /// <returns>入力文字列、または元の値</returns>
    public object ConvertBack(object value, Type targetType, object parameter,
        System.Globalization.CultureInfo culture)
    {
        // 文字列の場合はそのまま返す（ViewModel 側で Enum への変換を行う）
        if (value is string s && !string.IsNullOrEmpty(s))
            return s;
        return value ?? string.Empty;
    }
}
