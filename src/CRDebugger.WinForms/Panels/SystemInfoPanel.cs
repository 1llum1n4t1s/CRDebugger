using System.Collections.Specialized;
using System.ComponentModel;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.WinForms.Controls;

namespace CRDebugger.WinForms.Panels;

/// <summary>
/// システム情報パネル
/// カテゴリごとにグループ化された情報をListView形式で表示する
/// モダンデザイン: 改善されたスペーシングとフォント
/// </summary>
public sealed class SystemInfoPanel : Panel
{
    private readonly SystemInfoViewModel _viewModel;
    private readonly ListView _listView;
    private readonly Button _refreshButton;
    private readonly Label _titleLabel;
    private ThemeColors _colors;

    public SystemInfoPanel(SystemInfoViewModel viewModel, ThemeColors colors)
    {
        _viewModel = viewModel;
        _colors = colors;
        DoubleBuffered = true;

        // タイトルバー
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(16, 12, 16, 8),
        };

        _titleLabel = new Label
        {
            Text = "\u2139 System Information",
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

        // リストビュー
        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BorderStyle = BorderStyle.None,
            ShowGroups = true,
            Font = new Font("Segoe UI", 9.5f),
        };

        _listView.Columns.Add("Key", 220);
        _listView.Columns.Add("Value", 450);

        Controls.Add(_listView);

        // ViewModel変更を監視
        _viewModel.Groups.CollectionChanged += OnGroupsChanged;
        PopulateList();
        ApplyTheme(colors);
    }

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

        foreach (Control c in Controls)
        {
            if (c is Panel p)
                p.BackColor = OptionControlFactory.ArgbToColor(colors.Surface);
        }
    }

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (InvokeRequired)
            Invoke(PopulateList);
        else
            PopulateList();
    }

    private void PopulateList()
    {
        _listView.BeginUpdate();
        try
        {
            _listView.Items.Clear();
            _listView.Groups.Clear();

            foreach (var group in _viewModel.Groups)
            {
                var lvGroup = new ListViewGroup(group.Category, group.Category)
                {
                    HeaderAlignment = HorizontalAlignment.Left,
                };
                _listView.Groups.Add(lvGroup);

                foreach (var item in group.Items)
                {
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewModel.Groups.CollectionChanged -= OnGroupsChanged;
        }
        base.Dispose(disposing);
    }
}
