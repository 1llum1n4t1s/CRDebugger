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

            // Location は Full のみ（パスにユーザー名が混入することがある PII のため Standard では出さない）
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
    /// Standard レベル用にコマンドラインをマスクする。
    /// <list type="number">
    ///   <item>先頭トークン（実行ファイルのフルパス）をファイル名だけに縮める。
    ///         フルパスにはユーザー名が含まれ得るため、Location を Full 限定にした方針と揃える。</item>
    ///   <item><c>--key=value</c> / <c>-key=value</c> 形式の value を <c>***</c> に置換する。</item>
    /// </list>
    /// <c>--key value</c>（スペース区切り）形式は意図的にマスクしない。
    /// 次のトークンが値なのか別のフラグ・サブコマンドなのかを機械的に判別できず、
    /// 一律に潰すと <c>--verbose --profile dev</c> の <c>--profile</c> まで壊してしまうため。
    /// スペース区切りで secret を渡す場合は <see cref="SystemInfoCollectionLevel.Minimal"/> を使うこと。
    /// </summary>
    /// <param name="commandLine">元のコマンドライン文字列</param>
    /// <returns>マスク済みのコマンドライン文字列</returns>
    private static string MaskCommandLine(string commandLine)
    {
        if (string.IsNullOrEmpty(commandLine)) return commandLine;

        // 先頭の実行ファイルパスをファイル名だけに縮める（ユーザー名を含むフルパスを出さない）
        var withoutPath = StripExecutablePath(commandLine);

        // --key=value または -key=value → value を *** に
        return Regex.Replace(withoutPath, @"(--?\w+=)([^\s]+)", "$1***", RegexOptions.None, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// コマンドライン先頭の実行ファイルパスをファイル名だけに置き換える。
    /// Windows では引用符付き（<c>"C:\Program Files\..\app.exe" --flag</c>）になる場合があるため両形式を扱う。
    /// </summary>
    /// <param name="commandLine">元のコマンドライン文字列</param>
    /// <returns>先頭トークンをファイル名に縮めた文字列</returns>
    private static string StripExecutablePath(string commandLine)
    {
        string exePath;
        string rest;

        if (commandLine[0] == '"')
        {
            // 引用符で囲まれている場合は閉じ引用符までが実行ファイルパス
            var closing = commandLine.IndexOf('"', 1);
            if (closing < 0) return commandLine; // 閉じ引用符が無い異常系はそのまま返す
            exePath = commandLine.Substring(1, closing - 1);
            rest = commandLine.Substring(closing + 1);
        }
        else
        {
            // 引用符無しの場合は最初の空白までが実行ファイルパス
            var space = commandLine.IndexOf(' ');
            exePath = space < 0 ? commandLine : commandLine.Substring(0, space);
            rest = space < 0 ? string.Empty : commandLine.Substring(space);
        }

        // パス区切りを含まない場合（既にファイル名だけ）はそのまま返す
        var fileName = System.IO.Path.GetFileName(exePath);
        return string.IsNullOrEmpty(fileName) ? commandLine : fileName + rest;
    }
}
