using System.Text.Json;
using CRDebugger.Core.Abstractions;

namespace CRDebugger.Core.BugReporter;

/// <summary>
/// デフォルトのバグレポート送信先。
/// 送信先が未設定の場合のフォールバックとして機能し、レポート概要を JSON 形式でコンソールに出力する。
/// </summary>
public sealed class DefaultConsoleBugReportSender : IBugReportSender
{
    /// <summary>
    /// バグレポートの概要を JSON 形式でコンソールに出力する
    /// </summary>
    /// <param name="report">出力するバグレポート</param>
    /// <param name="cancellationToken">キャンセルトークン（このクラスでは使用しない）</param>
    /// <returns>常に <c>true</c>（コンソール出力は常に成功扱い）</returns>
    public Task<bool> SendAsync(BugReport report, CancellationToken cancellationToken = default)
    {
        // スクリーンショットのバイナリデータは大きいため、存在有無のみを bool で要約する
        var summary = new
        {
            report.Id,                                    // レポートの一意識別子
            report.CreatedAt,                             // レポート作成日時
            report.UserMessage,                           // ユーザーが入力したバグ説明
            report.UserEmail,                             // ユーザーのメールアドレス
            SystemInfoCount = report.SystemInfo.Count,    // システム情報エントリ数
            LogCount = report.RecentLogs.Count,           // ログエントリ数
            HasScreenshot = report.Screenshot != null     // スクリーンショットの有無
        };

        // 匿名オブジェクトをインデント付き JSON にシリアライズして可読性を高める
        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });

        // [CRDebugger] プレフィックスを付けてコンソールに出力することで他のログと区別しやすくする
        Console.WriteLine($"[CRDebugger] バグレポート送信:\n{json}");

        // コンソール出力は常に成功するため true を同期的に返す
        return Task.FromResult(true);
    }
}
