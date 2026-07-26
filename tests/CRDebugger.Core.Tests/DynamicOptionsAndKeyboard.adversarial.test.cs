using CRDebugger.Core.Input;
using CRDebugger.Core.Options;

namespace CRDebugger.Core.Tests;

/// <summary>
/// DynamicOptionContainer と KeyboardShortcutManager の嫌がらせテスト
/// </summary>
public sealed class DynamicOptionsAndKeyboardAdversarialTests
{
    // ───────────────────────────────────
    // 🗡️ 境界値・極端入力 (Boundary Assault)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// @description 空文字名でオプションを登録してもクラッシュしないこと
    /// @expected 登録成功、Idは "Dynamic.Cat." 形式
    /// </summary>
    [Fact]
    public void AddBool_EmptyName_DoesNotCrash()
    {
        var container = new DynamicOptionContainer("Cat");
        var val = false;
        container.AddBool("", () => val, v => val = v);

        Assert.Single(container.Options);
        Assert.Equal("Dynamic.Cat.", container.Options[0].Id);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// @description 極端に長い名前のオプションが登録できること
    /// @expected クラッシュせず、Idに長い名前が含まれる
    /// </summary>
    [Fact]
    public void AddString_ExtremeLongName_DoesNotCrash()
    {
        var container = new DynamicOptionContainer();
        var longName = new string('X', 10_000);
        string? val = null;
        container.AddString(longName, () => val, v => val = v);

        Assert.Single(container.Options);
        Assert.Contains(longName, container.Options[0].Id);
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// @description 全修飾キー組み合わせでKeyCombinationが生成できること
    /// @expected 全7パターンが一意のHashCodeを持つ
    /// </summary>
    [Fact]
    public void KeyCombination_AllModifierCombinations_UniqueHashCodes()
    {
        var modifiers = new[]
        {
            CRModifierKeys.None, CRModifierKeys.Ctrl, CRModifierKeys.Shift,
            CRModifierKeys.Alt, CRModifierKeys.CtrlShift, CRModifierKeys.CtrlAlt,
            CRModifierKeys.Shift | CRModifierKeys.Alt,
        };

        var combos = modifiers.Select(m => new KeyCombination(CRKey.A, m)).ToList();
        var uniqueHashCodes = combos.Select(c => c.GetHashCode()).Distinct().Count();
        Assert.Equal(combos.Count, uniqueHashCodes);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// @description CRKey.None でショートカットを登録・発火できること
    /// @expected HandleKeyDown(CRKey.None, None) で発火する
    /// </summary>
    [Fact]
    public void Register_KeyNone_CanStillFire()
    {
        var mgr = new KeyboardShortcutManager();
        var fired = false;
        mgr.Register(new KeyCombination(CRKey.None, CRModifierKeys.None), () => fired = true);

        var handled = mgr.HandleKeyDown(CRKey.None, CRModifierKeys.None);

        Assert.True(handled);
        Assert.True(fired);
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// @description AddAction で空ラベルを登録してもクラッシュしないこと
    /// @expected アクション登録成功
    /// </summary>
    [Fact]
    public void AddAction_EmptyLabel_DoesNotCrash()
    {
        var container = new DynamicOptionContainer("Test");
        container.AddAction("", () => { });

        Assert.Single(container.Actions);
        Assert.Equal("", container.Actions[0].Label);
    }

    // ───────────────────────────────────
    // ⚡ 並行性 (Concurrency Havoc)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category concurrency @severity high
    /// @description 複数スレッドから同時にショートカットを登録しても例外が飛ばないこと
    /// @expected 全登録が完了（辞書破損で例外が出ないこと）
    /// </summary>
    [Fact]
    public void Register_ConcurrentRegistration_NoException()
    {
        var mgr = new KeyboardShortcutManager();
        var keys = Enum.GetValues<CRKey>();

        var exceptions = new List<Exception>();
        var threads = new Thread[10];
        for (var i = 0; i < threads.Length; i++)
        {
            var idx = i;
            threads[i] = new Thread(() =>
            {
                try
                {
                    for (var k = 0; k < 100; k++)
                    {
                        var key = keys[(idx * 100 + k) % keys.Length];
                        mgr.Register(new KeyCombination(key, CRModifierKeys.None), () => { });
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) exceptions.Add(ex);
                }
            });
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        // KeyboardShortcutManager は ConcurrentDictionary を使うため、
        // 並行登録で例外が出ることは無い。収集した例外を実際に検査する。
        Assert.Empty(exceptions);

        // 登録された組み合わせが実際に引けることを確認する（辞書が壊れていないことの実証）。
        // 各スレッドは keys 配列を順に舐めるため、None を除く全キーが登録済みになる。
        var registered = mgr.GetAll();
        foreach (var key in keys)
        {
            if (key == CRKey.None) continue;
            Assert.Contains(registered.Keys, c => c.Key == key && c.Modifiers == CRModifierKeys.None);
        }
    }

    /// <summary>
    /// @adversarial @category concurrency @severity high
    /// @description 複数スレッドから同時にHandleKeyDownしても致命的エラーにならないこと
    /// @expected プロセス生存
    /// </summary>
    [Fact]
    public async Task HandleKeyDown_ConcurrentCalls_NoFatalError()
    {
        var mgr = new KeyboardShortcutManager();
        var counter = 0;
        mgr.Register(new KeyCombination(CRKey.F5, CRModifierKeys.None), () => Interlocked.Increment(ref counter));

        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 100; i++)
                mgr.HandleKeyDown(CRKey.F5, CRModifierKeys.None);
        })).ToArray();

        await Task.WhenAll(tasks);
        Assert.True(counter > 0, "少なくとも1回は発火しているはず");
    }

    // ───────────────────────────────────
    // 💀 リソース枯渇 (Resource Exhaustion)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category resource @severity high
    /// @description 数千個のショートカットを登録しても正常動作すること
    /// @expected 全キー×全修飾子の登録・検索が成功
    /// </summary>
    [Fact]
    public void Register_ThousandsOfShortcuts_AllRetrievable()
    {
        var mgr = new KeyboardShortcutManager();
        var keys = Enum.GetValues<CRKey>();
        var mods = new[] { CRModifierKeys.None, CRModifierKeys.Ctrl, CRModifierKeys.Shift,
                           CRModifierKeys.Alt, CRModifierKeys.CtrlShift, CRModifierKeys.CtrlAlt };
        var registered = 0;

        foreach (var key in keys)
        foreach (var mod in mods)
        {
            mgr.Register(new KeyCombination(key, mod), () => { });
            registered++;
        }

        var all = mgr.GetAll();
        Assert.Equal(registered, all.Count);
    }

    /// <summary>
    /// @adversarial @category resource @severity high
    /// @description DynamicOptionContainerに大量のオプションを登録できること
    /// @expected 5000個のboolオプション登録が完了
    /// </summary>
    [Fact]
    public void DynamicContainer_5000Options_NoOom()
    {
        var container = new DynamicOptionContainer("Stress");
        var val = false;

        for (var i = 0; i < 5_000; i++)
            container.AddBool($"Opt{i}", () => val, v => val = v);

        Assert.Equal(5_000, container.Options.Count);
    }

    /// <summary>
    /// @adversarial @category resource @severity medium
    /// @description 大量アクション登録後も全Execute呼び出し可能であること
    /// @expected 1000アクション全てのExecuteが実行される
    /// </summary>
    [Fact]
    public void DynamicContainer_1000Actions_AllExecutable()
    {
        var container = new DynamicOptionContainer("Stress");
        var callCount = 0;

        for (var i = 0; i < 1_000; i++)
            container.AddAction($"Act{i}", () => Interlocked.Increment(ref callCount));

        foreach (var action in container.Actions)
            action.Execute();

        Assert.Equal(1_000, callCount);
    }

    // ───────────────────────────────────
    // 🔀 状態遷移 (State Transition Attacks)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category state @severity high
    /// @description Enabled=false 時に HandleKeyDown がショートカットを実行しないこと
    /// @expected false を返し、アクション未実行
    /// </summary>
    [Fact]
    public void HandleKeyDown_WhenDisabled_ReturnsFalse()
    {
        var mgr = new KeyboardShortcutManager();
        var fired = false;
        mgr.Register(new KeyCombination(CRKey.F1, CRModifierKeys.None), () => fired = true);
        mgr.Enabled = false;

        var result = mgr.HandleKeyDown(CRKey.F1, CRModifierKeys.None);

        Assert.False(result);
        Assert.False(fired);
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// @description 同じキーを再登録するとアクションが上書きされること
    /// @expected 2回目のアクションのみ実行される
    /// </summary>
    [Fact]
    public void Register_SameKeyCombination_OverwritesAction()
    {
        var mgr = new KeyboardShortcutManager();
        var firstFired = false;
        var secondFired = false;
        var combo = new KeyCombination(CRKey.S, CRModifierKeys.Ctrl);

        mgr.Register(combo, () => firstFired = true);
        mgr.Register(combo, () => secondFired = true);
        mgr.HandleKeyDown(CRKey.S, CRModifierKeys.Ctrl);

        Assert.False(firstFired);
        Assert.True(secondFired);
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// @description Unregister 後に HandleKeyDown しても発火しないこと
    /// @expected false を返す
    /// </summary>
    [Fact]
    public void Unregister_ThenHandleKeyDown_ReturnsFalse()
    {
        var mgr = new KeyboardShortcutManager();
        var combo = new KeyCombination(CRKey.Z, CRModifierKeys.CtrlShift);
        mgr.Register(combo, () => { });
        mgr.Unregister(combo);

        var result = mgr.HandleKeyDown(CRKey.Z, CRModifierKeys.CtrlShift);

        Assert.False(result);
        Assert.Empty(mgr.GetAll());
    }

    /// <summary>
    /// @adversarial @category state @severity medium
    /// @description 未登録のキーを Unregister しても例外が出ないこと
    /// @expected 例外なし
    /// </summary>
    [Fact]
    public void Unregister_NonExistentKey_DoesNotThrow()
    {
        var mgr = new KeyboardShortcutManager();
        var ex = Record.Exception(() =>
            mgr.Unregister(new KeyCombination(CRKey.F12, CRModifierKeys.CtrlAlt)));
        Assert.Null(ex);
    }

    /// <summary>
    /// @adversarial @category state @severity medium
    /// @description Enabled を切り替えながらキー処理しても一貫した結果になること
    /// @expected Enabled=true の時だけ発火
    /// </summary>
    [Fact]
    public void EnabledToggle_DuringHandling_ConsistentBehavior()
    {
        var mgr = new KeyboardShortcutManager();
        var count = 0;
        mgr.Register(new KeyCombination(CRKey.A, CRModifierKeys.None), () => count++);

        mgr.Enabled = true;
        mgr.HandleKeyDown(CRKey.A, CRModifierKeys.None);
        Assert.Equal(1, count);

        mgr.Enabled = false;
        mgr.HandleKeyDown(CRKey.A, CRModifierKeys.None);
        Assert.Equal(1, count); // 増えない

        mgr.Enabled = true;
        mgr.HandleKeyDown(CRKey.A, CRModifierKeys.None);
        Assert.Equal(2, count);
    }

    /// <summary>
    /// @adversarial @category state @severity medium
    /// @description 未登録キーの HandleKeyDown が false を返すこと
    /// @expected false
    /// </summary>
    [Fact]
    public void HandleKeyDown_UnregisteredKey_ReturnsFalse()
    {
        var mgr = new KeyboardShortcutManager();
        var result = mgr.HandleKeyDown(CRKey.Escape, CRModifierKeys.None);
        Assert.False(result);
    }

    // ───────────────────────────────────
    // 🎭 型パンチ (Type Punching)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category type @severity high
    /// @description 同じ名前のオプションを複数登録してもクラッシュしないこと（Accepted仕様）
    /// @expected 両方とも Options に格納される（append動作）
    /// </summary>
    [Fact]
    public void DuplicateName_Registration_AppendsBoth()
    {
        var container = new DynamicOptionContainer("Dup");
        var a = false;
        var b = 0;

        container
            .AddBool("SameName", () => a, v => a = v)
            .AddInt("SameName", () => b, v => b = v);

        Assert.Equal(2, container.Options.Count);
        Assert.Equal("Dynamic.Dup.SameName", container.Options[0].Id);
        Assert.Equal("Dynamic.Dup.SameName", container.Options[1].Id);
    }

    /// <summary>
    /// @adversarial @category type @severity high
    /// @description AddAsyncAction の同期 Execute は fire-and-forget でありTask例外を伝搬しないこと
    /// @expected Execute() 呼び出しが例外を投げない
    /// </summary>
    [Fact]
    public void AddAsyncAction_SyncExecute_FireAndForget_NoException()
    {
        var container = new DynamicOptionContainer("Async");
        container.AddAsyncAction("Boom", async () =>
        {
            await Task.Delay(10);
            throw new InvalidOperationException("非同期爆発");
        });

        var action = container.Actions[0];
        // Execute は fire-and-forget なので例外は伝搬しない
        var ex = Record.Exception(() => action.Execute());
        Assert.Null(ex);
    }

    /// <summary>
    /// @adversarial @category type @severity medium
    /// @description AddInt の Setter に null を渡すと Convert.ToInt32(null) → 0 になること
    /// @expected setter に 0 が渡される
    /// </summary>
    [Fact]
    public void AddInt_SetterCalledWithNull_ConvertsToZero()
    {
        var container = new DynamicOptionContainer();
        var captured = -1;
        container.AddInt("NullTest", () => captured, v => captured = v);

        // Setter は Action<object?> なので null を渡せる
        container.Options[0].Setter!(null);

        Assert.Equal(0, captured);
    }

    /// <summary>
    /// @adversarial @category type @severity medium
    /// @description AddBool の Setter に null を渡すと false にフォールバックすること
    /// @expected setter に false が渡される
    /// </summary>
    [Fact]
    public void AddBool_SetterCalledWithNull_FallsBackToFalse()
    {
        var container = new DynamicOptionContainer();
        var captured = true;
        container.AddBool("NullBool", () => captured, v => captured = v);

        container.Options[0].Setter!(null);

        Assert.False(captured);
    }

    /// <summary>
    /// @adversarial @category type @severity medium
    /// @description AddFloat の Setter に文字列数値を渡すと Convert.ToSingle で変換されること
    /// @expected 正しい float 値が設定される
    /// </summary>
    [Fact]
    public void AddFloat_SetterCalledWithStringNumber_Converts()
    {
        var container = new DynamicOptionContainer();
        var captured = 0f;
        container.AddFloat("FloatStr", () => captured, v => captured = v);

        container.Options[0].Setter!("3.14");

        Assert.Equal(3.14f, captured, 0.01f);
    }

    /// <summary>
    /// @adversarial @category type @severity high
    /// @description メソッドチェーンで全型を混在登録してもOptions/Actionsが正しく分離すること
    /// @expected 各リストに正しい件数
    /// </summary>
    [Fact]
    public void MethodChain_MixedTypes_CorrectCounts()
    {
        var bv = false;
        var iv = 0;
        var fv = 0f;
        string? sv = null;
        string? cv = null;

        var container = new DynamicOptionContainer("Mix")
            .AddBool("B", () => bv, v => bv = v)
            .AddInt("I", () => iv, v => iv = v)
            .AddFloat("F", () => fv, v => fv = v)
            .AddString("S", () => sv, v => sv = v)
            .AddColor("C", () => cv, v => cv = v)
            .AddAction("Act", () => { })
            .AddAsyncAction("AsyncAct", () => Task.CompletedTask);

        Assert.Equal(5, container.Options.Count);
        Assert.Equal(2, container.Actions.Count);
    }

    // ───────────────────────────────────
    // 🌪️ カオス (Chaos Engineering)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category chaos @severity critical
    /// @description ショートカットのアクションが例外を投げた場合、HandleKeyDownが例外を伝搬すること
    /// @expected InvalidOperationException が飛ぶ
    /// </summary>
    [Fact]
    public void HandleKeyDown_ActionThrows_PropagatesException()
    {
        var mgr = new KeyboardShortcutManager();
        mgr.Register(new KeyCombination(CRKey.F5, CRModifierKeys.Ctrl),
            () => throw new InvalidOperationException("爆発"));

        Assert.Throws<InvalidOperationException>(() =>
            mgr.HandleKeyDown(CRKey.F5, CRModifierKeys.Ctrl));
    }

    /// <summary>
    /// @adversarial @category chaos @severity critical
    /// @description 同期アクションの Execute が例外を投げた場合、そのまま伝搬すること
    /// @expected InvalidOperationException がスロー
    /// </summary>
    [Fact]
    public void AddAction_ExecuteThrows_PropagatesException()
    {
        var container = new DynamicOptionContainer("Chaos");
        container.AddAction("Explode", () => throw new InvalidOperationException("同期爆発"));

        Assert.Throws<InvalidOperationException>(() => container.Actions[0].Execute());
    }

    /// <summary>
    /// @adversarial @category chaos @severity high
    /// @description 非同期アクションの ExecuteAsync が例外を投げた場合、Task経由で伝搬すること
    /// @expected AggregateException 内に InvalidOperationException
    /// </summary>
    [Fact]
    public async Task AddAsyncAction_ExecuteAsyncThrows_PropagatesViaTask()
    {
        var container = new DynamicOptionContainer("Chaos");
        container.AddAsyncAction("AsyncExplode", () =>
            throw new InvalidOperationException("非同期爆発"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => container.Actions[0].ExecuteAsync());
    }

    /// <summary>
    /// @adversarial @category chaos @severity high
    /// @description Getter が例外を投げるオプションを読み取った場合、例外がスローされること
    /// @expected InvalidOperationException
    /// </summary>
    [Fact]
    public void AddBool_GetterThrows_PropagatesOnAccess()
    {
        var container = new DynamicOptionContainer("Chaos");
        container.AddBool("BadGetter",
            () => throw new InvalidOperationException("getter爆発"),
            _ => { });

        Assert.Throws<InvalidOperationException>(() => container.Options[0].Getter());
    }

    /// <summary>
    /// @adversarial @category chaos @severity high
    /// @description Register に null アクションを渡すと ArgumentNullException がスローされること
    /// @expected ArgumentNullException
    /// </summary>
    [Fact]
    public void Register_NullAction_ThrowsArgumentNull()
    {
        var mgr = new KeyboardShortcutManager();

        Assert.Throws<ArgumentNullException>(() =>
            mgr.Register(new KeyCombination(CRKey.A, CRModifierKeys.None), null!));
    }

    /// <summary>
    /// @adversarial @category chaos @severity medium
    /// @description GetAll が返すディクショナリを変更しても内部状態に影響しないこと
    /// @expected 元の登録数が変わらない
    /// </summary>
    [Fact]
    public void GetAll_ReturnedDictionary_IsDefensiveCopy()
    {
        var mgr = new KeyboardShortcutManager();
        var combo = new KeyCombination(CRKey.C, CRModifierKeys.Ctrl);
        mgr.Register(combo, () => { });

        var snapshot = mgr.GetAll() as Dictionary<KeyCombination, Action>;
        snapshot?.Clear();

        // 内部状態は影響を受けない
        Assert.Single(mgr.GetAll());
    }

    /// <summary>
    /// @adversarial @category chaos @severity medium
    /// @description KeyCombination の record 等値比較が key + modifiers の組み合わせで正しく動作すること
    /// @expected 同じ組み合わせは Equal、異なればNotEqual
    /// </summary>
    [Fact]
    public void KeyCombination_RecordEquality_WorksCorrectly()
    {
        var a = new KeyCombination(CRKey.S, CRModifierKeys.Ctrl);
        var b = new KeyCombination(CRKey.S, CRModifierKeys.Ctrl);
        var c = new KeyCombination(CRKey.S, CRModifierKeys.Shift);
        var d = new KeyCombination(CRKey.A, CRModifierKeys.Ctrl);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(a, d);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
