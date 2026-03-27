using System.Collections.Specialized;
using System.ComponentModel;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.WinForms.Controls;

namespace CRDebugger.WinForms.Panels;

/// <summary>
/// システム情報パネル。
/// OS・CPU・メモリ・.NETランタイムなどのシステム情報を
/// カテゴリごとにグループ化した ListView 形式で表示する。
/// ViewModel の Groups コレクション変更を監視して自動更新する。
/// モダンデザイン: 改善されたスペーシングとフォントを採用。
/// </summary>
public sealed class SystemInfoPanel : Panel
{
    /// <summary>システム情報の収集・管理ロジックを持つ ViewModel。</summary>
    private readonly SystemInfoViewModel _viewModel;

    /// <summary>システム情報をカテゴリ別グループで表示する ListView。</summary>
    private readonly ListView _listView;

    /// <summary>システム情報を再収集するリフレッシュボタン。</summary>
    private readonly Button _refreshButton;

    /// <summary>パネルタイトルを表示するラベル。</summary>
    private readonly Label _titleLabel;

    /// <summary>現在適用中のテーマカラー。</summary>
    private ThemeColors _colors;

    /// <summary>
    /// <see cref="SystemInfoPanel"/> を初期化してUIコントロールを構築する。
    /// ViewModel の Groups コレクション変更を購読して自動更新する。
    /// </summary>
    /// <param name="viewModel">システム情報の収集・管理ロジックを持つ <see cref="SystemInfoViewModel"/>。</param>
    /// <param name="colors">初期適用するテーマカラー情報。</param>
    public SystemInfoPanel(SystemInfoViewModel viewModel, ThemeColors colors)
    {
        _viewModel = viewModel;
        _colors = colors;
        // ダブルバッファリングで描画のちらつきを防ぐ
        DoubleBuffered = true;

        // タイトルバー（タイトルラベル + リフレッシュボタン）
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(16, 12, 16, 8),
        };

        // パネルタイトル（情報アイコン + System Information）
        _titleLabel = new Label
        {
            Text = "\u2139 System Information",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Left,
        };

        // システム情報を再収集するボタン（右端に配置）
        _refreshButton = new Button
        {
            Text = "\u21BB Refresh",
            FlatStyle = FlatStyle.Flat,
            Size = new Size(100, 30),
            Dock = DockStyle.Right,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9),
        };
        _refreshButton.FlatAppearance.BorderSize = 1;
        // クリック時に ViewModel の RefreshCommand を実行
        _refreshButton.Click += (_, _) => _viewModel.RefreshCommand.Execute(null);

        headerPanel.Controls.Add(_titleLabel);
        headerPanel.Controls.Add(_refreshButton);
        Controls.Add(headerPanel);

        // システム情報をグループ付きで表示する ListView
        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BorderStyle = BorderStyle.None,
            // グループ表示を有効化してカテゴリごとに区切る
            ShowGroups = true,
            Font = new Font("Segoe UI", 9.5f),
        };

        // カラム定義（Key列: 項目名、Value列: 値）
        _listView.Columns.Add("Key", 220);
        _listView.Columns.Add("Value", 450);

        Controls.Add(_listView);

        // ViewModel の Groups コレクション変更を監視して ListView を自動更新
        _viewModel.Groups.CollectionChanged += OnGroupsChanged;
        // 初期データを ListView に表示
        PopulateList();
        ApplyTheme(colors);
    }

    /// <summary>
    /// 指定したテーマカラーをパネル全体に適用する。
    /// ListView、ヘッダーパネル、ボタンなどの色を一括更新する。
    /// </summary>
    /// <param name="colors">適用するテーマカラー情報。</param>
    public void ApplyTheme(ThemeColors colors)
    {
        _colors = colors;
        BackColor = OptionControlFactory.ArgbToColor(colors.Background);
        _listView.BackColor = OptionControlFactory.ArgbToColor(colors.Surface);
        _listView.ForeColor = OptionControlFactory.ArgbToColor(colors.OnSurface);
        _titleLabel.ForeColor = OptionControlFactory.ArgbToColor(colors.OnBackground);
        _refreshButton.BackColor = OptionControlFactory.ArgbToColor(colors.SurfaceAlt);
        _refreshButton.ForeColor = OptionControlFactory.ArgbToColor(colors.OnSurface);
        _refreshButton.FlatAppearance.BorderColor = OptionControlFactory.ArgbToColor(colors.Border);

        // ヘッダーパネルの背景色を更新
        foreach (Control c in Controls)
        {
            if (c is Panel p)
                p.BackColor = OptionControlFactory.ArgbToColor(colors.Surface);
        }
    }

    /// <summary>
    /// Groups コレクション変更イベントハンドラー。
    /// UIスレッド以外からの呼び出しは Invoke でマーシャリングして ListView を再構築する。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">コレクション変更イベント引数。</param>
    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (InvokeRequired)
            Invoke(PopulateList);
        else
            PopulateList();
    }

    /// <summary>
    /// ListView の全アイテムとグループをクリアして、ViewModel の Groups データで再構築する。
    /// カテゴリごとに <see cref="ListViewGroup"/> を作成し、その中に各システム情報エントリを追加する。
    /// </summary>
    private void PopulateList()
    {
        // 大量アイテム更新時のちらつきを防ぐためバルク更新モードを使用
        _listView.BeginUpdate();
        try
        {
            // 既存のアイテムとグループをすべてクリア
            _listView.Items.Clear();
            _listView.Groups.Clear();

            foreach (var group in _viewModel.Groups)
            {
                // カテゴリごとのグループを作成（ヘッダーは左揃え）
                var lvGroup = new ListViewGroup(group.Category, group.Category)
                {
                    HeaderAlignment = HorizontalAlignment.Left,
                };
                _listView.Groups.Add(lvGroup);

                // グループ内の各エントリをアイテムとして追加
                foreach (var item in group.Items)
                {
                    // Key列に項目名、Value列に値を設定
                    var lvi = new ListViewItem(item.Key, lvGroup);
                    lvi.SubItems.Add(item.Value);
                    _listView.Items.Add(lvi);
                }
            }
        }
        finally
        {
            _listView.EndUpdate();
        }
    }

    /// <summary>
    /// リソースを解放する。ViewModel のイベント購読を解除してメモリリークを防ぐ。
    /// </summary>
    /// <param name="disposing">マネージドリソースを解放する場合は true。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Groups コレクション変更イベントの購読を解除
            _viewModel.Groups.CollectionChanged -= OnGroupsChanged;
        }
        base.Dispose(disposing);
    }
}
