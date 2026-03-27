using System.ComponentModel;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.WinForms.Controls;

namespace CRDebugger.WinForms.Panels;

/// <summary>
/// バグレポートパネル。
/// ユーザーがバグの説明メッセージとメールアドレスを入力して送信できるフォームを提供する。
/// 送信状態に応じてボタンの有効/無効切り替えとステータスメッセージの色変更を行う。
/// モダンデザイン: 改善されたスペーシング、フォント、ボタンスタイルを採用。
/// </summary>
public sealed class BugReporterPanel : Panel
{
    /// <summary>バグレポート送信ロジックを持つ ViewModel。</summary>
    private readonly BugReporterViewModel _viewModel;

    /// <summary>パネルタイトルを表示するラベル。</summary>
    private readonly Label _titleLabel;

    /// <summary>送信結果や入力エラーを表示するステータスラベル。</summary>
    private readonly Label _statusLabel;

    /// <summary>ユーザーのメールアドレス入力欄。</summary>
    private readonly TextBox _emailBox;

    /// <summary>バグ説明テキストの複数行入力欄。</summary>
    private readonly TextBox _messageBox;

    /// <summary>バグレポート送信ボタン。送信中は無効化される。</summary>
    private readonly Button _sendButton;

    /// <summary>現在適用中のテーマカラー。</summary>
    private ThemeColors _colors;

    /// <summary>
    /// <see cref="BugReporterPanel"/> を初期化してUIコントロールを構築する。
    /// ViewModel のプロパティ変更イベントを購読して双方向バインディングを設定する。
    /// </summary>
    /// <param name="viewModel">バグレポート送信ロジックを持つ <see cref="BugReporterViewModel"/>。</param>
    /// <param name="colors">初期適用するテーマカラー情報。</param>
    public BugReporterPanel(BugReporterViewModel viewModel, ThemeColors colors)
    {
        _viewModel = viewModel;
        _colors = colors;
        // ダブルバッファリングで描画のちらつきを防ぐ
        DoubleBuffered = true;

        // ヘッダーパネルを生成（タイトルラベルを配置）
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(16, 12, 16, 8),
        };

        // パネルタイトル（封筒アイコン + Bug Reporter）
        _titleLabel = new Label
        {
            Text = "\u2709 Bug Reporter",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Left,
        };
        headerPanel.Controls.Add(_titleLabel);
        Controls.Add(headerPanel);

