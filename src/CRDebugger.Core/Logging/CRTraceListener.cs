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
    /// 複数スレッドから断片が混ざらないよう <see cref="ThreadStaticAttribute"/> でスレッド毎に持つ。
    /// </summary>
    [ThreadStatic]
    private static System.Text.StringBuilder? _messageBuilder;

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
    /// このリスナーがスレッドセーフであることを示す。
    /// <c>true</c> を返すと <see cref="TraceListener"/> 既定の <c>lock(this)</c> をスキップでき、
    /// ロック競合によるホスト側のデッドロックを回避できる（内部のスレッド安全性は本実装で確保）。
    /// </summary>
    public override bool IsThreadSafe => true;

    /// <summary>
    /// 改行なしの断片メッセージを蓄積する
    /// </summary>
    /// <param name="message">追記するメッセージ断片。<c>null</c> の場合は何もしない</param>
    public override void Write(string? message)
    {
        // null メッセージは無視する
        if (message == null) return;
        try
        {
            // ThreadStatic のため初回呼び出し時にスレッド毎の StringBuilder を遅延生成する
            _messageBuilder ??= new System.Text.StringBuilder();
            _messageBuilder.Append(message);
        }
        catch
        {
            // CRDebugger 内部の例外は絶対にホスト側に伝播させない（Trace 経路は全プロセス共有の脆弱経路）
        }
    }

    /// <summary>
    /// 1行分のメッセージを確定して <see cref="LogStore"/> に書き込む。
    /// 蓄積中の断片がある場合はそれも結合する。
    /// </summary>
    /// <param name="message">行末に追記するメッセージ。<c>null</c> の場合は空文字扱い</param>
    public override void WriteLine(string? message)
    {
        try
        {
            string fullMessage;
            var builder = _messageBuilder;
            if (builder != null)
            {
                // 蓄積済み断片がある場合は行末メッセージを結合して完成させる
                if (message != null) builder.Append(message);
                fullMessage = builder.ToString();
                // StringBuilder を再利用するためにクリアする
                builder.Clear();
            }
            else
            {
                // 断片がない場合はそのまま使用する
                fullMessage = message ?? string.Empty;
            }

            // Debug チャネルとして LogStore に記録する
            _logStore.Append(CRLogLevel.Debug, "Trace", fullMessage);
        }
        catch
        {
            // CRDebugger 内部の例外は絶対にホスト側に伝播させない
        }
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
        try
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
        catch
        {
            // CRDebugger 内部の例外は絶対にホスト側に伝播させない
        }
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
        // 引数がある場合は書式展開し、ない場合はそのまま使う。
        // string.Format は引数不一致等で FormatException をスローし得るため、
        // 失敗時は生の format をフォールバックとして記録し、ホスト側に例外を逆流させない (#A2-002)
        string message;
        if (args != null && format != null)
        {
            try
            {
                message = string.Format(format, args);
            }
            catch (FormatException)
            {
                // 書式展開に失敗した場合は生の format をログに残し、Trace 経路を切らさない
                message = format;
            }
            catch
            {
                // 想定外例外も握りつぶし、format を生のまま記録する
                message = format;
            }
        }
        else
        {
            message = format ?? string.Empty;
        }
        // 展開済みメッセージを単一メッセージ版のオーバーロードに委譲する
        TraceEvent(eventCache, source, eventType, id, message);
    }
}
