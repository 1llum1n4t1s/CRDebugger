namespace CRDebugger.Core.Logging;

/// <summary>
/// ログフィルタ条件
/// </summary>
public sealed record LogFilter(
    bool ShowDebug = true,
    bool ShowInfo = true,
    bool ShowWarning = true,
    bool ShowError = true,
    string? SearchText = null
)
{
    public bool Matches(LogEntry entry)
    {
        var levelMatch = entry.Level switch
        {
            CRLogLevel.Debug => ShowDebug,
            CRLogLevel.Info => ShowInfo,
            CRLogLevel.Warning => ShowWarning,
            CRLogLevel.Error => ShowError,
            _ => true
        };

        if (!levelMatch) return false;

        if (!string.IsNullOrEmpty(SearchText))
        {
            return entry.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }
}
