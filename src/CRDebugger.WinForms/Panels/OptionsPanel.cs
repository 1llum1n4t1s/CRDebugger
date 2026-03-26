using System.Collections.Specialized;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.WinForms.Controls;

namespace CRDebugger.WinForms.Panels;

/// <summary>
/// オプションパネル
/// OptionKindに基づいてコントロールを動的生成し、カテゴリごとにグループ表示する
/// モダンデザイン: 改善されたスペーシングとフォント
/// </summary>
public sealed class OptionsPanel : Panel
{
    private readonly OptionsViewModel _viewModel;
    private readonly Panel _scrollPanel;
    private readonly Label _titleLabel;
    private readonly Button _refreshButton;
    private ThemeColors _colors;

    public OptionsPanel(OptionsViewModel viewModel, ThemeColors colors)
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
            Text = "\u2699 Options",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Left,
        };

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
        _refreshButton.Click += (_, _) => _viewModel.RefreshCommand.Execute(null);

        headerPanel.Controls.Add(_titleLabel);
        headerPanel.Controls.Add(_refreshButton);
        Controls.Add(headerPanel);

        // スクロール可能なコンテンツ領域
        _scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12),
        };
        Controls.Add(_scrollPanel);

        // ViewModelの変更を監視
        _viewModel.Categories.CollectionChanged += OnCategoriesChanged;
        RebuildControls();
        ApplyTheme(colors);
    }

    public void ApplyTheme(ThemeColors colors)
    {
        _colors = colors;
        BackColor = OptionControlFactory.ArgbToColor(colors.Background);
        _scrollPanel.BackColor = OptionControlFactory.ArgbToColor(colors.Background);
        _titleLabel.ForeColor = OptionControlFactory.ArgbToColor(colors.OnBackground);
        _refreshButton.BackColor = OptionControlFactory.ArgbToColor(colors.SurfaceAlt);
        _refreshButton.ForeColor = OptionControlFactory.ArgbToColor(colors.OnSurface);
        _refreshButton.FlatAppearance.BorderColor = OptionControlFactory.ArgbToColor(colors.Border);

        foreach (Control c in Controls)
        {
            if (c is Panel p && p != _scrollPanel)
                p.BackColor = OptionControlFactory.ArgbToColor(colors.Surface);
        }

        // コントロールを再構築してテーマ反映
        RebuildControls();
    }

    private void OnCategoriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            try { Invoke(RebuildControls); } catch (ObjectDisposedException) { }
            return;
        }
        RebuildControls();
    }

    private void RebuildControls()
    {
        _scrollPanel.SuspendLayout();
        try
        {
            // 既存コントロールを破棄
            foreach (Control c in _scrollPanel.Controls)
                c.Dispose();
            _scrollPanel.Controls.Clear();

            // カテゴリごとにグループを構築（逆順追加でDock.Topが正しい順序になるよう）
            var categories = _viewModel.Categories.ToList();
            categories.Reverse();

            foreach (var category in categories)
            {
                // カテゴリ内のオプションコントロール（逆順）
                var items = category.Items.ToList();
                items.Reverse();

                foreach (var item in items)
                {
                    var control = OptionControlFactory.Create(item, _colors);
                    control.Dock = DockStyle.Top;
                    control.Margin = new Padding(0, 2, 0, 2);
                    _scrollPanel.Controls.Add(control);
                }

                // カテゴリヘッダー
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
            _scrollPanel.ResumeLayout(true);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewModel.Categories.CollectionChanged -= OnCategoriesChanged;
        }
        base.Dispose(disposing);
    }
}
