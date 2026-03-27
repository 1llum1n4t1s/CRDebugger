using Avalonia.Controls;

namespace CRDebugger.Avalonia.Views;

/// <summary>
/// ログコンソールタブの Avalonia UserControl。
/// AXAML ファイル（ConsoleView.axaml）と対になるコードビハインド。
/// DataContext には <see cref="CRDebugger.Core.ViewModels.ConsoleViewModel"/> を設定する。
/// </summary>
public partial class ConsoleView : UserControl
{
    /// <summary>
    /// ConsoleView を初期化し、AXAML で定義したコンポーネントをロードする。
    /// </summary>
    public ConsoleView()
    {
        InitializeComponent();
    }
}
