using CRDebugger.Core.Abstractions;
using CRDebugger.Core.BugReporter;
using CRDebugger.Core.Logging;
using CRDebugger.Core.SystemInfo;
using CRDebugger.Core.Theming;
using Moq;

namespace CRDebugger.Core.Tests;

/// <summary>
/// BugReportEngine / SystemInfoCollector / ThemeManager の嫌がらせテスト
/// </summary>
public sealed class BugReporterAndSystemInfoAdversarialTests
{
    // ───────────────────────────────────
    // BugReportEngine
    // ───────────────────────────────────

    // ── 🗡️ 境界値 ──

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 空文字列のメッセージ/メールでレポートが作成できること
    /// </summary>
    [Fact]
    public async Task BugReport_EmptyStrings_Accepted()
    {
        var store = new LogStore();
        var sysInfo = new SystemInfoCollector();
        var mockSender = new Mock<IBugReportSender>();
        mockSender.Setup(s => s.SendAsync(It.IsAny<BugReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var engine = new BugReportEngine(store, sysInfo, mockSender.Object);
        var report = await engine.CreateAndSendAsync("", "", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(report);
        Assert.Equal("", report.UserMessage);
        Assert.Equal("", report.UserEmail);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 巨大メッセージでレポートが作成できること
    /// </summary>
    [Fact]
    public async Task BugReport_HugeMessage_Accepted()
    {
        var store = new LogStore();
        var sysInfo = new SystemInfoCollector();
        var mockSender = new Mock<IBugReportSender>();
        mockSender.Setup(s => s.SendAsync(It.IsAny<BugReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var engine = new BugReportEngine(store, sysInfo, mockSender.Object);
        var hugeMsg = new string('X', 1_000_000);
        var report = await engine.CreateAndSendAsync(hugeMsg, "test@example.com", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1_000_000, report.UserMessage.Length);
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// スクリーンショットなしでレポートが作成できること
    /// </summary>
    [Fact]
    public async Task BugReport_NoScreenshot_Works()
    {
        var store = new LogStore();
        var sysInfo = new SystemInfoCollector();
        var mockSender = new Mock<IBugReportSender>();
        mockSender.Setup(s => s.SendAsync(It.IsAny<BugReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var engine = new BugReportEngine(store, sysInfo, mockSender.Object);
        var report = await engine.CreateAndSendAsync("bug!", "a@b.c", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(report.Screenshot);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// スクリーンショットがnullを返す場合
    /// </summary>
    [Fact]
    public async Task BugReport_ScreenshotReturnsNull_Works()
    {
        var store = new LogStore();
        var sysInfo = new SystemInfoCollector();
        var mockSender = new Mock<IBugReportSender>();
        mockSender.Setup(s => s.SendAsync(It.IsAny<BugReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var engine = new BugReportEngine(store, sysInfo, mockSender.Object);
        var report = await engine.CreateAndSendAsync("bug!", "a@b.c",
            screenshotCapture: () => Task.FromResult<byte[]?>(null),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(report.Screenshot);
    }

    // ── ⚡ 並行性 ──

    /// <summary>
    /// @adversarial @category concurrency @severity high
    /// 複数の同時送信がデッドロックしないこと
    /// </summary>
    [Fact]
    public async Task BugReport_ConcurrentSends_NoDeadlock()
    {
        var store = new LogStore();
        var sysInfo = new SystemInfoCollector();
        var mockSender = new Mock<IBugReportSender>();
        mockSender.Setup(s => s.SendAsync(It.IsAny<BugReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var engine = new BugReportEngine(store, sysInfo, mockSender.Object);
        var tasks = Enumerable.Range(0, 10).Select(i =>
            engine.CreateAndSendAsync($"bug-{i}", $"user{i}@test.com")
        ).ToArray();

        var reports = await Task.WhenAll(tasks);
        Assert.Equal(10, reports.Length);
        Assert.All(reports, r => Assert.NotNull(r));
    }

    // ── 💀 リソース枯渇 ──

    /// <summary>
    /// @adversarial @category resource @severity critical
    /// スクリーンショットキャプチャが永遠に完了しない場合の動作
    /// （タイムアウトなしの設計バグ確認）
    /// </summary>
    [Fact]
    public async Task BugReport_ScreenshotHangs_TaskNeverCompletes()
    {
        var store = new LogStore();
        var sysInfo = new SystemInfoCollector();
        var mockSender = new Mock<IBugReportSender>();
        mockSender.Setup(s => s.SendAsync(It.IsAny<BugReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var engine = new BugReportEngine(store, sysInfo, mockSender.Object);

        // 永遠に完了しないスクリーンショット
        var neverComplete = new TaskCompletionSource<byte[]?>();
        var reportTask = engine.CreateAndSendAsync("bug!", "a@b.c",
            screenshotCapture: () => neverComplete.Task,
            cancellationToken: TestContext.Current.CancellationToken);

        // 2秒以内に完了しないことを確認（タイムアウトなしのバグ）
        var completed = await Task.WhenAny(reportTask, Task.Delay(2000, TestContext.Current.CancellationToken));
        Assert.NotEqual(reportTask, completed); // レポートタスクは完了していない

        // クリーンアップ
        neverComplete.SetResult(null);
        await reportTask;
    }

    /// <summary>
    /// @adversarial @category resource @severity high
    /// スクリーンショットキャプチャが例外を投げた場合
    /// </summary>
    [Fact]
    public async Task BugReport_ScreenshotThrows_ExceptionPropagated()
    {
        var store = new LogStore();
        var sysInfo = new SystemInfoCollector();
        var mockSender = new Mock<IBugReportSender>();

        var engine = new BugReportEngine(store, sysInfo, mockSender.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.CreateAndSendAsync("bug!", "a@b.c",
                screenshotCapture: () => throw new InvalidOperationException("キャプチャ失敗"),
                cancellationToken: TestContext.Current.CancellationToken));
    }

    // ── 🔀 状態遷移 ──

    /// <summary>
    /// @adversarial @category state @severity high
    /// SetSenderにnullを渡すとArgumentNullExceptionがスローされること
    /// </summary>
    [Fact]
    public void BugReportEngine_SetSenderNull_Throws()
    {
        var store = new LogStore();
        var sysInfo = new SystemInfoCollector();
        var engine = new BugReportEngine(store, sysInfo);

        Assert.Throws<ArgumentNullException>(() => engine.SetSender(null!));
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// Senderの差し替えが反映されること
    /// </summary>
    [Fact]
    public async Task BugReportEngine_SetSender_UsesNewSender()
    {
        var store = new LogStore();
        var sysInfo = new SystemInfoCollector();
        var engine = new BugReportEngine(store, sysInfo);

        var mockSender = new Mock<IBugReportSender>();
        mockSender.Setup(s => s.SendAsync(It.IsAny<BugReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        engine.SetSender(mockSender.Object);
        await engine.CreateAndSendAsync("test", "a@b.c", cancellationToken: TestContext.Current.CancellationToken);

        mockSender.Verify(s => s.SendAsync(It.IsAny<BugReport>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// CancellationTokenでキャンセルできること
    /// </summary>
    [Fact]
    public async Task BugReport_CancelledToken_Throws()
    {
        var store = new LogStore();
        var sysInfo = new SystemInfoCollector();
        var mockSender = new Mock<IBugReportSender>();
        mockSender.Setup(s => s.SendAsync(It.IsAny<BugReport>(), It.IsAny<CancellationToken>()))
            .Returns<BugReport, CancellationToken>((_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(true);
            });

        var engine = new BugReportEngine(store, sysInfo, mockSender.Object);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            engine.CreateAndSendAsync("bug", "a@b.c", cancellationToken: cts.Token));
    }

    // ───────────────────────────────────
    // SystemInfoCollector
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// CollectAllが必須フィールドを含むこと
    /// </summary>
    [Fact]
    public void SystemInfo_CollectAll_HasRequiredCategories()
    {
        var collector = new SystemInfoCollector();
        var entries = collector.CollectAll();

        var categories = entries.Select(e => e.Category).Distinct().ToList();
        Assert.Contains("System", categories);
        Assert.Contains("Runtime", categories);
        Assert.Contains("Process", categories);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// カスタム情報を大量に追加してもクラッシュしないこと
    /// </summary>
    [Fact]
    public void SystemInfo_ManyCustomEntries_Accepted()
    {
        var collector = new SystemInfoCollector();
        for (int i = 0; i < 10_000; i++)
        {
            collector.AddCustomInfo("Custom", $"Key{i}", $"Value{i}");
        }

        var entries = collector.CollectAll();
        Assert.True(entries.Count >= 10_000);
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// 空文字列のカスタム情報が受け入れられること
    /// </summary>
    [Fact]
    public void SystemInfo_EmptyCustomInfo_Accepted()
    {
        var collector = new SystemInfoCollector();
        collector.AddCustomInfo("", "", "");

        var entries = collector.CollectAll();
        Assert.Contains(entries, e => e.Category == "" && e.Key == "" && e.Value == "");
    }

    /// <summary>
    /// @adversarial @category chaos @severity medium
    /// 2回CollectAllを呼んでも一貫した結果を返すこと
    /// </summary>
    [Fact]
    public void SystemInfo_CollectAllTwice_ConsistentCategories()
    {
        var collector = new SystemInfoCollector();
        var first = collector.CollectAll();
        var second = collector.CollectAll();

        // カテゴリが一致すること
        var cats1 = first.Select(e => e.Category).Distinct().OrderBy(c => c).ToList();
        var cats2 = second.Select(e => e.Category).Distinct().OrderBy(c => c).ToList();
        Assert.Equal(cats1, cats2);
    }

    // ───────────────────────────────────
    // ThemeManager
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// 不正なCRTheme列挙値がデフォルトケースに該当すること
    /// </summary>
    [Fact]
    public void ThemeManager_InvalidTheme_DefaultsToSystem()
    {
        var manager = new ThemeManager((CRTheme)999);
        var colors = manager.CurrentColors;
        // デフォルトケース（_）→systemIsDarkがfalse→Light
        Assert.Equal(ThemeColors.Light, colors);
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// SetThemeで同じテーマを設定してもイベントが発火すること
    /// </summary>
    [Fact]
    public void ThemeManager_SetSameTheme_FiresEvent()
    {
        var manager = new ThemeManager(CRTheme.Dark);
        int fireCount = 0;
        manager.ThemeChanged += (_, _) => fireCount++;

        manager.SetTheme(CRTheme.Dark); // 同じテーマ
        Assert.Equal(1, fireCount);
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// SystemテーマでNotifySystemThemeChangedが呼ばれたらイベントが発火すること
    /// </summary>
    [Fact]
    public void ThemeManager_SystemTheme_NotifyChanges()
    {
        var manager = new ThemeManager(CRTheme.System);
        ThemeColors? lastColors = null;
        manager.ThemeChanged += (_, c) => lastColors = c;

        manager.NotifySystemThemeChanged(true); // ダークモードに変更
        Assert.NotNull(lastColors);
        Assert.Equal(ThemeColors.Dark, lastColors);

        manager.NotifySystemThemeChanged(false); // ライトモードに変更
        Assert.Equal(ThemeColors.Light, lastColors);
    }

    /// <summary>
    /// @adversarial @category state @severity medium
    /// Light/DarkテーマではNotifySystemThemeChangedが無視されること
    /// </summary>
    [Fact]
    public void ThemeManager_NonSystemTheme_NotifyIgnored()
    {
        var manager = new ThemeManager(CRTheme.Light);
        int fireCount = 0;
        manager.ThemeChanged += (_, _) => fireCount++;

        manager.NotifySystemThemeChanged(true); // Lightテーマなので無視
        Assert.Equal(0, fireCount);
    }

    /// <summary>
    /// @adversarial @category concurrency @severity high
    /// SetThemeとNotifySystemThemeChangedの同時呼び出しがクラッシュしないこと
    /// </summary>
    [Fact]
    public async Task ThemeManager_ConcurrentThemeChanges_NoCrash()
    {
        var manager = new ThemeManager(CRTheme.System);
        manager.ThemeChanged += (_, _) => { /* nop */ };

        var tasks = new[]
        {
            Task.Run(() => { for (int i = 0; i < 1000; i++) manager.SetTheme(CRTheme.Dark); }, TestContext.Current.CancellationToken),
            Task.Run(() => { for (int i = 0; i < 1000; i++) manager.SetTheme(CRTheme.Light); }, TestContext.Current.CancellationToken),
            Task.Run(() => { for (int i = 0; i < 1000; i++) manager.NotifySystemThemeChanged(i % 2 == 0); }, TestContext.Current.CancellationToken),
            Task.Run(() => { for (int i = 0; i < 1000; i++) _ = manager.CurrentColors; }, TestContext.Current.CancellationToken)
        };

        await Task.WhenAll(tasks);
    }

    // ───────────────────────────────────
    // DefaultConsoleBugReportSender
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// デフォルトSenderが常にtrueを返すこと
    /// </summary>
    [Fact]
    public async Task DefaultSender_AlwaysReturnsTrue()
    {
        var sender = new DefaultConsoleBugReportSender();
        var report = new BugReport(
            Guid.NewGuid(), DateTimeOffset.Now, "msg", "email",
            Array.Empty<SystemInfoEntry>(), Array.Empty<LogEntry>(), null);

        var result = await sender.SendAsync(report, TestContext.Current.CancellationToken);
        Assert.True(result);
    }
}
