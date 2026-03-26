using System.Collections.Specialized;
using System.ComponentModel;
using CRDebugger.Core.Logging;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.WinForms.Controls;

namespace CRDebugger.WinForms.Panels;

/// <summary>
/// コンソールログパネル
/// ログレベルフィルター、検索、スタックトレース表示を備えたListView
/// モダンデザイン: 改善されたスペーシング、フォント、カラーリング
/// </summary>
public sealed class ConsolePanel : Panel
{
    private readonly ConsoleViewModel _viewModel;
    private readonly ListView _logListView;
    private readonly TextBox _searchBox;
    private readonly TextBox _stackTraceBox;
    private readonly SplitContainer _splitContainer;
    private readonly CheckBox _chkDebug;
    private readonly CheckBox _chkInfo;
    private readonly CheckBox _chkWarning;
    private readonly CheckBox _chkError;
    private readonly Button _clearButton;
    private readonly Label _titleLabel;
    private ThemeColors _colors;
    private bool _suppressFilterEvents;

    public ConsolePanel(ConsoleViewModel viewModel, ThemeColors colors)
    {
        _viewModel = viewModel;
        _colors = colors;
        DoubleBuffered = true;

        // ヘッダーパネル（タイトル + フィルター）
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            Padding = new Padding(12, 8, 12, 8),
        };

        // タイトル行
        _titleLabel = new Label
        {
            Text = "\u25B6 Console",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            Location = new Point(16, 8),
            AutoSize = true,
        };
        headerPanel.Controls.Add(_titleLabel);

