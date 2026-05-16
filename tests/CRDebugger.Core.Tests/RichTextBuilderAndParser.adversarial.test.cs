using CRDebugger.Core.Logging;

namespace CRDebugger.Core.Tests;

/// <summary>
/// RichTextBuilder / RichTextParser の嫌がらせテスト
/// </summary>
public sealed class RichTextBuilderAndParserAdversarialTests
{
    // ───────────────────────────────────
    // 🗡️ 境界値・極端入力 (Boundary Assault)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category boundary @severity critical
    /// @description 空文字列をパースすると空リストが返ること
    /// @expected 要素数0のリスト
    /// </summary>
    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        var result = RichTextParser.Parse("");
        Assert.Empty(result);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// @description 絵文字のみの文字列が正しくパースされること
    /// @expected 絵文字テキストが保持された単一スパン
    /// </summary>
    [Fact]
    public void Parse_EmojiOnly_PreservesText()
    {
        var result = RichTextParser.Parse("\U0001F600\U0001F4A9\U0001F680");
        Assert.Single(result);
        Assert.Equal("\U0001F600\U0001F4A9\U0001F680", result[0].Text);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// @description サロゲートペア文字を含むテキストがタグ内外で壊れないこと
    /// @expected サロゲートペアが分断されずに保持される
    /// </summary>
    [Fact]
    public void Parse_SurrogatePairs_NotCorrupted()
    {
        // U+1F4A5 (COLLISION SYMBOL) = サロゲートペア
        var markup = "<b>\U0001F4A5explosion\U0001F4A5</b>";
        var result = RichTextParser.Parse(markup);
        Assert.Single(result);
        Assert.Equal("\U0001F4A5explosion\U0001F4A5", result[0].Text);
        Assert.True(result[0].IsBold);
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// @description ゼロ幅文字（ZWSP, ZWJ, ZWNJ）が除去されずに保持されること
    /// @expected ゼロ幅文字を含むテキストがそのまま返る
    /// </summary>
    [Fact]
    public void Parse_ZeroWidthChars_Preserved()
    {
        // ZWSP(\u200B) + ZWJ(\u200D) + ZWNJ(\u200C)
        var text = "a\u200Bb\u200Dc\u200Ce";
        var result = RichTextParser.Parse(text);
        Assert.Single(result);
        Assert.Equal(text, result[0].Text);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// @description 深くネストされたタグが正しく装飾状態を追跡すること
    /// @expected 太字+斜体+色の組み合わせが正しく反映される
    /// </summary>
    [Fact]
    public void Parse_DeeplyNestedTags_TracksState()
    {
        var markup = "<b><i><color=#FF0000>deep</color></i></b>plain";
        var result = RichTextParser.Parse(markup);
        Assert.Equal(2, result.Count);

        // 最内部: 太字+斜体+赤色
        Assert.Equal("deep", result[0].Text);
        Assert.True(result[0].IsBold);
        Assert.True(result[0].IsItalic);
        Assert.Equal(0xFFFF0000u, result[0].ForegroundColor);

        // タグ閉じ後: 装飾なし
        Assert.Equal("plain", result[1].Text);
        Assert.False(result[1].IsBold);
        Assert.False(result[1].IsItalic);
        Assert.Null(result[1].ForegroundColor);
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// @description RichTextBuilder に空文字列テキストを追加しても例外にならないこと
    /// @expected 空文字列テキストのスパンが含まれたリストが返る
    /// </summary>
    [Fact]
    public void Builder_EmptyStringText_DoesNotThrow()
    {
        var builder = new RichTextBuilder();
        builder.Text("");
        var result = builder.Build();
        Assert.Single(result);
        Assert.Equal("", result[0].Text);
    }

    // ───────────────────────────────────
    // 💀 リソース枯渇 (Resource Exhaustion)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category resource @severity critical
    /// @description 100KB超のマークアップ文字列をパースしても破綻しないこと
    /// @expected メモリ不足やタイムアウトなく正常にパースされる
    /// </summary>
    [Fact]
    public void Parse_HugeMarkup_100KB_CompletesSuccessfully()
    {
        // 約120KBのマークアップを生成
        var chunk = "<b>hello</b><i>world</i>";
        var repeatCount = 5000;
        var markup = string.Concat(Enumerable.Repeat(chunk, repeatCount));
        Assert.True(markup.Length > 100_000);

        var result = RichTextParser.Parse(markup);

        // 各チャンクから2スパン生成されるので合計10000スパン
        Assert.Equal(repeatCount * 2, result.Count);
        Assert.Equal("hello", result[0].Text);
        Assert.True(result[0].IsBold);
        Assert.Equal("world", result[1].Text);
        Assert.True(result[1].IsItalic);
    }

    /// <summary>
    /// @adversarial @category resource @severity high
    /// @description 数千レベルの開きタグネストが Stack Overflow を起こさないこと
    /// @expected パーサーは再帰でなくループ実装のため正常完了する
    /// </summary>
    [Fact]
    public void Parse_ThousandsOfNestedBoldTags_NoStackOverflow()
    {
        var depth = 3000;
        var opens = string.Concat(Enumerable.Repeat("<b>", depth));
        var closes = string.Concat(Enumerable.Repeat("</b>", depth));
        var markup = opens + "innermost" + closes;

        var result = RichTextParser.Parse(markup);

        // テキストは1つだけ
        Assert.Single(result);
        Assert.Equal("innermost", result[0].Text);
        Assert.True(result[0].IsBold);
    }

    // ───────────────────────────────────
    // 🔀 状態遷移・ビルダー操作 (State Transition)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category state @severity critical
    /// @description Build()を複数回呼んでも同じ内容が返ること
    /// @expected 2回のBuild()が同一内容の読み取り専用リストを返す
    /// </summary>
    [Fact]
    public void Builder_BuildCalledTwice_ReturnsSameContent()
    {
        var builder = new RichTextBuilder();
        builder.Text("hello").Bold("world");

        var first = builder.Build();
        var second = builder.Build();

        Assert.Equal(2, first.Count);
        Assert.Equal(2, second.Count);
        Assert.Equal("hello", first[0].Text);
        Assert.Equal("world", first[1].Text);
        // AsReadOnly() は呼ぶたびに新しいラッパーを生成するが内容は同一
        Assert.Equal(first[0], second[0]);
        Assert.Equal(first[1], second[1]);
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// @description Build()後にさらにAdd操作すると、既存のBuild結果にも反映されること
    /// @expected AsReadOnly()はラッパーなのでBuild後の追加が既存結果に可視
    /// </summary>
    [Fact]
    public void Builder_AddAfterBuild_AffectsExistingBuildResult()
    {
        var builder = new RichTextBuilder();
        builder.Text("first");
        var result = builder.Build();
        Assert.Single(result);

        // Build後に追加 — AsReadOnly() はライブビューなので反映される
        builder.Italic("second");
        Assert.Equal(2, result.Count);
        Assert.Equal("second", result[1].Text);
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// @description 空のビルダーからBuild()すると空の読み取り専用リストが返ること
    /// @expected 要素数0の IReadOnlyList
    /// </summary>
    [Fact]
    public void Builder_EmptyBuild_ReturnsEmptyReadOnlyList()
    {
        var builder = new RichTextBuilder();
        var result = builder.Build();
        Assert.Empty(result);
        Assert.IsAssignableFrom<IReadOnlyList<RichTextSpan>>(result);
    }

    /// <summary>
    /// @adversarial @category state @severity medium
    /// @description 全メソッドチェーンが同一ビルダーインスタンスを返すこと
    /// @expected Text/Colored/Bold/Italic/Span全てが同一参照を返す
    /// </summary>
    [Fact]
    public void Builder_MethodChain_ReturnsSameInstance()
    {
        var builder = new RichTextBuilder();
        var span = new RichTextSpan("custom", IsBold: true, IsItalic: true);

        var r1 = builder.Text("a");
        var r2 = builder.Colored("b", 0xFF00FF00);
        var r3 = builder.Bold("c");
        var r4 = builder.Italic("d");
        var r5 = builder.Span(span);

        Assert.Same(builder, r1);
        Assert.Same(builder, r2);
        Assert.Same(builder, r3);
        Assert.Same(builder, r4);
        Assert.Same(builder, r5);
    }

    // ───────────────────────────────────
    // 🎭 型パンチ・不正入力 (Type Punching)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category type @severity critical
    /// @description 閉じ山括弧のないタグがリテラルテキストとして扱われること
    /// @expected '&lt;' から末尾までがプレーンテキストスパンになる
    /// </summary>
    [Fact]
    public void Parse_UnclosedAngleBracket_TreatedAsText()
    {
        var result = RichTextParser.Parse("hello<b");
        Assert.Equal(2, result.Count);
        Assert.Equal("hello", result[0].Text);
        Assert.Equal("<b", result[1].Text);
        Assert.False(result[1].IsBold); // タグとして処理されない
    }

    /// <summary>
    /// @adversarial @category type @severity high
    /// @description 不一致の閉じタグがあっても例外にならず装飾状態が適切に変化すること
    /// @expected /b が先に来ても bold が false になるだけで例外は発生しない
    /// </summary>
    [Fact]
    public void Parse_MismatchedClosingTag_NoException()
    {
        // 先に /b を閉じてからbを開く
        var result = RichTextParser.Parse("</b>text1<b>text2</b>");
        Assert.Equal(2, result.Count);

        // </b> はboldをfalseにする（既にfalseなので変化なし）
        Assert.Equal("text1", result[0].Text);
        Assert.False(result[0].IsBold);

        Assert.Equal("text2", result[1].Text);
        Assert.True(result[1].IsBold);
    }

    /// <summary>
    /// @adversarial @category type @severity critical
    /// @description 不正な16進数カラー値が無視されること
    /// @expected color タグがあっても不正値ならForegroundColorはnull
    /// </summary>
    [Fact]
    public void Parse_InvalidHexColor_IgnoredGracefully()
    {
        // "ZZZZZZ" は16進数ではない
        var result = RichTextParser.Parse("<color=#ZZZZZZ>text</color>");
        Assert.Single(result);
        Assert.Equal("text", result[0].Text);
        Assert.Null(result[0].ForegroundColor);
    }

    /// <summary>
    /// @adversarial @category type @severity high
    /// @description カラーフォーマットの寛容性: #なし6桁、#付き8桁(AARRGGBB)が全て通ること
    /// @expected 全フォーマットが正常にパースされる
    /// </summary>
    [Fact]
    public void Parse_LenientColorFormats_AllParse()
    {
        // #RRGGBB 形式
        var r1 = RichTextParser.Parse("<color=#FF8800>a</color>");
        Assert.Equal(0xFFFF8800u, r1[0].ForegroundColor);

        // RRGGBB (#なし) 形式 — Trim('#') は何もしないので生の6桁hex
        var r2 = RichTextParser.Parse("<color=FF8800>a</color>");
        Assert.Equal(0xFFFF8800u, r2[0].ForegroundColor);

        // #AARRGGBB 形式 — Trim('#')で先頭#除去後8桁hex
        // 0xFF112233 を入力 → 0xFF000000 | 0xFF112233 = 0xFF112233
        var r3 = RichTextParser.Parse("<color=#FF112233>a</color>");
        Assert.NotNull(r3[0].ForegroundColor);
        // 8桁parseの結果: parsed=0xFF112233, 0xFF000000 | 0xFF112233 = 0xFF112233
        Assert.Equal(0xFF112233u, r3[0].ForegroundColor);
    }

    /// <summary>
    /// @adversarial @category type @severity high
    /// @description XSS風の入力が安全にテキストとして処理されること
    /// @expected scriptタグ等が未知タグとして無視され、テキスト部分のみ残る
    /// </summary>
    [Fact]
    public void Parse_XssLikeInput_TreatedSafely()
    {
        var markup = "<script>alert('xss')</script><b>safe</b>";
        var result = RichTextParser.Parse(markup);

        // <script> は未知タグなので無視、alert('xss') はテキスト
        // </script> も未知タグなので無視、<b>safe</b> は太字
        Assert.Equal(2, result.Count);
        Assert.Equal("alert('xss')", result[0].Text);
        Assert.Equal("safe", result[1].Text);
        Assert.True(result[1].IsBold);
    }

    /// <summary>
    /// @adversarial @category type @severity medium
    /// @description タグ名の大文字小文字が混在していても正しくパースされること
    /// @expected タグ名はcase-insensitiveで処理される
    /// </summary>
    [Fact]
    public void Parse_MixedCaseTags_ParsedCaseInsensitive()
    {
        var result = RichTextParser.Parse("<B>bold<I>both</I></B><COLOR=#00FF00>green</COLOR>");
        Assert.Equal(3, result.Count);

        Assert.Equal("bold", result[0].Text);
        Assert.True(result[0].IsBold);
        Assert.False(result[0].IsItalic);

        Assert.Equal("both", result[1].Text);
        Assert.True(result[1].IsBold);
        Assert.True(result[1].IsItalic);

        Assert.Equal("green", result[2].Text);
        Assert.False(result[2].IsBold);
        Assert.False(result[2].IsItalic);
        Assert.Equal(0xFF00FF00u, result[2].ForegroundColor);
    }

    /// <summary>
    /// @adversarial @category type @severity medium
    /// @description HTMLエンティティ風の文字列がそのままテキストとして保持されること
    /// @expected &amp;amp; 等はデコードされずリテラル文字列として残る
    /// </summary>
    [Fact]
    public void Parse_HtmlEntities_NotDecoded()
    {
        var result = RichTextParser.Parse("<b>&amp;&lt;&gt;&quot;</b>");
        Assert.Single(result);
        Assert.Equal("&amp;&lt;&gt;&quot;", result[0].Text);
        Assert.True(result[0].IsBold);
    }

    // ───────────────────────────────────
    // 🌪️ カオス・異常環境 (Chaos)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category chaos @severity high
    /// @description RTL制御文字（RLM, LRM, RLE等）がタグ解析を壊さないこと
    /// @expected 制御文字はテキスト部分に保持され、タグは正常に処理される
    /// </summary>
    [Fact]
    public void Parse_RtlControlCharacters_DoNotBreakParsing()
    {
        // RLM(\u200F) + LRM(\u200E) + RLE(\u202B)
        var markup = "<b>\u200Fright\u200Eleft\u202Bbidi</b>";
        var result = RichTextParser.Parse(markup);
        Assert.Single(result);
        Assert.Equal("\u200Fright\u200Eleft\u202Bbidi", result[0].Text);
        Assert.True(result[0].IsBold);
    }

    /// <summary>
    /// @adversarial @category chaos @severity critical
    /// @description null バイト (\0) を含むマークアップが例外を起こさないこと
    /// @expected null バイトはテキストとして保持される
    /// </summary>
    [Fact]
    public void Parse_NullBytesInMarkup_DoNotCrash()
    {
        var markup = "before\0<b>mid\0dle</b>after\0";
        var result = RichTextParser.Parse(markup);
        Assert.Equal(3, result.Count);
        Assert.Equal("before\0", result[0].Text);
        Assert.Equal("mid\0dle", result[1].Text);
        Assert.True(result[1].IsBold);
        Assert.Equal("after\0", result[2].Text);
    }

    /// <summary>
    /// @adversarial @category chaos @severity high
    /// @description 有効タグと無効タグが交互に混在しても正しくパースされること
    /// @expected 有効タグは装飾を変更し、無効タグは無視される
    /// </summary>
    [Fact]
    public void Parse_MixedValidInvalidTags_CorrectlyParsed()
    {
        var markup = "<b>bold</b><fake>text1</fake><i>italic</i><color=#invalid>text2</color>";
        var result = RichTextParser.Parse(markup);
        Assert.Equal(4, result.Count);

        // <b>bold</b>
        Assert.Equal("bold", result[0].Text);
        Assert.True(result[0].IsBold);

        // <fake> は未知タグ → 無視、text1 はプレーン
        Assert.Equal("text1", result[1].Text);
        Assert.False(result[1].IsBold);
        Assert.False(result[1].IsItalic);

        // <i>italic</i>
        Assert.Equal("italic", result[2].Text);
        Assert.True(result[2].IsItalic);

        // <color=#invalid> は不正hex → 色なし、text2 はプレーン
        Assert.Equal("text2", result[3].Text);
        Assert.Null(result[3].ForegroundColor);
    }

    /// <summary>
    /// @adversarial @category chaos @severity medium
    /// @description タグ内にスペースが含まれていてもTrimで処理されること
    /// @expected タグ名の前後スペースが除去されて正しく認識される
    /// </summary>
    [Fact]
    public void Parse_TagsWithWhitespace_TrimmedAndParsed()
    {
        var result = RichTextParser.Parse("< b >bold</ b >");
        Assert.Single(result);
        Assert.Equal("bold", result[0].Text);
        Assert.True(result[0].IsBold);
    }

    /// <summary>
    /// @adversarial @category chaos @severity medium
    /// @description 連続する山括弧ペアが空タグとして処理されテキストに影響しないこと
    /// @expected 空タグ &lt;&gt; は未知タグとして無視される
    /// </summary>
    [Fact]
    public void Parse_EmptyAngleBrackets_IgnoredAsUnknownTag()
    {
        var result = RichTextParser.Parse("a<>b<>c");
        // <> の間は空文字列 → 未知タグとして無視
        // テキスト: "a", "b", "c"
        Assert.Equal(3, result.Count);
        Assert.Equal("a", result[0].Text);
        Assert.Equal("b", result[1].Text);
        Assert.Equal("c", result[2].Text);
    }

    /// <summary>
    /// @adversarial @category chaos @severity high
    /// @description 閉じタグなしで終わるマークアップでも全テキストが回収されること
    /// @expected 装飾状態が開いたまま終了してもテキストは失われない
    /// </summary>
    [Fact]
    public void Parse_UnclosedTagsAtEnd_TextNotLost()
    {
        var result = RichTextParser.Parse("<b><i><color=#00AAFF>all styled but never closed");
        Assert.Single(result);
        Assert.Equal("all styled but never closed", result[0].Text);
        Assert.True(result[0].IsBold);
        Assert.True(result[0].IsItalic);
        Assert.Equal(0xFF00AAFFu, result[0].ForegroundColor);
    }
}
