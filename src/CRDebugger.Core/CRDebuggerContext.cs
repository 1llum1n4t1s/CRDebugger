using System.Diagnostics;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.BugReporter;
using CRDebugger.Core.Input;
using CRDebugger.Core.Logging;
using CRDebugger.Core.Options;
using CRDebugger.Core.Profiler;
using CRDebugger.Core.SystemInfo;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using Microsoft.Extensions.Logging;
using SuperLightLogger;

namespace CRDebugger.Core;

/// <summary>
/// CRDebugger が保持する全サービスの内部コンテキスト。
/// 初期化時に全サービスを生成・配線し、Dispose 時にリソースを解放する。
/// このクラスは内部実装であり、ホストアプリから直接参照しないこと。
/// </summary>
internal sealed class CRDebuggerContext : IDisposable
{
    /// <summary>ログエントリを蓄積・管理するストア</summary>
    public LogStore LogStore { get; }

    /// <summary>システム情報を収集するコレクター</summary>
    public SystemInfoCollector SystemInfo { get; }

    /// <summary>動的オプション（CROption属性）を管理するエンジン</summary>
    public OptionsEngine Options { get; }

    /// <summary>FPS・操作計測・GPU監視を行うプロファイラーエンジン</summary>
    public ProfilerEngine Profiler { get; }

    /// <summary>バグレポートの収集・送信を担うエンジン</summary>
    public BugReportEngine BugReporter { get; }

    /// <summary>テーマ（ライト/ダーク/System）を管理するマネージャー</summary>
    public ThemeManager ThemeManager { get; }

    /// <summary>デバッガーウィンドウ全体のルートViewModel</summary>
    public DebuggerViewModel RootViewModel { get; }

    /// <summary>Microsoft.Extensions.Logging 統合用のLoggerProvider</summary>
    public CRLoggerProvider LoggerProvider { get; }

    /// <summary>SuperLightLogger のアプリケーション用ロガー（CRDebugger.Log等で使用）</summary>
    public ILog AppLogger { get; }

    /// <summary>キーボードショートカットの登録・処理を管理するマネージャー</summary>
    public KeyboardShortcutManager ShortcutManager { get; }

    /// <summary>UIフレームワーク固有のデバッガーウィンドウ実装</summary>
    public IDebuggerWindow Window { get; }

    /// <summary>UIスレッドへのディスパッチ処理を抽象化したインターフェース</summary>
    public IUiThread UiThread { get; }

    /// <summary>System.Diagnostics.Trace 出力をキャプチャするリスナー（無効時はnull）</summary>
    private CRTraceListener? _traceListener;

    /// <summary>システムテーマ監視プロバイダー（Dispose 時に StopMonitoring を呼ぶため保持）</summary>
    private readonly IThemeProvider? _themeProvider;

    /// <summary>
    /// CRDebuggerContextを構築し、全サービスを初期化・配線する。
    /// </summary>
    /// <param name="options">初期化オプション（UIフレームワーク層が設定した内部プロパティを含む）</param>
    /// <exception cref="CRDebuggerConfigurationException">必須の内部プロパティが未設定の場合</exception>
    public CRDebuggerContext(CRDebuggerOptions options)
    {
        // UIフレームワーク層が設定した必須プロパティを検証
        var window = options.Window ?? throw new CRDebuggerConfigurationException(
            "IDebuggerWindowが設定されていません。UseWpf(), UseAvalonia(), UseWinForms() のいずれかを呼んでください。");
        var uiThread = options.UiThread ?? throw new CRDebuggerConfigurationException(
            "IUiThreadが設定されていません。");

        // UIフレームワーク固有実装をフィールドに保持
        Window = window;
        UiThread = uiThread;

        // コアサービスを順に初期化（依存関係の少ないものから順番に生成）
        LogStore = new LogStore(options.MaxLogEntries, options.CollapseDuplicateLogs);
        SystemInfo = new SystemInfoCollector(options.SystemInfoCollectionLevel);
        Options = new OptionsEngine(options.RequireOptInAttribute, options.OptionsStore);
        Profiler = new ProfilerEngine(options.ProfilerSampleInterval, options.GpuMonitor);
        BugReporter = new BugReportEngine(LogStore, SystemInfo, options.BugReportSender, options.BugReportSendTimeout);
        ThemeManager = new ThemeManager(options.Theme);
        LoggerProvider = new CRLoggerProvider(LogStore);

        // SuperLightLogger の構成はオプトイン（デフォルト false）。
        // ホストアプリが既に LogManager.Configure 済みのケースを破壊しないため、
        // AttachToSuperLightLoggerManager = true「かつ」FileLogPath 指定という
        // 明示的な 2 条件がそろった場合にだけ構成する（FileLogPath 単独では構成しない）。
        if (options.AttachToSuperLightLoggerManager && !string.IsNullOrEmpty(options.FileLogPath))
        {
            LogManager.Configure(builder => builder.AddSuperLightFile(options.FileLogPath));
        }

        // アプリケーション用の SuperLightLogger ロガーを取得
        AppLogger = LogManager.GetLogger(typeof(CRDebuggerContext));

        // キーボードショートカットマネージャーを生成し、初期有効状態をオプションから設定
        ShortcutManager = new KeyboardShortcutManager
        {
            Enabled = options.EnableKeyboardShortcuts
        };

        // System.Diagnostics.Trace/Debug 出力のキャプチャを有効化
        if (options.CaptureTraceOutput)
        {
            _traceListener = new CRTraceListener(LogStore);
            Trace.Listeners.Add(_traceListener); // グローバルリスナーに登録
        }

        // AppDomain レベルの未処理例外をキャプチャしてログに記録
        if (options.CaptureUnhandledExceptions)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        // 各タブのViewModelをサービスから生成
        var systemInfoVm = new SystemInfoViewModel(SystemInfo);
        var consoleVm = new ConsoleViewModel(LogStore, UiThread);
        var optionsVm = new OptionsViewModel(Options);
        var profilerVm = new ProfilerViewModel(Profiler, UiThread);
        var bugReporterVm = new BugReporterViewModel(BugReporter, Window);

        // 全TabViewModelを束ねるルートViewModelを生成
        RootViewModel = new DebuggerViewModel(
            systemInfoVm, consoleVm, optionsVm, profilerVm, bugReporterVm,
            ThemeManager, options.DefaultTab, options.DisabledTabs);

        // F1〜F5・Esc などのデフォルトショートカットを登録
        RegisterDefaultShortcuts();

        // システムテーマ（ライト/ダーク）の監視を開始
        _themeProvider = options.ThemeProvider;
        if (_themeProvider != null)
        {
            // 現在のシステムテーマを即時反映
            ThemeManager.NotifySystemThemeChanged(_themeProvider.IsSystemDarkMode());

            // システムテーマ変更の監視コールバックを登録（UIスレッドで適用）
            _themeProvider.StartMonitoring(isDark =>
            {
                UiThread.Invoke(() => ThemeManager.NotifySystemThemeChanged(isDark));
            });
        }

        // プロファイラーのサンプリングタイマーを開始
        Profiler.Start();
    }

