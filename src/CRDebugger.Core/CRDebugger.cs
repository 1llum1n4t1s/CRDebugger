using CRDebugger.Core.Input;
using CRDebugger.Core.Logging;
using CRDebugger.Core.Profiler;
using Microsoft.Extensions.Logging;

namespace CRDebugger.Core;

/// <summary>
/// パネル表示状態変更イベント引数
/// </summary>
/// <param name="IsVisible">パネルが表示されている場合true</param>
public sealed record PanelVisibilityChangedEventArgs(bool IsVisible);

/// <summary>
/// CRDebugger 静的ファサード - メインエントリポイント。
/// 全パブリックAPIはCRDebugger内部エラーをCRDebuggerExceptionにラップし、
/// ホストアプリに影響を与えないよう設計されている。
/// </summary>
public static class CRDebugger
{
    /// <summary>初期化済みコンテキスト（未初期化時はnull）</summary>
    private static CRDebuggerContext? _context;

    /// <summary>Initialize/Shutdown の競合を防ぐための排他ロックオブジェクト</summary>
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
    /// <param name="options">初期化オプション</param>
    /// <exception cref="CRDebuggerAlreadyInitializedException">既に初期化済みの場合</exception>
    /// <exception cref="CRDebuggerConfigurationException">構成が不正な場合</exception>
    public static void Initialize(CRDebuggerOptions options)
    {
        lock (_initLock)
        {
            // 二重初期化は明示的な例外で防止する
            if (_context != null)
                throw new CRDebuggerAlreadyInitializedException();

            try
            {
                // 全サービスを生成・配線するコンテキストを構築
                _context = new CRDebuggerContext(options);
            }
            catch (CRDebuggerException) { throw; } // CRDebugger由来の例外はそのまま再スロー
            catch (Exception ex)
            {
                // 予期しない例外は構成エラーとしてラップしてスロー
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
            ctx.Window.Show(ctx.RootViewModel); // ViewModelをバインドしてウィンドウを表示
            ctx.Window.ApplyTheme(ctx.ThemeManager.CurrentColors); // 現在のテーマ配色を適用
            PanelVisibilityChanged?.Invoke(null, new PanelVisibilityChangedEventArgs(true)); // 表示状態変更を通知
        });
    }

    /// <summary>デバッガーウィンドウを表示して指定タブに切り替え</summary>
    /// <param name="tab">表示するタブ</param>
    public static void Show(CRTab tab)
    {
        SafeExecute(() =>
        {
            var ctx = GetContext();
            ctx.RootViewModel.SelectedTab = tab; // 先にタブを切り替えてからウィンドウを表示
            Show();
        });
    }

    /// <summary>デバッガーウィンドウを非表示</summary>
    public static void Hide()
    {
        SafeExecute(() =>
        {
            GetContext().Window.Hide(); // ウィンドウを非表示にする
            PanelVisibilityChanged?.Invoke(null, new PanelVisibilityChangedEventArgs(false)); // 非表示状態変更を通知
        });
    }

    /// <summary>表示/非表示を切り替え</summary>
    public static void Toggle()
    {
        SafeExecute(() =>
        {
            var ctx = GetContext();
            // 現在の表示状態に応じて Hide または Show を呼び分ける
            if (ctx.Window.IsVisible) Hide(); else Show();
        });
    }

    /// <summary>ウィンドウの表示状態</summary>
    public static bool IsVisible => _context?.Window.IsVisible ?? false;

    // ── ロギングAPI（ホストアプリをクラッシュさせない安全設計） ──

    /// <summary>Infoレベルでログを記録する</summary>
    /// <param name="message">ログメッセージ</param>
    public static void Log(string message) =>
        SafeExecute(() => GetContext().LogStore.Append(CRLogLevel.Info, "App", message));

    /// <summary>指定レベルでログを記録する</summary>
    /// <param name="message">ログメッセージ</param>
    /// <param name="level">ログレベル</param>
    public static void Log(string message, CRLogLevel level) =>
        SafeExecute(() => GetContext().LogStore.Append(level, "App", message));

    /// <summary>Warningレベルでログを記録する</summary>
    /// <param name="message">ログメッセージ</param>
    public static void LogWarning(string message) =>
        SafeExecute(() => GetContext().LogStore.Append(CRLogLevel.Warning, "App", message));

