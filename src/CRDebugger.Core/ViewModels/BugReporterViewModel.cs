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
        set => SetProperty(ref _isSending, value);
    }

    /// <summary>
    /// バグレポートを送信するコマンド。
    /// 実行すると <see cref="SendAsync"/> が呼び出される。
    /// </summary>
    public ICommand SendCommand { get; }

    /// <summary>
    /// <see cref="BugReporterViewModel"/> のインスタンスを生成する
    /// </summary>
    /// <param name="engine">バグレポートの作成・送信を担うエンジン</param>
    /// <param name="window">スクリーンショット取得用のウィンドウインターフェース</param>
    public BugReporterViewModel(BugReportEngine engine, IDebuggerWindow window)
    {
        _engine = engine;
        _window = window;
        // 非同期送信処理をRelayCommandでラップしてコマンドとして公開
        SendCommand = new RelayCommand(async () => await SendAsync());
    }

    /// <summary>
    /// バグレポートを非同期で送信する内部処理。
    /// メッセージが空の場合はバリデーションエラーを表示して処理を中断する。
    /// スクリーンショットの取得、エンジンによるレポート作成・送信を順に行う。
    /// </summary>
    private async Task SendAsync()
    {
        // 必須項目であるメッセージが未入力の場合はエラーメッセージを表示して早期リターン
        if (string.IsNullOrWhiteSpace(UserMessage))
        {
            StatusMessage = "メッセージを入力してください。";
            return;
        }

        // 送信開始状態に移行
        IsSending = true;
        StatusMessage = "送信中...";

        try
        {
            // エンジン経由でスクリーンショット付きバグレポートを作成・送信
            await _engine.CreateAndSendAsync(
                UserMessage,
                UserEmail,
                () => _window.CaptureScreenshotAsync()
            ).ConfigureAwait(false);

            // 送信成功時の状態更新
            StatusMessage = "バグレポートを送信しました！";
            // 送信済みメッセージをクリア（メールアドレスは保持）
            UserMessage = string.Empty;
        }
        catch (Exception ex)
        {
            // 送信失敗時は例外メッセージをユーザーに提示
            StatusMessage = $"送信失敗: {ex.Message}";
        }
        finally
        {
            // 成功・失敗にかかわらず送信中フラグを解除
            IsSending = false;
        }
    }
}
