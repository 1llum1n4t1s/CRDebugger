namespace CRDebugger.Core;

/// <summary>
/// CRDebugger内部で発生した例外の基底クラス。
/// ホストアプリ側で catch (CRDebuggerException) でCRDebugger由来の例外だけを捕捉可能。
/// </summary>
public class CRDebuggerException : Exception
{
    public CRDebuggerException(string message) : base($"[CRDebugger] {message}") { }
    public CRDebuggerException(string message, Exception innerException) : base($"[CRDebugger] {message}", innerException) { }
}

/// <summary>
/// CRDebuggerが初期化されていない状態でAPIが呼ばれた場合の例外
/// </summary>
public sealed class CRDebuggerNotInitializedException : CRDebuggerException
{
    public CRDebuggerNotInitializedException()
        : base("CRDebuggerが初期化されていません。CRDebugger.Initialize() を先に呼んでください。") { }
}

/// <summary>
/// CRDebuggerが既に初期化済みの状態で再初期化が試みられた場合の例外
/// </summary>
public sealed class CRDebuggerAlreadyInitializedException : CRDebuggerException
{
    public CRDebuggerAlreadyInitializedException()
        : base("CRDebuggerは既に初期化されています。再初期化する場合は先にShutdown()を呼んでください。") { }
}

/// <summary>
/// CRDebuggerの構成が不正な場合の例外
/// </summary>
public sealed class CRDebuggerConfigurationException : CRDebuggerException
{
    public CRDebuggerConfigurationException(string message) : base(message) { }
    public CRDebuggerConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// CRDebugger内部でエラーが発生した場合の例外。
/// これはCRDebuggerのバグである可能性が高い。
/// </summary>
public sealed class CRDebuggerInternalException : CRDebuggerException
{
    public CRDebuggerInternalException(string message, Exception innerException)
        : base($"内部エラー: {message} （これはCRDebuggerのバグの可能性があります。GitHubでIssueを報告してください）", innerException) { }
}
