using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace CRDebugger.Core.SystemInfo;

/// <summary>
/// システム情報を収集するオーケストレーター
/// </summary>
public sealed class SystemInfoCollector
{
    private readonly List<SystemInfoEntry> _customEntries = new();

    /// <summary>カスタム情報を追加</summary>
    public void AddCustomInfo(string category, string key, string value)
    {
        _customEntries.Add(new SystemInfoEntry(category, key, value));
    }

    /// <summary>全システム情報を収集</summary>
    public IReadOnlyList<SystemInfoEntry> CollectAll()
    {
        var entries = new List<SystemInfoEntry>();

        // System
        entries.Add(new("System", "OS", RuntimeInformation.OSDescription));
        entries.Add(new("System", "OS Architecture", RuntimeInformation.OSArchitecture.ToString()));
        entries.Add(new("System", "Machine Name", Environment.MachineName));
        entries.Add(new("System", "User Name", Environment.UserName));
        entries.Add(new("System", "Processor Count", Environment.ProcessorCount.ToString()));
        entries.Add(new("System", "System Memory",
            $"{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024)} MB"));

        // .NET Runtime
        entries.Add(new("Runtime", ".NET Version", RuntimeInformation.FrameworkDescription));
        entries.Add(new("Runtime", "Process Architecture", RuntimeInformation.ProcessArchitecture.ToString()));
        entries.Add(new("Runtime", "Runtime Identifier", RuntimeInformation.RuntimeIdentifier));
        entries.Add(new("Runtime", "GC Mode", GCSettings.IsServerGC ? "Server" : "Workstation"));
        entries.Add(new("Runtime", "GC Latency Mode", GCSettings.LatencyMode.ToString()));

        // Process
        using var process = Process.GetCurrentProcess();
        entries.Add(new("Process", "Process ID", process.Id.ToString()));
        entries.Add(new("Process", "Process Name", process.ProcessName));
        entries.Add(new("Process", "Working Set", $"{process.WorkingSet64 / (1024 * 1024)} MB"));
        entries.Add(new("Process", "Private Memory", $"{process.PrivateMemorySize64 / (1024 * 1024)} MB"));
        entries.Add(new("Process", "Thread Count", process.Threads.Count.ToString()));
        entries.Add(new("Process", "Start Time", process.StartTime.ToString("yyyy-MM-dd HH:mm:ss")));
        entries.Add(new("Process", "Uptime",
            (DateTimeOffset.Now - process.StartTime).ToString(@"hh\:mm\:ss")));

        // Application
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        if (assembly != null)
        {
            entries.Add(new("Application", "Name", assembly.GetName().Name ?? "Unknown"));
            entries.Add(new("Application", "Version", assembly.GetName().Version?.ToString() ?? "Unknown"));
            entries.Add(new("Application", "Location", assembly.Location));
        }
        entries.Add(new("Application", "Current Directory", Environment.CurrentDirectory));
        entries.Add(new("Application", "Command Line", Environment.CommandLine));

        // Display (基本情報のみ、詳細はUIフレームワーク層で追加)
        entries.Add(new("Display", "Environment Version", Environment.Version.ToString()));

        // カスタムエントリ
        entries.AddRange(_customEntries);

        return entries;
    }
}
