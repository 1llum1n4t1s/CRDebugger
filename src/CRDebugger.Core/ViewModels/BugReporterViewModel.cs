using System.Windows.Input;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.BugReporter;

namespace CRDebugger.Core.ViewModels;

/// <summary>
/// バグレポート画面のViewModel。
/// ユーザーが入力したメッセージ・メールアドレスをもとにバグレポートを作成し、
/// <see cref="BugReportEngine"/> 経由で送信する機能を提供する。
/// </summary>
public sealed class BugReporterViewModel : ViewModelBase
{
    /// <summary>バグレポートの作成・送信処理を担うエンジン</summary>
    private readonly BugReportEngine _engine;

    /// <summary>スクリーンショット取得元となるデバッガーウィンドウ</summary>
    private readonly IDebuggerWindow _window;

    /// <summary>ユーザーが入力したバグの説明テキスト（バッキングフィールド）</summary>
    private string _userMessage = string.Empty;

    /// <summary>ユーザーの連絡先メールアドレス（バッキングフィールド）</summary>
    private string _userEmail = string.Empty;

    /// <summary>送信処理の状態を示すメッセージ（バッキングフィールド）</summary>
    private string _statusMessage = string.Empty;

    /// <summary>送信処理中フラグ（バッキングフィールド）</summary>
    private bool _isSending;

    /// <summary>
    /// 送信処理の再入ガード（0 = 待機中、1 = 送信中）。
    /// <see cref="IsSending"/> は UI バインド用のプロパティで書き込み順序の保証が無いため、
    /// 実際の排他は Interlocked で行う。
    /// </summary>
    private int _sendGuard;

    /// <summary>送信処理のキャンセル制御用 CTS（送信のたびに作り直し、Dispose で解放）</summary>
    private CancellationTokenSource? _sendCts;

    /// <summary>
    /// ユーザーが入力したバグの説明。
    /// 送信前に空文字チェックが行われる。
    /// </summary>
    public string UserMessage
    {
        get => _userMessage;
        set => SetProperty(ref _userMessage, value);
    }

    /// <summary>
    /// ユーザーの連絡先メールアドレス。
    /// 任意項目であり、空文字でも送信可能。
    /// </summary>
    public string UserEmail
    {
        get => _userEmail;
        set => SetProperty(ref _userEmail, value);
    }

    /// <summary>
    /// 送信状態を示すメッセージ。
    /// 送信中・送信成功・送信失敗の各状態でUIに表示される。
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// 送信処理中かどうかを示すフラグ。
    /// <c>true</c> の間はUIの送信ボタンを無効化する用途で使用される。
    /// </summary>
    public bool IsSending
    {
        get => _isSending;
        set
        {
            // 送信中フラグが変わったら、送信コマンドの実行可否を UI に再評価させる
            if (SetProperty(ref _isSending, value))
                _sendCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// バグレポートを送信するコマンド。
    /// 実行すると <see cref="SendAsync"/> が呼び出される。
    /// 送信中は <c>CanExecute</c> が false になり、UI 側のボタンが無効化される。
    /// </summary>
    public ICommand SendCommand => _sendCommand;

    /// <summary>
    /// <see cref="SendCommand"/> の実体。
    /// <see cref="RelayCommand.RaiseCanExecuteChanged"/> を呼ぶために具象型で保持する。
    /// </summary>
    private readonly RelayCommand _sendCommand;

    /// <summary>
    /// <see cref="BugReporterViewModel"/> のインスタンスを生成する
    /// </summary>
    /// <param name="engine">バグレポートの作成・送信を担うエンジン</param>
    /// <param name="window">スクリーンショット取得用のウィンドウインターフェース</param>
    public BugReporterViewModel(BugReportEngine engine, IDebuggerWindow window)
    {
        _engine = engine;
        _window = window;
        // 非同期送信処理をRelayCommandでラップしてコマンドとして公開。
        // 送信中は CanExecute を false にして UI 側のボタンを無効化する（連打対策の一次防御）。
        _sendCommand = new RelayCommand(async () => await SendAsync(), () => !IsSending);
    }

    /// <summary>
    /// バグレポートを非同期で送信する内部処理。
    /// メッセージが空の場合はバリデーションエラーを表示して処理を中断する。
    /// スクリーンショットの取得、エンジンによるレポート作成・送信を順に行う。
    /// 60 秒のタイムアウト CancellationToken を内部生成し、ハング時に強制的に送信を打ち切る (#20)。
    /// </summary>
    private async Task SendAsync()
    {
        // 必須項目であるメッセージが未入力の場合はエラーメッセージを表示して早期リターン
        if (string.IsNullOrWhiteSpace(UserMessage))
        {
            StatusMessage = "メッセージを入力してください。";
            return;
        }

        // 再入ガード。送信中に再度実行された場合は何もしない。
        // ガードが無いと、下の _sendCts?.Dispose() が進行中の送信のトークンを破棄してしまい、
        // 1 本目の送信が ObjectDisposedException で落ちる（かつキャンセル制御も効かなくなる）。
        if (Interlocked.CompareExchange(ref _sendGuard, 1, 0) != 0)
            return;

        // 送信開始状態に移行
        IsSending = true;
        StatusMessage = "送信中...";

        // 前回の CTS を破棄してから新規生成（リソースリーク防止）。
        // 再入ガードにより、ここで破棄されるのは必ず完了済みの送信の CTS になる。
        _sendCts?.Dispose();
        _sendCts = new CancellationTokenSource();
        _sendCts.CancelAfter(TimeSpan.FromSeconds(60));

        try
        {
            // エンジン経由でスクリーンショット付きバグレポートを作成・送信
            // .ConfigureAwait(false) は付けない — UI スレッドに戻って StatusMessage / IsSending を
            // 更新する必要があるため（INotifyPropertyChanged の発火は UI スレッドが望ましい）(#19)
            await _engine.CreateAndSendAsync(
                UserMessage,
                UserEmail,
                () => _window.CaptureScreenshotAsync(),
                _sendCts.Token
            );

            // 送信成功時の状態更新
            StatusMessage = "バグレポートを送信しました！";
            // 送信済みメッセージをクリア（メールアドレスは保持）
            UserMessage = string.Empty;
        }
        catch (OperationCanceledException)
        {
            // タイムアウト or 明示的キャンセル時の状態更新
            StatusMessage = "送信がタイムアウトまたはキャンセルされました。";
        }
        catch (Exception ex)
        {
            // 送信失敗時は例外メッセージをユーザーに提示
            StatusMessage = $"送信失敗: {ex.Message}";
        }
        finally
        {
            // 成功・失敗にかかわらず送信中フラグと再入ガードを解除する。
            // ガードの解除を忘れると以降の送信が永久にブロックされるため必ず finally で行う。
            IsSending = false;
            Interlocked.Exchange(ref _sendGuard, 0);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 進行中の送信があれば中断＆ CTS を解放
            try { _sendCts?.Cancel(); } catch { /* 解放経路で握りつぶし */ }
            _sendCts?.Dispose();
            _sendCts = null;
        }

        base.Dispose(disposing);
    }
}
