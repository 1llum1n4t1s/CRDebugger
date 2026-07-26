using CRDebugger.Core.Abstractions;
using CRDebugger.Core.BugReporter;
using CRDebugger.Core.Logging;
using CRDebugger.Core.SystemInfo;
using CRDebugger.Core.ViewModels;
using Moq;

namespace CRDebugger.Core.Tests;

/// <summary>
/// <see cref="BugReporterViewModel"/> の送信フローのテスト。
/// 連打による再入と、送信失敗の握りつぶしの再発防止が主目的。
/// </summary>
public sealed class BugReporterViewModelTests
{
    /// <summary>スクリーンショットを返さないダミーウィンドウを作る</summary>
    private static IDebuggerWindow CreateWindow()
    {
        var mock = new Mock<IDebuggerWindow>();
        mock.Setup(w => w.CaptureScreenshotAsync()).ReturnsAsync((byte[]?)null);
        return mock.Object;
    }

    /// <summary>指定した送信先でエンジンを組み立てる</summary>
    private static BugReportEngine CreateEngine(IBugReportSender sender) =>
        new(new LogStore(50), new SystemInfoCollector(SystemInfoCollectionLevel.Minimal), sender);

    /// <summary>送信完了を外部から制御できる送信先</summary>
    private sealed class GatedSender : IBugReportSender
    {
        private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>SendAsync が呼ばれた回数</summary>
        public int CallCount;

        /// <summary>SendAsync に渡された CancellationToken（破棄検知に使う）</summary>
        public CancellationToken LastToken { get; private set; }

        /// <summary>送信を完了させる</summary>
        public void Release(bool result) => _gate.TrySetResult(result);

        /// <inheritdoc/>
        public async Task<bool> SendAsync(BugReport report, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref CallCount);
            LastToken = cancellationToken;
            return await _gate.Task;
        }
    }

    /// <summary>常に失敗（false）を返す送信先</summary>
    private sealed class FailingSender : IBugReportSender
    {
        /// <inheritdoc/>
        public Task<bool> SendAsync(BugReport report, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    /// <summary>
    /// 送信中に再度送信コマンドを実行しても 2 本目が走らないこと。
    /// ガードが無いと 2 本目が進行中の CancellationTokenSource を Dispose し、
    /// 1 本目のトークンが ObjectDisposedException を誘発する。
    /// </summary>
    [Fact]
    public async Task SendCommand_ExecutedWhileSending_DoesNotStartSecondSend()
    {
        var sender = new GatedSender();
        using var vm = new BugReporterViewModel(CreateEngine(sender), CreateWindow())
        {
            UserMessage = "再現手順です"
        };

        // 1 本目を開始（GatedSender が完了させないので送信中のまま止まる）
        vm.SendCommand.Execute(null);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!vm.IsSending && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.True(vm.IsSending, "1 本目の送信が開始されていない");
        // 送信中は CanExecute が false になり、UI 側のボタンが無効化される
        Assert.False(vm.SendCommand.CanExecute(null));

        // 2 本目を連打で実行（ガードにより無視されるはず）
        vm.SendCommand.Execute(null);
        vm.SendCommand.Execute(null);
        await Task.Delay(100);

        Assert.Equal(1, Volatile.Read(ref sender.CallCount));

        // 1 本目のトークンが破棄されていないこと（破棄済みなら Register が投げる）
        var thrown = Record.Exception(() => sender.LastToken.Register(() => { }).Dispose());
        Assert.Null(thrown);

        // 1 本目を完了させると送信中フラグとコマンドが復帰する
        sender.Release(true);

        deadline = DateTime.UtcNow.AddSeconds(5);
        while (vm.IsSending && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.False(vm.IsSending);
        Assert.True(vm.SendCommand.CanExecute(null));
        Assert.Equal("バグレポートを送信しました！", vm.StatusMessage);
    }

    /// <summary>
    /// 送信完了後は再度送信できること（再入ガードが解除されていること）。
    /// finally での解除を忘れると以降の送信が永久にブロックされる。
    /// </summary>
    [Fact]
    public async Task SendCommand_AfterCompletion_CanSendAgain()
    {
        var sender = new GatedSender();
        sender.Release(true); // 即座に完了する

        using var vm = new BugReporterViewModel(CreateEngine(sender), CreateWindow())
        {
            UserMessage = "1 回目"
        };

        vm.SendCommand.Execute(null);
        await WaitUntilIdleAsync(vm);

        vm.UserMessage = "2 回目";
        vm.SendCommand.Execute(null);
        await WaitUntilIdleAsync(vm);

        Assert.Equal(2, Volatile.Read(ref sender.CallCount));
    }

    /// <summary>
    /// 送信先が契約どおり false（失敗）を返した場合、成功表示にならないこと。
    /// 戻り値を捨てると「届いていないのに送信しました」と表示されてしまう。
    /// </summary>
    [Fact]
    public async Task SendCommand_SenderReturnsFalse_ShowsFailure()
    {
        using var vm = new BugReporterViewModel(CreateEngine(new FailingSender()), CreateWindow())
        {
            UserMessage = "失敗するはず"
        };

        vm.SendCommand.Execute(null);
        await WaitUntilIdleAsync(vm);

        Assert.StartsWith("送信失敗:", vm.StatusMessage);
        // 失敗時は入力内容を消さない（ユーザーが再送できるようにするため）
        Assert.Equal("失敗するはず", vm.UserMessage);
    }

    /// <summary>送信中フラグが下りるまで待つ（CI ランナー向けに最大 5 秒）</summary>
    private static async Task WaitUntilIdleAsync(BugReporterViewModel vm)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        // 開始を待ってから完了を待つ（Execute は非同期に走り出すため）
        while (!vm.IsSending && string.IsNullOrEmpty(vm.StatusMessage) && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        while (vm.IsSending && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        // 状態更新の反映を待つ
        await Task.Delay(50);
    }
}
