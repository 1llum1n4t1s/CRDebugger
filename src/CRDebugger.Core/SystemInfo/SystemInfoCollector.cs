using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace CRDebugger.Core.SystemInfo;

/// <summary>
/// システム情報を収集するオーケストレーター。
/// OS・ランタイム・プロセス・アプリケーション等の各カテゴリ情報を収集し、
/// カスタムエントリの追加もサポートする。
/// </summary>
public sealed class SystemInfoCollector
{
    /// <summary>ユーザーが追加したカスタムエントリの一覧</summary>
    private readonly List<SystemInfoEntry> _customEntries = new();

    /// <summary>
    /// カスタムシステム情報を追加する。
    /// 追加したエントリは <see cref="CollectAll"/> の結果末尾に含まれる。
    /// </summary>
    /// <param name="category">カテゴリ名（例: "Application", "Network" 等）</param>
    /// <param name="key">情報のキー名（例: "Version", "Endpoint" 等）</param>
    /// <param name="value">情報の値（文字列）</param>
    public void AddCustomInfo(string category, string key, string value)
    {
        _customEntries.Add(new SystemInfoEntry(category, key, value));
    }

    /// <summary>
    /// 全システム情報を収集して返す。
    /// System / Runtime / Process / Application / Display の各カテゴリに加え、
    /// <see cref="AddCustomInfo"/> で登録されたカスタムエントリも末尾に追加される。
    /// </summary>
    /// <returns>収集されたシステム情報のリスト（読み取り専用）</returns>
    public IReadOnlyList<SystemInfoEntry> CollectAll()
    {
        var entries = new List<SystemInfoEntry>();

        // ─── System カテゴリ ───────────────────────────────────────────────
        // OSの説明文（例: "Microsoft Windows 10.0.19041"）
        entries.Add(new("System", "OS", RuntimeInformation.OSDescription));
        // OSのアーキテクチャ（X64, Arm64 等）
        entries.Add(new("System", "OS Architecture", RuntimeInformation.OSArchitecture.ToString()));
        // マシン名（ホスト名）
        entries.Add(new("System", "Machine Name", Environment.MachineName));
        // ログオン中のユーザー名
        entries.Add(new("System", "User Name", Environment.UserName));
        // 論理プロセッサ数
        entries.Add(new("System", "Processor Count", Environment.ProcessorCount.ToString()));
        // GCが利用可能と報告するシステム全体の物理メモリ量（MB単位）
        entries.Add(new("System", "System Memory",
            $"{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024)} MB"));

        // ─── Runtime カテゴリ ─────────────────────────────────────────────
        // .NET フレームワークの説明（例: ".NET 8.0.1"）
        entries.Add(new("Runtime", ".NET Version", RuntimeInformation.FrameworkDescription));
        // 現在のプロセスのアーキテクチャ（X64, Arm64 等）
        entries.Add(new("Runtime", "Process Architecture", RuntimeInformation.ProcessArchitecture.ToString()));
        // ランタイム識別子（例: "win-x64", "linux-x64"）
        entries.Add(new("Runtime", "Runtime Identifier", RuntimeInformation.RuntimeIdentifier));
        // GCモード：サーバーGC（高スループット）かワークステーションGC（低レイテンシ）か
        entries.Add(new("Runtime", "GC Mode", GCSettings.IsServerGC ? "Server" : "Workstation"));
        // GCレイテンシモード（Batch, Interactive, LowLatency 等）
        entries.Add(new("Runtime", "GC Latency Mode", GCSettings.LatencyMode.ToString()));

        // ─── Process カテゴリ ─────────────────────────────────────────────
        // 現在のプロセスオブジェクトを取得（using でリソースを確実に解放）
        using var process = Process.GetCurrentProcess();
        // プロセスID
        entries.Add(new("Process", "Process ID", process.Id.ToString()));
        // プロセス名（拡張子なし）
        entries.Add(new("Process", "Process Name", process.ProcessName));
        // ワーキングセット（物理メモリ使用量）MB単位
        entries.Add(new("Process", "Working Set", $"{process.WorkingSet64 / (1024 * 1024)} MB"));
        // プライベートメモリ（仮想メモリ中のプロセス専有部分）MB単位
        entries.Add(new("Process", "Private Memory", $"{process.PrivateMemorySize64 / (1024 * 1024)} MB"));
        // 現在のスレッド数
        entries.Add(new("Process", "Thread Count", process.Threads.Count.ToString()));
        // プロセス起動時刻（ローカル時刻、ISO形式）
        entries.Add(new("Process", "Start Time", process.StartTime.ToString("yyyy-MM-dd HH:mm:ss")));
        // 起動からの経過時間（hh:mm:ss 形式）
        entries.Add(new("Process", "Uptime",
            (DateTimeOffset.Now - process.StartTime).ToString(@"hh\:mm\:ss")));

        // ─── Application カテゴリ ─────────────────────────────────────────
        // エントリアセンブリ（起動 .exe）の情報を取得（null になる場合もある）
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        if (assembly != null)
        {
            // アセンブリ名
            entries.Add(new("Application", "Name", assembly.GetName().Name ?? "Unknown"));
            // アセンブリバージョン（Major.Minor.Build.Revision）
            entries.Add(new("Application", "Version", assembly.GetName().Version?.ToString() ?? "Unknown"));
            // アセンブリの物理パス
            entries.Add(new("Application", "Location", assembly.Location));
        }
        // カレントディレクトリ
        entries.Add(new("Application", "Current Directory", Environment.CurrentDirectory));
        // 起動時のコマンドライン全体
        entries.Add(new("Application", "Command Line", Environment.CommandLine));

        // ─── Display カテゴリ ─────────────────────────────────────────────
        // 基本情報のみ収集（解像度・DPI 等の詳細はUIフレームワーク層で AddCustomInfo で追加する）
        entries.Add(new("Display", "Environment Version", Environment.Version.ToString()));

        // ─── カスタムエントリ ─────────────────────────────────────────────
        // AddCustomInfo で登録されたユーザー定義のエントリを末尾に追加
        entries.AddRange(_customEntries);

        return entries;
    }
}
