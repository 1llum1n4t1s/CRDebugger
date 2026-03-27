namespace CRDebugger.Core;

/// <summary>
/// CRDebugger内部で発生した例外の基底クラス。
/// ホストアプリ側で catch (CRDebuggerException) でCRDebugger由来の例外だけを捕捉可能。
/// </summary>
public class CRDebuggerException : Exception
{
    /// <summary>
    /// 指定されたメッセージで <see cref="CRDebuggerException"/> を生成する
    /// </summary>
    /// <param name="message">エラーメッセージ</param>
    public CRDebuggerException(string message) : base($"[CRDebugger] {message}") { }

    /// <summary>
    /// 指定されたメッセージと内部例外で <see cref="CRDebuggerException"/> を生成する
    /// </summary>
    /// <param name="message">エラーメッセージ</param>
    /// <param name="innerException">内部例外</param>
    public CRDebuggerException(string message, Exception innerException) : base($"[CRDebugger] {message}", innerException) { }
}

/// <summary>
/// CRDebuggerが初期化されていない状態でAPIが呼ばれた場合の例外
/// </summary>
public sealed class CRDebuggerNotInitializedException : CRDebuggerException
{
    /// <summary>
    /// <see cref="CRDebuggerNotInitializedException"/> のインスタンスを生成する
    /// </summary>
    public CRDebuggerNotInitializedException()
        : base("CRDebuggerが初期化されていません。CRDebugger.Initialize() を先に呼んでください。") { }
}

/// <summary>
/// CRDebuggerが既に初期化済みの状態で再初期化が試みられた場合の例外
/// </summary>
public sealed class CRDebuggerAlreadyInitializedException : CRDebuggerException
{
    /// <summary>
    /// <see cref="CRDebuggerAlreadyInitializedException"/> のインスタンスを生成する
    /// </summary>
    public CRDebuggerAlreadyInitializedException()
        : base("CRDebuggerは既に初期化されています。再初期化する場合は先にShutdown()を呼んでください。") { }
}

/// <summary>
/// CRDebuggerの構成が不正な場合の例外
/// </summary>
public sealed class CRDebuggerConfigurationException : CRDebuggerException
{
    /// <summary>
    /// 指定されたメッセージで <see cref="CRDebuggerConfigurationException"/> を生成する
    /// </summary>
    /// <param name="message">エラーメッセージ</param>
    public CRDebuggerConfigurationException(string message) : base(message) { }

    /// <summary>
    /// 指定されたメッセージと内部例外で <see cref="CRDebuggerConfigurationException"/> を生成する
    /// </summary>
    /// <param name="message">エラーメッセージ</param>
    /// <param name="innerException">内部例外</param>
    public CRDebuggerConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// CRDebugger内部でエラーが発生した場合の例外。
/// これはCRDebuggerのバグである可能性が高い。
/// </summary>
public sealed class CRDebuggerInternalException : CRDebuggerException
{
    /// <summary>
    /// 指定されたメッセージと内部例外で <see cref="CRDebuggerInternalException"/> を生成する
    /// </summary>
    /// <param name="message">エラーメッセージ</param>
    /// <param name="innerException">原因となった内部例外</param>
    public CRDebuggerInternalException(string message, Exception innerException)
        : base($"内部エラー: {message} （これはCRDebuggerのバグの可能性があります。GitHubでIssueを報告してください）", innerException) { }
}
