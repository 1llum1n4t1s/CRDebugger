namespace CRDebugger.Core.Logging;

/// <summary>
/// イミュータブルなログエントリ。
/// record 型で定義されており、with 式による差分コピーをサポートする。
/// </summary>
/// <param name="Id">ログエントリの連番ID（<see cref="LogStore"/> が発番する）</param>
/// <param name="Timestamp">ログ記録日時（タイムゾーン付き）</param>
/// <param name="Level">ログレベル（Debug / Info / Warning / Error）</param>
/// <param name="Channel">ログのチャネル名（カテゴリ。例: 名前空間、"Trace" など）</param>
/// <param name="Message">ログメッセージ本文</param>
/// <param name="StackTrace">例外発生時のスタックトレース。なければ <c>null</c></param>
/// <param name="DuplicateCount">同一ログの重複回数（折りたたみ機能で使用。初期値 1）</param>
/// <param name="RichSpans">リッチテキスト装飾のスパン情報。なければ <c>null</c></param>
public sealed record LogEntry(
    int Id,
    DateTimeOffset Timestamp,
    CRLogLevel Level,
    string Channel,
    string Message,
    string? StackTrace,
    int DuplicateCount = 1,
    IReadOnlyList<RichTextSpan>? RichSpans = null
);
