using CRDebugger.Core;
using CRDebugger.Core.Options.Attributes;
using CRDebugger.Core.Theming;
using CRDebugger.WinForms;

namespace Sample.WinForms;

public class MainForm : Form
{
    private int _logCount;

    public MainForm()
    {
        Text = "CRDebugger WinForms Sample";
        Size = new System.Drawing.Size(600, 400);
        StartPosition = FormStartPosition.CenterScreen;

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(20),
        };

        var title = new Label
        {
            Text = "CRDebugger WinForms サンプル",
            Font = new System.Drawing.Font("Segoe UI", 18, System.Drawing.FontStyle.Bold),
            AutoSize = true
        };

        var desc = new Label { Text = "下のボタンでデバッガーを開きます", AutoSize = true };

        var btnOpen = new Button { Text = "CRDebugger を開く (F12)", Size = new System.Drawing.Size(200, 35) };
        btnOpen.Click += (_, _) => CRDebugger.Core.CRDebugger.Show();

        var btnLog = new Button { Text = "ログを追加", Size = new System.Drawing.Size(200, 35) };
        btnLog.Click += (_, _) => CRDebugger.Core.CRDebugger.Log($"サンプルログメッセージ #{++_logCount}");

        var btnWarn = new Button { Text = "警告を追加", Size = new System.Drawing.Size(200, 35) };
        btnWarn.Click += (_, _) => CRDebugger.Core.CRDebugger.LogWarning($"サンプル警告 #{++_logCount}");

        var btnErr = new Button { Text = "エラーを追加", Size = new System.Drawing.Size(200, 35) };
        btnErr.Click += (_, _) =>
        {
            try { throw new InvalidOperationException("テスト例外"); }
            catch (Exception ex) { CRDebugger.Core.CRDebugger.LogError($"サンプルエラー #{++_logCount}", ex); }
        };

        panel.Controls.AddRange(new Control[] { title, desc, btnOpen, btnLog, btnWarn, btnErr });
        Controls.Add(panel);

        // CRDebugger初期化
        if (!CRDebugger.Core.CRDebugger.IsInitialized)
        {
            var options = new CRDebuggerOptions();
            options.UseWinForms();
            options.Theme = CRTheme.Dark;
            options.DefaultTab = CRTab.Console;
            CRDebugger.Core.CRDebugger.Initialize(options);

            CRDebugger.Core.CRDebugger.AddOptionContainer(new SampleOptions());
            CRDebugger.Core.CRDebugger.Log("CRDebugger が初期化されました");
        }

        // F12キーでデバッガー開閉
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F12)
                CRDebugger.Core.CRDebugger.Toggle();
        };
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
