using System.Collections.Specialized;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.WinForms.Controls;

namespace CRDebugger.WinForms.Panels;

/// <summary>
/// オプションパネル。
/// <see cref="OptionKind"/> に基づいてコントロールを動的生成し、
/// カテゴリごとにグループ表示するスクロール可能なパネル。
/// カテゴリ変更時に自動的に再構築される。
/// モダンデザイン: 改善されたスペーシングとフォントを採用。
/// </summary>
public sealed class OptionsPanel : Panel
{
    /// <summary>オプション設定のロジックを持つ ViewModel。</summary>
    private readonly OptionsViewModel _viewModel;

    /// <summary>オプションコントロールをスクロール可能に配置する内側パネル。</summary>
    private readonly Panel _scrollPanel;

    /// <summary>パネルタイトルを表示するラベル。</summary>
    private readonly Label _titleLabel;

    /// <summary>オプション一覧を再読み込みするリフレッシュボタン。</summary>
    private readonly Button _refreshButton;

    /// <summary>現在適用中のテーマカラー。</summary>
    private ThemeColors _colors;

    /// <summary>
    /// <see cref="OptionsPanel"/> を初期化してUIコントロールを構築する。
    /// ViewModel の Categories コレクション変更を監視して自動再構築する。
    /// </summary>
    /// <param name="viewModel">オプション設定のロジックを持つ <see cref="OptionsViewModel"/>。</param>
    /// <param name="colors">初期適用するテーマカラー情報。</param>
    public OptionsPanel(OptionsViewModel viewModel, ThemeColors colors)
    {
        _viewModel = viewModel;
        _colors = colors;
        // ダブルバッファリングで描画のちらつきを防ぐ
        DoubleBuffered = true;

        // ヘッダーパネル（タイトルラベル + リフレッシュボタン）
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(16, 12, 16, 8),
        };

        // パネルタイトル（歯車アイコン + Options）
        _titleLabel = new Label
        {
            Text = "\u2699 Options",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Left,
        };

        // オプション一覧を再読み込みするボタン（右端に配置）
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

        // スクロール可能なコンテンツ領域（オプションコントロールを縦積みで配置）
        _scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12),
        };
        Controls.Add(_scrollPanel);

        // ViewModelのカテゴリコレクション変更を監視してコントロールを再構築
        _viewModel.FilteredCategories.CollectionChanged += OnCategoriesChanged;
        RebuildControls();
        ApplyTheme(colors);
    }

    /// <summary>
    /// 指定したテーマカラーをパネル全体に適用する。
    /// テーマ変更後はコントロールを再構築して新しい色を反映する。
    /// </summary>
    /// <param name="colors">適用するテーマカラー情報。</param>
    public void ApplyTheme(ThemeColors colors)
    {
        _colors = colors;
        BackColor = OptionControlFactory.ArgbToColor(colors.Background);
        _scrollPanel.BackColor = OptionControlFactory.ArgbToColor(colors.Background);
        _titleLabel.ForeColor = OptionControlFactory.ArgbToColor(colors.OnBackground);
        _refreshButton.BackColor = OptionControlFactory.ArgbToColor(colors.SurfaceAlt);
        _refreshButton.ForeColor = OptionControlFactory.ArgbToColor(colors.OnSurface);
        _refreshButton.FlatAppearance.BorderColor = OptionControlFactory.ArgbToColor(colors.Border);

        // ヘッダーパネルにテーマカラーを適用（スクロールパネルは除外）
        foreach (Control c in Controls)
        {
            if (c is Panel p && p != _scrollPanel)
                p.BackColor = OptionControlFactory.ArgbToColor(colors.Surface);
        }

        // コントロールを再構築して新テーマを各オプションコントロールに反映
        RebuildControls();
    }

    /// <summary>
    /// Categories コレクション変更イベントハンドラー。
    /// UIスレッド以外からの呼び出しは Invoke でマーシャリングしてコントロールを再構築する。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">コレクション変更イベント引数。</param>
    private void OnCategoriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.SafeInvoke(RebuildControls);
    }

    /// <summary>
    /// スクロールパネル内のオプションコントロールをすべて破棄して再構築する。
    /// カテゴリごとにヘッダーラベルとオプションコントロールを Dock.Top で縦積みする。
    /// WinForms の Dock.Top は後から追加したコントロールが上になるため、逆順で追加する。
    /// </summary>
    private void RebuildControls()
    {
        // レイアウト更新を一時停止してちらつきを防ぐ
        _scrollPanel.SuspendLayout();
        try
        {
            // 既存コントロールを破棄してクリア（GC対象にするため Dispose を呼ぶ）
            foreach (Control c in _scrollPanel.Controls)
                c.Dispose();
            _scrollPanel.Controls.Clear();

            // カテゴリごとにグループを構築
            // Dock.Top は後から追加したものが上になるため、逆順で追加して正しい表示順を実現
            var categories = _viewModel.FilteredCategories.ToList();
            categories.Reverse();

            foreach (var category in categories)
            {
                // オプションコントロールも同様に逆順で追加
                var items = category.FilteredItems.ToList();
                items.Reverse();

                foreach (var item in items)
                {
                    // OptionControlFactory でオプション種別に応じたコントロールを生成
                    var control = OptionControlFactory.Create(item, _colors);
                    control.Dock = DockStyle.Top;
                    control.Margin = new Padding(0, 2, 0, 2);
                    _scrollPanel.Controls.Add(control);
                }

                // アクションボタンも逆順で追加
                var actions = category.FilteredActions.ToList();
                actions.Reverse();

                foreach (var action in actions)
                {
                    var control = OptionControlFactory.CreateAction(action, _colors);
                    control.Dock = DockStyle.Top;
                    control.Margin = new Padding(0, 2, 0, 2);
                    _scrollPanel.Controls.Add(control);
                }

                // カテゴリヘッダーラベル（オプションコントロールより後に追加することで上に表示）
                var categoryHeader = new Label
                {
                    Text = category.Name,
                    Dock = DockStyle.Top,
                    Height = 32,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = OptionControlFactory.ArgbToColor(_colors.Primary),
                    BackColor = OptionControlFactory.ArgbToColor(_colors.SurfaceAlt),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(14, 0, 0, 0),
                    Margin = new Padding(0, 10, 0, 4),
                };
                _scrollPanel.Controls.Add(categoryHeader);
            }
        }
        finally
        {
            // レイアウト再計算を再開して表示を更新
            _scrollPanel.ResumeLayout(true);
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
            // カテゴリコレクション変更イベントの購読を解除
            _viewModel.FilteredCategories.CollectionChanged -= OnCategoriesChanged;
        }
        base.Dispose(disposing);
    }
}
