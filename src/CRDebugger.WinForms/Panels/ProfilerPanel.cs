using System.Collections.Specialized;
using System.ComponentModel;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.WinForms.Controls;

namespace CRDebugger.WinForms.Panels;

/// <summary>
/// プロファイラーパネル
/// メモリ使用量、GC情報、FPSをリアルタイム表示する
/// メモリ履歴の簡易グラフ描画も含む
/// </summary>
public sealed class ProfilerPanel : Panel
{
    private readonly ProfilerViewModel _viewModel;
    private readonly Label _titleLabel;
    private readonly Label _fpsLabel;
    private readonly Label _workingSetLabel;
    private readonly Label _privateMemLabel;
    private readonly Label _gcMemLabel;
    private readonly Label _gen0Label;
    private readonly Label _gen1Label;
    private readonly Label _gen2Label;
    private readonly Button _gcButton;
    private readonly Panel _graphPanel;
    private ThemeColors _colors;

    public ProfilerPanel(ProfilerViewModel viewModel, ThemeColors colors)
    {
        _viewModel = viewModel;
        _colors = colors;

        // ヘッダー
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(12, 8, 12, 4),
        };

        _titleLabel = new Label
        {
            Text = "\u2261 Profiler",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Left,
        };

        _gcButton = new Button
        {
            Text = "Force GC",
            FlatStyle = FlatStyle.Flat,
            Size = new Size(90, 28),
            Dock = DockStyle.Right,
            Cursor = Cursors.Hand,
        };
        _gcButton.FlatAppearance.BorderSize = 1;
        _gcButton.Click += (_, _) => _viewModel.GcCollectCommand.Execute(null);

        headerPanel.Controls.Add(_titleLabel);
        headerPanel.Controls.Add(_gcButton);
        Controls.Add(headerPanel);

