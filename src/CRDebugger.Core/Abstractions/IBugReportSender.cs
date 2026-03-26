using CRDebugger.Core.BugReporter;

namespace CRDebugger.Core.Abstractions;

/// <summary>
/// バグレポートの送信先を抽象化
/// </summary>
public interface IBugReportSender
{
    Task<bool> SendAsync(BugReport report, CancellationToken cancellationToken = default);
}
