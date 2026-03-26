using System.ComponentModel;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.WinForms.Controls;

namespace CRDebugger.WinForms.Panels;

/// <summary>
/// バグレポートパネル
/// ユーザーメッセージ、メールアドレスの入力フォームと送信ボタン
/// モダンデザイン: 改善されたスペーシング、フォント、ボタンスタイル
/// </summary>
public sealed class BugReporterPanel : Panel
{
    private readonly BugReporterViewModel _viewModel;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly TextBox _emailBox;
    private readonly TextBox _messageBox;
    private readonly Button _sendButton;
    private ThemeColors _colors;

    public BugReporterPanel(BugReporterViewModel viewModel, ThemeColors colors)
    {
        _viewModel = viewModel;
        _colors = colors;
        DoubleBuffered = true;

        // ヘッダー
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(16, 12, 16, 8),
        };

        _titleLabel = new Label
        {
            Text = "\u2709 Bug Reporter",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Left,
        };
        headerPanel.Controls.Add(_titleLabel);
        Controls.Add(headerPanel);

        // フォームレイアウト
        var formPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 12, 20, 20),
        };

        // ステータスラベル（下部）
        _statusLabel = new Label
        {
            Text = string.Empty,
            Dock = DockStyle.Bottom,
            Height = 32,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0),
        };
        formPanel.Controls.Add(_statusLabel);

        // 送信ボタン（下部）
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(0, 10, 0, 0),
        };

        _sendButton = new Button
        {
            Text = "\u2709 Send Bug Report",
            FlatStyle = FlatStyle.Flat,
            Height = 40,
            Dock = DockStyle.Fill,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
        };
        _sendButton.FlatAppearance.BorderSize = 0;
        _sendButton.Click += async (_, _) =>
        {
            _viewModel.SendCommand.Execute(null);
        };
        buttonPanel.Controls.Add(_sendButton);
        formPanel.Controls.Add(buttonPanel);

        // メールアドレス（下部）
        var emailPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            Padding = new Padding(0, 6, 0, 6),
        };

        var emailLabel = new Label
        {
            Text = "Email (optional):",
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font("Segoe UI", 9),
        };

        _emailBox = new TextBox
        {
            PlaceholderText = "your@email.com",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10),
        };
        _emailBox.TextChanged += (_, _) => _viewModel.UserEmail = _emailBox.Text;

        emailPanel.Controls.Add(_emailBox);
        emailPanel.Controls.Add(emailLabel);
        formPanel.Controls.Add(emailPanel);

        // メッセージラベル
        var msgLabel = new Label
        {
            Text = "Describe the bug:",
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI", 9),
            Padding = new Padding(0, 6, 0, 0),
        };
        formPanel.Controls.Add(msgLabel);

        // メッセージ入力（残り領域を使用）
        _messageBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10),
            AcceptsReturn = true,
        };
        _messageBox.TextChanged += (_, _) => _viewModel.UserMessage = _messageBox.Text;
        formPanel.Controls.Add(_messageBox);

        Controls.Add(formPanel);

        // ViewModelイベントの購読
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        ApplyTheme(colors);
    }

    public void ApplyTheme(ThemeColors colors)
    {
        _colors = colors;
        var bg = OptionControlFactory.ArgbToColor(colors.Background);
        var surface = OptionControlFactory.ArgbToColor(colors.Surface);
        var onBg = OptionControlFactory.ArgbToColor(colors.OnBackground);
        var onSurface = OptionControlFactory.ArgbToColor(colors.OnSurface);
        var surfaceAlt = OptionControlFactory.ArgbToColor(colors.SurfaceAlt);
        var primary = OptionControlFactory.ArgbToColor(colors.Primary);
        var success = OptionControlFactory.ArgbToColor(colors.Success);

        BackColor = bg;
        _titleLabel.ForeColor = onBg;
        _messageBox.BackColor = surfaceAlt;
        _messageBox.ForeColor = onSurface;
        _emailBox.BackColor = surfaceAlt;
        _emailBox.ForeColor = onSurface;
        _sendButton.BackColor = primary;
        _sendButton.ForeColor = Color.White;
        _statusLabel.ForeColor = success;

        foreach (Control c in Controls)
        {
            if (c is Panel p)
            {
                p.BackColor = surface;
                foreach (Control child in p.Controls)
                {
                    if (child is Label lbl && child != _statusLabel && child != _titleLabel)
                    {
                        lbl.ForeColor = OptionControlFactory.ArgbToColor(colors.OnSurfaceMuted);
                        lbl.BackColor = surface;
                    }
                    if (child is Panel subPanel)
                    {
                        subPanel.BackColor = surface;
                        foreach (Control subChild in subPanel.Controls)
                        {
                            if (subChild is Label subLbl)
                            {
                                subLbl.ForeColor = OptionControlFactory.ArgbToColor(colors.OnSurfaceMuted);
                                subLbl.BackColor = surface;
                            }
                        }
                    }
                }
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        void Update()
        {
            switch (e.PropertyName)
            {
                case nameof(BugReporterViewModel.StatusMessage):
                    _statusLabel.Text = _viewModel.StatusMessage;
                    // エラーメッセージの色分け
                    if (_viewModel.StatusMessage.Contains("失敗") || _viewModel.StatusMessage.Contains("入力"))
                        _statusLabel.ForeColor = OptionControlFactory.ArgbToColor(_colors.LogError);
                    else if (_viewModel.StatusMessage.Contains("送信中"))
                        _statusLabel.ForeColor = OptionControlFactory.ArgbToColor(_colors.LogWarning);
                    else
                        _statusLabel.ForeColor = OptionControlFactory.ArgbToColor(_colors.Success);
                    break;

                case nameof(BugReporterViewModel.IsSending):
                    _sendButton.Enabled = !_viewModel.IsSending;
                    _sendButton.Text = _viewModel.IsSending ? "Sending..." : "\u2709 Send Bug Report";
                    break;

                case nameof(BugReporterViewModel.UserMessage):
                    if (_messageBox.Text != _viewModel.UserMessage)
                        _messageBox.Text = _viewModel.UserMessage;
                    break;

                case nameof(BugReporterViewModel.UserEmail):
                    if (_emailBox.Text != _viewModel.UserEmail)
                        _emailBox.Text = _viewModel.UserEmail;
                    break;
            }
        }

        if (InvokeRequired)
        {
            try { Invoke(Update); } catch (ObjectDisposedException) { }
        }
        else
        {
            Update();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
        base.Dispose(disposing);
    }
}
