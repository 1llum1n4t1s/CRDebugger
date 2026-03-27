using Avalonia.Controls;

namespace CRDebugger.Avalonia.Views;

/// <summary>
/// バグレポート送信フォームの Avalonia UserControl。
/// AXAML ファイル（BugReporterView.axaml）と対になるコードビハインド。
/// DataContext には <see cref="CRDebugger.Core.ViewModels.BugReporterViewModel"/> を設定する。
/// </summary>
public partial class BugReporterView : UserControl
{
    /// <summary>
    /// BugReporterView を初期化し、AXAML で定義したコンポーネントをロードする。
    /// </summary>
    public BugReporterView()
    {
        InitializeComponent();
    }
}
