# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- `CRDebuggerOptions.IsEnabled` フラグ（Release ビルドでの完全無効化対応）
- `CRDebuggerOptions.RequireOptInAttribute`（Options タブの opt-in モード — 明示属性付与のみ表示）
- `CRDebuggerOptions.SystemInfoCollectionLevel` enum（システム情報収集レベルで PII 除外を制御）
- `CRDebuggerOptions.OptionsStore` + `IOptionsStore` + `JsonFileOptionsStore`（Options 値の永続化）
- WPF Options タブで `[CRAction]` ボタンの表示対応
- Avalonia の `CaptureScreenshotAsync` 実装
- Source Link / snupkg / Deterministic Build を有効化
- マルチ TFM テスト実行 (`net8.0;net10.0`)
- CI で偶数パッチバージョンガード（リリース時の意図しない奇数バージョン公開を防止）
- CI で `dotnet test` ステップを追加

### Changed
- `Microsoft.Extensions.Logging.Abstractions` を TFM 別バージョンに分離（net8.0 → 8.0.2 / net10.0 → 10.0.6）
- `publish.ps1` をホワイトリスト方式に変更（`CRDebugger.{WinForms,Wpf,Avalonia}` のみ公開対象）
- サンプルプロジェクトに `IsPackable=false` を明示

### Fixed
- ホストアプリの SuperLightLogger 設定が CRDebugger により上書きされる問題

## [1.0.24] - 2026

### Added
- SuperLightLogger 統合（ファイルログ出力サポート、`CRDebuggerOptions.FileLogPath` で出力先設定）
- 嫌がらせテスト（adversarial test）追加 — 境界値・並行性・リソース枯渇・状態遷移の検証

### Changed
- パフォーマンス最適化（ログストア・コンソール描画パス）

## [1.0.22] - 2026

### Added
- Avalonia 12 対応
- .NET 8 / .NET 10 限定サポート（旧 TFM 削除）

## [1.0.20] - 2026

### Added
- Options タブ大幅改善 — 検索機能・カテゴリ折りたたみ・非同期ボタン・説明属性・カラーピッカー

## [1.0.18] - 2026

### Added
- CPU / メモリホットスポット表示（プロファイラタブ）

### Changed
- コード品質改善（リファクタリング、命名統一）

## [1.0.16] - 2026

### Added
- 全ソースに日本語コメント追加

### Fixed
- Avalonia の意図しない黄色化（FluentTheme `SystemAccentColor` 流入）を修正
- 全 View に `SharedStyles.axaml` の `StyleInclude` を追加し、スタイル重複を解消

## [1.0.14] - 2026

### Fixed
- コンソールの黄色背景を除去
- 閉じる / ピンボタンをコンテンツエリア右上に移動

## [1.0.12] - 2026

### Fixed
- `OptionsView` の Action ボタンスタイル修正
- Items 空時にカードを非表示化

## [1.0.10] - 2026

### Changed
- Core ソースリンク方式に移行 — NuGet パッケージから `CRDebugger.Core` 依存を完全除去
- 各プラットフォーム DLL に Core のソースを `<Compile Include>` で取り込む単一 DLL 構成

## [1.0.8] - 2026

### Fixed
- NuGet パッケージから `CRDebugger.Core` 依存を一時的に除去（後に 1.0.10 で正式対応）

## [1.0.6] - 2026

### Added
- SRDebugger 風の「常に前面に固定」ピンボタン

### Fixed
- `OptionsView` の Action 表示修正、閉じるボタン復活

## [1.0.4] - 2026

### Added
- CRDebugger 初版機能の大幅追加（コンソール、Options、プロファイラ、システム情報タブ）
- UI モダン化 — アクリル効果・丸角・アニメーション導入
- テスト整備（xUnit + Moq）
- NuGet 公開設定
