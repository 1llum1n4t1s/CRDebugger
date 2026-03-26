# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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

# NuGet公開（NUGET_API_KEY環境変数が必要）
.\publish.ps1
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

WPF の XAML で Core の型を参照する場合、`assembly=` を省略すること（ソースリンクで同一アセンブリに含まれるため）:
```xml
xmlns:vm="clr-namespace:CRDebugger.Core.ViewModels"     ← 正しい
xmlns:vm="clr-namespace:CRDebugger.Core.ViewModels;assembly=CRDebugger.Core"  ← エラーになる
```

### Timer の曖昧参照

WinForms プロジェクトでは `System.Windows.Forms.Timer` と `System.Threading.Timer` が衝突する。Core 内では `System.Threading.Timer` と完全修飾で記述すること。

### Avalonia スタイル

共通スタイルは `src/CRDebugger.Avalonia/Styles/SharedStyles.axaml` に定義。各 View で重複スタイルを書かないこと。カードは `cr-card` クラスを使用。

Avalonia では `AvaloniaUseCompiledBindingsByDefault=true` のため `x:DataType` の指定が必須。`IsVisible` に `int` を直接バインドすると型不一致エラーになるので `CountToVisibilityConverter` を使うこと。

## Version Rules

- パッチバージョンは **偶数のみ** 使用（1.0.0, 1.0.2, 1.0.4, ...）
- 1.0.998 の次は **1.1.0**（1.0.999 は使わない）
- バージョンは `Directory.Build.props` の `<Version>` で一元管理

## CI/CD

- `release/**` ブランチへのプッシュで NuGet 公開ワークフローが発動
- 3パッケージのみ pack & publish（Core は対象外）
- `publish.ps1` が `artifacts/` 内の全 `.nupkg` を nuget.org にプッシュ

## Test Structure

テストは `tests/CRDebugger.Core.Tests/` に集約。xUnit + Moq を使用。
- `*.adversarial.test.cs` — 嫌がらせテスト（境界値、並行性、リソース枯渇、状態遷移、型パンチ、環境異常）