        // フォームレイアウトパネル（入力欄と送信ボタンを格納）
        var formPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 12, 20, 20),
        };

        // ステータスラベル（下部に固定配置、送信結果や入力エラーを表示）
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

        // 送信ボタンのコンテナパネル（下部に固定配置）
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(0, 10, 0, 0),
        };

        // バグレポート送信ボタン
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
        // クリック時に ViewModel の SendCommand を実行
        _sendButton.Click += (_, _) => _viewModel.SendCommand.Execute(null);
        buttonPanel.Controls.Add(_sendButton);
        formPanel.Controls.Add(buttonPanel);

        // メールアドレス入力パネル（下部に固定配置）
        var emailPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            Padding = new Padding(0, 6, 0, 6),
        };

        // メールアドレスフィールドのラベル
        var emailLabel = new Label
        {
            Text = "Email (optional):",
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font("Segoe UI", 9),
        };

        // メールアドレス入力テキストボックス（プレースホルダー付き）
        _emailBox = new TextBox
        {
            PlaceholderText = "your@email.com",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10),
        };
        // テキスト変更時に ViewModel の UserEmail を更新
        _emailBox.TextChanged += (_, _) => _viewModel.UserEmail = _emailBox.Text;

        emailPanel.Controls.Add(_emailBox);
        emailPanel.Controls.Add(emailLabel);
        formPanel.Controls.Add(emailPanel);

        // バグ説明フィールドのラベル
        var msgLabel = new Label
        {
            Text = "Describe the bug:",
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI", 9),
            Padding = new Padding(0, 6, 0, 0),
        };
        formPanel.Controls.Add(msgLabel);

        // バグ説明入力テキストボックス（複数行、縦スクロール付き）
        // 残り領域（Dock.Fill）を使用して最大限の入力スペースを確保
        _messageBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10),
            AcceptsReturn = true,
        };
        // テキスト変更時に ViewModel の UserMessage を更新
        _messageBox.TextChanged += (_, _) => _viewModel.UserMessage = _messageBox.Text;
        formPanel.Controls.Add(_messageBox);

        Controls.Add(formPanel);

        // ViewModelイベントの購読（ステータス・送信状態・入力値の変化を監視）
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // 初期テーマを適用
        ApplyTheme(colors);
    }

    /// <summary>
    /// 指定したテーマカラーをパネル全体に適用する。
    /// 背景色、テキスト色、ボタン色などを一括更新する。
    /// </summary>
    /// <param name="colors">適用するテーマカラー情報。</param>
    public void ApplyTheme(ThemeColors colors)
    {
        _colors = colors;
        // 各色をテーマカラーから変換
        var bg = OptionControlFactory.ArgbToColor(colors.Background);
        var surface = OptionControlFactory.ArgbToColor(colors.Surface);
        var onBg = OptionControlFactory.ArgbToColor(colors.OnBackground);
        var onSurface = OptionControlFactory.ArgbToColor(colors.OnSurface);
        var surfaceAlt = OptionControlFactory.ArgbToColor(colors.SurfaceAlt);
        var primary = OptionControlFactory.ArgbToColor(colors.Primary);
        var success = OptionControlFactory.ArgbToColor(colors.Success);

        // 各コントロールにテーマカラーを適用
        BackColor = bg;
        _titleLabel.ForeColor = onBg;
        _messageBox.BackColor = surfaceAlt;
        _messageBox.ForeColor = onSurface;
        _emailBox.BackColor = surfaceAlt;
        _emailBox.ForeColor = onSurface;
        _sendButton.BackColor = primary;
        _sendButton.ForeColor = Color.White;
        _statusLabel.ForeColor = success;

        // 子パネルおよびその中のラベルにテーマカラーを再帰適用
        foreach (Control c in Controls)
        {
            if (c is Panel p)
            {
                p.BackColor = surface;
                foreach (Control child in p.Controls)
                {
                    // ステータスラベルとタイトルラベルは個別に色設定済みのためスキップ
                    if (child is Label lbl && child != _statusLabel && child != _titleLabel)
                    {
                        lbl.ForeColor = OptionControlFactory.ArgbToColor(colors.OnSurfaceMuted);
                        lbl.BackColor = surface;
                    }
                    // ネストしたサブパネル内のラベルにも適用
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

    /// <summary>
    /// ViewModel のプロパティ変更イベントハンドラー。
    /// ステータスメッセージ・送信中状態・入力値の変化に応じてUIを更新する。
    /// UIスレッド以外からの呼び出しは Invoke でマーシャリングする。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">プロパティ変更イベント引数。</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        void Update()
        {
            switch (e.PropertyName)
            {
                case nameof(BugReporterViewModel.StatusMessage):
                    // ステータスメッセージを表示し、内容に応じて文字色を切り替える
                    _statusLabel.Text = _viewModel.StatusMessage;
                    // エラーメッセージの色分け（失敗・入力不備 → 赤、送信中 → 黄、成功 → 緑）
                    if (_viewModel.StatusMessage.Contains("失敗") || _viewModel.StatusMessage.Contains("入力"))
                        _statusLabel.ForeColor = OptionControlFactory.ArgbToColor(_colors.LogError);
                    else if (_viewModel.StatusMessage.Contains("送信中"))
                        _statusLabel.ForeColor = OptionControlFactory.ArgbToColor(_colors.LogWarning);
                    else
                        _statusLabel.ForeColor = OptionControlFactory.ArgbToColor(_colors.Success);
                    break;

                case nameof(BugReporterViewModel.IsSending):
                    // 送信中はボタンを無効化してテキストを「Sending...」に変更
                    _sendButton.Enabled = !_viewModel.IsSending;
                    _sendButton.Text = _viewModel.IsSending ? "Sending..." : "\u2709 Send Bug Report";
                    break;

                case nameof(BugReporterViewModel.UserMessage):
                    // ViewModel 側で変更された場合のみテキストボックスを同期（無限ループ防止）
                    if (_messageBox.Text != _viewModel.UserMessage)
                        _messageBox.Text = _viewModel.UserMessage;
                    break;

                case nameof(BugReporterViewModel.UserEmail):
                    // ViewModel 側で変更された場合のみメールボックスを同期（無限ループ防止）
                    if (_emailBox.Text != _viewModel.UserEmail)
                        _emailBox.Text = _viewModel.UserEmail;
                    break;
            }
        }

        // UIスレッドで安全に実行
        this.SafeInvoke(Update);
    }

    /// <summary>
    /// リソースを解放する。ViewModel のイベント購読を解除してメモリリークを防ぐ。
    /// </summary>
    /// <param name="disposing">マネージドリソースを解放する場合は true。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // イベント購読を解除してメモリリークを防ぐ
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
        base.Dispose(disposing);
    }
}
