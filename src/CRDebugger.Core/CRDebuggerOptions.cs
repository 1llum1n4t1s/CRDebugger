using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Theming;

namespace CRDebugger.Core;

/// <summary>
/// CRDebugger初期化オプション
/// </summary>
public sealed class CRDebuggerOptions
{
    public CRTheme Theme { get; set; } = CRTheme.System;
    public CRTab DefaultTab { get; set; } = CRTab.Console;
    public int MaxLogEntries { get; set; } = 2000;
    public bool CaptureTraceOutput { get; set; } = true;
    public bool CaptureUnhandledExceptions { get; set; } = true;
    public TimeSpan ProfilerSampleInterval { get; set; } = TimeSpan.FromMilliseconds(500);
    public IBugReportSender? BugReportSender { get; set; }
    public double WindowWidth { get; set; } = 900;
    public double WindowHeight { get; set; } = 600;

    /// <summary>ウィンドウを常に前面に表示するか（デフォルト: false、画面上のピンボタンで切替可能）</summary>
    public bool Topmost { get; set; } = false;

    /// <summary>連続する同一ログを折りたたむ</summary>
    public bool CollapseDuplicateLogs { get; set; } = true;

    /// <summary>キーボードショートカットを有効にする</summary>
    public bool EnableKeyboardShortcuts { get; set; } = true;

    /// <summary>無効化するタブの一覧</summary>
    public HashSet<CRTab> DisabledTabs { get; set; } = new();

    /// <summary>GPU監視プロバイダー（プラットフォーム固有実装を注入）</summary>
    public IGpuMonitor? GpuMonitor { get; set; }

    // UIフレームワーク層が設定（内部使用）
    internal IDebuggerWindow? Window { get; set; }
    internal IUiThread? UiThread { get; set; }
    internal IThemeProvider? ThemeProvider { get; set; }
}
