# CRDebugger 設計

この文書は、現在のコードと設定に基づくシステム設計の正本である。利用方法は [README.md](README.md)、作業規約と検証手順は [AGENTS.md](AGENTS.md) を参照する。

## 目的と範囲

CRDebugger は .NET デスクトップアプリへ組み込むランタイムデバッグパネルである。ホストアプリを停止せずに、システム情報、ログ、実行時オプション、プロファイリング、バグレポートを WinForms / WPF / Avalonia の各UIで提供する。単独で動くアプリや外部バックエンドは持たず、保存先・GPU計測・バグレポート送信先はホスト側から差し替える。

## パッケージと境界

| コンポーネント | 責務 |
| --- | --- |
| `CRDebugger.Core` | ファサード、サービス、ViewModel、フレームワーク非依存の抽象。`IsPackable=false` の共有ソース。 |
| `CRDebugger.WinForms` | WinForms のウィンドウ、パネル、UIスレッド、テーマ監視。 |
| `CRDebugger.Wpf` | WPF のウィンドウ、XAML View、UIスレッド、テーマ監視。 |
| `CRDebugger.Avalonia` | Avalonia のウィンドウ、AXAML View、UIスレッド、テーマ監視。 |
| `samples/*` | 各プラットフォームの組み込み例。 |
| `CRDebugger.Core.Tests` | Coreの公開契約、境界値、並行性、状態遷移、資源解放の回帰検証。 |

各UIプロジェクトは Core の `.cs` を `<Compile Include>` で直接コンパイルする。利用者へ配布するのは3つのプラットフォームパッケージだけで、各パッケージは単一DLLに Core を内包し、別の `CRDebugger.Core` パッケージへ推移的依存しない。

## 構成とライフサイクル

```text
ホストアプリ
  -> UseWinForms / UseWpf / UseAvalonia
  -> CRDebuggerOptions
  -> CRDebugger.Initialize
  -> CRDebuggerContext
       -> Coreサービス群
       -> タブ別ViewModel
       -> IDebuggerWindow / IUiThread / IThemeProvider
```

1. プラットフォーム拡張が `IDebuggerWindow`、`IUiThread`、`IThemeProvider` を `CRDebuggerOptions` へ登録する。
2. 静的ファサード `CRDebugger` が初期化を直列化し、`CRDebuggerContext` を1つだけ保持する。`IsEnabled=false` はコンテキストを作らず、以後の公開APIを no-op にする。
3. `CRDebuggerContext` が Core サービスを依存順に構築し、ViewModelへ配線する。必要に応じて Trace、未処理例外、OSテーマ監視を購読し、プロファイラーを開始する。
4. `Show` はルートViewModelをプラットフォームウィンドウへ渡し、UI実装が表示とスクリーンショット取得を担当する。
5. `Shutdown` はタイマーとテーマ監視を停止し、ViewModel購読、TraceListener、未処理例外ハンドラーを解除し、Optionsストアをフラッシュする。完了後は再初期化できる。

## 主要サービスとデータフロー

### ログ

`CRDebugger.Log*`、`Microsoft.Extensions.Logging`、`System.Diagnostics.Trace`、未処理例外を `LogStore` へ集約し、`ConsoleViewModel` がフィルター済みの表示状態へ変換する。`LogStore` はロック付き循環バッファで件数を制限し、連続する同一ログを任意に折りたたむ。`CRDebugger.Log*` は同時に SuperLightLogger へも書き込むが、グローバルなファイルロガー構成は `AttachToSuperLightLoggerManager=true` と `FileLogPath` の両方が指定された場合だけ行う。

### 実行時オプション

`OptionsEngine` は登録オブジェクトの public プロパティと `[CRAction]` メソッドを記述子へ変換し、`DynamicOptionContainer` は既成の記述子を直接提供する。ViewModelと各UIは同じ記述子からコントロールを生成する。`IOptionsStore` がある場合は値をID単位で初回だけ復元し、元のセッター成功後に保存する。壊れた保存値や保存失敗はホスト動作を止めない。

