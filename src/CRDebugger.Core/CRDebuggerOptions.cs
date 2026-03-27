using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Theming;

namespace CRDebugger.Core;

/// <summary>
/// CRDebugger初期化オプション
/// </summary>
public sealed class CRDebuggerOptions
{
    /// <summary>UIテーマ（デフォルト: System）</summary>
    public CRTheme Theme { get; set; } = CRTheme.System;

    /// <summary>初期表示するタブ（デフォルト: Console）</summary>
    public CRTab DefaultTab { get; set; } = CRTab.Console;

    /// <summary>ログバッファの最大保持件数（デフォルト: 2000）</summary>
    public int MaxLogEntries { get; set; } = 2000;

    /// <summary>System.Diagnostics.Trace/Debug 出力をキャプチャするか（デフォルト: true）</summary>
    public bool CaptureTraceOutput { get; set; } = true;

    /// <summary>未処理例外をキャプチャするか（デフォルト: true）</summary>
    public bool CaptureUnhandledExceptions { get; set; } = true;

    /// <summary>プロファイラーのサンプリング間隔（デフォルト: 500ms）</summary>
    public TimeSpan ProfilerSampleInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>バグレポートの送信先。<c>null</c> の場合はコンソール出力</summary>
    public IBugReportSender? BugReportSender { get; set; }

    /// <summary>ウィンドウの初期幅（ピクセル）</summary>
    public double WindowWidth { get; set; } = 900;

    /// <summary>ウィンドウの初期高さ（ピクセル）</summary>
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
