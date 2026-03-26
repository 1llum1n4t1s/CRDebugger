# CRDebugger

**SRDebugger-like debug panel for .NET desktop applications**

Unity の [SRDebugger](https://www.stompyrobot.uk/tools/srdebugger/) にインスパイアされた、WinForms / WPF / Avalonia 対応のランタイムデバッグパネルです。

## Features

- **System** - OS、CPU、メモリ、.NET ランタイム、プロセス情報を一覧表示
- **Console** - アプリケーションログのリアルタイム表示（Debug/Info/Warning/Error フィルタ、検索、スタックトレース）
- **Options** - リフレクション+アトリビュートによるプロパティの自動 UI コントロール生成
- **Profiler** - メモリ使用量、GC 統計（Gen0/1/2）、FPS 計測
- **Bug Reporter** - スクリーンショット付きバグレポート送信

## Packages

| Package | Description | NuGet |
|---------|-------------|-------|
| `CRDebugger.Core` | プラットフォーム非依存のコアロジック | [![NuGet](https://img.shields.io/nuget/v/CRDebugger.Core)](https://www.nuget.org/packages/CRDebugger.Core) |
| `CRDebugger.WinForms` | WinForms UI 実装 | [![NuGet](https://img.shields.io/nuget/v/CRDebugger.WinForms)](https://www.nuget.org/packages/CRDebugger.WinForms) |
| `CRDebugger.Wpf` | WPF UI 実装 | [![NuGet](https://img.shields.io/nuget/v/CRDebugger.Wpf)](https://www.nuget.org/packages/CRDebugger.Wpf) |
| `CRDebugger.Avalonia` | Avalonia UI 実装 | [![NuGet](https://img.shields.io/nuget/v/CRDebugger.Avalonia)](https://www.nuget.org/packages/CRDebugger.Avalonia) |

## Quick Start

### 1. NuGet パッケージをインストール

```bash
# WPF アプリの場合
dotnet add package CRDebugger.Wpf

# WinForms アプリの場合
dotnet add package CRDebugger.WinForms

# Avalonia アプリの場合
dotnet add package CRDebugger.Avalonia
```

### 2. 初期化

```csharp
using CRDebugger.Core;
using CRDebugger.Core.Theming;

// WPF の場合
using CRDebugger.Wpf;

var options = new CRDebuggerOptions();
options.UseWpf();           // or UseWinForms(), UseAvalonia()
options.Theme = CRTheme.Dark;
options.DefaultTab = CRTab.Console;
CRDebugger.Initialize(options);
```

### 3. 使用

```csharp
// デバッガーウィンドウを表示
CRDebugger.Show();
CRDebugger.Toggle();   // 表示/非表示切替

// ログ出力
CRDebugger.Log("情報メッセージ");
CRDebugger.LogWarning("警告メッセージ");
CRDebugger.LogError("エラーメッセージ", exception);

// Microsoft.Extensions.Logging 統合
builder.Logging.AddProvider(CRDebugger.CreateLoggerProvider());

// Options タブにオブジェクトを登録
CRDebugger.AddOptionContainer(myOptions);
```

## Options タブの使い方

プロパティにアトリビュートを付けると、自動的に UI コントロールが生成されます：

```csharp
using CRDebugger.Core.Options.Attributes;

public class GameOptions
{
    [CRCategory("Graphics")]
    [CRDisplayName("画質レベル")]
    [CRRange(0, 5, Step = 1)]
    public int QualityLevel { get; set; } = 3;

    [CRCategory("Graphics")]
    [CRDisplayName("フルスクリーン")]
    public bool IsFullscreen { get; set; }

    [CRCategory("Audio")]
    [CRDisplayName("マスター音量")]
    [CRRange(0, 100)]
    public int MasterVolume { get; set; } = 80;

    [CRCategory("Debug")]
    [CRAction(Label = "テストログ出力")]
    public void PrintTestLog()
    {
        CRDebugger.Log("テストログ！");
    }
}

// 登録
CRDebugger.AddOptionContainer(new GameOptions());
```

### 対応する型とコントロール

| 型 | コントロール |
|---|---|
| `bool` | チェックボックス / トグルスイッチ |
| `int`, `float`, `double` | スライダー + 数値入力（`CRRange` 指定時） |
| `string` | テキストボックス |
| `enum` | コンボボックス |
| `void` メソッド + `[CRAction]` | ボタン |

## テーマ

3 種類のテーマをサポート：

```csharp
CRDebugger.SetTheme(CRTheme.System);  // OS 設定に追従
CRDebugger.SetTheme(CRTheme.Light);   // ライトテーマ
CRDebugger.SetTheme(CRTheme.Dark);    // ダークテーマ
```

## ログ統合

4 つのログソースに対応：

```csharp
// 1. カスタム API
CRDebugger.Log("message");
CRDebugger.LogWarning("warning");
CRDebugger.LogError("error", exception);

// 2. Microsoft.Extensions.Logging
services.AddLogging(b => b.AddProvider(CRDebugger.CreateLoggerProvider()));

// 3. System.Diagnostics.Trace / Debug
System.Diagnostics.Trace.WriteLine("traced message");  // 自動キャプチャ

// 4. 未処理例外
// AppDomain.UnhandledException を自動キャプチャ（設定で無効化可能）
```

## バグレポート

カスタム送信先を設定可能：

```csharp
public class MyBugReportSender : IBugReportSender
{
    public async Task<bool> SendAsync(BugReport report, CancellationToken ct)
    {
        // HTTP POST でサーバーに送信、メール送信など
        return true;
    }
}

var options = new CRDebuggerOptions();
options.BugReportSender = new MyBugReportSender();
```

## Requirements

- .NET 6.0 以上
- WinForms / WPF: Windows のみ
- Avalonia: Windows / macOS / Linux

## License

MIT License - Copyright (C) 2026 ゆろち
