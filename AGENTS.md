# CLAUDE.md

This file provides guidance to Claude Code and other coding agents working in this repository.

## Build & Test Commands

```bash
# 全体ビルド
dotnet build CRDebugger.slnx

# テスト実行
dotnet test tests/CRDebugger.Core.Tests

# 単一テスト実行
dotnet test tests/CRDebugger.Core.Tests --filter "FullyQualifiedName~TestMethodName"

# NuGetパッケージ作成（3パッケージ）
dotnet pack src/CRDebugger.WinForms -c Release -o artifacts
dotnet pack src/CRDebugger.Wpf -c Release -o artifacts
dotnet pack src/CRDebugger.Avalonia -c Release -o artifacts

# NuGet公開は release/** ブランチから Publish to NuGet workflow を実行
gh workflow run publish.yml --ref release/x.y.z
```

## Architecture

### ソースリンク方式

CRDebugger.Core は **NuGetパッケージとして公開しない**（`IsPackable=false`）。各プラットフォームプロジェクトが Core の `.cs` ファイルを `<Compile Include>` で直接コンパイルし、単一DLLとして利用者に提供する。

```
CRDebugger.Core (IsPackable=false, 共有ソース)
  ↓ <Compile Include="..\CRDebugger.Core\**\*.cs" .../>
CRDebugger.Avalonia.dll  ← Core のコードを内包
CRDebugger.Wpf.dll       ← Core のコードを内包
CRDebugger.WinForms.dll  ← Core のコードを内包
```

この設計により利用者は `dotnet add package CRDebugger.Avalonia` だけで全機能が使える。`CRDebugger.Core` への推移的依存は発生しない。

### WPF XAML の注意

WPF の XAML で Core の型を参照する場合、`assembly=` を省略する（ソースリンクで同一アセンブリに含まれるため）:
```xml
xmlns:vm="clr-namespace:CRDebugger.Core.ViewModels"     ← 正しい
xmlns:vm="clr-namespace:CRDebugger.Core.ViewModels;assembly=CRDebugger.Core"  ← エラーになる
```

### Timer の曖昧参照

WinForms プロジェクトでは `System.Windows.Forms.Timer` と `System.Threading.Timer` が衝突する。Core 内では `System.Threading.Timer` と完全修飾で記述する。

### Avalonia スタイル

共通スタイルは `src/CRDebugger.Avalonia/Styles/SharedStyles.axaml` に定義。各 View では共通スタイルを再利用し、重複スタイルは書かない。カードは `cr-card` クラスを使用。

Avalonia では `AvaloniaUseCompiledBindingsByDefault=true` のため `x:DataType` の指定が必須。`IsVisible` に `int` を直接バインドすると型不一致エラーになるので `CountToVisibilityConverter` を使う。

### Avalonia 色指定の鉄則

**色は必ず不透明色（6桁 `#RRGGBB`）を使う**。半透明色 `#FFFFFFxx` を使うと FluentTheme の `SystemAccentColor`（ユーザーのOS設定に依存）が背景に流入し、黄色やピンク等の意図しない色になるため。

```
❌ Background="#FFFFFF06"  ← 半透明（OSアクセントカラーが透ける）
✅ Background="#252538"    ← 不透明（確実にダークブルー）
```

同様に `ExtendClientAreaToDecorationsHint="True"` は DWM タイトルバー背景（=SystemAccentColor）をクライアントエリアに流入させるため、使わずにダーククロームは下記 Win32 統合で明示指定する。

### Avalonia ControlTheme

FluentTheme の ToggleButton/Button がアクセントカラーを使う問題は、リソース上書きやスタイルセレクタでは解決できない。`ControlTheme` でテンプレートごと差し替えて完全バイパスする（`ConsoleView.axaml` の `FilterToggleTheme`、`DebuggerWindow.axaml` の `SidebarButtonTheme` を参照）。

### Avalonia Win32 統合

`DebuggerWindow.axaml.cs` の `ApplyDarkWindowChrome()` で Win32 DWM API を使用：
- `DWMWA_BORDER_COLOR` / `DWMWA_CAPTION_COLOR` — ウィンドウ枠線・タイトルバー色を強制指定
- `DWMWA_USE_IMMERSIVE_DARK_MODE` — ダークモードキャプションボタン
- `WS_EX_DLGMODALFRAME` + `WM_SETICON` — タイトルバーアイコン非表示

### SuperLightLogger 統合

CRDebugger は **SuperLightLogger** を使用してファイルログ出力をサポート。ログの流れ:
- `CRDebugger.Log()` → LogStore（コンソールUI） + SuperLightLogger（ファイル出力）
- `CRDebugger.GetLogger<T>()` で SuperLightLogger の ILog を直接取得可能
- `CRDebuggerOptions.FileLogPath` でファイル出力先を設定（null ならファイル出力なし）

## Version Management

- バージョンは `Directory.Build.props` の `<Version>` で一元管理

## CI/CD

- `release/**` ブランチへのプッシュで NuGet 公開ワークフローが発動
- 3パッケージのみ pack & publish（Core は対象外）
- NuGet.org Trusted Publishing が workflow と3パッケージを限定し、短期資格情報で公開

## Test Structure

テストは `tests/CRDebugger.Core.Tests/` に集約。xUnit + Moq を使用。
- `*.adversarial.test.cs` — 嫌がらせテスト（境界値、並行性、リソース枯渇、状態遷移、型パンチ、環境異常）
