using CRDebugger.Core.Input;
using CRDebugger.Core.Logging;
using CRDebugger.Core.Profiler;
using Microsoft.Extensions.Logging;

namespace CRDebugger.Core;

/// <summary>
/// パネル表示状態変更イベント引数
/// </summary>
public sealed record PanelVisibilityChangedEventArgs(bool IsVisible);

/// <summary>
/// CRDebugger 静的ファサード - メインエントリポイント。
/// 全パブリックAPIはCRDebugger内部エラーをCRDebuggerExceptionにラップし、
/// ホストアプリに影響を与えないよう設計されている。
/// </summary>
public static class CRDebugger
{
    private static CRDebuggerContext? _context;
    private static readonly object _initLock = new();

    /// <summary>パネルの表示/非表示が変更された時に発火</summary>
    public static event EventHandler<PanelVisibilityChangedEventArgs>? PanelVisibilityChanged;

    /// <summary>CRDebugger内部でエラーが発生した時に発火（ホストアプリのクラッシュを防ぐ）</summary>
    public static event EventHandler<CRDebuggerException>? InternalError;

    /// <summary>初期化済みかどうか</summary>
    public static bool IsInitialized => _context != null;

    /// <summary>
    /// CRDebuggerを初期化する。UIフレームワーク層の拡張メソッド経由で呼ぶ。
    /// </summary>
    /// <exception cref="CRDebuggerAlreadyInitializedException">既に初期化済みの場合</exception>
    /// <exception cref="CRDebuggerConfigurationException">構成が不正な場合</exception>
    public static void Initialize(CRDebuggerOptions options)
    {
        lock (_initLock)
        {
            if (_context != null)
                throw new CRDebuggerAlreadyInitializedException();

            try
            {
                _context = new CRDebuggerContext(options);
            }
            catch (CRDebuggerException) { throw; }
            catch (Exception ex)
            {
                throw new CRDebuggerConfigurationException(
                    "初期化中にエラーが発生しました。オプション設定を確認してください。", ex);
            }
        }
    }

    // ── 表示制御 ──

    /// <summary>デバッガーウィンドウを表示</summary>
    public static void Show()
    {
        SafeExecute(() =>
        {
            var ctx = GetContext();
            ctx.Window.Show(ctx.RootViewModel);
            ctx.Window.ApplyTheme(ctx.ThemeManager.CurrentColors);
            PanelVisibilityChanged?.Invoke(null, new PanelVisibilityChangedEventArgs(true));
        });
    }

    /// <summary>デバッガーウィンドウを表示して指定タブに切り替え</summary>
    public static void Show(CRTab tab)
    {
        SafeExecute(() =>
        {
            var ctx = GetContext();
            ctx.RootViewModel.SelectedTab = tab;
            Show();
        });
    }

    /// <summary>デバッガーウィンドウを非表示</summary>
    public static void Hide()
    {
        SafeExecute(() =>
        {
            GetContext().Window.Hide();
            PanelVisibilityChanged?.Invoke(null, new PanelVisibilityChangedEventArgs(false));
        });
    }

    /// <summary>表示/非表示を切り替え</summary>
    public static void Toggle()
    {
        SafeExecute(() =>
        {
            var ctx = GetContext();
            if (ctx.Window.IsVisible) Hide(); else Show();
        });
    }

    /// <summary>ウィンドウの表示状態</summary>
    public static bool IsVisible => _context?.Window.IsVisible ?? false;

    // ── ロギングAPI（ホストアプリをクラッシュさせない安全設計） ──

    /// <summary>Infoログ</summary>
    public static void Log(string message) =>
        SafeExecute(() => GetContext().LogStore.Append(CRLogLevel.Info, "App", message));

    /// <summary>レベル指定ログ</summary>
    public static void Log(string message, CRLogLevel level) =>
        SafeExecute(() => GetContext().LogStore.Append(level, "App", message));

    /// <summary>警告ログ</summary>
    public static void LogWarning(string message) =>
        SafeExecute(() => GetContext().LogStore.Append(CRLogLevel.Warning, "App", message));

