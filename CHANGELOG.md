# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.26] - 2026-05-17

12 人分隊コードレビュー (`/rere`) に基づく大規模改修。ホスト保護・並行性・パフォーマンス・CI 品質を網羅的に強化。

### Added
- `CRDebuggerOptions.IsEnabled` フラグ（Release ビルドでの無効化セーフティ層）
- `CRDebuggerOptions.RequireOptInAttribute`（Options タブの opt-in モード — `CROption` 属性必須化）
- `CRDebuggerOptions.SystemInfoCollectionLevel` enum（`Minimal` / `Standard` / `Full`、デフォルト `Standard` で PII 除外）
- `CRDebuggerOptions.OptionsStore` + `IOptionsStore` + `JsonFileOptionsStore`（Options 値の永続化基盤）
- `CRDebuggerOptions.BugReportSendTimeout`（デフォルト 60 秒、`BugReporterViewModel` のハング回避）
- `CRDebuggerOptions.AttachToSuperLightLoggerManager`（デフォルト false、ホストの LogManager 設定保護）
- WPF Options タブで `[CRAction]` ボタン表示の実装
- Avalonia の `CaptureScreenshotAsync` を `RenderTargetBitmap` で実装
- WPF / WinForms に `Initialize(Action<CRDebuggerOptions>)` ヘルパー追加（Avalonia と API 対称化）
- `DynamicOptionContainer.AddDouble` / `AddLong` メソッド追加
- Source Link / snupkg / Deterministic Build / `Microsoft.SourceLink.GitHub` 有効化（本番デバッグ可能性確保）
- マルチ TFM テスト実行 (`net8.0;net10.0`)
- CI で偶数パッチバージョン検証 + `permissions: contents: read` + `dotnet test` ステップ
- CHANGELOG.md の新規作成（Keep a Changelog 形式）

### Changed
- `LogManager.Configure` をオプトイン化（ホストの SuperLightLogger 設定を破壊しない設計に変更）
- `OverrideSystemAccentColor` を Window スコープに閉じ込め（ホストアプリのアクセントカラー毀損を防止）
- Avalonia `DebuggerWindow.OnClosing` を条件分岐化（ShutdownMode 等を見てホストアプリ終了を阻害しない）
- `SystemInfoCollector` のデフォルト挙動: BugReport に `UserName` / `Command Line` を含めない（secure-by-default）
- `OperationTracker._metrics` に上限 1024 件 + FIFO eviction を追加（永久メモリリーク防止）
- `KeyboardShortcutManager` を `ConcurrentDictionary` 化
- `CRTraceListener` の `IsThreadSafe = true` + `[ThreadStatic]` + `FormatException` 握りつぶし
- WinForms `ConsolePanel` を差分適用化（コレクション変更 O(N²) → O(1)）
- `ConsoleViewModel` に 16ms バッチング Timer 追加（ログフラッディング対策）
- WPF / WinForms / Avalonia の `CaptureScreenshotAsync` を真の非同期化（UI スレッドフリーズ回避）
- `BugReportEngine.CreateAndSendAsync` を `Task.Run` でオフロード、スクショ失敗 fallback 追加
- Core ViewModel 群 (Debugger/Console/Profiler/Options/BugReporter/SystemInfo) を `IDisposable` 化
- 静的イベント (`PanelVisibilityChanged` / `InternalError`) を `CRDebugger.Shutdown` でクリア
- `ProfilerEngine.OnTick` 全体を try/catch で保護（Linux/macOS の `Process` API 例外で Timer 死を回避）
- `ThemeColors.Border` を半透明 `0x10FFFFFF` から不透明 `0xFF3A3A55` に変更（CLAUDE.md 規約準拠）
- Avalonia の `ItemsControl` に `VirtualizingStackPanel` を明示
- `Microsoft.Extensions.Logging.Abstractions` を `10.0.8` に統一（SuperLightLogger 1.0.7 互換）
- `SuperLightLogger` を `1.0.7` に更新
- `Microsoft.SourceLink.GitHub` を `10.0.300` に更新
- `Avalonia` / `Avalonia.Themes.Fluent` を `12.0.3` に更新
- `publish.ps1` をホワイトリスト方式（`CRDebugger.{WinForms,Wpf,Avalonia}` のみ）
- サンプルプロジェクトに `IsPackable=false` を明示
- `.gitignore` から `tests/` を削除（新規嫌がらせテスト追跡可能化）

### Fixed
- `IThemeProvider.StopMonitoring` が `Dispose` で呼ばれずイベント＆タイマーリーク（3 プラットフォーム共通）
- `SystemInfoCollector._customEntries` の並行アクセスでコレクション破壊する race
- `AvaloniaThemeProvider` のコールバック呼び出しで NRE race（ローカルキャプチャで解決）
- `TraceListener.Dispose` が `Trace.Listeners.Remove` の後で呼ばれていない問題
- WPF `WpfDebuggerWindow.Show` の `_viewModel` 上書き順序でハンドラ解除失敗
- `LogStore.EntryUpdated` イベントが `ConsoleViewModel` に購読されておらず重複折りたたみカウンタが UI 更新されない問題
- `OptionItemViewModel.Value` setter の型変換例外が UI に伝播してクラッシュ
- WPF Options タブの `FilteredActions` バインド欠落（`[CRAction]` ボタン表示不可）
- README の TFM 表記が `.NET 6.0 以上` で実際の `net8.0;net10.0` と乖離

### Removed
- WinForms `DebuggerForm.FindUiThread()` デッドメソッド
- WinForms `WinFormsDebuggerWindow._viewModel` デッドフィールド
- Avalonia `DebuggerWindow.OnCloseClick` デッドメソッド
- WPF `OptionsView.OnOptionItemsLoaded` 空メソッド

### Security
- BugReport の SystemInfo に `UserName` / `MachineName` / `CommandLine` / `CurrentDirectory` が無条件で含まれていた問題を修正（デフォルト `Standard` レベルで除外、`Command Line` は `--key=***` 自動マスキング、`Full` レベルで旧挙動可）
- `CRTraceListener.TraceEvent` の `string.Format` 例外がホストアプリに逆流する問題を修正
- `OperationTracker._metrics` の動的 operation name による永久メモリリーク経路を閉鎖

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
