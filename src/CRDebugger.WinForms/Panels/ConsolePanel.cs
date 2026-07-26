using System.Collections.Specialized;
using System.ComponentModel;
using CRDebugger.Core.Logging;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.WinForms.Controls;

namespace CRDebugger.WinForms.Panels;

/// <summary>
/// コンソールログパネル。
/// ログレベルフィルター（DBG/INF/WRN/ERR）、キーワード検索、スタックトレース表示を備えた
/// オーナードロー ListView でログエントリを一覧表示する。
/// モダンデザイン: 改善されたスペーシング、フォント、カラーリングを採用。
/// </summary>
public sealed class ConsolePanel : Panel
{
    /// <summary>コンソールログの表示ロジックを持つ ViewModel。</summary>
    private readonly ConsoleViewModel _viewModel;

    /// <summary>ログエントリを一覧表示するカスタム描画 ListView。</summary>
    private readonly ListView _logListView;

    /// <summary>ログのキーワード検索入力欄。</summary>
    private readonly TextBox _searchBox;

    /// <summary>選択したログエントリのスタックトレースを表示するテキストボックス。</summary>
    private readonly TextBox _stackTraceBox;

    /// <summary>ログリストとスタックトレースを上下に分割するコンテナ。</summary>
    private readonly SplitContainer _splitContainer;

    /// <summary>Debugレベルのログを表示するかどうかのチェックボックス。</summary>
    private readonly CheckBox _chkDebug;

    /// <summary>Infoレベルのログを表示するかどうかのチェックボックス。</summary>
    private readonly CheckBox _chkInfo;

    /// <summary>Warningレベルのログを表示するかどうかのチェックボックス。</summary>
    private readonly CheckBox _chkWarning;

    /// <summary>Errorレベルのログを表示するかどうかのチェックボックス。</summary>
    private readonly CheckBox _chkError;

    /// <summary>全ログをクリアするボタン。</summary>
    private readonly Button _clearButton;

    /// <summary>パネルタイトルを表示するラベル。</summary>
    private readonly Label _titleLabel;

    /// <summary>現在適用中のテーマカラー。</summary>
    private ThemeColors _colors;

    /// <summary>
    /// フィルターチェックボックスのイベントを一時的に抑制するフラグ。
    /// バッジテキスト更新時などに誤ってフィルターが変更されるのを防ぐ。
    /// </summary>
    private bool _suppressFilterEvents;

    /// <summary>
    /// <see cref="ConsolePanel"/> を初期化してUIコントロールを構築する。
    /// ViewModel の DisplayEntries コレクション変更とプロパティ変更イベントを購読する。
    /// </summary>
    /// <param name="viewModel">コンソールログの表示ロジックを持つ <see cref="ConsoleViewModel"/>。</param>
    /// <param name="colors">初期適用するテーマカラー情報。</param>
    public ConsolePanel(ConsoleViewModel viewModel, ThemeColors colors)
    {
        _viewModel = viewModel;
        _colors = colors;
        // ダブルバッファリングで描画のちらつきを防ぐ
        DoubleBuffered = true;

        // ヘッダーパネル（タイトル + フィルターチェックボックス + 検索欄）
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            Padding = new Padding(12, 8, 12, 8),
        };

        // タイトル行（再生アイコン + Console）
        _titleLabel = new Label
        {
            Text = "\u25B6 Console",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            Location = new Point(16, 8),
            AutoSize = true,
        };
        headerPanel.Controls.Add(_titleLabel);

        // 全ログクリアボタン（右端に配置）
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
        // クリック時に ViewModel の ClearCommand を実行
        _clearButton.Click += (_, _) => _viewModel.ClearCommand.Execute(null);
        headerPanel.Controls.Add(_clearButton);

        // フィルターチェックボックス行（2行目に配置）
        var filterY = 42;

        // 各ログレベルのフィルターチェックボックスを生成（初期状態はすべてチェック）
        _chkDebug = CreateFilterCheckBox("DBG", filterY, 16, true);
        _chkInfo = CreateFilterCheckBox("INF", filterY, 80, true);
        _chkWarning = CreateFilterCheckBox("WRN", filterY, 144, true);
        _chkError = CreateFilterCheckBox("ERR", filterY, 208, true);

        headerPanel.Controls.Add(_chkDebug);
        headerPanel.Controls.Add(_chkInfo);
        headerPanel.Controls.Add(_chkWarning);
        headerPanel.Controls.Add(_chkError);