    /// <summary>
    /// デフォルトのキーボードショートカットを登録する。
    /// F1〜F5 でタブ切替、Esc でウィンドウを閉じる。
    /// </summary>
    private void RegisterDefaultShortcuts()
    {
        // F1〜F5 キーで各タブに直接切り替え
        ShortcutManager.Register(new KeyCombination(CRKey.F1), () =>
            UiThread.Invoke(() => RootViewModel.SelectedTab = CRTab.System));
        ShortcutManager.Register(new KeyCombination(CRKey.F2), () =>
            UiThread.Invoke(() => RootViewModel.SelectedTab = CRTab.Console));
        ShortcutManager.Register(new KeyCombination(CRKey.F3), () =>
            UiThread.Invoke(() => RootViewModel.SelectedTab = CRTab.Options));
        ShortcutManager.Register(new KeyCombination(CRKey.F4), () =>
            UiThread.Invoke(() => RootViewModel.SelectedTab = CRTab.Profiler));
        ShortcutManager.Register(new KeyCombination(CRKey.F5), () =>
            UiThread.Invoke(() => RootViewModel.SelectedTab = CRTab.BugReporter));

        // Esc キーでデバッガーウィンドウを非表示にする
        ShortcutManager.Register(new KeyCombination(CRKey.Escape), () =>
            UiThread.Invoke(() => Window.Hide()));
    }

    /// <summary>
    /// AppDomain の未処理例外イベントハンドラー。
    /// 例外情報をエラーレベルでログに記録し、ファイルログにも書き出す。
    /// </summary>
    /// <param name="sender">イベント送信元</param>
    /// <param name="e">未処理例外イベント引数</param>
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // ExceptionObject は Exception 以外の場合もあるためキャストを試みる
        var ex = e.ExceptionObject as Exception;

        // メッセージには例外型名を添える。TypeInitializationException のように
        // Message だけでは何も分からない例外が実在するため、型名は必須の手がかりになる。
        var message = ex != null ? $"{ex.GetType().FullName}: {ex.Message}" : "不明な例外";

        // 詳細側は ToString() を使い、InnerException の連鎖とその各スタックトレースまで残す。
        // StackTrace だけだと最外殻しか出ず、真因（InnerException 側）に到達できない。
        var detail = ex?.ToString();

        LogStore.Append(CRLogLevel.Error, "UnhandledException", message, detail);

        try
        {
            // プロセスを終了させる種類のイベントなので、揮発する LogStore だけでなく
            // ファイルログにも必ず流す（FileLogPath 構成時に事後調査できるようにする）。
            if (ex != null)
                AppLogger.Error(message, ex);
            else
                AppLogger.Error(message);
        }
        catch (Exception)
        {
            // クラッシュ処理中のログ出力失敗で、さらに例外を重ねない
        }
    }

    /// <summary>
    /// コンテキストが保持するリソースをすべて解放する。
    /// プロファイラータイマーの停止、ThemeProvider 監視停止、
    /// TraceListenerの解除、未処理例外イベントの登録解除、
    /// RootViewModel の Dispose、OptionsStore.Flush を行う。
    /// </summary>
    public void Dispose()
    {
        // システムテーマ監視を停止して OS イベント購読を解除
        try { _themeProvider?.StopMonitoring(); } catch { /* 解放経路で握りつぶし */ }
        (_themeProvider as IDisposable)?.Dispose();

        // RootViewModel を Dispose して ThemeManager/LogStore/Profiler 等のイベント購読を解除
        try { RootViewModel.Dispose(); } catch { /* 解放経路で握りつぶし */ }

        // プロファイラーのサンプリングタイマーを停止・解放
        Profiler.Dispose();

        // Options 永続化ストアに保留中の変更をフラッシュ
        try { Options.FlushStore(); } catch { /* 解放経路で握りつぶし */ }

        // グローバルTraceListenerから自分自身を解除＆Disposeしてリーク防止
        if (_traceListener != null)
        {
            Trace.Listeners.Remove(_traceListener);
            _traceListener.Dispose();
            _traceListener = null;
        }

        // 未処理例外ハンドラーの登録を解除
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
    }
}
