using System.Windows.Input;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.BugReporter;

namespace CRDebugger.Core.ViewModels;

public sealed class BugReporterViewModel : ViewModelBase
{
    private readonly BugReportEngine _engine;
    private readonly IDebuggerWindow _window;
    private string _userMessage = string.Empty;
    private string _userEmail = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isSending;

    public string UserMessage
    {
        get => _userMessage;
        set => SetProperty(ref _userMessage, value);
    }

    public string UserEmail
    {
        get => _userEmail;
        set => SetProperty(ref _userEmail, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsSending
    {
        get => _isSending;
        set => SetProperty(ref _isSending, value);
    }

    public ICommand SendCommand { get; }

    public BugReporterViewModel(BugReportEngine engine, IDebuggerWindow window)
    {
        _engine = engine;
        _window = window;
        SendCommand = new RelayCommand(async () => await SendAsync());
    }

    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(UserMessage))
        {
            StatusMessage = "メッセージを入力してください。";
            return;
        }

        IsSending = true;
        StatusMessage = "送信中...";

        try
        {
            await _engine.CreateAndSendAsync(
                UserMessage,
                UserEmail,
                () => _window.CaptureScreenshotAsync()
            ).ConfigureAwait(false);

            StatusMessage = "バグレポートを送信しました！";
            UserMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"送信失敗: {ex.Message}";
        }
        finally
        {
            IsSending = false;
        }
    }
}