    /// <summary>Errorレベルでログを記録する</summary>
    /// <param name="message">ログメッセージ</param>
    /// <param name="ex">関連する例外（省略可）</param>
    public static void LogError(string message, Exception? ex = null) =>
        SafeExecute(() => GetContext().LogStore.Append(CRLogLevel.Error, "App", message, ex?.StackTrace));

    /// <summary>リッチテキスト付きログを記録する</summary>
    /// <param name="message">プレーンテキストメッセージ</param>
    /// <param name="richSpans">リッチテキストスパンのリスト</param>
    /// <param name="level">ログレベル（デフォルト: Info）</param>
    public static void LogRich(string message, IReadOnlyList<RichTextSpan> richSpans, CRLogLevel level = CRLogLevel.Info) =>
        SafeExecute(() => GetContext().LogStore.Append(level, "App", message, richSpans: richSpans));

    /// <summary>リッチテキスト付きログをビルダーAPIで記録する</summary>
    /// <param name="level">ログレベル</param>
    /// <param name="builder">リッチテキストを構築するデリゲート</param>
    public static void LogRich(CRLogLevel level, Action<RichTextBuilder> builder)
    {
        SafeExecute(() =>
        {
            var b = new RichTextBuilder();
            builder(b); // 呼び出し元がビルダーにスパンを追加する
            var spans = b.Build(); // スパンのリストを確定させる
            var message = string.Concat(spans.Select(s => s.Text)); // プレーンテキストを連結してメッセージを生成
            GetContext().LogStore.Append(level, "App", message, richSpans: spans);
        });
    }

    /// <summary>マークアップ付きログを記録する（例: "&lt;b&gt;太字&lt;/b&gt; &lt;color=#FF0000&gt;赤&lt;/color&gt;"）</summary>
    /// <param name="markup">マークアップ文字列</param>
    /// <param name="level">ログレベル（デフォルト: Info）</param>
    public static void LogMarkup(string markup, CRLogLevel level = CRLogLevel.Info)
    {
        SafeExecute(() =>
        {
            var spans = RichTextParser.Parse(markup); // マークアップ文字列をリッチテキストスパンに変換
            var message = string.Concat(spans.Select(s => s.Text)); // スパンのテキスト部分を連結してプレーンテキストを生成
            GetContext().LogStore.Append(level, "App", message, richSpans: spans);
        });
    }

    // ── ILogger統合 ──

    /// <summary>Microsoft.Extensions.Logging用プロバイダーを取得する</summary>
    /// <returns>CRDebuggerに統合されたILoggerProvider</returns>
    /// <exception cref="CRDebuggerNotInitializedException">未初期化の場合</exception>
    public static ILoggerProvider CreateLoggerProvider() =>
        GetContext().LoggerProvider;

    /// <summary>指定カテゴリのILoggerを取得する</summary>
    /// <param name="categoryName">ログカテゴリ名</param>
    /// <returns>指定カテゴリ用のILogger</returns>
    /// <exception cref="CRDebuggerNotInitializedException">未初期化の場合</exception>
    public static ILogger CreateLogger(string categoryName) =>
        GetContext().LoggerProvider.CreateLogger(categoryName);

    // ── Options ──

    /// <summary>Optionsタブにオプションコンテナを登録する</summary>
    /// <param name="container">CROption属性付きプロパティを持つオブジェクト</param>
    public static void AddOptionContainer(object container) =>
        SafeExecute(() => GetContext().Options.AddContainer(container));

    /// <summary>Optionsタブからオプションコンテナを解除する</summary>
    /// <param name="container">解除するコンテナオブジェクト</param>
    public static void RemoveOptionContainer(object container) =>
        SafeExecute(() => GetContext().Options.RemoveContainer(container));

    // ── SystemInfo ──

    /// <summary>カスタムシステム情報を追加する</summary>
    /// <param name="category">カテゴリ名（例: "GPU"）</param>
    /// <param name="key">キー名（例: "Device Name"）</param>
    /// <param name="value">値（例: "GeForce RTX 4090"）</param>
    public static void AddSystemInfo(string category, string key, string value) =>
        SafeExecute(() => GetContext().SystemInfo.AddCustomInfo(category, key, value));

