using Avalonia.Controls;

namespace CRDebugger.Avalonia.Views;

/// <summary>
/// システム情報タブの Avalonia UserControl。
/// AXAML ファイル（SystemInfoView.axaml）と対になるコードビハインド。
/// DataContext には <see cref="CRDebugger.Core.ViewModels.SystemInfoViewModel"/> を設定する。
/// </summary>
public partial class SystemInfoView : UserControl
{
    /// <summary>
    /// SystemInfoView を初期化し、AXAML で定義したコンポーネントをロードする。
    /// </summary>
    public SystemInfoView()
    {
        InitializeComponent();
    }
}