    /// <summary>エラーログ</summary>
    public static void LogError(string message, Exception? ex = null) =>
        SafeExecute(() => GetContext().LogStore.Append(CRLogLevel.Error, "App", message, ex?.StackTrace));

    /// <summary>リッチテキストログ</summary>
    public static void LogRich(string message, IReadOnlyList<RichTextSpan> richSpans, CRLogLevel level = CRLogLevel.Info) =>
        SafeExecute(() => GetContext().LogStore.Append(level, "App", message, richSpans: richSpans));

    /// <summary>リッチテキストログ（ビルダーAPI）</summary>
    public static void LogRich(CRLogLevel level, Action<RichTextBuilder> builder)
    {
        SafeExecute(() =>
        {
            var b = new RichTextBuilder();
            builder(b);
            var spans = b.Build();
            var message = string.Concat(spans.Select(s => s.Text));
            GetContext().LogStore.Append(level, "App", message, richSpans: spans);
        });
    }

    /// <summary>マークアップ付きログ（例: "&lt;b&gt;太字&lt;/b&gt; &lt;color=#FF0000&gt;赤&lt;/color&gt;"）</summary>
    public static void LogMarkup(string markup, CRLogLevel level = CRLogLevel.Info)
    {
        SafeExecute(() =>
        {
            var spans = RichTextParser.Parse(markup);
            var message = string.Concat(spans.Select(s => s.Text));
            GetContext().LogStore.Append(level, "App", message, richSpans: spans);
        });
    }

    // ── ILogger統合 ──

    /// <summary>Microsoft.Extensions.Logging用プロバイダーを取得</summary>
    /// <exception cref="CRDebuggerNotInitializedException">未初期化の場合</exception>
    public static ILoggerProvider CreateLoggerProvider() =>
        GetContext().LoggerProvider;

    /// <summary>指定カテゴリのILoggerを取得</summary>
    /// <exception cref="CRDebuggerNotInitializedException">未初期化の場合</exception>
    public static ILogger CreateLogger(string categoryName) =>
        GetContext().LoggerProvider.CreateLogger(categoryName);

    // ── Options ──

    /// <summary>Optionsタブにオブジェクトを登録</summary>
    public static void AddOptionContainer(object container) =>
        SafeExecute(() => GetContext().Options.AddContainer(container));

    /// <summary>Optionsタブからオブジェクトを解除</summary>
    public static void RemoveOptionContainer(object container) =>
        SafeExecute(() => GetContext().Options.RemoveContainer(container));

    // ── SystemInfo ──

    /// <summary>カスタムシステム情報を追加</summary>
    public static void AddSystemInfo(string category, string key, string value) =>
        SafeExecute(() => GetContext().SystemInfo.AddCustomInfo(category, key, value));

    // ── Profiler ──

    /// <summary>フレームをカウント（ゲームループ等から呼ぶ）</summary>
    public static void RecordFrame() =>
        _context?.Profiler.RecordFrame();

    /// <summary>
    /// ロジック単位の計測スコープを開始する（usingパターン）
    /// 使用例: using (CRDebugger.Profile("DBクエリ")) { ... }
    /// </summary>
    public static ProfilingScope Profile(string operationName, string category = "General") =>
        GetContext().Profiler.Operations.BeginScope(operationName, category);

    /// <summary>同期処理を計測する</summary>
    public static T Measure<T>(string operationName, Func<T> action, string category = "General") =>
        GetContext().Profiler.Operations.Measure(operationName, action, category);

    /// <summary>同期処理を計測する（戻り値なし）</summary>
    public static void Measure(string operationName, Action action, string category = "General") =>
        GetContext().Profiler.Operations.Measure(operationName, action, category);