        // メトリクスパネル
        var metricsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 160,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(16, 8, 16, 8),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
        };

        metricsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        metricsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var rowHeight = new RowStyle(SizeType.Absolute, 22);
        for (var i = 0; i < 7; i++)
            metricsPanel.RowStyles.Add(rowHeight);

        // FPS
        AddMetricRow(metricsPanel, 0, "FPS:", out _fpsLabel);
        _fpsLabel.Text = "0.0";

        // Working Set
        AddMetricRow(metricsPanel, 1, "Working Set:", out _workingSetLabel);

        // Private Memory
        AddMetricRow(metricsPanel, 2, "Private Memory:", out _privateMemLabel);

        // GC Total Memory
        AddMetricRow(metricsPanel, 3, "GC Memory:", out _gcMemLabel);

        // GC世代カウント
        AddMetricRow(metricsPanel, 4, "Gen 0 Collections:", out _gen0Label);
        AddMetricRow(metricsPanel, 5, "Gen 1 Collections:", out _gen1Label);
        AddMetricRow(metricsPanel, 6, "Gen 2 Collections:", out _gen2Label);

        Controls.Add(metricsPanel);

        // メモリグラフパネル
        var graphLabel = new Label
        {
            Text = "  Memory History (MB)",
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        Controls.Add(graphLabel);

        _graphPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 8, 16, 16),
        };
        _graphPanel.Paint += PaintMemoryGraph;
        Controls.Add(_graphPanel);

        // ViewModelイベントの購読
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.MemoryHistory.CollectionChanged += OnMemoryHistoryChanged;

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
        var border = OptionControlFactory.ArgbToColor(colors.Border);
        var primary = OptionControlFactory.ArgbToColor(colors.Primary);

        BackColor = bg;
        _titleLabel.ForeColor = onBg;
        _gcButton.BackColor = surfaceAlt;
        _gcButton.ForeColor = onSurface;
        _gcButton.FlatAppearance.BorderColor = border;
        _graphPanel.BackColor = surface;

        // メトリクスの色更新
        _fpsLabel.ForeColor = primary;
        _workingSetLabel.ForeColor = primary;
        _privateMemLabel.ForeColor = primary;
        _gcMemLabel.ForeColor = primary;
        _gen0Label.ForeColor = onSurface;
        _gen1Label.ForeColor = onSurface;
        _gen2Label.ForeColor = onSurface;

        foreach (Control c in Controls)
        {
            if (c is Panel p && p != _graphPanel)
                p.BackColor = surface;
            if (c is TableLayoutPanel tlp)
            {
                tlp.BackColor = surface;
                foreach (Control child in tlp.Controls)
                {
                    if (child is Label lbl && lbl.Tag is "key")
                        lbl.ForeColor = OptionControlFactory.ArgbToColor(colors.OnSurfaceMuted);
                }
            }
            if (c is Label lb)
            {
                lb.ForeColor = onBg;
                lb.BackColor = surfaceAlt;
            }
        }
    }

    private void AddMetricRow(TableLayoutPanel table, int row, string keyText, out Label valueLabel)
    {
        var keyLabel = new Label
        {
            Text = keyText,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9),
            Tag = "key",
        };
        table.Controls.Add(keyLabel, 0, row);

        valueLabel = new Label
        {
            Text = "---",
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
        };
        table.Controls.Add(valueLabel, 1, row);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        void UpdateLabels()
        {
            _fpsLabel.Text = $"{_viewModel.Fps:F1}";
            _workingSetLabel.Text = _viewModel.WorkingSet;
            _privateMemLabel.Text = _viewModel.PrivateMemory;
            _gcMemLabel.Text = _viewModel.GcMemory;
            _gen0Label.Text = _viewModel.Gen0;
            _gen1Label.Text = _viewModel.Gen1;
            _gen2Label.Text = _viewModel.Gen2;
        }

        if (InvokeRequired)
        {
            try { Invoke(UpdateLabels); } catch (ObjectDisposedException) { }
        }
        else
        {
            UpdateLabels();
        }
    }

    private void OnMemoryHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            try { Invoke(() => _graphPanel.Invalidate()); } catch (ObjectDisposedException) { }
        }
        else
        {
            _graphPanel.Invalidate();
        }
    }

    private void PaintMemoryGraph(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var rect = _graphPanel.ClientRectangle;
        var pad = _graphPanel.Padding;
        var graphRect = new Rectangle(
            pad.Left, pad.Top,
            rect.Width - pad.Left - pad.Right,
            rect.Height - pad.Top - pad.Bottom);

        if (graphRect.Width <= 0 || graphRect.Height <= 0) return;

        // 背景
        using var bgBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.SurfaceAlt));
        g.FillRectangle(bgBrush, graphRect);

        // 枠線
        using var borderPen = new Pen(OptionControlFactory.ArgbToColor(_colors.Border), 1);
        g.DrawRectangle(borderPen, graphRect);

        // グリッド線
        using var gridPen = new Pen(Color.FromArgb(30, OptionControlFactory.ArgbToColor(_colors.OnSurface)), 1);
        for (var i = 1; i < 4; i++)
        {
            var y = graphRect.Y + graphRect.Height * i / 4;
            g.DrawLine(gridPen, graphRect.X, y, graphRect.Right, y);
        }

        var data = _viewModel.MemoryHistory.ToList();
        if (data.Count < 2) return;

        // データの範囲を計算
        var maxVal = data.Max();
        var minVal = data.Min();
        if (maxVal <= minVal) maxVal = minVal + 1;
        var range = maxVal - minVal;

        // グラフ描画
        var points = new PointF[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            var x = graphRect.X + (float)i / (data.Count - 1) * graphRect.Width;
            var y = graphRect.Bottom - (float)((data[i] - minVal) / range) * graphRect.Height;
            points[i] = new PointF(x, y);
        }

        // 塗りつぶし
        var fillPoints = new PointF[points.Length + 2];
        points.CopyTo(fillPoints, 0);
        fillPoints[points.Length] = new PointF(graphRect.Right, graphRect.Bottom);
        fillPoints[points.Length + 1] = new PointF(graphRect.X, graphRect.Bottom);

        using var fillBrush = new SolidBrush(
            Color.FromArgb(30, OptionControlFactory.ArgbToColor(_colors.Primary)));
        g.FillPolygon(fillBrush, fillPoints);

        // ライン
        using var linePen = new Pen(OptionControlFactory.ArgbToColor(_colors.Primary), 2);
        g.DrawLines(linePen, points);

        // 最新値を表示
        using var valFont = new Font("Segoe UI", 8);
        using var valBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.OnBackground));
        var latestVal = data[^1];
        g.DrawString($"{latestVal:F1} MB", valFont, valBrush,
            graphRect.Right - 60, graphRect.Y + 4);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.MemoryHistory.CollectionChanged -= OnMemoryHistoryChanged;
        }
        base.Dispose(disposing);
    }
}
