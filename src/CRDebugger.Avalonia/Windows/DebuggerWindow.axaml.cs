using Avalonia.Controls;
using Avalonia.Interactivity;
using CRDebugger.Core;
using CRDebugger.Core.ViewModels;

namespace CRDebugger.Avalonia.Windows;

/// <summary>
/// デバッガーメインウィンドウ
/// </summary>
public partial class DebuggerWindow : Window
{
    public DebuggerWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// サイドバーのタブボタンクリック処理
    /// </summary>
    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is CRTab tab && DataContext is DebuggerViewModel vm)
        {
            vm.SelectedTab = tab;
        }
    }

    /// <summary>
    /// 閉じるボタンクリック → ウィンドウを非表示にする
    /// </summary>
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    /// <summary>
    /// ウィンドウを閉じるのではなく非表示にする
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
