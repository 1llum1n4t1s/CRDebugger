using Microsoft.Extensions.Logging;

namespace CRDebugger.Core.Logging;

/// <summary>
/// Microsoft.Extensions.Logging 統合用 ILoggerProvider。
/// <see cref="LogStore"/> へログを転送する <see cref="ILogger"/> を生成する。
/// </summary>
public sealed class CRLoggerProvider : ILoggerProvider
{
    /// <summary>ログの書き込み先ストア</summary>
    private readonly LogStore _logStore;

    /// <summary>
    /// <see cref="CRLoggerProvider"/> のインスタンスを生成する
    /// </summary>
    /// <param name="logStore">ログの保存先となるログストア</param>
    /// <exception cref="ArgumentNullException"><paramref name="logStore"/> が <c>null</c> の場合</exception>
    public CRLoggerProvider(LogStore logStore)
    {
        _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
    }

    /// <summary>
    /// 指定されたカテゴリ名で <see cref="ILogger"/> を作成する
    /// </summary>
    /// <param name="categoryName">ログカテゴリ名（名前空間やクラス名など）</param>
    /// <returns>CRDebugger統合用のロガー</returns>
    public ILogger CreateLogger(string categoryName) => new CRLogger(_logStore, categoryName);

    /// <inheritdoc/>
    public void Dispose() { }
}

/// <summary>
/// Microsoft.Extensions.Logging 統合用 ILogger の実装。
/// <see cref="LogStore"/> にログエントリを書き込む。
/// </summary>
internal sealed class CRLogger : ILogger
{
    /// <summary>ログエントリの書き込み先</summary>
    private readonly LogStore _logStore;
    /// <summary>このロガーのカテゴリ名（クラス名や名前空間）</summary>
    private readonly string _category;

    /// <summary>
    /// <see cref="CRLogger"/> のインスタンスを生成する
    /// </summary>
    /// <param name="logStore">ログエントリの書き込み先ストア</param>
    /// <param name="category">ログカテゴリ名</param>
    public CRLogger(LogStore logStore, string category)
    {
        _logStore = logStore;
        _category = category;
    }

    /// <summary>
    /// ログスコープを開始する（CRDebugger では未使用）
    /// </summary>
    /// <typeparam name="TState">スコープ状態の型</typeparam>
    /// <param name="state">スコープ状態</param>
    /// <returns>常に <c>null</c></returns>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <summary>
    /// 指定ログレベルが有効かどうかを返す（常に有効）
    /// </summary>
    /// <param name="logLevel">チェックするログレベル</param>
    /// <returns>常に <c>true</c></returns>
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    /// <summary>
    /// ログエントリを <see cref="LogStore"/> に書き込む
    /// </summary>
    /// <typeparam name="TState">ログ状態の型</typeparam>
    /// <param name="logLevel">Microsoft.Extensions.Logging のログレベル</param>
    /// <param name="eventId">イベントID</param>
    /// <param name="state">ログの状態オブジェクト</param>
    /// <param name="exception">例外（なければ <c>null</c>）</param>
    /// <param name="formatter">メッセージ整形関数</param>
    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // Microsoft.Extensions.Logging のログレベルを CRLogLevel に変換する
        var level = logLevel switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace or
            Microsoft.Extensions.Logging.LogLevel.Debug => CRLogLevel.Debug,
            Microsoft.Extensions.Logging.LogLevel.Information => CRLogLevel.Info,
            Microsoft.Extensions.Logging.LogLevel.Warning => CRLogLevel.Warning,
            // Critical / Error / None はすべて Error 扱い
            _ => CRLogLevel.Error
        };

        // フォーマッタでメッセージ文字列を生成する
        var message = formatter(state, exception);
        // 例外があればスタックトレースも保存する
        var stackTrace = exception?.StackTrace;

        _logStore.Append(level, _category, message, stackTrace);
    }
}
