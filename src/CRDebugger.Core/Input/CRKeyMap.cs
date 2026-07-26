namespace CRDebugger.Core.Input;

/// <summary>
/// プラットフォーム固有のキー列挙体から <see cref="CRKey"/> へ変換するヘルパー。
/// <para>
/// WPF の <c>System.Windows.Input.Key</c>、WinForms の <c>System.Windows.Forms.Keys</c>、
/// Avalonia の <c>Avalonia.Input.Key</c> はいずれも "Escape" / "F1"〜"F12" / "D0"〜"D9" / "A"〜"Z" という
/// 同じメンバー名を使うため、名前一致で安全に変換できる。
/// 各プラットフォーム層はキーの <c>ToString()</c> をここに渡すだけでよい。
/// </para>
/// </summary>
public static class CRKeyMap
{
    /// <summary>
    /// キー名から <see cref="CRKey"/> を引くための辞書。
    /// <c>Enum.TryParse</c> を直接使うと "5" のような数値文字列が未定義の列挙値に化けるため、
    /// 名前の完全一致だけを許す辞書で引く。
    /// </summary>
    private static readonly Dictionary<string, CRKey> s_byName = BuildNameMap();

    /// <summary>名前 → CRKey の辞書を構築する（None は変換対象外として除外する）</summary>
    /// <returns>キー名で引ける辞書</returns>
    private static Dictionary<string, CRKey> BuildNameMap()
    {
        var map = new Dictionary<string, CRKey>(StringComparer.Ordinal);
        foreach (CRKey value in Enum.GetValues<CRKey>())
        {
            if (value == CRKey.None) continue;
            map[value.ToString()] = value;
        }
        return map;
    }

    /// <summary>
    /// プラットフォーム固有のキー名を <see cref="CRKey"/> に変換する。
    /// </summary>
    /// <param name="platformKeyName">プラットフォームのキー列挙体の名前（例: "F1", "Escape"）</param>
    /// <param name="key">変換結果。失敗した場合は <see cref="CRKey.None"/></param>
    /// <returns>対応する <see cref="CRKey"/> が存在する場合は true</returns>
    public static bool TryFromName(string? platformKeyName, out CRKey key)
    {
        if (string.IsNullOrEmpty(platformKeyName))
        {
            key = CRKey.None;
            return false;
        }

        return s_byName.TryGetValue(platformKeyName, out key);
    }
}
