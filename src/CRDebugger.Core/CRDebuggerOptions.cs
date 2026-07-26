using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Options;
using CRDebugger.Core.Theming;

namespace CRDebugger.Core;

/// <summary>
/// BugReport に含める SystemInfo の収集レベル。
/// secure-by-default のため、デフォルトは Standard（UserName / Command Line を除外）。
/// </summary>
public enum SystemInfoCollectionLevel
{
    /// <summary>OS 種別と .NET バージョンのみ。最小限の情報。</summary>
    Minimal,
    /// <summary>UserName / Command Line / Current Directory を除外（デフォルト）。GDPR/CCPA に配慮。</summary>
    Standard,
    /// <summary>全項目を含める（旧 1.0.x 互換動作）。明示オプトイン時のみ推奨。</summary>
    Full
}

/// <summary>
/// CRDebugger初期化オプション
/// </summary>
public sealed class CRDebuggerOptions
{
    /// <summary>
    /// CRDebugger を有効にするか（デフォルト true）。
    /// false の場合、Initialize はコンテキストを生成せず、Log/Show 等の API は no-op になる。
    /// Release ビルドで `options.IsEnabled = !Debugger.IsAttached` 等の条件で本番無効化に使う。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

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

    /// <summary>
    /// SuperLightLogger のファイルログ出力先パス。
    /// ${shortdate} 等のNLogレイアウト変数が使用可能。
    /// nullの場合はファイルログ出力を行わない。
    /// 例: "logs/app_${shortdate}.log"
    /// <para>
    /// <b>重要</b>: このプロパティを設定しただけではファイルログは有効にならない。
    /// ホストアプリが既に構成済みの LogManager を壊さないため、
    /// <see cref="AttachToSuperLightLoggerManager"/> も <c>true</c> にする必要がある（2 条件の AND）。
    /// </para>
    /// </summary>
    public string? FileLogPath { get; set; }

    /// <summary>
    /// SuperLightLogger のグローバル LogManager を CRDebugger が再構成するか（デフォルト false）。
    /// false の場合、ホストアプリが既に設定済みの LogManager を尊重する。
    /// true にすると CRDebugger が <see cref="FileLogPath"/> を含む構成で LogManager.Configure を呼ぶ。
    /// <para>ファイルログを有効にするには、これを <c>true</c> にした上で <see cref="FileLogPath"/> も設定すること。</para>
    /// </summary>
    public bool AttachToSuperLightLoggerManager { get; set; } = false;

    /// <summary>
    /// Options タブに公開するプロパティ判定モード。
    /// false（デフォルト・SRDebugger 互換）: 全 public プロパティを対象とする opt-out モード。
    /// true: <see cref="CROptionAttribute"/> 付きのプロパティのみを対象とする opt-in モード。
    /// </summary>
    public bool RequireOptInAttribute { get; set; } = false;

    /// <summary>
    /// BugReport に含める SystemInfo の収集レベル（デフォルト Standard）。
    /// secure-by-default のため、デフォルトでは UserName / Command Line / Current Directory を除外する。
    /// </summary>
    public SystemInfoCollectionLevel SystemInfoCollectionLevel { get; set; } = SystemInfoCollectionLevel.Standard;

    /// <summary>
    /// BugReport 送信時のタイムアウト（デフォルト 60 秒）。
    /// スクリーンショット取得・Sender 送信ハングを防ぐ。
    /// </summary>
    public TimeSpan BugReportSendTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Options 永続化ストア（デフォルト null = 永続化しない）。
    /// <see cref="JsonFileOptionsStore"/> 等を指定すると、ユーザーが UI で変更した値を保存/復元できる。
    /// </summary>
    public IOptionsStore? OptionsStore { get; set; }

    // UIフレームワーク層が設定（内部使用）
    internal IDebuggerWindow? Window { get; set; }
    internal IUiThread? UiThread { get; set; }
    internal IThemeProvider? ThemeProvider { get; set; }
}
