# CRDebugger

**SRDebugger-like debug panel for .NET desktop applications**

Unity の [SRDebugger](https://www.stompyrobot.uk/tools/srdebugger/) にインスパイアされた、WinForms / WPF / Avalonia 対応のランタイムデバッグパネルです。

<img width="600" alt="スクリーンショット 2026-03-28 135900" src="https://github.com/user-attachments/assets/1d631852-d6c8-4ae8-a3f3-16425a21d468" />


## Features

- **System** - OS、CPU、メモリ、.NET ランタイム、プロセス情報を一覧表示
- **Console** - リアルタイムログ表示（Debug/Info/Warning/Error フィルタ、テキスト検索、スタックトレース、重複ログ折りたたみ、リッチテキスト対応）
- **Options** - リフレクション+アトリビュートによるプロパティの自動 UI 生成 + 動的オプションコンテナ
  - 検索バー（カテゴリ名・オプション名・アクションラベルで横断検索）
  - カテゴリ折りたたみ（クリックで展開/折りたたみ、状態を保持）
  - 非同期アクションボタン（`Task` 戻り値対応、実行中スピナー＋成功/失敗フィードバック）
  - `[CRDescription]` 属性（オプション/アクションに説明テキストを表示）
  - `[CRColor]` 属性（カラースウォッチ＋HEX入力のカラーピッカー）
- **Profiler** - メモリ使用量、GC 統計（Gen0/1/2）、FPS 計測、ロジック単位プロファイリング（CPU/メモリ/処理時間/ネットワーク/ストレージ/GPU）
- **Bug Reporter** - スクリーンショット付きバグレポート送信
- **キーボードショートカット** - F1〜F5 でタブ切替、Esc で閉じる（カスタマイズ可能）
- **タブ制御** - タブの有効/無効を動的に切替可能
- **常に前面に固定** - ピン📌ボタンで Topmost を切替
- **テーマ** - ダーク / ライト / システム追従（アクリル効果対応）
- **エラーハンドリング** - ホストアプリをクラッシュさせない安全設計

## Packages

| Package | Description | NuGet |
|---------|-------------|-------|
| `CRDebugger.WinForms` | WinForms UI 実装 | [![NuGet](https://img.shields.io/nuget/v/CRDebugger.WinForms)](https://www.nuget.org/packages/CRDebugger.WinForms) |
| `CRDebugger.Wpf` | WPF UI 実装 | [![NuGet](https://img.shields.io/nuget/v/CRDebugger.Wpf)](https://www.nuget.org/packages/CRDebugger.Wpf) |
| `CRDebugger.Avalonia` | Avalonia UI 実装 | [![NuGet](https://img.shields.io/nuget/v/CRDebugger.Avalonia)](https://www.nuget.org/packages/CRDebugger.Avalonia) |

> 使用するプラットフォームのパッケージを1つインストールするだけでOKです。

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

var options = new CRDebuggerOptions
{
    Theme = CRTheme.Dark,
    DefaultTab = CRTab.Console,
    CollapseDuplicateLogs = true,
    EnableKeyboardShortcuts = true,
    Topmost = false,
};
options.UseWpf();           // or UseWinForms(), UseAvalonia()
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

// リッチテキストログ
CRDebugger.LogMarkup("<b>太字</b> <color=#FF0000>赤文字</color>");

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
    [CRDescription("0=最低画質 5=最高画質。シェーダー品質とテクスチャ解像度に影響")]
    [CRRange(0, 5, Step = 1)]
    public int QualityLevel { get; set; } = 3;

    [CRCategory("Graphics")]
    [CRDisplayName("フルスクリーン")]
    public bool IsFullscreen { get; set; }

    [CRCategory("Graphics")]
    [CRDisplayName("アクセントカラー")]
    [CRColor]
    public string AccentColor { get; set; } = "#7C8FFF";

    [CRCategory("Audio")]
    [CRDisplayName("マスター音量")]
    [CRRange(0, 100)]
    public int MasterVolume { get; set; } = 80;

    [CRCategory("Debug")]
    [CRAction(Label = "テストログ出力")]
    [CRDescription("Console タブにテストログを送信して表示を確認")]
    public void PrintTestLog()
    {
        CRDebugger.Log("テストログ！");
    }

    // Task 戻り値の非同期メソッドも対応（実行中はスピナー表示）
    [CRCategory("Debug")]
    [CRAction(Label = "非同期テスト")]
    [CRDescription("3秒間のダミー処理を実行してスピナー表示を確認")]
    public async Task AsyncTest()
    {
        await Task.Delay(3000);
        CRDebugger.Log("非同期テスト完了！");
    }
}

// 登録
CRDebugger.AddOptionContainer(new GameOptions());
```

### 動的オプションコンテナ

コードからオプションを動的に定義することもできます：

```csharp
using CRDebugger.Core.Options;

var dynamic = new DynamicOptionContainer("Runtime")
    .AddBool("Verbose Logging", () => verbose, v => verbose = v,
        description: "詳細ログを有効にする")
    .AddInt("Max Retries", () => retries, v => retries = v, min: 0, max: 10,
        description: "API呼び出しの最大リトライ回数")
    .AddColor("Theme Color", () => themeColor, v => themeColor = v,
        description: "UIのアクセントカラー (#RRGGBB)")
    .AddAction("Clear Cache", () => cache.Clear(),
        description: "全キャッシュデータを即時削除")
    .AddAsyncAction("Sync Data", async () => await SyncAsync(),
        description: "リモートサーバーとデータを同期（実行中はスピナー表示）");

CRDebugger.AddOptionContainer(dynamic);
```

### アトリビュート一覧

| アトリビュート | 対象 | 説明 |
|---|---|---|
| `[CRCategory("名前")]` | プロパティ / メソッド | カテゴリグループを指定（省略時は "General"） |
| `[CRDisplayName("名前")]` | プロパティ / メソッド | UI 表示名をカスタマイズ |
| `[CRDescription("説明")]` | プロパティ / メソッド | 説明テキストをサブテキスト表示 |
| `[CRRange(min, max)]` | 数値プロパティ | スライダーの範囲制約（`Step` も指定可能） |
| `[CRAction(Label = "名前")]` | `void` / `Task` メソッド | ボタンとして UI に表示 |
| `[CRColor]` | `string` プロパティ | カラースウォッチ＋HEX入力として表示 |
| `[CRSortOrder(n)]` | プロパティ / メソッド | カテゴリ内の表示順を指定（昇順） |

### 対応する型とコントロール

| 型 | コントロール |
|---|---|
| `bool` | トグルスイッチ |
| `int`, `float`, `double` | スライダー + 数値入力（`CRRange` 指定時） |
| `string` | テキストボックス |
| `string` + `[CRColor]` | カラースウォッチ + HEX入力 |
| `enum` | コンボボックス |
| `void` / `Task` メソッド + `[CRAction]` | ボタン（非同期対応、ステータス表示付き） |

## ロジック単位プロファイリング

処理時間、CPU使用率、メモリ消費、ネットワーク/ストレージI/Oをロジック単位で計測：

```csharp
// using パターン（最もシンプル）
using (CRDebugger.Profile("データベースクエリ", "DB"))
{
    await db.QueryAsync("SELECT ...");
}

// ラムダで計測
var result = CRDebugger.Measure("JSON解析", () => JsonSerializer.Deserialize<T>(json));

// 非同期計測
await CRDebugger.MeasureAsync("API呼び出し", () => httpClient.GetAsync(url));

// ホットスポット分析
var tracker = CRDebugger.GetOperationTracker();
var cpuHeavy = tracker.GetCpuHotspots(5);      // CPU使用率TOP5
var memHeavy = tracker.GetMemoryHotspots(5);    // メモリ消費TOP5
var slowOps  = tracker.GetDurationHotspots(5);  // 処理時間TOP5
```

## テーマ

3 種類のテーマをサポート（アクリル効果対応）：

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

## エラーハンドリング

CRDebugger はホストアプリをクラッシュさせない安全設計：

```csharp
// CRDebugger 由来の例外だけキャッチ
try { CRDebugger.Initialize(options); }
catch (CRDebuggerConfigurationException ex) { /* 設定ミス */ }
catch (CRDebuggerAlreadyInitializedException) { /* 二重初期化 */ }

// 内部エラーをモニタリング（クラッシュしない）
CRDebugger.InternalError += (_, ex) =>
    logger.LogWarning("CRDebugger内部エラー: {Message}", ex.Message);
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

var options = new CRDebuggerOptions
{
    BugReportSender = new MyBugReportSender()
};
```

## Requirements

- .NET 6.0 以上
- WinForms / WPF: Windows のみ
- Avalonia: Windows / macOS / Linux

## License

MIT License - Copyright (C) 2026 ゆろち
