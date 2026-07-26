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

    /// <summary>
    /// レポートを実際に送信する送信先（差し替え可能）。
    /// <see cref="SetSender"/> による更新と並行する読み取りで参照の torn read/可視性問題を避けるため
    /// <c>volatile</c> でメモリバリアを担保する (#50)。
    /// </summary>
    private volatile IBugReportSender _sender;

    /// <summary>送信タイムアウト（CancellationToken 未指定時のデフォルト）</summary>
    private readonly TimeSpan _sendTimeout;

    /// <summary>
    /// <see cref="BugReportEngine"/> のインスタンスを生成する
    /// </summary>
    /// <param name="logStore">ログストア</param>
    /// <param name="systemInfo">システム情報コレクター</param>
    /// <param name="sender">バグレポート送信先。<c>null</c> の場合はコンソール出力</param>
    /// <param name="sendTimeout">送信タイムアウト。<c>null</c> の場合は 60 秒</param>
    public BugReportEngine(
        LogStore logStore,
        SystemInfoCollector systemInfo,
        IBugReportSender? sender = null,
        TimeSpan? sendTimeout = null)
    {
        _logStore = logStore;
        _systemInfo = systemInfo;
        // sender が未指定の場合はデフォルトのコンソール送信先を使用する
        _sender = sender ?? new DefaultConsoleBugReportSender();
        // タイムアウト未指定なら 60 秒をデフォルトとして使用する
        _sendTimeout = sendTimeout ?? TimeSpan.FromSeconds(60);
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
    /// バグレポートを作成して送信する。
    /// スクリーンショット取得・ログ収集・送信処理は <see cref="Task.Run(Func{Task})"/> でワーカースレッドへオフロードされ、
    /// UI スレッドをブロックしない。
    /// </summary>
    /// <param name="userMessage">ユーザーが入力したバグの説明</param>
    /// <param name="userEmail">ユーザーの連絡先メールアドレス</param>
    /// <param name="screenshotCapture">スクリーンショット取得デリゲート（省略可）</param>
    /// <param name="cancellationToken">キャンセルトークン。<c>default</c> の場合はコンストラクタで指定したタイムアウトが適用される</param>
    /// <returns>作成されたバグレポート</returns>
    public async Task<BugReport> CreateAndSendAsync(
        string userMessage,
        string userEmail,
        Func<Task<byte[]?>>? screenshotCapture = null,
        CancellationToken cancellationToken = default)
    {
        // 呼び出し元が CancellationToken を渡してこなかった場合は内部 CTS でタイムアウト制御する
        // 渡してきた場合はそのトークンを尊重する（外部から長い待機をキャンセル可能にするため）
        CancellationTokenSource? internalCts = null;
        var effectiveToken = cancellationToken;
        if (!cancellationToken.CanBeCanceled)
        {
            internalCts = new CancellationTokenSource(_sendTimeout);
            effectiveToken = internalCts.Token;
        }

        // _sender は SetSender により別スレッドから差し替えられる可能性があるため、
        // 本呼び出し中は一貫したインスタンスを使うようローカルにキャプチャする (#50)。
        // volatile 読みでメモリバリアを担保した上でローカル変数に固定する
        var sender = _sender;

        try
        {
            // スクリーンショット取得・SystemInfo 収集・LogStore.GetAll() は UI スレッドから逃がして実行する
            // （SystemInfo.CollectAll は WMI 等で長時間ブロックする可能性があり、UI スレッドを止めないため）
            return await Task.Run(async () =>
            {
                // スクリーンショット取得デリゲートが指定されている場合のみキャプチャを実行する
                // 取得失敗時は screenshot=null で続行（バグレポート送信自体は止めない）
                byte[]? screenshot = null;
                if (screenshotCapture != null)
                {
                    try
                    {
                        screenshot = await screenshotCapture().ConfigureAwait(false);
                    }
                    catch
                    {
                        // スクリーンショット取得失敗は本流の送信処理を阻害しない
                        screenshot = null;
                    }
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

                // 設定された送信先にレポートを非同期送信する（タイムアウト・キャンセル制御は effectiveToken に集約）。
                // _sender ではなくローカルキャプチャ済み sender を使い、本呼び出し中の差し替えに影響されないようにする (#50)
                var sent = await sender.SendAsync(report, effectiveToken).ConfigureAwait(false);

                // IBugReportSender は「失敗時は false を返す」契約なので、戻り値を捨てずに例外へ変換する。
                // 捨てると UI が常に「送信しました！」と表示し、届いていないレポートを届いたと誤認させてしまう。
                if (!sent)
                    throw new CRDebuggerBugReportSendException();

                return report;
            }, effectiveToken).ConfigureAwait(false);
        }
        finally
        {
            // 内部 CTS を生成した場合のみ解放する（外部 CTS は呼び出し元の責任で解放）
            internalCts?.Dispose();
        }
    }
}
