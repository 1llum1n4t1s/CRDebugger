using System.Diagnostics;

namespace CRDebugger.Core.Logging;

/// <summary>
/// System.Diagnostics.Trace / Debug 出力をキャプチャして <see cref="LogStore"/> に転送する TraceListener。
/// <see cref="Write"/> で断片を蓄積し、<see cref="WriteLine"/> で1行として確定する。
/// </summary>
public sealed class CRTraceListener : TraceListener
{
    /// <summary>キャプチャしたトレース出力の書き込み先</summary>
    private readonly LogStore _logStore;
    /// <summary>
    /// <see cref="Write"/> で蓄積中の断片メッセージ。
    /// <see cref="WriteLine"/> が呼ばれた時点でフラッシュされる。
    /// </summary>
    private System.Text.StringBuilder? _messageBuilder;

    /// <summary>
    /// <see cref="CRTraceListener"/> のインスタンスを生成する
    /// </summary>
    /// <param name="logStore">キャプチャしたトレース出力の保存先</param>
    /// <exception cref="ArgumentNullException"><paramref name="logStore"/> が <c>null</c> の場合</exception>
    public CRTraceListener(LogStore logStore)
    {
        _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
        // TraceListener の識別名を設定する
        Name = "CRDebugger";
    }

    /// <summary>
    /// 改行なしの断片メッセージを蓄積する
    /// </summary>
    /// <param name="message">追記するメッセージ断片。<c>null</c> の場合は何もしない</param>
    public override void Write(string? message)
    {
        // null メッセージは無視する
        if (message == null) return;
        // 初回呼び出し時に StringBuilder を遅延生成する
        _messageBuilder ??= new System.Text.StringBuilder();
        _messageBuilder.Append(message);
    }

    /// <summary>
    /// 1行分のメッセージを確定して <see cref="LogStore"/> に書き込む。
    /// 蓄積中の断片がある場合はそれも結合する。
    /// </summary>
    /// <param name="message">行末に追記するメッセージ。<c>null</c> の場合は空文字扱い</param>
    public override void WriteLine(string? message)
    {
        string fullMessage;
        if (_messageBuilder != null)
        {
            // 蓄積済み断片がある場合は行末メッセージを結合して完成させる
            if (message != null) _messageBuilder.Append(message);
            fullMessage = _messageBuilder.ToString();
            // StringBuilder を再利用するためにクリアする
            _messageBuilder.Clear();
        }
        else
        {
            // 断片がない場合はそのまま使用する
            fullMessage = message ?? string.Empty;
        }

        // Debug チャネルとして LogStore に記録する
        _logStore.Append(CRLogLevel.Debug, "Trace", fullMessage);
    }

    /// <summary>
    /// 構造化されたトレースイベントを受け取り、イベント種別を <see cref="CRLogLevel"/> に変換して記録する
    /// </summary>
    /// <param name="eventCache">トレースイベントのキャッシュ情報</param>
    /// <param name="source">トレースのソース名</param>
    /// <param name="eventType">トレースイベントの種別</param>
    /// <param name="id">イベントID</param>
    /// <param name="message">ログメッセージ</param>
    public override void TraceEvent(TraceEventCache? eventCache, string source,
        TraceEventType eventType, int id, string? message)
    {
        // TraceEventType を CRLogLevel にマッピングする
        var level = eventType switch
        {
            TraceEventType.Critical or TraceEventType.Error => CRLogLevel.Error,
            TraceEventType.Warning => CRLogLevel.Warning,
            TraceEventType.Information => CRLogLevel.Info,
            // Verbose / Start / Stop / Transfer などは Debug 扱い
            _ => CRLogLevel.Debug
        };
        _logStore.Append(level, source, message ?? string.Empty);
    }

    /// <summary>
    /// 書式付きトレースイベントを受け取り、<c>string.Format</c> で展開してから記録する
    /// </summary>
    /// <param name="eventCache">トレースイベントのキャッシュ情報</param>
    /// <param name="source">トレースのソース名</param>
    /// <param name="eventType">トレースイベントの種別</param>
    /// <param name="id">イベントID</param>
    /// <param name="format">書式文字列</param>
    /// <param name="args">書式引数</param>
    public override void TraceEvent(TraceEventCache? eventCache, string source,
        TraceEventType eventType, int id, string? format, params object?[]? args)
    {
        // 引数がある場合は書式展開し、ない場合はそのまま使う
        var message = args != null && format != null
            ? string.Format(format, args)
            : format ?? string.Empty;
        // 展開済みメッセージを単一メッセージ版のオーバーロードに委譲する
        TraceEvent(eventCache, source, eventType, id, message);
    }
}
