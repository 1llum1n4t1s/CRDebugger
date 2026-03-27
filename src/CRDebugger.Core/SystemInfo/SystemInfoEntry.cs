namespace CRDebugger.Core.SystemInfo;

/// <summary>
/// システム情報の1件分を表すキーバリューペア。
/// カテゴリ・キー・値の3要素で構成される不変レコード型。
/// <see cref="SystemInfoCollector.CollectAll"/> の返却値要素として使用される。
/// </summary>
/// <param name="Category">
/// 情報のカテゴリ名（例: "System", "Runtime", "Process", "Application", "Display" 等）。
/// 画面上のグループ見出しとして使用される。
/// </param>
/// <param name="Key">
/// 情報のキー名（例: "OS", ".NET Version", "Process ID" 等）。
/// カテゴリ内での識別子となる。
/// </param>
/// <param name="Value">
/// 情報の値（文字列）。数値・パス・フラグ等もすべて文字列で保持する。
/// </param>
public sealed record SystemInfoEntry(string Category, string Key, string Value);
