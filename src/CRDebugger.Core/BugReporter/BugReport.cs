using CRDebugger.Core.Logging;
using CRDebugger.Core.SystemInfo;

namespace CRDebugger.Core.BugReporter;

/// <summary>
/// バグレポートデータ
/// </summary>
public sealed record BugReport(
    Guid Id,
    DateTimeOffset CreatedAt,
    string UserMessage,
    string UserEmail,
    IReadOnlyList<SystemInfoEntry> SystemInfo,
    IReadOnlyList<LogEntry> RecentLogs,
    byte[]? Screenshot
);
