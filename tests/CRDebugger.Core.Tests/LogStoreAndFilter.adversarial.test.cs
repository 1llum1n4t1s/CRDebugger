using CRDebugger.Core.Logging;

namespace CRDebugger.Core.Tests;

/// <summary>
/// LogStore / LogFilter の嫌がらせテスト
/// </summary>
public sealed class LogStoreAndFilterAdversarialTests
{
    // ───────────────────────────────────
    // 🗡️ 境界値・極端入力 (Boundary Assault)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category boundary @severity critical
    /// MaxLogEntries=0でLogStoreを作成するとエラーになること
    /// </summary>
    [Fact]
    public void LogStore_ZeroCapacity_Throws()
    {
        Assert.ThrowsAny<Exception>(() => new LogStore(0));
    }

    /// <summary>
    /// @adversarial @category boundary @severity critical
    /// 負の容量でLogStoreを作成するとエラーになること
    /// </summary>
    [Fact]
    public void LogStore_NegativeCapacity_Throws()
    {
        Assert.ThrowsAny<Exception>(() => new LogStore(-1));
        Assert.ThrowsAny<Exception>(() => new LogStore(int.MinValue));
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 容量1のLogStoreが正常動作すること
    /// </summary>
    [Fact]
    public void LogStore_Capacity1_Works()
    {
        var store = new LogStore(1);
        store.Append(CRLogLevel.Info, "ch", "msg1");
        store.Append(CRLogLevel.Error, "ch", "msg2");

        Assert.Equal(1, store.Count);
        var all = store.GetAll();
        Assert.Single(all);
        Assert.Equal("msg2", all[0].Message);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 空文字列のメッセージ/チャネルが受け入れられること
    /// </summary>
    [Fact]
    public void LogStore_EmptyStrings_Accepted()
    {
        var store = new LogStore();
        store.Append(CRLogLevel.Info, "", "");
        store.Append(CRLogLevel.Debug, "", "");

        Assert.Equal(2, store.Count);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 非常に長いメッセージが格納できること
    /// </summary>
    [Fact]
    public void LogStore_HugeMessage_Accepted()
    {
        var store = new LogStore(5);
        var hugeMsg = new string('A', 1_000_000); // 1MB
        var hugeStack = new string('B', 500_000);

        store.Append(CRLogLevel.Error, "test", hugeMsg, hugeStack);
        var all = store.GetAll();
        Assert.Equal(1_000_000, all[0].Message.Length);
        Assert.Equal(500_000, all[0].StackTrace!.Length);
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// Unicode特殊文字を含むメッセージが正しく格納されること
    /// </summary>
    [Fact]
    public void LogStore_UnicodeMessages_Preserved()
    {
        var store = new LogStore();

        var messages = new[]
        {
            "日本語テスト 🎉",
            "ゼロ幅スペース\u200B入り",
            "RTL\u202Eテスト",
            "サロゲートペア𩸽",
            "絵文字結合👨‍👩‍👧‍👦",
            "\x00ヌルバイト\x00"
        };

        foreach (var msg in messages)
            store.Append(CRLogLevel.Info, "unicode", msg);

        var all = store.GetAll();
        Assert.Equal(messages.Length, all.Count);
        for (int i = 0; i < messages.Length; i++)
            Assert.Equal(messages[i], all[i].Message);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 不正なCRLogLevel列挙値が渡された場合にクラッシュしないこと
    /// </summary>
    [Fact]
    public void LogStore_InvalidLogLevel_DoesNotCrash()
    {
        var store = new LogStore();
        store.Append((CRLogLevel)999, "ch", "msg");
        Assert.Equal(1, store.Count);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 不正なCRLogLevel値でGetCountsが正しく動作すること
    /// </summary>
    [Fact]
    public void LogStore_GetCounts_InvalidLevel_DoesNotCount()
    {
        var store = new LogStore();
        store.Append((CRLogLevel)999, "ch", "msg");
        store.Append(CRLogLevel.Info, "ch", "msg");

        var (d, i, w, e) = store.GetCounts();
        Assert.Equal(0, d);
        Assert.Equal(1, i);
        Assert.Equal(0, w);
        Assert.Equal(0, e);
        // 不正なレベルのエントリはどのカウントにも含まれない
    }

    // ── LogFilter 境界値 ──

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 全レベル非表示のフィルタが全件除外すること
    /// </summary>
    [Fact]
    public void LogFilter_AllLevelsDisabled_MatchesNothing()
    {
        var filter = new LogFilter(false, false, false, false);
        var entry = new LogEntry(1, DateTimeOffset.Now, CRLogLevel.Info, "ch", "msg", null);

        Assert.False(filter.Matches(entry));
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// 検索テキストが空文字列の場合はフィルタなしと同じこと
    /// </summary>
    [Fact]
    public void LogFilter_EmptySearchText_MatchesAll()
    {
        var filter = new LogFilter(SearchText: "");
        var entry = new LogEntry(1, DateTimeOffset.Now, CRLogLevel.Info, "ch", "msg", null);

        Assert.True(filter.Matches(entry));
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 検索テキストが大文字小文字を区別しないこと
    /// </summary>
    [Fact]
    public void LogFilter_SearchText_CaseInsensitive()
    {
        var filter = new LogFilter(SearchText: "ERROR");
        var entry = new LogEntry(1, DateTimeOffset.Now, CRLogLevel.Info, "ch", "An error occurred", null);

        Assert.True(filter.Matches(entry));
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// 不正なCRLogLevel値がデフォルトケースに該当すること
    /// </summary>
    [Fact]
    public void LogFilter_InvalidLogLevel_DefaultsToTrue()
    {
        var filter = new LogFilter(ShowDebug: false, ShowInfo: false, ShowWarning: false, ShowError: false);
        var entry = new LogEntry(1, DateTimeOffset.Now, (CRLogLevel)42, "ch", "msg", null);

        // switchのデフォルトケース（_ => true）に該当
        Assert.True(filter.Matches(entry));
    }

    // ───────────────────────────────────
    // ⚡ 並行性・レースコンディション (Concurrency Chaos)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category concurrency @severity critical
    /// 複数スレッドから同時にAppendしてもクラッシュしないこと
    /// </summary>
    [Fact]
    public void LogStore_ConcurrentAppend_NoCorruption()
    {
        var store = new LogStore(1000);
        const int threadCount = 8;
        const int entriesPerThread = 1000;

        var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < entriesPerThread; i++)
            {
                store.Append(CRLogLevel.Info, $"thread-{t}", $"msg-{i}");
            }
        })).ToArray();

        Task.WaitAll(tasks);

        // 循環バッファなので最大1000件、ただし全件追加された（8000件）
        Assert.Equal(1000, store.Count);
    }

    /// <summary>
    /// @adversarial @category concurrency @severity critical
    /// AppendとGetAllの同時実行がデッドロックしないこと
    /// </summary>
    [Fact]
    public void LogStore_ConcurrentAppendAndGetAll_NoDeadlock()
    {
        var store = new LogStore(100);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var writer = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
                store.Append(CRLogLevel.Info, "w", "msg");
        });

        var reader = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
                _ = store.GetAll();
        });

        var clearer = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                Thread.Sleep(100);
                store.Clear();
            }
        });

        // 3秒以内にタスクが完了すること（デッドロックしない）
        Assert.True(Task.WaitAll(new[] { writer, reader, clearer }, TimeSpan.FromSeconds(5)));
    }

    /// <summary>
    /// @adversarial @category concurrency @severity high
    /// ConcurrentにGetFilteredを呼んでもクラッシュしないこと
    /// </summary>
    [Fact]
    public void LogStore_ConcurrentGetFiltered_NoCrash()
    {
        var store = new LogStore(500);
        for (int i = 0; i < 500; i++)
            store.Append(i % 2 == 0 ? CRLogLevel.Info : CRLogLevel.Error, "ch", $"msg-{i}");

        var filter = new LogFilter(ShowInfo: false, SearchText: "msg");
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            var result = store.GetFiltered(filter);
            Assert.All(result, entry => Assert.Equal(CRLogLevel.Error, entry.Level));
        })).ToArray();

        Task.WaitAll(tasks);
    }

    /// <summary>
    /// @adversarial @category concurrency @severity high
    /// EntryAddedイベントハンドラが例外を投げてもAppendが成功すること
    /// （修正済み: try-catchでイベントハンドラ例外を握りつぶす）
    /// </summary>
    [Fact]
    public void LogStore_EventHandlerThrows_AppendStillWorks()
    {
        var store = new LogStore();
        store.EntryAdded += (_, _) => throw new InvalidOperationException("ハンドラが壊れた！");

        // イベントハンドラの例外はtry-catchで握りつぶされ、Appendは正常に完了する
        store.Append(CRLogLevel.Info, "ch", "msg");
        Assert.Equal(1, store.Count);
    }

    // ───────────────────────────────────
    // 💀 リソース枯渇 (Resource Exhaustion)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category resource @severity high
    /// LogStoreのID（_nextId）がint.MaxValueを超えた場合の動作
    /// </summary>
    [Fact]
    public void LogStore_IdOverflow_DoesNotCrash()
    {
        var store = new LogStore(5);

        // _nextIdフィールドをリフレクションでint.MaxValue - 2に設定
        var field = typeof(LogStore).GetField("_nextId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        field!.SetValue(store, int.MaxValue - 2);

        // 3回追加するとオーバーフロー
        store.Append(CRLogLevel.Info, "ch", "msg1"); // int.MaxValue - 1
        store.Append(CRLogLevel.Info, "ch", "msg2"); // int.MaxValue
        store.Append(CRLogLevel.Info, "ch", "msg3"); // int.MinValue（オーバーフロー）

        var all = store.GetAll();
        Assert.Equal(3, all.Count);
        // IDがオーバーフローしても動作が壊れないこと
        Assert.True(all[2].Id < 0, "IDがオーバーフローして負の値になるはず");
    }

    /// <summary>
    /// @adversarial @category resource @severity high
    /// 大量のGetFiltered呼び出しがGCを圧迫すること（アロケーション爆弾）
    /// </summary>
    [Fact]
    public void LogStore_RepeatedGetFiltered_DoesNotOOM()
    {
        var store = new LogStore(1000);
        for (int i = 0; i < 1000; i++)
            store.Append(CRLogLevel.Info, "ch", $"msg-{i}");

        var filter = new LogFilter(SearchText: "msg");

        // 10000回フィルタ実行してもOOMにならない
        for (int i = 0; i < 10_000; i++)
        {
            var result = store.GetFiltered(filter);
            Assert.Equal(1000, result.Count);
        }
    }

    // ───────────────────────────────────
    // 🔀 状態遷移の矛盾 (State Machine Abuse)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category state @severity high
    /// Clear中にAppendが呼ばれてもデッドロックしないこと
    /// </summary>
    [Fact]
    public void LogStore_ClearDuringAppend_NoDeadlock()
    {
        var store = new LogStore(100);
        for (int i = 0; i < 50; i++) store.Append(CRLogLevel.Info, "ch", "msg");

        // Clear と Append を交互に呼ぶ
        for (int i = 0; i < 1000; i++)
        {
            if (i % 3 == 0) store.Clear();
            else store.Append(CRLogLevel.Debug, "ch", $"msg-{i}");
        }

        // クラッシュせずに完了すること
        Assert.True(store.Count <= 100);
    }

    /// <summary>
    /// @adversarial @category state @severity medium
    /// GetCounts の結果がCount合計と一致すること
    /// </summary>
    [Fact]
    public void LogStore_GetCounts_SumMatchesCount()
    {
        var store = new LogStore(100);
        store.Append(CRLogLevel.Debug, "ch", "d1");
        store.Append(CRLogLevel.Debug, "ch", "d2");
        store.Append(CRLogLevel.Info, "ch", "i1");
        store.Append(CRLogLevel.Warning, "ch", "w1");
        store.Append(CRLogLevel.Error, "ch", "e1");
        store.Append(CRLogLevel.Error, "ch", "e2");
        store.Append(CRLogLevel.Error, "ch", "e3");

        var (d, i, w, e) = store.GetCounts();
        Assert.Equal(2, d);
        Assert.Equal(1, i);
        Assert.Equal(1, w);
        Assert.Equal(3, e);
        Assert.Equal(store.Count, d + i + w + e);
    }

    // ───────────────────────────────────
    // 🎭 型パンチ (Type Punching)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category type @severity high
    /// nullのstackTraceが正しく処理されること
    /// </summary>
    [Fact]
    public void LogStore_NullStackTrace_Accepted()
    {
        var store = new LogStore();
        store.Append(CRLogLevel.Error, "ch", "error msg", null);

        var all = store.GetAll();
        Assert.Single(all);
        Assert.Null(all[0].StackTrace);
    }

    // ───────────────────────────────────
    // 🌪️ 環境異常 (Environmental Chaos)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category chaos @severity medium
    /// Timestampが異なるタイムゾーンでも正しく格納されること
    /// </summary>
    [Fact]
    public void LogStore_Append_TimestampPreserved()
    {
        var store = new LogStore();
        var before = DateTimeOffset.Now;
        store.Append(CRLogLevel.Info, "ch", "msg");
        var after = DateTimeOffset.Now;

        var entry = store.GetAll()[0];
        Assert.InRange(entry.Timestamp, before, after);
    }
}
