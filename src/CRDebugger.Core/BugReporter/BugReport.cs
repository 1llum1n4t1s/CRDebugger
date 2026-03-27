using CRDebugger.Core.Logging;
using CRDebugger.Core.SystemInfo;

namespace CRDebugger.Core.BugReporter;

/// <summary>
/// バグレポートのデータコンテナ。
/// イミュータブルなレコード型で、レポート送信時に一度だけ生成される。
/// スクリーンショット・ログ・システム情報など診断に必要な情報をすべて内包する。
/// </summary>
/// <param name="Id">レポートの一意識別子（ランダム生成 Guid）</param>
/// <param name="CreatedAt">レポート作成日時（タイムゾーン付き）</param>
/// <param name="UserMessage">ユーザーが入力したバグの説明テキスト</param>
/// <param name="UserEmail">返信先となるユーザーの連絡先メールアドレス</param>
/// <param name="SystemInfo">収集されたシステム情報エントリの一覧</param>
/// <param name="RecentLogs">レポート送信時点までに蓄積された直近のログエントリ一覧</param>
/// <param name="Screenshot">スクリーンショット画像データ（PNG形式バイト列）。未取得の場合は <c>null</c></param>
public sealed record BugReport(
    Guid Id,
    DateTimeOffset CreatedAt,
    string UserMessage,
    string UserEmail,
    IReadOnlyList<SystemInfoEntry> SystemInfo,
    IReadOnlyList<LogEntry> RecentLogs,
    byte[]? Screenshot
);
