using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Input;
using CRDebugger.Core.Logging;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using Moq;

namespace CRDebugger.Core.Tests;

/// <summary>
/// 静的ファサード <see cref="CRDebugger"/> を操作するテストクラスをまとめるコレクション。
/// <see cref="CRDebugger"/> はプロセス内で単一のグローバル状態（_context / _disabled / 静的イベント）を持つため、
/// 複数のテストクラスが並列に Initialize / Shutdown すると互いの状態を壊す。
/// このコレクションに属するクラスは xUnit によって直列実行される。
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CRDebuggerFacadeCollection
{
    /// <summary>コレクション名</summary>
    public const string Name = "CRDebuggerFacade";
}

/// <summary>
/// <see cref="CRDebuggerOptions.IsEnabled"/> = false の no-op 契約のテスト。
/// </summary>
[Collection(CRDebuggerFacadeCollection.Name)]
public sealed class DisabledFacadeTests : IDisposable
{
    public DisabledFacadeTests() => CRDebugger.Shutdown();

    public void Dispose() => CRDebugger.Shutdown();

    /// <summary>IsEnabled = false のオプションを作る</summary>
    private static CRDebuggerOptions CreateDisabledOptions() => new()
    {
        IsEnabled = false,
        CaptureTraceOutput = false,
        CaptureUnhandledExceptions = false,
    };

    /// <summary>
    /// IsEnabled = false で初期化した後、全公開 API が例外を投げずに no-op になること。
    /// ドキュメントが「Log/Show 等の API は no-op になる」と契約しているため、
    /// ここで例外が出るとホストアプリのリリースビルドがクラッシュする。
    /// </summary>
    [Fact]
    public void Disabled_AllPublicApis_AreNoOpAndDoNotThrow()
    {
        CRDebugger.Initialize(CreateDisabledOptions());

        var thrown = Record.Exception(() =>
        {
            // 表示制御
            CRDebugger.Show();
            CRDebugger.Show(CRTab.Console);
            CRDebugger.Hide();
            CRDebugger.Toggle();
            CRDebugger.ShowBugReporter();

            // ロギング
            CRDebugger.Log("info");
            CRDebugger.Log("debug", CRLogLevel.Debug);
            CRDebugger.LogWarning("warn");
            CRDebugger.LogError("error");
            CRDebugger.LogError("error", new InvalidOperationException("test"));
            CRDebugger.LogMarkup("<b>bold</b>");
            CRDebugger.LogRich(CRLogLevel.Info, b => b.Text("x"));

            // Options / SystemInfo
            CRDebugger.AddOptionContainer(new object());
            CRDebugger.RemoveOptionContainer(new object());
            CRDebugger.AddSystemInfo("cat", "key", "value");

            // テーマ・タブ・ショートカット
            CRDebugger.SetTheme(CRTheme.Light);
            CRDebugger.SetTabEnabled(CRTab.Options, false);
            CRDebugger.RegisterShortcut(new KeyCombination(CRKey.F9), () => { });
            CRDebugger.UnregisterShortcut(new KeyCombination(CRKey.F9));

            // プロファイラ
            CRDebugger.RecordFrame();
            CRDebugger.RecordNetworkIO(1, 1);
            CRDebugger.RecordStorageIO(1, 1);
            using (CRDebugger.Profile("op")) { }
        });

        Assert.Null(thrown);
    }

    /// <summary>
    /// 無効化時の値返し API が既定値を返すこと（例外にならないこと）。
    /// </summary>
    [Fact]
    public void Disabled_ValueReturningApis_ReturnDefaults()
    {
        CRDebugger.Initialize(CreateDisabledOptions());

        Assert.False(CRDebugger.IsVisible);
        Assert.Equal(0, CRDebugger.GetCpuUsage());
        Assert.Empty(CRDebugger.GetCpuHistory());
        Assert.Empty(CRDebugger.GetCpuHotspots());
        Assert.Empty(CRDebugger.GetMemoryHotspots());
        Assert.True(CRDebugger.IsTabEnabled(CRTab.Console));
        Assert.False(CRDebugger.HandleKeyDown(CRKey.F1));
        Assert.NotNull(CRDebugger.GetOperationTracker());
        Assert.NotNull(CRDebugger.CreateLoggerProvider());
        Assert.NotNull(CRDebugger.CreateLogger("cat"));
    }

