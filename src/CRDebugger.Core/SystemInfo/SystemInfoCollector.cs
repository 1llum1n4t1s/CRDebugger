using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace CRDebugger.Core.SystemInfo;

/// <summary>
/// システム情報を収集するオーケストレーター。
/// OS・ランタイム・プロセス・アプリケーション等の各カテゴリ情報を収集し、
/// カスタムエントリの追加もサポートする。
/// 収集レベル（<see cref="SystemInfoCollectionLevel"/>）に応じて PII（UserName / Command Line 等）を除外する。
/// </summary>
public sealed class SystemInfoCollector
{
    /// <summary>ユーザーが追加したカスタムエントリの一覧（並行安全）</summary>
    private readonly object _customLock = new();
    private readonly List<SystemInfoEntry> _customEntries = new();

    private readonly SystemInfoCollectionLevel _level;

    /// <summary>デフォルト構築（Standard レベル、secure-by-default）</summary>
    public SystemInfoCollector() : this(SystemInfoCollectionLevel.Standard) { }

    /// <summary>
    /// 収集レベルを指定して構築する。
    /// </summary>
    /// <param name="level">PII 除外ポリシー</param>
    public SystemInfoCollector(SystemInfoCollectionLevel level)
    {
        _level = level;
    }

    /// <summary>
    /// カスタムシステム情報を追加する。
    /// 追加したエントリは <see cref="CollectAll"/> の結果末尾に含まれる。
    /// </summary>
    /// <param name="category">カテゴリ名（例: "Application", "Network" 等）</param>
    /// <param name="key">情報のキー名（例: "Version", "Endpoint" 等）</param>
    /// <param name="value">情報の値（文字列）</param>
    public void AddCustomInfo(string category, string key, string value)
    {
        lock (_customLock)
        {
            _customEntries.Add(new SystemInfoEntry(category, key, value));
        }
    }

    /// <summary>
    /// 全システム情報を収集して返す（収集レベルに応じて PII を除外）。
    /// </summary>
    /// <returns>収集されたシステム情報のリスト（読み取り専用）</returns>
    public IReadOnlyList<SystemInfoEntry> CollectAll()
    {
        var entries = new List<SystemInfoEntry>();

        // ─── System カテゴリ ───────────────────────────────────────────────
        entries.Add(new("System", "OS", RuntimeInformation.OSDescription));
        entries.Add(new("System", "OS Architecture", RuntimeInformation.OSArchitecture.ToString()));

        // Standard / Full では Machine Name を含める（Minimal では除外）
        if (_level != SystemInfoCollectionLevel.Minimal)
            entries.Add(new("System", "Machine Name", Environment.MachineName));

        // Full レベルのみユーザー名を含める（PII）
        if (_level == SystemInfoCollectionLevel.Full)
            entries.Add(new("System", "User Name", Environment.UserName));

        entries.Add(new("System", "Processor Count", Environment.ProcessorCount.ToString()));
        try
        {
            entries.Add(new("System", "System Memory",
                $"{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024)} MB"));
        }
        catch { /* GC 情報取得失敗時はスキップ */ }

        // ─── Runtime カテゴリ ─────────────────────────────────────────────
        entries.Add(new("Runtime", ".NET Version", RuntimeInformation.FrameworkDescription));
        entries.Add(new("Runtime", "Process Architecture", RuntimeInformation.ProcessArchitecture.ToString()));
        entries.Add(new("Runtime", "Runtime Identifier", RuntimeInformation.RuntimeIdentifier));
        entries.Add(new("Runtime", "GC Mode", GCSettings.IsServerGC ? "Server" : "Workstation"));
        entries.Add(new("Runtime", "GC Latency Mode", GCSettings.LatencyMode.ToString()));

        // ─── Process カテゴリ（Process API 失敗時のフォールバック付き） ───
        if (_level != SystemInfoCollectionLevel.Minimal)
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                entries.Add(new("Process", "Process ID", process.Id.ToString()));
                entries.Add(new("Process", "Process Name", process.ProcessName));
                entries.Add(new("Process", "Working Set", $"{process.WorkingSet64 / (1024 * 1024)} MB"));
                entries.Add(new("Process", "Private Memory", $"{process.PrivateMemorySize64 / (1024 * 1024)} MB"));
                try { entries.Add(new("Process", "Thread Count", process.Threads.Count.ToString())); }
                catch { /* sandboxed 環境で Threads が取れないケース */ }
                entries.Add(new("Process", "Start Time", process.StartTime.ToString("yyyy-MM-dd HH:mm:ss")));
                entries.Add(new("Process", "Uptime",
                    (DateTimeOffset.Now - process.StartTime).ToString(@"hh\:mm\:ss")));
            }
            catch
            {
                entries.Add(new("Process", "Process ID", Environment.ProcessId.ToString()));
                entries.Add(new("Process", "Process Info", "(取得不可: sandboxed environment)"));
            }
        }

        // ─── Application カテゴリ ─────────────────────────────────────────
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        if (assembly != null)
        {
            entries.Add(new("Application", "Name", assembly.GetName().Name ?? "Unknown"));
            entries.Add(new("Application", "Version", assembly.GetName().Version?.ToString() ?? "Unknown"));

            // Standard / Full では Location を含める（パスにユーザー名が混入することがある PII）
            if (_level == SystemInfoCollectionLevel.Full)
                entries.Add(new("Application", "Location", assembly.Location));
        }

        // Full レベルのみ Current Directory / Command Line を含める（PII / secret 漏洩リスク）
        if (_level == SystemInfoCollectionLevel.Full)
        {
            entries.Add(new("Application", "Current Directory", Environment.CurrentDirectory));
            entries.Add(new("Application", "Command Line", Environment.CommandLine));
        }
        else if (_level == SystemInfoCollectionLevel.Standard)
        {
            // Standard では Command Line は `--key=***` 形式に自動マスキングして含める（デバッグ参考のため）
            entries.Add(new("Application", "Command Line (masked)", MaskCommandLine(Environment.CommandLine)));
        }

        // ─── Display カテゴリ ─────────────────────────────────────────────
        entries.Add(new("Display", "Environment Version", Environment.Version.ToString()));

        // ─── カスタムエントリ ─────────────────────────────────────────────
        lock (_customLock)
        {
            entries.AddRange(_customEntries);
        }

        return entries;
    }

    /// <summary>
    /// コマンドラインの `--key=value` または `--key value` 形式の value 部分を `***` にマスクする。
    /// secret / token / password 等の漏洩リスクを最小化。
    /// </summary>
    private static string MaskCommandLine(string commandLine)
    {
        if (string.IsNullOrEmpty(commandLine)) return commandLine;

        // パターン 1: --key=value または -key=value → value を *** に
        var masked = Regex.Replace(commandLine, @"(--?\w+=)([^\s]+)", "$1***", RegexOptions.None, TimeSpan.FromMilliseconds(100));
        return masked;
    }
}
