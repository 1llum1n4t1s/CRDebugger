namespace CRDebugger.Core.Logging;

/// <summary>
/// ログフィルタ条件を表すイミュータブルな値オブジェクト。
/// 表示するログレベルと検索テキストを組み合わせて使う。
/// </summary>
/// <param name="ShowDebug">Debug レベルのログを表示するか</param>
/// <param name="ShowInfo">Info レベルのログを表示するか</param>
/// <param name="ShowWarning">Warning レベルのログを表示するか</param>
/// <param name="ShowError">Error レベルのログを表示するか</param>
/// <param name="SearchText">検索テキスト（部分一致・大文字小文字無視）。<c>null</c> または空文字で全件表示</param>
public sealed record LogFilter(
    bool ShowDebug = true,
    bool ShowInfo = true,
    bool ShowWarning = true,
    bool ShowError = true,
    string? SearchText = null
)
{
    /// <summary>
    /// 指定されたログエントリがフィルタ条件に合致するか判定する
    /// </summary>
    /// <param name="entry">判定対象のログエントリ</param>
    /// <returns>フィルタ条件に合致する場合は <c>true</c>、そうでなければ <c>false</c></returns>
    public bool Matches(LogEntry entry)
    {
        // ログレベルのフラグが ON かどうかを確認する
        var levelMatch = entry.Level switch
        {
            CRLogLevel.Debug => ShowDebug,
            CRLogLevel.Info => ShowInfo,
            CRLogLevel.Warning => ShowWarning,
            CRLogLevel.Error => ShowError,
            // 未知のレベルは表示対象とする
            _ => true
        };

        // レベルが一致しない場合は早期リターン
        if (!levelMatch) return false;

        // 検索テキストが指定されている場合はメッセージ本文に対して部分一致検索を行う
        if (!string.IsNullOrEmpty(SearchText))
        {
            return entry.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        // すべての条件を満たした
        return true;
    }
}
