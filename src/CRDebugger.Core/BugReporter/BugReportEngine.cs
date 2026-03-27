using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Logging;
using CRDebugger.Core.SystemInfo;

namespace CRDebugger.Core.BugReporter;

/// <summary>
/// バグレポートの収集と送信を統括するエンジン。
/// ログ・システム情報・スクリーンショットを集約し、指定された送信先へ届ける。
/// </summary>
public sealed class BugReportEngine
{
    /// <summary>ログエントリを蓄積するストア</summary>
    private readonly LogStore _logStore;

    /// <summary>OS・ハードウェア情報を収集するコレクター</summary>
    private readonly SystemInfoCollector _systemInfo;

    /// <summary>レポートを実際に送信する送信先（差し替え可能）</summary>
    private IBugReportSender _sender;

    /// <summary>
    /// <see cref="BugReportEngine"/> のインスタンスを生成する
    /// </summary>
    /// <param name="logStore">ログストア</param>
    /// <param name="systemInfo">システム情報コレクター</param>
    /// <param name="sender">バグレポート送信先。<c>null</c> の場合はコンソール出力</param>
    public BugReportEngine(LogStore logStore, SystemInfoCollector systemInfo, IBugReportSender? sender = null)
    {
        _logStore = logStore;
        _systemInfo = systemInfo;
        // sender が未指定の場合はデフォルトのコンソール送信先を使用する
        _sender = sender ?? new DefaultConsoleBugReportSender();
    }

    /// <summary>
    /// バグレポートの送信先を変更する
    /// </summary>
    /// <param name="sender">新しい送信先</param>
    /// <exception cref="ArgumentNullException"><paramref name="sender"/> が <c>null</c> の場合</exception>
    public void SetSender(IBugReportSender sender)
    {
        // null チェック：送信先が null のままだとレポート送信時に NullReferenceException が発生するため必須
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// バグレポートを作成して送信する
    /// </summary>
    /// <param name="userMessage">ユーザーが入力したバグの説明</param>
    /// <param name="userEmail">ユーザーの連絡先メールアドレス</param>
    /// <param name="screenshotCapture">スクリーンショット取得デリゲート（省略可）</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>作成されたバグレポート</returns>
    public async Task<BugReport> CreateAndSendAsync(
        string userMessage,
        string userEmail,
        Func<Task<byte[]?>>? screenshotCapture = null,
        CancellationToken cancellationToken = default)
    {
        // スクリーンショット取得デリゲートが指定されている場合のみ非同期でキャプチャを実行する
        byte[]? screenshot = null;
        if (screenshotCapture != null)
        {
            // ConfigureAwait(false) でデッドロックを防止しながらスクリーンショットを取得する
            screenshot = await screenshotCapture().ConfigureAwait(false);
        }

        // 収集したすべての情報を組み合わせてイミュータブルなレポートレコードを生成する
        var report = new BugReport(
            Id: Guid.NewGuid(),                  // 一意識別子をランダム生成
            CreatedAt: DateTimeOffset.Now,        // レポート作成日時（タイムゾーン付き）
            UserMessage: userMessage,             // ユーザーが入力したバグの説明文
            UserEmail: userEmail,                 // 返信先メールアドレス
            SystemInfo: _systemInfo.CollectAll(), // OS・CPU・メモリ等のシステム情報を収集
            RecentLogs: _logStore.GetAll(),       // 直近のログエントリを全件取得
            Screenshot: screenshot                // キャプチャ画像（取得できなかった場合は null）
        );

        // 設定された送信先にレポートを非同期送信する（ConfigureAwait でコンテキスト切り替えを抑制）
        await _sender.SendAsync(report, cancellationToken).ConfigureAwait(false);
        return report;
    }
}
