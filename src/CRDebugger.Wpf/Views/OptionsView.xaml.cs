using System.Windows;
using System.Windows.Controls;
using CRDebugger.Core.ViewModels;
using CRDebugger.Wpf.Controls;

namespace CRDebugger.Wpf.Views;

/// <summary>
/// アプリケーション設定の一覧を表示する UserControl。
/// 各オプション項目に対して OptionControlFactory が適切な WPF コントロールを動的生成する。
/// </summary>
public partial class OptionsView : UserControl
{
    /// <summary>
    /// OptionsView を初期化する
    /// </summary>
    public OptionsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// ItemsControl がロードされたときのイベントハンドラ
    /// </summary>
    /// <param name="sender">イベント送信元</param>
    /// <param name="e">ルーティングイベント引数</param>
    private void OnOptionItemsLoaded(object sender, RoutedEventArgs e)
    {
        // ItemsControl のロード時処理（必要に応じて追加実装）
    }

    /// <summary>
    /// 各オプション項目の ContentPresenter がロードされたときのイベントハンドラ。
    /// OptionControlFactory で動的に生成したコントロールを ContentPresenter に差し込み、
    /// テーマのテキスト色（OnSurfaceBrush）を適用する。
    /// </summary>
    /// <param name="sender">ロードされた ContentPresenter</param>
    /// <param name="e">ルーティングイベント引数</param>
    private void OnOptionItemLoaded(object sender, RoutedEventArgs e)
    {
        // ContentPresenter の Content が OptionItemViewModel の場合にコントロールを動的生成
        if (sender is ContentPresenter presenter && presenter.Content is OptionItemViewModel item)
        {
            // OptionControlFactory でオプションの種類に応じたコントロールを生成
            var control = OptionControlFactory.CreateControl(item);

            // 生成したコントロールが Control の場合はテーマの文字色を適用
            if (control is System.Windows.Controls.Control ctrl)
            {
                ctrl.Foreground = FindResource("OnSurfaceBrush") as System.Windows.Media.Brush;
            }

            // 一度 null にしてから再設定することでバインディングを更新
            presenter.Content = null;
            presenter.Content = control;
        }
    }
}
