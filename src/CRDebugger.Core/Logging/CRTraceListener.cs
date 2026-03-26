using System.Diagnostics;

namespace CRDebugger.Core.Logging;

/// <summary>
/// System.Diagnostics.Trace / Debug 出力をキャプチャするTraceListener
/// </summary>
public sealed class CRTraceListener : TraceListener
{
    private readonly LogStore _logStore;
    private System.Text.StringBuilder? _messageBuilder;

    public CRTraceListener(LogStore logStore)
    {
        _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
        Name = "CRDebugger";
    }

    public override void Write(string? message)
    {
        if (message == null) return;
        _messageBuilder ??= new System.Text.StringBuilder();
        _messageBuilder.Append(message);
    }

    public override void WriteLine(string? message)
    {
        string fullMessage;
        if (_messageBuilder != null)
        {
            if (message != null) _messageBuilder.Append(message);
            fullMessage = _messageBuilder.ToString();
            _messageBuilder.Clear();
        }
        else
        {
            fullMessage = message ?? string.Empty;
        }

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