        // キーワード検索テキストボックス（プレースホルダー付き）
        _searchBox = new TextBox
        {
            PlaceholderText = "\uD83D\uDD0D Search logs...",
            Location = new Point(280, filterY),
            Size = new Size(200, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f),
        };
        // テキスト変更時に ViewModel の SearchText を更新（フィルターイベント抑制中は無視）
        _searchBox.TextChanged += (_, _) =>
        {
            if (!_suppressFilterEvents)
                _viewModel.SearchText = _searchBox.Text;
        };
        headerPanel.Controls.Add(_searchBox);

        Controls.Add(headerPanel);

        // スプリットコンテナ（上: ログリスト、下: スタックトレース）
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 300,
            SplitterWidth = 4,
            Panel2MinSize = 60,
        };

        // オーナードロー ListView でログエントリを一覧表示
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
            // カスタム描画を有効化（レベル別カラーリングのため）
            OwnerDraw = true,
        };

        // カラム定義（No. / 時刻 / レベル / チャンネル / メッセージ）
        _logListView.Columns.Add("#", 44);
        _logListView.Columns.Add("Time", 90);
        _logListView.Columns.Add("Lvl", 50);
        _logListView.Columns.Add("Channel", 100);
        _logListView.Columns.Add("Message", 500);

        // カスタム描画イベントを登録
        _logListView.DrawColumnHeader += OnDrawColumnHeader;
        _logListView.DrawItem += OnDrawItem;
        _logListView.DrawSubItem += OnDrawSubItem;
        // 選択変更時にスタックトレースを更新
        _logListView.SelectedIndexChanged += OnLogSelectionChanged;

        _splitContainer.Panel1.Controls.Add(_logListView);

        // スタックトレース表示エリア（スプリット下段）
        var stackLabel = new Label
        {
            Text = "  Stack Trace:",
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Padding = new Padding(8, 4, 0, 0),
        };
        _splitContainer.Panel2.Controls.Add(stackLabel);

        // スタックトレースを表示する読み取り専用テキストボックス（横スクロール対応）
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

        // ViewModel の表示エントリ変更を監視してリストを再描画。
        // DisplayEntries はフィルタ変更時にインスタンスごと差し替えられるため、
        // 生のコレクションではなく Rebind 経由で購読する（差し替え後も追従できるようにする）。
        RebindDisplayEntries();
        // プロパティ変更を監視（選択エントリ変更 / カウント変更 / DisplayEntries 差し替え）
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // フィルターチェックボックスと ViewModel のプロパティを連動させる
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

        // 初期データを ListView に表示してテーマを適用
        RefreshLogList();
        ApplyTheme(colors);
    }

    /// <summary>
    /// 指定したテーマカラーをパネル全体に適用する。
    /// ListView、検索欄、スタックトレース欄などの色を一括更新する。
    /// </summary>
    /// <param name="colors">適用するテーマカラー情報。</param>
    public void ApplyTheme(ThemeColors colors)
    {
        _colors = colors;
        // テーマカラーを WinForms の Color 型に変換
        var bg = OptionControlFactory.ArgbToColor(colors.Background);
        var surface = OptionControlFactory.ArgbToColor(colors.Surface);
        var onSurface = OptionControlFactory.ArgbToColor(colors.OnSurface);
        var onBg = OptionControlFactory.ArgbToColor(colors.OnBackground);
        var surfaceAlt = OptionControlFactory.ArgbToColor(colors.SurfaceAlt);
        var border = OptionControlFactory.ArgbToColor(colors.Border);

        // 各コントロールにテーマカラーを適用
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

        // フィルターチェックボックスのテキスト色を更新
        foreach (var chk in new[] { _chkDebug, _chkInfo, _chkWarning, _chkError })
        {
            chk.ForeColor = onSurface;
        }

        // ヘッダーパネルの背景色を更新
        foreach (Control c in Controls)
        {
            if (c is Panel p)
                p.BackColor = surface;
        }

        // スタックトレースエリアのラベル色を更新
        foreach (Control c in _splitContainer.Panel2.Controls)
        {
            if (c is Label lbl)
            {
                lbl.ForeColor = onBg;
                lbl.BackColor = surfaceAlt;
            }
        }

        // テーマ変更後に ListView を再描画
        _logListView.Invalidate();
    }

    /// <summary>
    /// フィルターチェックボックスを生成するヘルパーメソッド。
    /// 指定した位置・テキスト・初期チェック状態でチェックボックスを作成する。
    /// </summary>
    /// <param name="text">チェックボックスに表示するテキスト（ログレベル略称）。</param>
    /// <param name="y">チェックボックスの Y 座標。</param>
    /// <param name="x">チェックボックスの X 座標。</param>
    /// <param name="isChecked">初期チェック状態。</param>
    /// <returns>生成した <see cref="CheckBox"/>。</returns>
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

    /// <summary>
    /// DisplayEntries コレクション変更イベントハンドラー。
    /// 変更種別に応じて差分のみリストを更新し、毎回 RefreshLogList で全件再構築するのを避ける（O(N²) 回避）。
    /// UIスレッド以外からの呼び出しは Invoke でマーシャリングする。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">コレクション変更イベント引数。</param>
    private void OnDisplayEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.SafeInvoke(() => ApplyCollectionChange(e));
    }

    /// <summary>
    /// 現在 CollectionChanged を購読している表示エントリコレクション。
    /// <see cref="ConsoleViewModel.DisplayEntries"/> は差し替えられるため、
    /// 解除対象を取り違えないよう購読中のインスタンスを保持しておく。
    /// </summary>
    private INotifyCollectionChanged? _subscribedEntries;

    /// <summary>
    /// <see cref="ConsoleViewModel.DisplayEntries"/> の購読を最新インスタンスへ張り替え、リストを再構築する。
    /// ViewModel はフィルタ・検索の変更時にコレクションを新しいインスタンスへ差し替えるため、
    /// 初期インスタンスを掴んだままだと以降の更新がまったく届かなくなる。
    /// </summary>
    private void RebindDisplayEntries()
    {
        // 旧インスタンスの購読を解除（購読漏れ・二重購読の両方を防ぐ）
        if (_subscribedEntries != null)
            _subscribedEntries.CollectionChanged -= OnDisplayEntriesChanged;

        _subscribedEntries = _viewModel.DisplayEntries;
        _subscribedEntries.CollectionChanged += OnDisplayEntriesChanged;

        // 差し替え後の内容で ListView を作り直す
        RefreshLogList();
    }

    /// <summary>
    /// コレクション変更イベントを ListView に差分適用する。
    /// Add/Remove/Replace は O(1) 単位の操作で済ませ、Reset や未知の操作はフル再構築にフォールバックする。
    /// </summary>
    /// <param name="e">コレクション変更イベント引数。</param>
    private void ApplyCollectionChange(NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems == null)
                {
                    RefreshLogList();
                    return;
                }
                _logListView.BeginUpdate();
                try
                {
                    foreach (var item in e.NewItems)
                    {
                        if (item is LogEntry entry)
                            AddLogItem(entry);
                    }
                    // 最新エントリが見えるようにリスト末尾にスクロール
                    if (_logListView.Items.Count > 0)
                        _logListView.EnsureVisible(_logListView.Items.Count - 1);
                }
                finally
                {
                    _logListView.EndUpdate();
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                // OldStartingIndex が有効でアイテム数の範囲内なら該当行のみ削除
                if (e.OldStartingIndex >= 0
                    && e.OldStartingIndex < _logListView.Items.Count
                    && e.OldItems != null)
                {
                    for (int i = e.OldItems.Count - 1; i >= 0; i--)
                    {
                        var index = e.OldStartingIndex + i;
                        if (index >= 0 && index < _logListView.Items.Count)
                            _logListView.Items.RemoveAt(index);
                    }
                }
                else
                {
                    // インデックス不明の場合は安全側でフル再構築
                    RefreshLogList();
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                // 同じインデックスでサブアイテムを差し替える
                if (e.NewItems != null
                    && e.NewStartingIndex >= 0
                    && e.NewStartingIndex < _logListView.Items.Count)
                {
                    for (int i = 0; i < e.NewItems.Count; i++)
                    {
                        var index = e.NewStartingIndex + i;
                        if (index < _logListView.Items.Count && e.NewItems[i] is LogEntry entry)
                        {
                            var lvi = _logListView.Items[index];
                            lvi.SubItems.Clear();
                            lvi.Text = entry.Id.ToString();
                            lvi.SubItems.Add(entry.Timestamp.ToString("HH:mm:ss.fff"));
                            lvi.SubItems.Add(entry.Level.ToString().Substring(0, 3).ToUpper());
                            lvi.SubItems.Add(entry.Channel);
                            lvi.SubItems.Add(entry.Message.Replace("\r\n", " ").Replace("\n", " "));
                            lvi.Tag = entry;
                        }
                    }
                }
                else
                {
                    RefreshLogList();
                }
                break;

            case NotifyCollectionChangedAction.Reset:
            default:
                // Reset または Move 等の未対応操作は安全側でフル再構築
                RefreshLogList();
                break;
        }
    }

    /// <summary>
    /// ViewModel のプロパティ変更イベントハンドラー。
    /// 選択エントリ変更時はスタックトレースを更新し、カウント変更時はバッジテキストを更新する。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">プロパティ変更イベント引数。</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ConsoleViewModel.DisplayEntries):
                // フィルタ/検索変更でコレクションが差し替わったので購読を張り替えて再描画する
                this.SafeInvoke(RebindDisplayEntries);
                break;
            case nameof(ConsoleViewModel.SelectedEntry):
                // 選択エントリが変わったらスタックトレースエリアを更新
                UpdateStackTrace();
                break;
            case nameof(ConsoleViewModel.DebugCount):
            case nameof(ConsoleViewModel.InfoCount):
            case nameof(ConsoleViewModel.WarningCount):
            case nameof(ConsoleViewModel.ErrorCount):
                // カウントが変わったらチェックボックスのバッジテキストを更新
                UpdateBadgeText();
                break;
        }
    }

    /// <summary>
    /// ListView のアイテムを全クリアして DisplayEntries の内容で再構築する。
    /// 最新のエントリが見えるようにリストの末尾にスクロールする。
    /// </summary>
    private void RefreshLogList()
    {
        // 大量のアイテム追加時のちらつきを防ぐためバルク更新モードを使用
        _logListView.BeginUpdate();
        try
        {
            // 既存アイテムをクリアして再描画
            _logListView.Items.Clear();
            foreach (var entry in _viewModel.DisplayEntries)
            {
                AddLogItem(entry);
            }

            // 最新エントリが見えるようにリスト末尾にスクロール
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

    /// <summary>
    /// 単一の <see cref="LogEntry"/> を ListView に追加する。
    /// エントリの情報を各サブアイテムに設定し、Tag に元の LogEntry を保持する。
    /// </summary>
    /// <param name="entry">追加するログエントリ。</param>
    private void AddLogItem(LogEntry entry)
    {
        // No. 列にエントリIDを設定
        var lvi = new ListViewItem(entry.Id.ToString());
        // 時刻を HH:mm:ss.fff 形式で追加
        lvi.SubItems.Add(entry.Timestamp.ToString("HH:mm:ss.fff"));
        // ログレベルを3文字大文字で追加（例: "DBG", "INF"）
        lvi.SubItems.Add(entry.Level.ToString().Substring(0, 3).ToUpper());
        lvi.SubItems.Add(entry.Channel);
        // メッセージの改行をスペースに置換して1行表示
        lvi.SubItems.Add(entry.Message.Replace("\r\n", " ").Replace("\n", " "));
        // OnDrawSubItem でエントリ情報を参照できるように Tag に保持
        lvi.Tag = entry;
        _logListView.Items.Add(lvi);
    }

    /// <summary>
    /// ListView の選択変更イベントハンドラー。
    /// 選択されたアイテムに対応する <see cref="LogEntry"/> を ViewModel の SelectedEntry に設定する。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">イベント引数。</param>
    private void OnLogSelectionChanged(object? sender, EventArgs e)
    {
        if (_logListView.SelectedItems.Count > 0)
        {
            // Tag から LogEntry を取得して ViewModel に設定
            var entry = _logListView.SelectedItems[0].Tag as LogEntry;
            _viewModel.SelectedEntry = entry;
        }
    }

    /// <summary>
    /// スタックトレース表示エリアを選択中のログエントリの内容で更新する。
    /// UIスレッド以外からの呼び出しは Invoke でマーシャリングする。
    /// </summary>
    private void UpdateStackTrace()
    {
        var entry = _viewModel.SelectedEntry;
        // スタックトレースがない場合は空文字を表示
        this.SafeInvoke(() => _stackTraceBox.Text = entry?.StackTrace ?? string.Empty);
    }

    /// <summary>
    /// フィルターチェックボックスのバッジテキストを現在のログカウントで更新する。
    /// テキスト変更中はフィルターイベントを抑制して誤ったフィルター適用を防ぐ。
    /// </summary>
    private void UpdateBadgeText()
    {
        this.SafeInvoke(() =>
        {
            // イベント抑制フラグを立ててテキスト変更によるフィルター適用を防ぐ
            _suppressFilterEvents = true;
            _chkDebug.Text = $"DBG ({_viewModel.DebugCount})";
            _chkInfo.Text = $"INF ({_viewModel.InfoCount})";
            _chkWarning.Text = $"WRN ({_viewModel.WarningCount})";
            _chkError.Text = $"ERR ({_viewModel.ErrorCount})";
            _suppressFilterEvents = false;
        });
    }

    /// <summary>
    /// ListView のカラムヘッダーをカスタム描画する。
    /// テーマカラーに合わせた背景色と太字テキストで描画する。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">カラムヘッダー描画イベント引数。</param>
    private void OnDrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        // ヘッダー背景をテーマカラーで塗りつぶす
        using var bgBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.SurfaceAlt));
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        // 太字のヘッダーテキストを描画
        using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var textBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.OnSurfaceMuted));
        var textRect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 2, e.Bounds.Width - 12, e.Bounds.Height - 4);
        e.Graphics.DrawString(e.Header!.Text, font, textBrush, textRect, StringFormat.GenericDefault);
    }

    /// <summary>
    /// ListView のアイテム描画イベントハンドラー。
    /// サブアイテム単位の描画（<see cref="OnDrawSubItem"/>）に委譲するため何もしない。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">アイテム描画イベント引数。</param>
    private void OnDrawItem(object? sender, DrawListViewItemEventArgs e)
    {
        // OnDrawSubItemに委譲（このメソッドでは何もしない）
    }

    /// <summary>
    /// ListView のサブアイテムをカスタム描画する。
    /// 行の選択状態・偶数/奇数インデックスによる背景色の交互表示と、
    /// ログレベルに応じたテキスト色を適用する。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">サブアイテム描画イベント引数。</param>
    private void OnDrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        // Tag から LogEntry を取得（取得できない場合は描画をスキップ）
        if (e.Item?.Tag is not LogEntry entry) return;

        // 背景色（選択中 → アクセント色、偶数行 → Surface、奇数行 → SurfaceAlt）
        Color bgColor;
        if (e.Item.Selected)
            bgColor = Color.FromArgb(30, OptionControlFactory.ArgbToColor(_colors.Primary));
        else if (e.ItemIndex % 2 == 0)
            bgColor = OptionControlFactory.ArgbToColor(_colors.Surface);
        else
            bgColor = OptionControlFactory.ArgbToColor(_colors.SurfaceAlt);

        // 背景を塗りつぶす
        using var bgBrush = new SolidBrush(bgColor);
        e.Graphics!.FillRectangle(bgBrush, e.Bounds);

        // テキスト色（レベル列とメッセージ列はログレベルに対応した色を使用）
        Color textColor;
        if (e.ColumnIndex == 2 || e.ColumnIndex == 4) // レベル列・メッセージ列はレベル色で描画
        {
            textColor = GetLevelColor(entry.Level);
        }
        else
        {
            // その他の列はミュートテキスト色で描画
            textColor = OptionControlFactory.ArgbToColor(_colors.OnSurfaceMuted);
        }

        // テキストを省略記号付きで1行描画
        using var textBrush = new SolidBrush(textColor);
        var font = e.SubItem!.Font ?? _logListView.Font;
        var textRect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 1, e.Bounds.Width - 12, e.Bounds.Height - 2);

        using var sf = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,  // 長いテキストは末尾を省略記号で切り捨て
            FormatFlags = StringFormatFlags.NoWrap,        // 改行なしで1行に収める
            LineAlignment = StringAlignment.Center,        // 垂直方向は中央揃え
        };
        e.Graphics.DrawString(e.SubItem.Text, font, textBrush, textRect, sf);
    }

    /// <summary>
    /// 指定した <see cref="CRLogLevel"/> に対応するログテキスト色を返す。
    /// </summary>
    /// <param name="level">色を取得するログレベル。</param>
    /// <returns>ログレベルに対応した <see cref="Color"/>。</returns>
    private Color GetLevelColor(CRLogLevel level) => level switch
    {
        CRLogLevel.Debug => OptionControlFactory.ArgbToColor(_colors.LogDebug),
        CRLogLevel.Info => OptionControlFactory.ArgbToColor(_colors.LogInfo),
        CRLogLevel.Warning => OptionControlFactory.ArgbToColor(_colors.LogWarning),
        CRLogLevel.Error => OptionControlFactory.ArgbToColor(_colors.LogError),
        // 不明なレベルは通常のOnSurface色を返す
        _ => OptionControlFactory.ArgbToColor(_colors.OnSurface),
    };

    /// <summary>
    /// リソースを解放する。ViewModel のイベント購読を解除してメモリリークを防ぐ。
    /// </summary>
    /// <param name="disposing">マネージドリソースを解放する場合は true。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // コレクション変更イベントとプロパティ変更イベントの購読を解除。
            // コレクションは差し替えられるため、購読中のインスタンスから確実に外す。
            if (_subscribedEntries != null)
            {
                _subscribedEntries.CollectionChanged -= OnDisplayEntriesChanged;
                _subscribedEntries = null;
            }
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
        base.Dispose(disposing);
    }
}
