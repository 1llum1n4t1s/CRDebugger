using CRDebugger.Core.BugReporter;

namespace CRDebugger.Core.Abstractions;

/// <summary>
/// バグレポートの送信先を抽象化するインターフェース。
/// HTTP API・メール・ローカルファイルなど任意の送信先を実装できる。
/// デフォルト実装は <see cref="DefaultConsoleBugReportSender"/>。
/// </summary>
public interface IBugReportSender
{
    /// <summary>
    /// バグレポートを非同期で送信する
    /// </summary>
    /// <param name="report">送信するバグレポート</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>送信成功時は <c>true</c>、失敗時は <c>false</c></returns>
    Task<bool> SendAsync(BugReport report, CancellationToken cancellationToken = default);
}
