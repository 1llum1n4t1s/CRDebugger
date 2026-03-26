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

    // UIフレームワーク層が設定（内部使用）
    internal IDebuggerWindow? Window { get; set; }
    internal IUiThread? UiThread { get; set; }
    internal IThemeProvider? ThemeProvider { get; set; }
}
