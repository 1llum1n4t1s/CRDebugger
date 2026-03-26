namespace CRDebugger.Core.Logging;

/// <summary>
/// リッチテキストの個別スパン（色・太字・斜体対応）
/// </summary>
public sealed record RichTextSpan(
    string Text,
    uint? ForegroundColor = null,
    uint? BackgroundColor = null,
    bool IsBold = false,
    bool IsItalic = false
);

/// <summary>
/// リッチテキストのビルダー
/// </summary>
public sealed class RichTextBuilder
{
    private readonly List<RichTextSpan> _spans = new();

    /// <summary>プレーンテキストを追加</summary>
    public RichTextBuilder Text(string text)
    {
        _spans.Add(new RichTextSpan(text));
        return this;
    }

    /// <summary>色付きテキストを追加</summary>
    public RichTextBuilder Colored(string text, uint foregroundColor)
    {
        _spans.Add(new RichTextSpan(text, ForegroundColor: foregroundColor));
        return this;
    }

    /// <summary>太字テキストを追加</summary>
    public RichTextBuilder Bold(string text)
    {
        _spans.Add(new RichTextSpan(text, IsBold: true));
        return this;
    }

    /// <summary>斜体テキストを追加</summary>
    public RichTextBuilder Italic(string text)
    {
        _spans.Add(new RichTextSpan(text, IsItalic: true));
        return this;
    }

    /// <summary>カスタムスパンを追加</summary>
    public RichTextBuilder Span(RichTextSpan span)
    {
        _spans.Add(span);
        return this;
    }

    /// <summary>スパンリストをビルド</summary>
    public IReadOnlyList<RichTextSpan> Build() => _spans.ToList();
}

/// <summary>
/// シンプルなマークアップパーサー
/// 対応タグ: &lt;b&gt;, &lt;i&gt;, &lt;color=#RRGGBB&gt;
/// </summary>
public static class RichTextParser
{
    /// <summary>マークアップ文字列をRichTextSpanリストにパース</summary>
    public static IReadOnlyList<RichTextSpan> Parse(string markup)
    {
        var spans = new List<RichTextSpan>();
        var pos = 0;
        var bold = false;
        var italic = false;
        uint? color = null;

        while (pos < markup.Length)
        {
            var tagStart = markup.IndexOf('<', pos);
            if (tagStart < 0)
            {
                // 残りすべてをテキストとして追加
                var remaining = markup[pos..];
                if (remaining.Length > 0)
                    spans.Add(new RichTextSpan(remaining, color, null, bold, italic));
                break;
            }

            // タグ前のテキストを追加
            if (tagStart > pos)
            {
                var text = markup[pos..tagStart];
                spans.Add(new RichTextSpan(text, color, null, bold, italic));
            }

            var tagEnd = markup.IndexOf('>', tagStart);
            if (tagEnd < 0)
            {
                // 閉じタグがない場合は残り全てをテキストとして
                var remaining = markup[tagStart..];
                spans.Add(new RichTextSpan(remaining, color, null, bold, italic));
                break;
            }

            var tag = markup[(tagStart + 1)..tagEnd].Trim();
            switch (tag.ToLowerInvariant())
            {
                case "b": bold = true; break;
                case "/b": bold = false; break;
                case "i": italic = true; break;
                case "/i": italic = false; break;
                case "/color": color = null; break;
                default:
                    if (tag.StartsWith("color=", StringComparison.OrdinalIgnoreCase))
                    {
                        var colorStr = tag[6..].Trim('#');
                        if (uint.TryParse(colorStr, System.Globalization.NumberStyles.HexNumber, null, out var parsed))
                            color = 0xFF000000 | parsed;
                    }
                    break;
            }

            pos = tagEnd + 1;
        }

        return spans;
    }
}
