using System.Diagnostics;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.BugReporter;
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

    public IDebuggerWindow Window { get; }
    public IUiThread UiThread { get; }

    private CRTraceListener? _traceListener;

    public CRDebuggerContext(CRDebuggerOptions options)
    {
        var window = options.Window ?? throw new InvalidOperationException(
            "IDebuggerWindowが設定されていません。UseWpf(), UseAvalonia(), UseWinForms() のいずれかを呼んでください。");
        var uiThread = options.UiThread ?? throw new InvalidOperationException(
            "IUiThreadが設定されていません。");

        Window = window;
        UiThread = uiThread;

        // サービス初期化
        LogStore = new LogStore(options.MaxLogEntries);
        SystemInfo = new SystemInfoCollector();
        Options = new OptionsEngine();
        Profiler = new ProfilerEngine(options.ProfilerSampleInterval);
        BugReporter = new BugReportEngine(LogStore, SystemInfo, options.BugReportSender);
        ThemeManager = new ThemeManager(options.Theme);
        LoggerProvider = new CRLoggerProvider(LogStore);

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
            ThemeManager, options.DefaultTab);

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
