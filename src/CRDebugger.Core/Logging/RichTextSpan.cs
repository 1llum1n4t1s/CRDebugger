namespace CRDebugger.Core.Logging;

/// <summary>
/// リッチテキストの個別スパン。色・太字・斜体などの装飾情報を保持するイミュータブルな値オブジェクト。
/// </summary>
/// <param name="Text">表示テキスト本文</param>
/// <param name="ForegroundColor">前景色（ARGB 形式 uint）。<c>null</c> の場合はデフォルト色を使用</param>
/// <param name="BackgroundColor">背景色（ARGB 形式 uint）。<c>null</c> の場合は透明</param>
/// <param name="IsBold">太字にするかどうか</param>
/// <param name="IsItalic">斜体にするかどうか</param>
public sealed record RichTextSpan(
    string Text,
    uint? ForegroundColor = null,
    uint? BackgroundColor = null,
    bool IsBold = false,
    bool IsItalic = false
);

/// <summary>
/// <see cref="RichTextSpan"/> のリストをメソッドチェーンで構築するビルダー。
/// </summary>
public sealed class RichTextBuilder
{
    /// <summary>構築中のスパンリスト</summary>
    private readonly List<RichTextSpan> _spans = new();

    /// <summary>
    /// 装飾なしのプレーンテキストスパンを追加する
    /// </summary>
    /// <param name="text">追加するテキスト</param>
    /// <returns>メソッドチェーン用のビルダー自身</returns>
    public RichTextBuilder Text(string text)
    {
        _spans.Add(new RichTextSpan(text));
        return this;
    }

    /// <summary>
    /// 指定した前景色のテキストスパンを追加する
    /// </summary>
    /// <param name="text">追加するテキスト</param>
    /// <param name="foregroundColor">前景色（ARGB 形式 uint）</param>
    /// <returns>メソッドチェーン用のビルダー自身</returns>
    public RichTextBuilder Colored(string text, uint foregroundColor)
    {
        _spans.Add(new RichTextSpan(text, ForegroundColor: foregroundColor));
        return this;
    }

    /// <summary>
    /// 太字のテキストスパンを追加する
    /// </summary>
    /// <param name="text">追加するテキスト</param>
    /// <returns>メソッドチェーン用のビルダー自身</returns>
    public RichTextBuilder Bold(string text)
    {
        _spans.Add(new RichTextSpan(text, IsBold: true));
        return this;
    }

    /// <summary>
    /// 斜体のテキストスパンを追加する
    /// </summary>
    /// <param name="text">追加するテキスト</param>
    /// <returns>メソッドチェーン用のビルダー自身</returns>
    public RichTextBuilder Italic(string text)
    {
        _spans.Add(new RichTextSpan(text, IsItalic: true));
        return this;
    }

    /// <summary>
    /// カスタムスパンをそのまま追加する
    /// </summary>
    /// <param name="span">追加する <see cref="RichTextSpan"/></param>
    /// <returns>メソッドチェーン用のビルダー自身</returns>
    public RichTextBuilder Span(RichTextSpan span)
    {
        _spans.Add(span);
        return this;
    }

    /// <summary>
    /// 構築されたスパンリストを返す
    /// </summary>
    /// <returns>追加順に並んだリッチテキストスパンの読み取り専用リスト</returns>
    public IReadOnlyList<RichTextSpan> Build() => _spans.ToList();
}

/// <summary>
/// シンプルなマークアップ文字列を <see cref="RichTextSpan"/> リストにパースする静的クラス。
/// 対応タグ: &lt;b&gt;, &lt;/b&gt;, &lt;i&gt;, &lt;/i&gt;, &lt;color=#RRGGBB&gt;, &lt;/color&gt;
/// </summary>
public static class RichTextParser
{
    /// <summary>
    /// マークアップ文字列を <see cref="RichTextSpan"/> リストにパースする
    /// </summary>
    /// <param name="markup">パースするマークアップ文字列</param>
    /// <returns>パースされたリッチテキストスパンのリスト</returns>
    public static IReadOnlyList<RichTextSpan> Parse(string markup)
    {
        var spans = new List<RichTextSpan>();
        // 現在の読み取り位置
        var pos = 0;
        // 現在の装飾状態
        var bold = false;
        var italic = false;
        uint? color = null;

        while (pos < markup.Length)
        {
            // 次のタグ開始位置（'<'）を探す
            var tagStart = markup.IndexOf('<', pos);
            if (tagStart < 0)
            {
                // 残りすべてをテキストとして追加してループ終了
                var remaining = markup[pos..];
                if (remaining.Length > 0)
                    spans.Add(new RichTextSpan(remaining, color, null, bold, italic));
                break;
            }

            // タグ前にテキストがあれば現在の装飾状態でスパンを追加する
            if (tagStart > pos)
            {
                var text = markup[pos..tagStart];
                spans.Add(new RichTextSpan(text, color, null, bold, italic));
            }

            // タグ終了位置（'>'）を探す
            var tagEnd = markup.IndexOf('>', tagStart);
            if (tagEnd < 0)
            {
                // 閉じ '>' が見つからない場合は残り全体をテキストとして追加して終了
                var remaining = markup[tagStart..];
                spans.Add(new RichTextSpan(remaining, color, null, bold, italic));
                break;
            }

            // '<' と '>' の間のタグ名を取り出して小文字化する
            var tag = markup[(tagStart + 1)..tagEnd].Trim();
            switch (tag.ToLowerInvariant())
            {
                case "b": bold = true; break;       // 太字開始
                case "/b": bold = false; break;     // 太字終了
                case "i": italic = true; break;     // 斜体開始
                case "/i": italic = false; break;   // 斜体終了
                case "/color": color = null; break; // 色指定終了
                default:
                    // color=#RRGGBB 形式のタグを解析して ARGB 値に変換する
                    if (tag.StartsWith("color=", StringComparison.OrdinalIgnoreCase))
                    {
                        // "color=" の後ろの '#' を除いた16進数文字列を取り出す
                        var colorStr = tag[6..].Trim('#');
                        // 16進数パースに成功した場合は完全不透明（0xFF）のα値を付与する
                        if (uint.TryParse(colorStr, System.Globalization.NumberStyles.HexNumber, null, out var parsed))
                            color = 0xFF000000 | parsed;
                    }
                    break;
            }

            // 次の走査位置を '>' の直後に進める
            pos = tagEnd + 1;
        }

        return spans;
    }
}
