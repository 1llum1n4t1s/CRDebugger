# AGENTS.md

This file provides guidance to Claude Code and other coding agents working in this repository.
システム構造と設計判断の正本は [DESIGN.md](DESIGN.md) を参照する。

## Build & Test Commands

```bash
# 全体ビルド
dotnet build CRDebugger.slnx

# テスト実行
dotnet test --project tests/CRDebugger.Core.Tests/CRDebugger.Core.Tests.csproj --minimum-expected-tests 1

# 単一テスト実行
dotnet test --project tests/CRDebugger.Core.Tests/CRDebugger.Core.Tests.csproj --minimum-expected-tests 1 --filter "FullyQualifiedName~TestMethodName"

# NuGetパッケージ作成（3パッケージ）
dotnet pack src/CRDebugger.WinForms -c Release -o artifacts
dotnet pack src/CRDebugger.Wpf -c Release -o artifacts
dotnet pack src/CRDebugger.Avalonia -c Release -o artifacts

# NuGet公開は release/** ブランチから Publish to NuGet workflow を実行
gh workflow run publish.yml --ref release/x.y.z
```

## Architecture

### Core 変更時の検証

共有ソースの構造と理由は [DESIGN.md](DESIGN.md#パッケージと境界) を参照する。Core 変更時は Core テストに加え、全体ビルドで3つのUIプロジェクトへの組み込みを検証する。Core は公開対象に含めない。

### UIスレッドとライフサイクル

`Show` / `Hide` / `Toggle` / `SetTheme` / `SetTabEnabled` は UI スレッドから呼ぶ公開契約であり、失敗は呼び出し元へ伝播する。同期的な `IUiThread.Invoke` で包んでバックグラウンド呼び出しを許容すると WPF / WinForms でデッドロック経路を増やすため、この境界を変えない。ログやプロファイラー等から届くバックグラウンド通知だけを `IUiThread` で UI へ配送する。

初期化・終了処理を変更するときは、途中失敗時の購読解除、組み込みウィンドウの破棄、`PanelVisibilityChanged` の解除、例外後も再初期化できる状態への復帰を `tests/CRDebugger.Core.Tests/CRDebuggerFacade.adversarial.test.cs` で検証する。

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

### ログと例外の境界

ログの出力先と例外伝播の境界は [DESIGN.md](DESIGN.md#ログ) を参照する。CRDebugger は外部ロガーやファイル出力を構成せず、公開APIの失敗は呼び出し元へ伝播する。変更時は `tests/CRDebugger.Core.Tests/CRDebuggerFacade.adversarial.test.cs` でログストアへの記録と元の例外インスタンスの伝播を検証する。

## Version Management

- バージョンは `Directory.Build.props` の `<Version>` で一元管理。公開ワークフローは patch が偶数かつ 999 未満であることを要求する。
- `CRDebugger.Avalonia` の直接参照元、更新対象ファイル、復元条件、検証コマンドは、リポジトリ直下の `vava.config.json` を正本とする。直接参照元を追加・削除したときは、同じ変更内で `consumerUpdates.targets` を同期する。

## CI/CD

- `release/**` ブランチへのプッシュで NuGet 公開ワークフローが発動
- 3パッケージのみ pack & publish（Core は対象外）
- NuGet.org Trusted Publishing が workflow と3パッケージを限定し、短期資格情報で公開

## Test Structure

テストは `tests/CRDebugger.Core.Tests/` に集約。xUnit + Moq を使用。
- `global.json` で Microsoft Testing Platform を選択している。プロジェクト指定は `--project`、TRX出力は xUnit の `--report-xunit-trx` を使う。
- `*.adversarial.test.cs` — 嫌がらせテスト（境界値、並行性、リソース枯渇、状態遷移、型パンチ、環境異常）
