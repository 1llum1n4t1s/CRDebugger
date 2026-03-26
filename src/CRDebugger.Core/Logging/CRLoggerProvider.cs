using Microsoft.Extensions.Logging;

namespace CRDebugger.Core.Logging;

/// <summary>
/// Microsoft.Extensions.Logging 統合用 ILoggerProvider
/// </summary>
public sealed class CRLoggerProvider : ILoggerProvider
{
    private readonly LogStore _logStore;

    public CRLoggerProvider(LogStore logStore)
    {
        _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
    }

    public ILogger CreateLogger(string categoryName) => new CRLogger(_logStore, categoryName);

    public void Dispose() { }
}

/// <summary>
/// Microsoft.Extensions.Logging 統合用 ILogger
/// </summary>
internal sealed class CRLogger : ILogger
{
    private readonly LogStore _logStore;
    private readonly string _category;

    public CRLogger(LogStore logStore, string category)
    {
        _logStore = logStore;
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var level = logLevel switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace or
            Microsoft.Extensions.Logging.LogLevel.Debug => CRLogLevel.Debug,
            Microsoft.Extensions.Logging.LogLevel.Information => CRLogLevel.Info,
            Microsoft.Extensions.Logging.LogLevel.Warning => CRLogLevel.Warning,
            _ => CRLogLevel.Error
        };

        var message = formatter(state, exception);
        var stackTrace = exception?.StackTrace;

        _logStore.Append(level, _category, message, stackTrace);
    }
}