        _clearButton = new Button
        {
            Text = "Clear",
            FlatStyle = FlatStyle.Flat,
            Size = new Size(70, 28),
            Location = new Point(headerPanel.Width - 90, 8),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9),
        };
        _clearButton.FlatAppearance.BorderSize = 1;
        _clearButton.Click += (_, _) => _viewModel.ClearCommand.Execute(null);
        headerPanel.Controls.Add(_clearButton);

        // フィルター行
        var filterY = 42;

        _chkDebug = CreateFilterCheckBox("DBG", filterY, 16, true);
        _chkInfo = CreateFilterCheckBox("INF", filterY, 80, true);
        _chkWarning = CreateFilterCheckBox("WRN", filterY, 144, true);
        _chkError = CreateFilterCheckBox("ERR", filterY, 208, true);

        headerPanel.Controls.Add(_chkDebug);
        headerPanel.Controls.Add(_chkInfo);
        headerPanel.Controls.Add(_chkWarning);
        headerPanel.Controls.Add(_chkError);

        _searchBox = new TextBox
        {
            PlaceholderText = "\uD83D\uDD0D Search logs...",
            Location = new Point(280, filterY),
            Size = new Size(200, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f),
        };
        _searchBox.TextChanged += (_, _) =>
        {
            if (!_suppressFilterEvents)
                _viewModel.SearchText = _searchBox.Text;
        };
        headerPanel.Controls.Add(_searchBox);

        Controls.Add(headerPanel);

        // スプリットコンテナ（ログリスト + スタックトレース）
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 300,
            SplitterWidth = 4,
            Panel2MinSize = 60,
        };

        // ログリストビュー
        _logListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BorderStyle = BorderStyle.None,
            VirtualMode = false,
            Font = new Font("Consolas", 9.5f),
            OwnerDraw = true,
        };

        _logListView.Columns.Add("#", 44);
        _logListView.Columns.Add("Time", 90);
        _logListView.Columns.Add("Lvl", 50);
        _logListView.Columns.Add("Channel", 100);
        _logListView.Columns.Add("Message", 500);

        _logListView.DrawColumnHeader += OnDrawColumnHeader;
        _logListView.DrawItem += OnDrawItem;
        _logListView.DrawSubItem += OnDrawSubItem;
        _logListView.SelectedIndexChanged += OnLogSelectionChanged;

        _splitContainer.Panel1.Controls.Add(_logListView);

        // スタックトレース表示
        var stackLabel = new Label
        {
            Text = "  Stack Trace:",
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Padding = new Padding(8, 4, 0, 0),
        };
        _splitContainer.Panel2.Controls.Add(stackLabel);

        _stackTraceBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.None,
        };
        _splitContainer.Panel2.Controls.Add(_stackTraceBox);

        Controls.Add(_splitContainer);

        // ViewModelイベントの購読
        _viewModel.DisplayEntries.CollectionChanged += OnDisplayEntriesChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // チェックボックス連動
        _chkDebug.CheckedChanged += (_, _) =>
        {
            if (!_suppressFilterEvents) _viewModel.ShowDebug = _chkDebug.Checked;
        };
        _chkInfo.CheckedChanged += (_, _) =>
        {
            if (!_suppressFilterEvents) _viewModel.ShowInfo = _chkInfo.Checked;
        };
        _chkWarning.CheckedChanged += (_, _) =>
        {
            if (!_suppressFilterEvents) _viewModel.ShowWarning = _chkWarning.Checked;
        };
        _chkError.CheckedChanged += (_, _) =>
        {
            if (!_suppressFilterEvents) _viewModel.ShowError = _chkError.Checked;
        };

        // 初期データ表示
        RefreshLogList();
        ApplyTheme(colors);
    }

    public void ApplyTheme(ThemeColors colors)
    {
        _colors = colors;
        var bg = OptionControlFactory.ArgbToColor(colors.Background);
        var surface = OptionControlFactory.ArgbToColor(colors.Surface);
        var onSurface = OptionControlFactory.ArgbToColor(colors.OnSurface);
        var onBg = OptionControlFactory.ArgbToColor(colors.OnBackground);
        var surfaceAlt = OptionControlFactory.ArgbToColor(colors.SurfaceAlt);
        var border = OptionControlFactory.ArgbToColor(colors.Border);

        BackColor = bg;
        _logListView.BackColor = surface;
        _logListView.ForeColor = onSurface;
        _searchBox.BackColor = surfaceAlt;
        _searchBox.ForeColor = onSurface;
        _stackTraceBox.BackColor = surfaceAlt;
        _stackTraceBox.ForeColor = onSurface;
        _titleLabel.ForeColor = onBg;
        _clearButton.BackColor = surfaceAlt;
        _clearButton.ForeColor = onSurface;
        _clearButton.FlatAppearance.BorderColor = border;
        _splitContainer.BackColor = bg;

        foreach (var chk in new[] { _chkDebug, _chkInfo, _chkWarning, _chkError })
        {
            chk.ForeColor = onSurface;
        }

        foreach (Control c in Controls)
        {
            if (c is Panel p)
                p.BackColor = surface;
        }

        // スタックトレースラベル
        foreach (Control c in _splitContainer.Panel2.Controls)
        {
            if (c is Label lbl)
            {
                lbl.ForeColor = onBg;
                lbl.BackColor = surfaceAlt;
            }
        }

        _logListView.Invalidate();
    }

    private CheckBox CreateFilterCheckBox(string text, int y, int x, bool isChecked)
    {
        return new CheckBox
        {
            Text = text,
            Checked = isChecked,
            Location = new Point(x, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
        };
    }

    private void OnDisplayEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            try { Invoke(RefreshLogList); } catch (ObjectDisposedException) { }
            return;
        }
        RefreshLogList();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ConsoleViewModel.SelectedEntry):
                UpdateStackTrace();
                break;
            case nameof(ConsoleViewModel.DebugCount):
            case nameof(ConsoleViewModel.InfoCount):
            case nameof(ConsoleViewModel.WarningCount):
            case nameof(ConsoleViewModel.ErrorCount):
                UpdateBadgeText();
                break;
        }
    }

    private void RefreshLogList()
    {
        _logListView.BeginUpdate();
        try
        {
            _logListView.Items.Clear();
            foreach (var entry in _viewModel.DisplayEntries)
            {
                AddLogItem(entry);
            }

            // 最新エントリにスクロール
            if (_logListView.Items.Count > 0)
            {
                _logListView.EnsureVisible(_logListView.Items.Count - 1);
            }
        }
        finally
        {
            _logListView.EndUpdate();
        }
    }

    private void AddLogItem(LogEntry entry)
    {
        var lvi = new ListViewItem(entry.Id.ToString());
        lvi.SubItems.Add(entry.Timestamp.ToString("HH:mm:ss.fff"));
        lvi.SubItems.Add(entry.Level.ToString().Substring(0, 3).ToUpper());
        lvi.SubItems.Add(entry.Channel);
        lvi.SubItems.Add(entry.Message.Replace("\r\n", " ").Replace("\n", " "));
        lvi.Tag = entry;
        _logListView.Items.Add(lvi);
    }

    private void OnLogSelectionChanged(object? sender, EventArgs e)
    {
        if (_logListView.SelectedItems.Count > 0)
        {
            var entry = _logListView.SelectedItems[0].Tag as LogEntry;
            _viewModel.SelectedEntry = entry;
        }
    }

    private void UpdateStackTrace()
    {
        var entry = _viewModel.SelectedEntry;
        if (InvokeRequired)
        {
            try
            {
                Invoke(() =>
                {
                    _stackTraceBox.Text = entry?.StackTrace ?? string.Empty;
                });
            }
            catch (ObjectDisposedException) { }
            return;
        }
        _stackTraceBox.Text = entry?.StackTrace ?? string.Empty;
    }

    private void UpdateBadgeText()
    {
        void Update()
        {
            _suppressFilterEvents = true;
            _chkDebug.Text = $"DBG ({_viewModel.DebugCount})";
            _chkInfo.Text = $"INF ({_viewModel.InfoCount})";
            _chkWarning.Text = $"WRN ({_viewModel.WarningCount})";
            _chkError.Text = $"ERR ({_viewModel.ErrorCount})";
            _suppressFilterEvents = false;
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

    private void OnDrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using var bgBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.SurfaceAlt));
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var textBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.OnSurfaceMuted));
        var textRect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 2, e.Bounds.Width - 12, e.Bounds.Height - 4);
        e.Graphics.DrawString(e.Header!.Text, font, textBrush, textRect, StringFormat.GenericDefault);
    }

    private void OnDrawItem(object? sender, DrawListViewItemEventArgs e)
    {
        // OnDrawSubItemに委譲
    }

    private void OnDrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item?.Tag is not LogEntry entry) return;

        // 背景色（交互色 + 選択色）
        Color bgColor;
        if (e.Item.Selected)
            bgColor = Color.FromArgb(30, OptionControlFactory.ArgbToColor(_colors.Primary));
        else if (e.ItemIndex % 2 == 0)
            bgColor = OptionControlFactory.ArgbToColor(_colors.Surface);
        else
            bgColor = OptionControlFactory.ArgbToColor(_colors.SurfaceAlt);

        using var bgBrush = new SolidBrush(bgColor);
        e.Graphics!.FillRectangle(bgBrush, e.Bounds);

        // テキスト色（レベルに応じた色分け）
        Color textColor;
        if (e.ColumnIndex == 2) // レベル列は常にレベル色
        {
            textColor = GetLevelColor(entry.Level);
        }
        else if (e.ColumnIndex == 4) // メッセージ列もレベル色
        {
            textColor = GetLevelColor(entry.Level);
        }
        else
        {
            textColor = OptionControlFactory.ArgbToColor(_colors.OnSurfaceMuted);
        }

        using var textBrush = new SolidBrush(textColor);
        var font = e.SubItem!.Font ?? _logListView.Font;
        var textRect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 1, e.Bounds.Width - 12, e.Bounds.Height - 2);

        using var sf = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
            LineAlignment = StringAlignment.Center,
        };
        e.Graphics.DrawString(e.SubItem.Text, font, textBrush, textRect, sf);
    }

    private Color GetLevelColor(CRLogLevel level) => level switch
    {
        CRLogLevel.Debug => OptionControlFactory.ArgbToColor(_colors.LogDebug),
        CRLogLevel.Info => OptionControlFactory.ArgbToColor(_colors.LogInfo),
        CRLogLevel.Warning => OptionControlFactory.ArgbToColor(_colors.LogWarning),
        CRLogLevel.Error => OptionControlFactory.ArgbToColor(_colors.LogError),
        _ => OptionControlFactory.ArgbToColor(_colors.OnSurface),
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewModel.DisplayEntries.CollectionChanged -= OnDisplayEntriesChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
        base.Dispose(disposing);
    }
}
