namespace CRDebugger.Core.SystemInfo;

/// <summary>
/// システム情報のキーバリューペア
/// </summary>
public sealed record SystemInfoEntry(string Category, string Key, string Value);
