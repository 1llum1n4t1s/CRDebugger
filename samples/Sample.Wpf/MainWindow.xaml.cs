using System;
using System.Windows;
using System.Windows.Input;
using CRDebugger.Core;
using CRDebugger.Core.Options.Attributes;
using CRDebugger.Core.Theming;
using CRDebugger.Wpf;

namespace Sample.Wpf;

public partial class MainWindow : Window
{
    private int _logCount;

    public MainWindow()
    {
        InitializeComponent();

        // CRDebugger初期化
        if (!CRDebugger.Core.CRDebugger.IsInitialized)
        {
            var options = new CRDebuggerOptions();
            options.UseWpf();
            options.Theme = CRTheme.Dark;
            options.DefaultTab = CRTab.Console;
            CRDebugger.Core.CRDebugger.Initialize(options);

            CRDebugger.Core.CRDebugger.AddOptionContainer(new SampleOptions());
            CRDebugger.Core.CRDebugger.Log("CRDebugger が初期化されました");
        }

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F12)
                CRDebugger.Core.CRDebugger.Toggle();
        };
    }

    private void OnOpenDebugger(object sender, RoutedEventArgs e) =>
        CRDebugger.Core.CRDebugger.Show();

    private void OnAddLog(object sender, RoutedEventArgs e) =>
        CRDebugger.Core.CRDebugger.Log($"サンプルログメッセージ #{++_logCount}");

    private void OnAddWarning(object sender, RoutedEventArgs e) =>
        CRDebugger.Core.CRDebugger.LogWarning($"サンプル警告メッセージ #{++_logCount}");

    private void OnAddError(object sender, RoutedEventArgs e)
    {
        try { throw new InvalidOperationException("テスト例外です"); }
        catch (Exception ex) { CRDebugger.Core.CRDebugger.LogError($"サンプルエラー #{++_logCount}", ex); }
    }
}

public class SampleOptions
{
    [CRCategory("Graphics")]
    [CRDisplayName("画質レベル")]
    [CRRange(0, 5, Step = 1)]
    public int QualityLevel { get; set; } = 3;

    [CRCategory("Graphics")]
    [CRDisplayName("フルスクリーン")]
    public bool IsFullscreen { get; set; }

    [CRCategory("Audio")]
    [CRDisplayName("マスター音量")]
    [CRRange(0, 100)]
    public int MasterVolume { get; set; } = 80;

    [CRCategory("Debug")]
    [CRDisplayName("プレイヤー名")]
    public string PlayerName { get; set; } = "Player1";

    [CRCategory("Debug")]
    [CRAction(Label = "テストログ出力")]
    public void PrintTestLog() =>
        CRDebugger.Core.CRDebugger.Log("Optionsからのテストログ！");
}
