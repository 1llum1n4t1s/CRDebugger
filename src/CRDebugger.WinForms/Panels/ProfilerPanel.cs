using System.Collections.Specialized;
using System.ComponentModel;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.WinForms.Controls;

namespace CRDebugger.WinForms.Panels;

/// <summary>
/// プロファイラーパネル。
/// FPS、メモリ使用量（Working Set / Private Memory / GC Memory）、
/// GCコレクション回数（Gen0/1/2）をリアルタイムに表示する。
/// 下部にはメモリ使用量の時系列グラフを描画する。
/// モダンデザイン: 改善されたスペーシング、フォント、グラフ描画を採用。
/// </summary>
public sealed class ProfilerPanel : Panel
{
    /// <summary>プロファイラー計測値のロジックを持つ ViewModel。</summary>
    private readonly ProfilerViewModel _viewModel;

    /// <summary>パネルタイトルを表示するラベル。</summary>
    private readonly Label _titleLabel;

    /// <summary>現在の FPS を表示するラベル。</summary>
    private readonly Label _fpsLabel;

    /// <summary>Working Set メモリ使用量を表示するラベル。</summary>
    private readonly Label _workingSetLabel;

    /// <summary>Private メモリ使用量を表示するラベル。</summary>
    private readonly Label _privateMemLabel;

    /// <summary>GC 管理メモリ使用量を表示するラベル。</summary>
    private readonly Label _gcMemLabel;

    /// <summary>Gen 0 GC コレクション回数を表示するラベル。</summary>
    private readonly Label _gen0Label;

    /// <summary>Gen 1 GC コレクション回数を表示するラベル。</summary>
    private readonly Label _gen1Label;

    /// <summary>Gen 2 GC コレクション回数を表示するラベル。</summary>
    private readonly Label _gen2Label;

    /// <summary>手動 GC 収集を実行するボタン。</summary>
    private readonly Button _gcButton;

    /// <summary>メモリ使用量の時系列グラフを描画するパネル。</summary>
    private readonly Panel _graphPanel;

    /// <summary>現在適用中のテーマカラー。</summary>
    private ThemeColors _colors;

    /// <summary>
    /// <see cref="ProfilerPanel"/> を初期化してUIコントロールを構築する。
    /// ViewModel のプロパティ変更と MemoryHistory コレクション変更を購読する。
    /// </summary>
    /// <param name="viewModel">プロファイラー計測値のロジックを持つ <see cref="ProfilerViewModel"/>。</param>
    /// <param name="colors">初期適用するテーマカラー情報。</param>
    public ProfilerPanel(ProfilerViewModel viewModel, ThemeColors colors)
    {
        _viewModel = viewModel;
        _colors = colors;
        // ダブルバッファリングで描画のちらつきを防ぐ
        DoubleBuffered = true;

        // ヘッダーパネル（タイトルラベル + Force GC ボタン）
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(16, 12, 16, 8),
        };

        // パネルタイトル（三本線アイコン + Profiler）
        _titleLabel = new Label
        {
            Text = "\u2261 Profiler",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Left,
        };