### プロファイリング

`ProfilerEngine` は `System.Threading.Timer` でCPU、メモリ、GC、FPS、任意のGPU情報を定期採取し、上限付き履歴へ `ProfilerSnapshot` を保存する。`OperationTracker` は `Profile` / `Measure` / `MeasureAsync` の処理時間・CPU・メモリと、明示記録されたネットワーク/ストレージ量をロジック名ごとに集計する。OS APIやイベント購読者の失敗はサンプリングを停止させない。

### システム情報とバグレポート

`SystemInfoCollector` は収集レベルに応じてOS、ランタイム、プロセス、アプリ情報を集める。`Minimal` は識別情報を抑え、`Standard` はコマンドラインの `--key=value` をマスクし、`Full` だけがユーザー名やパス等を含める。`BugReportEngine` はユーザー入力、システム情報、直近ログ、任意のPNGスクリーンショットを不変な `BugReport` にまとめ、`IBugReportSender` へタイムアウト付きで渡す。送信側の `false` は成功扱いにせず専用例外へ変換する。

### テーマと入力

`ThemeManager` がテーマ種別と解決済みカラーを管理し、プラットフォームの `IThemeProvider` がOSテーマ変化を通知する。UI更新は `IUiThread` を通す。ショートカットは Core の `KeyboardShortcutManager` が管理し、各UIがキー入力を転送する。Avalonia はOSアクセント色の流入を避けるため、不透明色と独自 `ControlTheme` を使った固定ダーク配色を採用する。

## 重要な不変条件

- `CRDebugger.Initialize` は同時に1コンテキストだけを許可し、二重初期化は専用例外にする。
- 有効状態の初期化には `IDebuggerWindow` と `IUiThread` が必須であり、UIフレームワーク固有処理は Core 抽象の外側へ置く。
- 公開APIの既知の契約違反は専用例外で通知し、予期しない内部失敗は `InternalError` へ通知してホストへ逆流させない。
- ログ、プロファイル履歴、操作メトリクスは上限を持ち、共有状態はロックまたは並行コレクションで保護する。
- UIスレッド境界を越える通知は `IUiThread` でマーシャリングする。
- Trace、AppDomainイベント、OSテーマ監視、タイマーは `Shutdown` で解除・破棄する。
- ファイルログ、Options永続化、GPU監視、バグレポート送信は明示設定された場合だけ外部状態へ接続する。

## 採用済み設計判断

| 判断 | 理由とトレードオフ |
| --- | --- |
| Coreを共有ソースとして各UI DLLへ内包 | 1パッケージだけで導入できる一方、Core変更は3パッケージすべてのビルド・テストが必要。 |
| 静的ファサード + 内部コンテキスト | ホストから簡潔に呼べる一方、ライフサイクルを明示的な `Initialize` / `Shutdown` で管理する必要がある。 |
| CoreのViewModelを全UIで共有 | 機能と状態遷移を揃えられる一方、表示差は各UIアダプターで吸収する。 |
| 反射スキャンと動的記述子の併用 | 既存オブジェクトを少ない記述で公開しつつ、実行時生成も扱える。反射結果はキャッシュし、動的コンテナは毎回読み直す。 |
| 容量制限と失敗隔離を既定化 | デバッグ機能によるメモリ増大やホスト停止を避ける代わりに、古い履歴や取得不能な補助情報は捨てる。 |

## ビルドと配布

対象TFMは .NET 8 / .NET 10、WinFormsとWPFはWindows、Avaloniaはクロスプラットフォームである。バージョンは `Directory.Build.props` で一元管理する。`release/**` の公開ワークフローが3つのUIパッケージをビルド・テスト・packし、NuGet Trusted Publishingで公開する。Coreは公開対象に含めない。
