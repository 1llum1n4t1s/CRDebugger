using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Logging;
using CRDebugger.Core.SystemInfo;

namespace CRDebugger.Core.BugReporter;

/// <summary>
/// バグレポートの収集と送信を統括
/// </summary>
public sealed class BugReportEngine
{
    private readonly LogStore _logStore;
    private readonly SystemInfoCollector _systemInfo;
    private IBugReportSender _sender;

    public BugReportEngine(LogStore logStore, SystemInfoCollector systemInfo, IBugReportSender? sender = null)
    {
        _logStore = logStore;
        _systemInfo = systemInfo;
        _sender = sender ?? new DefaultConsoleBugReportSender();
    }

    public void SetSender(IBugReportSender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// バグレポートを作成して送信
    /// </summary>
    public async Task<BugReport> CreateAndSendAsync(
        string userMessage,
        string userEmail,
        Func<Task<byte[]?>>? screenshotCapture = null,
        CancellationToken cancellationToken = default)
    {
        byte[]? screenshot = null;
        if (screenshotCapture != null)
        {
            screenshot = await screenshotCapture().ConfigureAwait(false);
        }

        var report = new BugReport(
            Id: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.Now,
            UserMessage: userMessage,
            UserEmail: userEmail,
            SystemInfo: _systemInfo.CollectAll(),
            RecentLogs: _logStore.GetAll(),
            Screenshot: screenshot
        );

        await _sender.SendAsync(report, cancellationToken).ConfigureAwait(false);
        return report;
    }
}
