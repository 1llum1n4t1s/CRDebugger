using Avalonia.Controls;

namespace CRDebugger.Avalonia.Views;

/// <summary>
/// プロファイラータブの Avalonia UserControl。
/// AXAML ファイル（ProfilerView.axaml）と対になるコードビハインド。
/// DataContext には <see cref="CRDebugger.Core.ViewModels.ProfilerViewModel"/> を設定する。
/// </summary>
public partial class ProfilerView : UserControl
{
    /// <summary>
    /// ProfilerView を初期化し、AXAML で定義したコンポーネントをロードする。
    /// </summary>
    public ProfilerView()
    {
        InitializeComponent();
    }
}
