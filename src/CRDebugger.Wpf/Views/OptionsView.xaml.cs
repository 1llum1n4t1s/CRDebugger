using System.Windows;
using System.Windows.Controls;
using CRDebugger.Core.ViewModels;
using CRDebugger.Wpf.Controls;

namespace CRDebugger.Wpf.Views;

/// <summary>
/// オプションビュー - OptionControlFactory で動的にコントロールを生成
/// </summary>
public partial class OptionsView : UserControl
{
    public OptionsView()
    {
        InitializeComponent();
    }

    private void OnOptionItemsLoaded(object sender, RoutedEventArgs e)
    {
        // ItemsControl のロード時処理（必要に応じて）
    }

    private void OnOptionItemLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ContentPresenter presenter && presenter.Content is OptionItemViewModel item)
        {
            var control = OptionControlFactory.CreateControl(item);
            if (control is System.Windows.Controls.Control ctrl)
            {
                ctrl.Foreground = FindResource("OnSurfaceBrush") as System.Windows.Media.Brush;
            }
            presenter.Content = null;
            presenter.Content = control;
        }
    }
}