    /// <summary>非同期処理を計測する</summary>
    public static Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> action, string category = "General") =>
        GetContext().Profiler.Operations.MeasureAsync(operationName, action, category);

    /// <summary>非同期処理を計測する（戻り値なし）</summary>
    public static Task MeasureAsync(string operationName, Func<Task> action, string category = "General") =>
        GetContext().Profiler.Operations.MeasureAsync(operationName, action, category);

    /// <summary>ネットワークI/Oを手動で記録する</summary>
    public static void RecordNetworkIO(long bytesRead, long bytesWritten) =>
        _context?.Profiler.Operations.RecordNetworkIO(bytesRead, bytesWritten);

    /// <summary>ストレージI/Oを手動で記録する</summary>
    public static void RecordStorageIO(long bytesRead, long bytesWritten) =>
        _context?.Profiler.Operations.RecordStorageIO(bytesRead, bytesWritten);

    /// <summary>ロジック単位プロファイラーのトラッカーを取得</summary>
    /// <exception cref="CRDebuggerNotInitializedException">未初期化の場合</exception>
    public static OperationTracker GetOperationTracker() =>
        GetContext().Profiler.Operations;

    // ── BugReporter ──

    /// <summary>バグレポート画面を表示</summary>
    public static void ShowBugReporter() => Show(CRTab.BugReporter);

    // ── テーマ ──

    /// <summary>テーマを変更</summary>
    public static void SetTheme(Theming.CRTheme theme)
    {
        SafeExecute(() =>
        {
            var ctx = GetContext();
            ctx.ThemeManager.SetTheme(theme);
            ctx.Window.ApplyTheme(ctx.ThemeManager.CurrentColors);
        });
    }

    // ── タブ制御 ──

    /// <summary>タブの有効/無効を設定</summary>
    public static void SetTabEnabled(CRTab tab, bool enabled) =>
        SafeExecute(() => GetContext().RootViewModel.SetTabEnabled(tab, enabled));

    /// <summary>タブが有効かどうか</summary>
    public static bool IsTabEnabled(CRTab tab) =>
        _context?.RootViewModel.IsTabEnabled(tab) ?? true;

    // ── キーボードショートカット ──

    /// <summary>ショートカットを登録</summary>
    public static void RegisterShortcut(KeyCombination combination, Action action) =>
        SafeExecute(() => GetContext().ShortcutManager.Register(combination, action));

    /// <summary>ショートカットを解除</summary>
    public static void UnregisterShortcut(KeyCombination combination) =>
        SafeExecute(() => GetContext().ShortcutManager.Unregister(combination));

    /// <summary>キー入力を処理（UIフレームワーク層から呼ぶ）</summary>
    public static bool HandleKeyDown(CRKey key, CRModifierKeys modifiers = CRModifierKeys.None) =>
        _context?.ShortcutManager.HandleKeyDown(key, modifiers) ?? false;

    // ── 破棄 ──

    /// <summary>CRDebuggerを破棄</summary>
    public static void Shutdown()
    {
        lock (_initLock)
        {
            try
            {
                _context?.Dispose();
            }
            catch (Exception ex)
            {
                RaiseInternalError("Shutdown中にエラーが発生しました。", ex);
            }
            _context = null;
        }
    }

    // ── 内部ヘルパー ──

    private static CRDebuggerContext GetContext() =>
        _context ?? throw new CRDebuggerNotInitializedException();

    /// <summary>
    /// パブリックAPIをCRDebugger内部エラーから保護する。
    /// ログ記録やオプション追加など「失敗してもホストアプリに影響しない」操作に使用。
    /// </summary>
    private static void SafeExecute(Action action)
    {
        try
        {
            action();
        }
        catch (CRDebuggerNotInitializedException) { throw; }
        catch (CRDebuggerAlreadyInitializedException) { throw; }
        catch (CRDebuggerException) { throw; }
        catch (Exception ex)
        {
            RaiseInternalError("予期しないエラーが発生しました。", ex);
        }
    }

    private static void RaiseInternalError(string message, Exception innerException)
    {
        var error = new CRDebuggerInternalException(message, innerException);
        try
        {
            InternalError?.Invoke(null, error);
        }
        catch
        {
            // InternalErrorハンドラ自体がスローした場合も握りつぶす
        }
    }
}
