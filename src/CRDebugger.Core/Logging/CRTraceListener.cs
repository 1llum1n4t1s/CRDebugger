using System.Diagnostics;

namespace CRDebugger.Core.Logging;

/// <summary>
/// System.Diagnostics.Trace / Debug 出力をキャプチャするTraceListener
/// </summary>
public sealed class CRTraceListener : TraceListener
{
    private readonly LogStore _logStore;
    private string? _partialMessage;

    public CRTraceListener(LogStore logStore)
    {
        _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
        Name = "CRDebugger";
    }

    public override void Write(string? message)
    {
        _partialMessage = (_partialMessage ?? string.Empty) + message;
    }

    public override void WriteLine(string? message)
    {
        var fullMessage = _partialMessage != null
            ? _partialMessage + message
            : message ?? string.Empty;
        _partialMessage = null;

        _logStore.Append(CRLogLevel.Debug, "Trace", fullMessage);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source,
        TraceEventType eventType, int id, string? message)
    {
        var level = eventType switch
        {
            TraceEventType.Critical or TraceEventType.Error => CRLogLevel.Error,
            TraceEventType.Warning => CRLogLevel.Warning,
            TraceEventType.Information => CRLogLevel.Info,
            _ => CRLogLevel.Debug
        };
        _logStore.Append(level, source, message ?? string.Empty);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source,
        TraceEventType eventType, int id, string? format, params object?[]? args)
    {
        var message = args != null && format != null
            ? string.Format(format, args)
            : format ?? string.Empty;
        TraceEvent(eventCache, source, eventType, id, message);
    }
}