    /// <summary>
    /// 無効化時でも Measure は対象処理そのものを必ず実行すること（計測だけを省く）。
    /// ここを no-op にするとホストのロジックが丸ごと実行されなくなる。
    /// </summary>
    [Fact]
    public void Disabled_Measure_StillExecutesAction()
    {
        CRDebugger.Initialize(CreateDisabledOptions());

        var ran = false;
        CRDebugger.Measure("op", () => { ran = true; });
        Assert.True(ran);

        var result = CRDebugger.Measure("op", () => 42);
        Assert.Equal(42, result);
    }

    /// <summary>
    /// 無効化状態も「初期化済み」として扱い、二重 Initialize を弾くこと。
    /// </summary>
    [Fact]
    public void Disabled_IsTreatedAsInitialized()
    {
        CRDebugger.Initialize(CreateDisabledOptions());

        Assert.True(CRDebugger.IsInitialized);
        Assert.Throws<CRDebuggerAlreadyInitializedException>(() => CRDebugger.Initialize(CreateDisabledOptions()));
    }

    /// <summary>
    /// Shutdown 後は無効化フラグも解除され、通常の初期化を受け付けること。
    /// </summary>
    [Fact]
    public void Disabled_AfterShutdown_CanInitializeAgain()
    {
        CRDebugger.Initialize(CreateDisabledOptions());
        CRDebugger.Shutdown();

        Assert.False(CRDebugger.IsInitialized);

        // 未初期化状態に戻っているので、未初期化例外が出ることを確認する
        Assert.Throws<CRDebuggerNotInitializedException>(() => CRDebugger.Log("x"));
    }
}

/// <summary>
/// <see cref="ConsoleViewModel"/> の表示リスト管理のテスト。
/// </summary>
public sealed class ConsoleViewModelTrimTests
{
    /// <summary>Invoke を同期実行する UI スレッドスタブを作る</summary>
    private static IUiThread CreateSyncUiThread()
    {
        var mock = new Mock<IUiThread>();
        mock.Setup(u => u.Invoke(It.IsAny<Action>())).Callback<Action>(a => a());
        mock.Setup(u => u.IsOnUiThread).Returns(true);
        return mock.Object;
    }

    /// <summary>
    /// LogStore の容量を超えてログを流しても、DisplayEntries が容量を超えて増え続けないこと。
    /// ここが壊れると MaxLogEntries の設定が表示側に効かず、
    /// フィルタ操作をしない長時間セッションでメモリを食い続ける。
    /// </summary>
    [Fact]
    public void DisplayEntries_ExceedingStoreCapacity_IsTrimmedToMaxEntries()
    {
        const int capacity = 20;
        var store = new LogStore(capacity, collapseDuplicates: false);
        using var vm = new ConsoleViewModel(store, CreateSyncUiThread());

        for (var i = 0; i < capacity * 10; i++)
            store.Append(CRLogLevel.Info, "ch", $"msg-{i}");

        // 16ms バッチタイマーによる反映を polling-with-deadline で待つ（CI ランナー向けに最大 5 秒）
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (vm.DisplayEntries.Count == 0 && DateTime.UtcNow < deadline)
            Thread.Sleep(20);

        // 反映が落ち着くまで少し待ってから上限を検証する
        Thread.Sleep(200);

        Assert.NotEmpty(vm.DisplayEntries);
        Assert.True(vm.DisplayEntries.Count <= capacity,
            $"DisplayEntries が上限 {capacity} を超えている（実際: {vm.DisplayEntries.Count}）");
    }

    /// <summary>
    /// UI スレッド側の処理が例外を投げても、タイマーコールバックがそれを外へ漏らさないこと。
    /// 漏らすとスレッドプールスレッドの未処理例外としてホストプロセスが即死する。
    /// </summary>
    [Fact]
    public void FlushPending_UiThreadThrows_DoesNotCrashTimerThread()
    {
        var store = new LogStore(100);
        var throwingUiThread = new Mock<IUiThread>();
        throwingUiThread.Setup(u => u.IsOnUiThread).Returns(true);
        throwingUiThread.Setup(u => u.Invoke(It.IsAny<Action>()))
            .Throws(new InvalidOperationException("UI ディスパッチャがシャットダウン済み"));

        using var vm = new ConsoleViewModel(store, throwingUiThread.Object);

        store.Append(CRLogLevel.Error, "ch", "boom");

        // タイマーが数回発火する時間を確保する。ここで例外が漏れていればテストホストごと落ちる。
        Thread.Sleep(300);

        Assert.Empty(vm.DisplayEntries);
    }
}
