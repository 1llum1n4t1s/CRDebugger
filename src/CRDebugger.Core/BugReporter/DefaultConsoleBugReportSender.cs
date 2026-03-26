using System.Text.Json;
using CRDebugger.Core.Abstractions;

namespace CRDebugger.Core.BugReporter;

/// <summary>
/// デフォルトのバグレポート送信先（JSONでコンソール出力）
/// </summary>
public sealed class DefaultConsoleBugReportSender : IBugReportSender
{
    public Task<bool> SendAsync(BugReport report, CancellationToken cancellationToken = default)
    {
        var summary = new
        {
            report.Id,
            report.CreatedAt,
            report.UserMessage,
            report.UserEmail,
            SystemInfoCount = report.SystemInfo.Count,
            LogCount = report.RecentLogs.Count,
            HasScreenshot = report.Screenshot != null
        };

        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine($"[CRDebugger] バグレポート送信:\n{json}");
        return Task.FromResult(true);
    }
}