    // ── Profiler ──

    /// <summary>フレームをカウント（ゲームループ等から呼ぶ）</summary>
    public static void RecordFrame() =>
        _context?.Profiler.RecordFrame();

    /// <summary>
    /// ロジック単位の計測スコープを開始する（usingパターン）。
    /// 使用例: using (CRDebugger.Profile("DBクエリ")) { ... }
    /// </summary>
    /// <param name="operationName">操作名</param>
    /// <param name="category">カテゴリ名（デフォルト: "General"）</param>
    /// <returns>Dispose時に計測結果を記録するスコープオブジェクト</returns>
    public static ProfilingScope Profile(string operationName, string category = "General") =>
        GetContext().Profiler.Operations.BeginScope(operationName, category);

    /// <summary>同期処理を計測する</summary>
    /// <typeparam name="T">戻り値の型</typeparam>
    /// <param name="operationName">操作名</param>
    /// <param name="action">計測対象の処理</param>
    /// <param name="category">カテゴリ名（デフォルト: "General"）</param>
    /// <returns>処理の戻り値</returns>
    public static T Measure<T>(string operationName, Func<T> action, string category = "General") =>
        GetContext().Profiler.Operations.Measure(operationName, action, category);

    /// <summary>同期処理を計測する（戻り値なし）</summary>
    /// <param name="operationName">操作名</param>
    /// <param name="action">計測対象の処理</param>
    /// <param name="category">カテゴリ名（デフォルト: "General"）</param>
    public static void Measure(string operationName, Action action, string category = "General") =>
        GetContext().Profiler.Operations.Measure(operationName, action, category);

