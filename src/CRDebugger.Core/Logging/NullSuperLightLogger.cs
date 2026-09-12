using SuperLightLogger;

namespace CRDebugger.Core.Logging;

/// <summary>明示的な無効化時に、ホストのロガーへ接続せず何も記録しない ILog。</summary>
internal sealed class NullSuperLightLogger : ILog
{
    internal static readonly NullSuperLightLogger Instance = new();

    private NullSuperLightLogger() { }

    /// <inheritdoc />
    public bool IsTraceEnabled => false;
    /// <inheritdoc />
    public void Trace(object? message) { }
    /// <inheritdoc />
    public void Trace(object? message, Exception? exception) { }
    /// <inheritdoc />
    public void TraceFormat(string format, params object?[] args) { }
    /// <inheritdoc />
    public void TraceFormat(string format, object? arg0) { }
    /// <inheritdoc />
    public void TraceFormat(string format, object? arg0, object? arg1) { }
    /// <inheritdoc />
    public void TraceFormat(string format, object? arg0, object? arg1, object? arg2) { }
    /// <inheritdoc />
    public void TraceFormat(IFormatProvider? provider, string format, params object?[] args) { }

    /// <inheritdoc />
    public bool IsDebugEnabled => false;
    /// <inheritdoc />
    public void Debug(object? message) { }
    /// <inheritdoc />
    public void Debug(object? message, Exception? exception) { }
    /// <inheritdoc />
    public void DebugFormat(string format, params object?[] args) { }
    /// <inheritdoc />
    public void DebugFormat(string format, object? arg0) { }
    /// <inheritdoc />
    public void DebugFormat(string format, object? arg0, object? arg1) { }
    /// <inheritdoc />
    public void DebugFormat(string format, object? arg0, object? arg1, object? arg2) { }
    /// <inheritdoc />
    public void DebugFormat(IFormatProvider? provider, string format, params object?[] args) { }

    /// <inheritdoc />
    public bool IsInfoEnabled => false;
    /// <inheritdoc />
    public void Info(object? message) { }
    /// <inheritdoc />
    public void Info(object? message, Exception? exception) { }
    /// <inheritdoc />
    public void InfoFormat(string format, params object?[] args) { }
    /// <inheritdoc />
    public void InfoFormat(string format, object? arg0) { }
    /// <inheritdoc />
    public void InfoFormat(string format, object? arg0, object? arg1) { }
    /// <inheritdoc />
    public void InfoFormat(string format, object? arg0, object? arg1, object? arg2) { }
    /// <inheritdoc />
    public void InfoFormat(IFormatProvider? provider, string format, params object?[] args) { }

    /// <inheritdoc />
    public bool IsWarnEnabled => false;
    /// <inheritdoc />
    public void Warn(object? message) { }
    /// <inheritdoc />
    public void Warn(object? message, Exception? exception) { }
    /// <inheritdoc />
    public void WarnFormat(string format, params object?[] args) { }
    /// <inheritdoc />
    public void WarnFormat(string format, object? arg0) { }
    /// <inheritdoc />
    public void WarnFormat(string format, object? arg0, object? arg1) { }
    /// <inheritdoc />
    public void WarnFormat(string format, object? arg0, object? arg1, object? arg2) { }
    /// <inheritdoc />
    public void WarnFormat(IFormatProvider? provider, string format, params object?[] args) { }

    /// <inheritdoc />
    public bool IsErrorEnabled => false;
    /// <inheritdoc />
    public void Error(object? message) { }
    /// <inheritdoc />
    public void Error(object? message, Exception? exception) { }
    /// <inheritdoc />
    public void ErrorFormat(string format, params object?[] args) { }
    /// <inheritdoc />
    public void ErrorFormat(string format, object? arg0) { }
    /// <inheritdoc />
    public void ErrorFormat(string format, object? arg0, object? arg1) { }
    /// <inheritdoc />
    public void ErrorFormat(string format, object? arg0, object? arg1, object? arg2) { }
    /// <inheritdoc />
    public void ErrorFormat(IFormatProvider? provider, string format, params object?[] args) { }

    /// <inheritdoc />
    public bool IsFatalEnabled => false;
    /// <inheritdoc />
    public void Fatal(object? message) { }
    /// <inheritdoc />
    public void Fatal(object? message, Exception? exception) { }
    /// <inheritdoc />
    public void FatalFormat(string format, params object?[] args) { }
    /// <inheritdoc />
    public void FatalFormat(string format, object? arg0) { }
    /// <inheritdoc />
    public void FatalFormat(string format, object? arg0, object? arg1) { }
    /// <inheritdoc />
    public void FatalFormat(string format, object? arg0, object? arg1, object? arg2) { }
    /// <inheritdoc />
    public void FatalFormat(IFormatProvider? provider, string format, params object?[] args) { }
}
