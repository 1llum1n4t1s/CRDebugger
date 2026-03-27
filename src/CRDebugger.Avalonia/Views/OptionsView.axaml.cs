using Avalonia.Controls;

namespace CRDebugger.Avalonia.Views;

/// <summary>
/// オプション設定タブの Avalonia UserControl。
/// AXAML ファイル（OptionsView.axaml）と対になるコードビハインド。
/// DataContext には <see cref="CRDebugger.Core.ViewModels.OptionsViewModel"/> を設定する。
/// </summary>
public partial class OptionsView : UserControl
{
    /// <summary>
    /// OptionsView を初期化し、AXAML で定義したコンポーネントをロードする。
    /// </summary>
    public OptionsView()
    {
        InitializeComponent();
    }
}