        // 手動 GC 収集を実行するボタン（右端に配置）
        _gcButton = new Button
        {
            Text = "Force GC",
            FlatStyle = FlatStyle.Flat,
            Size = new Size(100, 30),
            Dock = DockStyle.Right,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9),
        };
        _gcButton.FlatAppearance.BorderSize = 1;
        // クリック時に ViewModel の GcCollectCommand を実行
        _gcButton.Click += (_, _) => _viewModel.GcCollectCommand.Execute(null);

        headerPanel.Controls.Add(_titleLabel);
        headerPanel.Controls.Add(_gcButton);
        Controls.Add(headerPanel);

        // メトリクス表示テーブル（2列: キー名 + 値）
        var metricsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 180,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(20, 12, 20, 12),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
        };

        // 左列: 固定幅170pxのキー名列
        metricsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        // 右列: 残りすべての幅を使う値列
        metricsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // 各行の高さを24pxで統一
        var rowHeight = new RowStyle(SizeType.Absolute, 24);
        for (var i = 0; i < 7; i++)
            metricsPanel.RowStyles.Add(rowHeight);

        // FPS メトリクス行
        AddMetricRow(metricsPanel, 0, "FPS:", out _fpsLabel);
        _fpsLabel.Text = "0.0";

        // Working Set メモリ使用量行
        AddMetricRow(metricsPanel, 1, "Working Set:", out _workingSetLabel);

        // Private メモリ使用量行
        AddMetricRow(metricsPanel, 2, "Private Memory:", out _privateMemLabel);

        // GC 管理メモリ使用量行
        AddMetricRow(metricsPanel, 3, "GC Memory:", out _gcMemLabel);

        // GC 世代別コレクション回数行（Gen0/1/2）
        AddMetricRow(metricsPanel, 4, "Gen 0 Collections:", out _gen0Label);
        AddMetricRow(metricsPanel, 5, "Gen 1 Collections:", out _gen1Label);
        AddMetricRow(metricsPanel, 6, "Gen 2 Collections:", out _gen2Label);

        Controls.Add(metricsPanel);

        // メモリグラフのタイトルラベル
        var graphLabel = new Label
        {
            Text = "  Memory History (MB)",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        Controls.Add(graphLabel);

        // メモリ使用量の時系列グラフを描画するパネル（残り領域をすべて使用）
        _graphPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 10, 20, 20),
        };
        // Paint イベントでカスタムグラフを描画
        _graphPanel.Paint += PaintMemoryGraph;
        Controls.Add(_graphPanel);

        // ViewModelのプロパティ変更とメモリ履歴変更を監視
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.MemoryHistory.CollectionChanged += OnMemoryHistoryChanged;

        // 初期テーマを適用
        ApplyTheme(colors);
    }

    /// <summary>
    /// 指定したテーマカラーをパネル全体に適用する。
    /// メトリクスラベル、グラフパネル、ボタンなどの色を一括更新する。
    /// </summary>
    /// <param name="colors">適用するテーマカラー情報。</param>
    public void ApplyTheme(ThemeColors colors)
    {
        _colors = colors;
        // テーマカラーを WinForms の Color 型に変換
        var bg = OptionControlFactory.ArgbToColor(colors.Background);
        var surface = OptionControlFactory.ArgbToColor(colors.Surface);
        var onBg = OptionControlFactory.ArgbToColor(colors.OnBackground);
        var onSurface = OptionControlFactory.ArgbToColor(colors.OnSurface);
        var surfaceAlt = OptionControlFactory.ArgbToColor(colors.SurfaceAlt);
        var border = OptionControlFactory.ArgbToColor(colors.Border);
        var primary = OptionControlFactory.ArgbToColor(colors.Primary);

        // 基本コントロールにテーマカラーを適用
        BackColor = bg;
        _titleLabel.ForeColor = onBg;
        _gcButton.BackColor = surfaceAlt;
        _gcButton.ForeColor = onSurface;
        _gcButton.FlatAppearance.BorderColor = border;
        _graphPanel.BackColor = surface;

        // メトリクス値ラベルの色を更新（FPS/メモリ系はプライマリ色、GC系はOnSurface色）
        _fpsLabel.ForeColor = primary;
        _workingSetLabel.ForeColor = primary;
        _privateMemLabel.ForeColor = primary;
        _gcMemLabel.ForeColor = primary;
        _gen0Label.ForeColor = onSurface;
        _gen1Label.ForeColor = onSurface;
        _gen2Label.ForeColor = onSurface;

        // 子コントロール全体にテーマカラーを適用
        foreach (Control c in Controls)
        {
            // グラフパネル以外のパネルには Surface 色を適用
            if (c is Panel p && p != _graphPanel)
                p.BackColor = surface;
            if (c is TableLayoutPanel tlp)
            {
                tlp.BackColor = surface;
                // メトリクステーブルのキー名ラベルにはミュートカラーを適用
                foreach (Control child in tlp.Controls)
                {
                    if (child is Label lbl && lbl.Tag is "key")
                        lbl.ForeColor = OptionControlFactory.ArgbToColor(colors.OnSurfaceMuted);
                }
            }
            // グラフタイトルラベルには OnBackground 色を適用
            if (c is Label lb)
            {
                lb.ForeColor = onBg;
                lb.BackColor = surfaceAlt;
            }
        }
    }

    /// <summary>
    /// メトリクステーブルにキー名ラベルと値ラベルの行を追加する。
    /// キー名ラベルの Tag に "key" を設定してテーマ適用時に識別できるようにする。
    /// </summary>
    /// <param name="table">行を追加する対象の <see cref="TableLayoutPanel"/>。</param>
    /// <param name="row">追加する行インデックス（0始まり）。</param>
    /// <param name="keyText">キー名ラベルに表示するテキスト。</param>
    /// <param name="valueLabel">生成した値ラベルを受け取る出力パラメーター。</param>
    private void AddMetricRow(TableLayoutPanel table, int row, string keyText, out Label valueLabel)
    {
        // キー名ラベルを左列に追加（Tag "key" でテーマ適用時に識別）
        var keyLabel = new Label
        {
            Text = keyText,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f),
            Tag = "key",
        };
        table.Controls.Add(keyLabel, 0, row);

        // 値ラベルを右列に追加（初期値は "---" でプレースホルダー表示）
        valueLabel = new Label
        {
            Text = "---",
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
        };
        table.Controls.Add(valueLabel, 1, row);
    }

    /// <summary>
    /// ViewModel のプロパティ変更イベントハンドラー。
    /// FPS・メモリ・GCコレクション回数の値ラベルを最新値で一括更新する。
    /// UIスレッド以外からの呼び出しは Invoke でマーシャリングする。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">プロパティ変更イベント引数。</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        void UpdateLabels()
        {
            // 全メトリクスラベルを最新の ViewModel 値で更新
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
            // フォームが破棄済みの場合の ObjectDisposedException を握りつぶす
            try { Invoke(UpdateLabels); } catch (ObjectDisposedException) { }
        }
        else
        {
            UpdateLabels();
        }
    }

    /// <summary>
    /// MemoryHistory コレクション変更イベントハンドラー。
    /// 新しいデータが追加されるたびにグラフパネルを再描画する。
    /// UIスレッド以外からの呼び出しは Invoke でマーシャリングする。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">コレクション変更イベント引数。</param>
    private void OnMemoryHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            // グラフパネルの無効化（再描画リクエスト）をUIスレッドでマーシャリング
            try { Invoke(() => _graphPanel.Invalidate()); } catch (ObjectDisposedException) { }
        }
        else
        {
            _graphPanel.Invalidate();
        }
    }

    /// <summary>
    /// メモリ使用量の時系列グラフをカスタム描画する。
    /// 背景・グリッド線・塗りつぶしポリゴン・折れ線グラフ・最新値テキストを描画する。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">Paint イベント引数。</param>
    private void PaintMemoryGraph(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        // スムージングモードを有効化して線を滑らかに描画
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // パディングを考慮したグラフ描画領域を計算
        var rect = _graphPanel.ClientRectangle;
        var pad = _graphPanel.Padding;
        var graphRect = new Rectangle(
            pad.Left, pad.Top,
            rect.Width - pad.Left - pad.Right,
            rect.Height - pad.Top - pad.Bottom);

        // 描画領域が無効な場合はスキップ
        if (graphRect.Width <= 0 || graphRect.Height <= 0) return;

        // グラフ背景を塗りつぶす
        using var bgBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.SurfaceAlt));
        g.FillRectangle(bgBrush, graphRect);

        // グラフ枠線を描画（半透明の白）
        using var borderPen = new Pen(Color.FromArgb(20, 255, 255, 255), 1);
        g.DrawRectangle(borderPen, graphRect);

        // 水平グリッド線を4分割で描画
        using var gridPen = new Pen(Color.FromArgb(15, 255, 255, 255), 1);
        for (var i = 1; i < 4; i++)
        {
            var y = graphRect.Y + graphRect.Height * i / 4;
            g.DrawLine(gridPen, graphRect.X, y, graphRect.Right, y);
        }

        // メモリ履歴データを取得（データが2点以上ないとグラフは描画しない）
        var data = _viewModel.MemoryHistory.ToList();
        if (data.Count < 2) return;

        // データの最大・最小値を計算してグラフのY軸スケールを決定
        var maxVal = data.Max();
        var minVal = data.Min();
        // 最大値と最小値が同じ場合はレンジを1として0除算を防ぐ
        if (maxVal <= minVal) maxVal = minVal + 1;
        var range = maxVal - minVal;

        // 各データ点のX・Y座標を計算してPointF配列に変換
        var points = new PointF[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            // X座標: グラフ幅を等分して左から右に配置
            var x = graphRect.X + (float)i / (data.Count - 1) * graphRect.Width;
            // Y座標: 最小値が下端、最大値が上端になるように正規化して反転
            var y = graphRect.Bottom - (float)((data[i] - minVal) / range) * graphRect.Height;
            points[i] = new PointF(x, y);
        }

        // 折れ線の下側を塗りつぶすためのポリゴン頂点を作成（右下・左下を追加して閉じる）
        var fillPoints = new PointF[points.Length + 2];
        points.CopyTo(fillPoints, 0);
        fillPoints[points.Length] = new PointF(graphRect.Right, graphRect.Bottom);
        fillPoints[points.Length + 1] = new PointF(graphRect.X, graphRect.Bottom);

        // 塗りつぶし領域を半透明のプライマリ色で描画（グラデーション効果）
        using var fillBrush = new SolidBrush(
            Color.FromArgb(25, OptionControlFactory.ArgbToColor(_colors.Primary)));
        g.FillPolygon(fillBrush, fillPoints);

        // 折れ線グラフをプライマリ色の2px線で描画
        using var linePen = new Pen(OptionControlFactory.ArgbToColor(_colors.Primary), 2);
        g.DrawLines(linePen, points);

        // グラフ右上に最新のメモリ値（MB）をテキストで表示
        using var valFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var valBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.OnBackground));
        var latestVal = data[^1];
        g.DrawString($"{latestVal:F1} MB", valFont, valBrush,
            graphRect.Right - 65, graphRect.Y + 6);
    }

    /// <summary>
    /// リソースを解放する。ViewModel のイベント購読を解除してメモリリークを防ぐ。
    /// </summary>
    /// <param name="disposing">マネージドリソースを解放する場合は true。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // プロパティ変更とメモリ履歴変更イベントの購読を解除
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.MemoryHistory.CollectionChanged -= OnMemoryHistoryChanged;
        }
        base.Dispose(disposing);
    }
}