    /// <summary>非同期処理を計測する</summary>
    /// <typeparam name="T">戻り値の型</typeparam>
    /// <param name="operationName">操作名</param>
    /// <param name="action">計測対象の非同期処理</param>
    /// <param name="category">カテゴリ名（デフォルト: "General"）</param>
    /// <returns>処理の戻り値</returns>
    public static Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> action, string category = "General") =>
        GetContext().Profiler.Operations.MeasureAsync(operationName, action, category);

    /// <summary>非同期処理を計測する（戻り値なし）</summary>
    /// <param name="operationName">操作名</param>
    /// <param name="action">計測対象の非同期処理</param>
    /// <param name="category">カテゴリ名（デフォルト: "General"）</param>
    /// <returns>非同期操作を表すTask</returns>
    public static Task MeasureAsync(string operationName, Func<Task> action, string category = "General") =>
        GetContext().Profiler.Operations.MeasureAsync(operationName, action, category);

    /// <summary>ネットワークI/Oを手動で記録する</summary>
    /// <param name="bytesRead">読み込みバイト数</param>
    /// <param name="bytesWritten">書き込みバイト数</param>
    public static void RecordNetworkIO(long bytesRead, long bytesWritten) =>
        _context?.Profiler.Operations.RecordNetworkIO(bytesRead, bytesWritten);

    /// <summary>ストレージI/Oを手動で記録する</summary>
    /// <param name="bytesRead">読み込みバイト数</param>
    /// <param name="bytesWritten">書き込みバイト数</param>
    public static void RecordStorageIO(long bytesRead, long bytesWritten) =>
        _context?.Profiler.Operations.RecordStorageIO(bytesRead, bytesWritten);

    /// <summary>ロジック単位プロファイラーのトラッカーを取得する</summary>
    /// <returns>操作トラッカー</returns>
    /// <exception cref="CRDebuggerNotInitializedException">未初期化の場合</exception>
    public static OperationTracker GetOperationTracker() =>
        GetContext().Profiler.Operations;

    // ── BugReporter ──

    /// <summary>バグレポート画面を表示</summary>
    public static void ShowBugReporter() => Show(CRTab.BugReporter);

    // ── テーマ ──

    /// <summary>テーマを変更する</summary>
    /// <param name="theme">適用するテーマ</param>
    public static void SetTheme(Theming.CRTheme theme)
    {
        SafeExecute(() =>
        {
            var ctx = GetContext();
            ctx.ThemeManager.SetTheme(theme); // テーママネージャーに新しいテーマをセット
            ctx.Window.ApplyTheme(ctx.ThemeManager.CurrentColors); // ウィンドウに最新のテーマ配色を即時適用
        });
    }

    // ── タブ制御 ──

    /// <summary>タブの有効/無効を設定する</summary>
    /// <param name="tab">対象タブ</param>
    /// <param name="enabled">有効にする場合true</param>
    public static void SetTabEnabled(CRTab tab, bool enabled) =>
        SafeExecute(() => GetContext().RootViewModel.SetTabEnabled(tab, enabled));

    /// <summary>タブが有効かどうかを返す</summary>
    /// <param name="tab">確認するタブ</param>
    /// <returns>有効な場合true</returns>
    public static bool IsTabEnabled(CRTab tab) =>
        _context?.RootViewModel.IsTabEnabled(tab) ?? true;

    // ── キーボードショートカット ──

    /// <summary>キーボードショートカットを登録する</summary>
    /// <param name="combination">キーの組み合わせ</param>
    /// <param name="action">ショートカット押下時に実行するアクション</param>
    public static void RegisterShortcut(KeyCombination combination, Action action) =>
        SafeExecute(() => GetContext().ShortcutManager.Register(combination, action));

    /// <summary>キーボードショートカットを解除する</summary>
    /// <param name="combination">解除するキーの組み合わせ</param>
    public static void UnregisterShortcut(KeyCombination combination) =>
        SafeExecute(() => GetContext().ShortcutManager.Unregister(combination));

    /// <summary>キー入力を処理する（UIフレームワーク層から呼ぶ）</summary>
    /// <param name="key">押下されたキー</param>
    /// <param name="modifiers">修飾キー（Ctrl, Shift, Alt等）</param>
    /// <returns>ショートカットが見つかって実行された場合true</returns>
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
                _context?.Dispose(); // コンテキストのリソース（タイマー・TraceListener等）を解放
            }
            catch (Exception ex)
            {
                // Dispose 中の例外はInternalErrorイベントで通知するが再スローしない
                RaiseInternalError("Shutdown中にエラーが発生しました。", ex);
            }
            _context = null; // コンテキストをnullにして未初期化状態に戻す
        }
    }

    // ── 内部ヘルパー ──

    /// <summary>
    /// 初期化済みコンテキストを取得する。未初期化の場合は例外をスローする。
    /// </summary>
    /// <returns>現在の CRDebuggerContext</returns>
    /// <exception cref="CRDebuggerNotInitializedException">未初期化の場合</exception>
    private static CRDebuggerContext GetContext() =>
        _context ?? throw new CRDebuggerNotInitializedException();

    /// <summary>
    /// パブリックAPIをCRDebugger内部エラーから保護する。
    /// ログ記録やオプション追加など「失敗してもホストアプリに影響しない」操作に使用。
    /// </summary>
    /// <param name="action">実行する処理</param>
    private static void SafeExecute(Action action)
    {
        try
        {
            action();
        }
        catch (CRDebuggerNotInitializedException) { throw; } // 未初期化例外は呼び出し元に再スロー
        catch (CRDebuggerAlreadyInitializedException) { throw; } // 二重初期化例外は呼び出し元に再スロー
        catch (CRDebuggerException) { throw; } // CRDebugger既知例外はそのまま再スロー
        catch (Exception ex)
        {
            // 予期しない内部例外はInternalErrorイベントで通知して握りつぶす（ホストアプリを守る）
            RaiseInternalError("予期しないエラーが発生しました。", ex);
        }
    }

    /// <summary>
    /// 内部エラーを <see cref="InternalError"/> イベントで通知する。
    /// イベントハンドラー自身が例外をスローしても握りつぶす。
    /// </summary>
    /// <param name="message">エラーメッセージ</param>
    /// <param name="innerException">原因となった例外</param>
    private static void RaiseInternalError(string message, Exception innerException)
    {
        var error = new CRDebuggerInternalException(message, innerException);
        try
        {
            InternalError?.Invoke(null, error); // 登録済みハンドラーにエラーを通知
        }
        catch
        {
            // InternalErrorハンドラ自体がスローした場合も握りつぶす（ハンドラのバグでクラッシュさせない）
        }
    }
}
