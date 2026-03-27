using System.Windows.Controls;

namespace CRDebugger.Wpf.Views;

/// <summary>
/// OS・CPU・メモリ・GPU などのシステム情報を一覧表示する UserControl。
/// データは SystemInfoViewModel から XAML バインディングで取得する。
/// </summary>
public partial class SystemInfoView : UserControl
{
    /// <summary>
    /// SystemInfoView を初期化する
    /// </summary>
    public SystemInfoView()
    {
        InitializeComponent();
    }
}
