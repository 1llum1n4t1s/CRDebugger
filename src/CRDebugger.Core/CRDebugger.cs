using CRDebugger.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CRDebugger.Core;

/// <summary>
/// CRDebugger 静的ファサード - メインエントリポイント
/// </summary>
public static class CRDebugger
{
    private static CRDebuggerContext? _context;
    private static readonly object _initLock = new();

    /// <summary>初期化済みかどうか</summary>
    public static bool IsInitialized => _context != null;

    /// <summary>
    /// CRDebuggerを初期化する。UIフレームワーク層の拡張メソッド経由で呼ぶ。
    /// </summary>
    public static void Initialize(CRDebuggerOptions options)
    {
        lock (_initLock)
        {
            if (_context != null)
                throw new InvalidOperationException("CRDebuggerは既に初期化されています。");
            _context = new CRDebuggerContext(options);
        }
    }

    /// <summary>デバッガーウィンドウを表示</summary>
    public static void Show()
    {
        var ctx = GetContext();
        ctx.Window.Show(ctx.RootViewModel);
        ctx.Window.ApplyTheme(ctx.ThemeManager.CurrentColors);
    }

    /// <summary>デバッガーウィンドウを表示して指定タブに切り替え</summary>
    public static void Show(CRTab tab)
    {
        var ctx = GetContext();
        ctx.RootViewModel.SelectedTab = tab;
        Show();
    }

    /// <summary>デバッガーウィンドウを非表示</summary>
    public static void Hide()
    {
        GetContext().Window.Hide();
    }

    /// <summary>表示/非表示を切り替え</summary>
    public static void Toggle()
    {
        var ctx = GetContext();
        if (ctx.Window.IsVisible) Hide(); else Show();
    }

    /// <summary>ウィンドウの表示状態</summary>
    public static bool IsVisible => _context?.Window.IsVisible ?? false;

    // ── ロギングAPI ──

    /// <summary>デバッグログ</summary>
    public static void Log(string message) =>
        GetContext().LogStore.Append(CRLogLevel.Info, "App", message);

    /// <summary>デバッグログ（レベル指定）</summary>
    public static void Log(string message, CRLogLevel level) =>
        GetContext().LogStore.Append(level, "App", message);

    /// <summary>警告ログ</summary>
    public static void LogWarning(string message) =>
        GetContext().LogStore.Append(CRLogLevel.Warning, "App", message);

    /// <summary>エラーログ</summary>
    public static void LogError(string message, Exception? ex = null) =>
        GetContext().LogStore.Append(CRLogLevel.Error, "App", message, ex?.StackTrace);

    // ── ILogger統合 ──

    /// <summary>Microsoft.Extensions.Logging用プロバイダーを取得</summary>
    public static ILoggerProvider CreateLoggerProvider() =>
        GetContext().LoggerProvider;

    /// <summary>指定カテゴリのILoggerを取得</summary>
    public static ILogger CreateLogger(string categoryName) =>
        GetContext().LoggerProvider.CreateLogger(categoryName);

    // ── Options ──

    /// <summary>Optionsタブにオブジェクトを登録</summary>
    public static void AddOptionContainer(object container) =>
        GetContext().Options.AddContainer(container);

    /// <summary>Optionsタブからオブジェクトを解除</summary>
    public static void RemoveOptionContainer(object container) =>
        GetContext().Options.RemoveContainer(container);

    // ── SystemInfo ──

    /// <summary>カスタムシステム情報を追加</summary>
    public static void AddSystemInfo(string category, string key, string value) =>
        GetContext().SystemInfo.AddCustomInfo(category, key, value);

    // ── Profiler ──

    /// <summary>フレームをカウント（ゲームループ等から呼ぶ）</summary>
    public static void RecordFrame() =>
        _context?.Profiler.RecordFrame();

    // ── BugReporter ──

    /// <summary>バグレポート画面を表示</summary>
    public static void ShowBugReporter() => Show(CRTab.BugReporter);

    // ── テーマ ──

    /// <summary>テーマを変更</summary>
    public static void SetTheme(Theming.CRTheme theme)
    {
        var ctx = GetContext();
        ctx.ThemeManager.SetTheme(theme);
        ctx.Window.ApplyTheme(ctx.ThemeManager.CurrentColors);
    }

    // ── 破棄 ──

    /// <summary>CRDebuggerを破棄</summary>
    public static void Shutdown()
    {
        lock (_initLock)
        {
            _context?.Dispose();
            _context = null;
        }
    }

    private static CRDebuggerContext GetContext() =>
        _context ?? throw new InvalidOperationException(
            "CRDebuggerが初期化されていません。CRDebugger.Initialize() を先に呼んでください。");
}
