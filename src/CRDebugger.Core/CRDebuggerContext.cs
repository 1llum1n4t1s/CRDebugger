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

namespace CRDebugger.Core;

/// <summary>
/// 全サービスの内部コンテキスト
/// </summary>
internal sealed class CRDebuggerContext : IDisposable
{
    public LogStore LogStore { get; }
    public SystemInfoCollector SystemInfo { get; }
    public OptionsEngine Options { get; }
    public ProfilerEngine Profiler { get; }
    public BugReportEngine BugReporter { get; }
    public ThemeManager ThemeManager { get; }
    public DebuggerViewModel RootViewModel { get; }
    public CRLoggerProvider LoggerProvider { get; }
    public KeyboardShortcutManager ShortcutManager { get; }

    public IDebuggerWindow Window { get; }
    public IUiThread UiThread { get; }

    private CRTraceListener? _traceListener;

    public CRDebuggerContext(CRDebuggerOptions options)
    {
        var window = options.Window ?? throw new CRDebuggerConfigurationException(
            "IDebuggerWindowが設定されていません。UseWpf(), UseAvalonia(), UseWinForms() のいずれかを呼んでください。");
        var uiThread = options.UiThread ?? throw new CRDebuggerConfigurationException(
            "IUiThreadが設定されていません。");

        Window = window;
        UiThread = uiThread;

        // サービス初期化
        LogStore = new LogStore(options.MaxLogEntries, options.CollapseDuplicateLogs);
        SystemInfo = new SystemInfoCollector();
        Options = new OptionsEngine();
        Profiler = new ProfilerEngine(options.ProfilerSampleInterval, options.GpuMonitor);
        BugReporter = new BugReportEngine(LogStore, SystemInfo, options.BugReportSender);
        ThemeManager = new ThemeManager(options.Theme);
        LoggerProvider = new CRLoggerProvider(LogStore);
        ShortcutManager = new KeyboardShortcutManager
        {
            Enabled = options.EnableKeyboardShortcuts
        };

        // TraceListener登録
        if (options.CaptureTraceOutput)
        {
            _traceListener = new CRTraceListener(LogStore);
            Trace.Listeners.Add(_traceListener);
        }

        // 未処理例外のキャプチャ
        if (options.CaptureUnhandledExceptions)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        // ViewModel組み立て
        var systemInfoVm = new SystemInfoViewModel(SystemInfo);
        var consoleVm = new ConsoleViewModel(LogStore, UiThread);
        var optionsVm = new OptionsViewModel(Options);
        var profilerVm = new ProfilerViewModel(Profiler, UiThread);
        var bugReporterVm = new BugReporterViewModel(BugReporter, Window);

        RootViewModel = new DebuggerViewModel(
            systemInfoVm, consoleVm, optionsVm, profilerVm, bugReporterVm,
            ThemeManager, options.DefaultTab, options.DisabledTabs);

        // デフォルトキーボードショートカット登録
        RegisterDefaultShortcuts();

        // テーマプロバイダー設定
        if (options.ThemeProvider != null)
        {
            ThemeManager.NotifySystemThemeChanged(options.ThemeProvider.IsSystemDarkMode());
            options.ThemeProvider.StartMonitoring(isDark =>
            {
                UiThread.Invoke(() => ThemeManager.NotifySystemThemeChanged(isDark));
            });
        }

        // Profiler開始
        Profiler.Start();
    }

    private void RegisterDefaultShortcuts()
    {
        // F1〜F5でタブ切替
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

        // Escでウィンドウを閉じる
        ShortcutManager.Register(new KeyCombination(CRKey.Escape), () =>
            UiThread.Invoke(() => Window.Hide()));
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        LogStore.Append(CRLogLevel.Error, "UnhandledException",
            ex?.Message ?? "不明な例外", ex?.StackTrace);
    }

    public void Dispose()
    {
        Profiler.Dispose();
        if (_traceListener != null)
        {
            Trace.Listeners.Remove(_traceListener);
        }
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
    }
}
