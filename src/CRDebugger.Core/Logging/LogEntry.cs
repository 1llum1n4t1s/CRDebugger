namespace CRDebugger.Core.Logging;

/// <summary>
/// イミュータブルなログエントリ
/// </summary>
public sealed record LogEntry(
    int Id,
    DateTimeOffset Timestamp,
    CRLogLevel Level,
    string Channel,
    string Message,
    string? StackTrace
);
