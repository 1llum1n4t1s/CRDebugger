using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CRDebugger.Core.ViewModels;

namespace CRDebugger.Wpf.Views;

/// <summary>
/// メモリ使用量・GC 情報・操作プロファイルを表示する UserControl。
/// MemoryHistory の変更を購読してリアルタイムで折れ線グラフを再描画する。
/// </summary>
public partial class ProfilerView : UserControl
{
    /// <summary>
    /// ProfilerView を初期化し、DataContext 変更イベントを購読する
    /// </summary>
    public ProfilerView()
    {
        InitializeComponent();
        // DataContext が差し替わった際にメモリ履歴の購読を切り替える
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// DataContext が変更されたときのイベントハンドラ。
    /// 旧 ViewModel のメモリ履歴購読を解除し、新 ViewModel の購読を開始する。
    /// </summary>
    /// <param name="sender">イベント送信元</param>
    /// <param name="e">旧値と新値を含む DependencyPropertyChangedEventArgs</param>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 旧 ViewModel のメモリ履歴変更イベントを解除してリークを防ぐ
        if (e.OldValue is ProfilerViewModel oldVm)
        {
            oldVm.MemoryHistory.CollectionChanged -= OnMemoryHistoryChanged;
        }

        // 新 ViewModel のメモリ履歴変更イベントを購読してグラフ更新を有効化
        if (e.NewValue is ProfilerViewModel vm)
        {
            vm.MemoryHistory.CollectionChanged += OnMemoryHistoryChanged;
        }
    }

    /// <summary>
    /// メモリ使用量履歴コレクションが変更されたときのイベントハンドラ。
    /// UI スレッドで非同期にグラフを再描画する。
    /// </summary>
    /// <param name="sender">変更があった ObservableCollection&lt;double&gt;</param>
    /// <param name="e">コレクション変更の詳細情報</param>
    private void OnMemoryHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // sender を ObservableCollection<double> にキャストしてグラフ描画をスケジュール
        if (sender is ObservableCollection<double> history)
        {
            // Dispatcher.InvokeAsync で UI スレッドに処理を委譲
            Dispatcher.InvokeAsync(() => DrawChart(history));
        }
    }

    /// <summary>
    /// メモリ使用量の折れ線グラフを Canvas 上に描画する。
    /// データが 2 点未満または Canvas サイズが 0 の場合は描画をスキップする。
    /// </summary>
    /// <param name="data">描画するメモリ使用量（MB）の時系列データ</param>
    private void DrawChart(ObservableCollection<double> data)
    {
        // 前回の描画内容をクリア
        MemoryChart.Children.Clear();

        // 2点未満ではポリラインを描画できないためスキップ
        if (data.Count < 2)
            return;

        // Canvas の実際のサイズを取得
        var width = MemoryChart.ActualWidth;
        var height = MemoryChart.ActualHeight;

        // Canvas がまだレイアウト計算されていない場合はスキップ
        if (width <= 0 || height <= 0)
            return;

        // 正規化のために最大値・最小値・値域を計算
        var maxVal = data.Max();
        var minVal = data.Min();
        var range = maxVal - minVal;
        // 値域が極小の場合は 1 に固定して除算エラーを防ぐ
        if (range < 0.1) range = 1;

        // データ点間の水平間隔を計算
        var stepX = width / (data.Count - 1);

        // ポリラインでメモリ使用量の推移を描画
        var polyline = new Polyline
        {
            // テーマのプライマリカラーを線の色として使用（取得失敗時は CornflowerBlue）
            Stroke = FindResource("PrimaryBrush") as Brush ?? Brushes.CornflowerBlue,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round  // 折れ目を丸くして見た目を改善
        };

        // 各データ点を Canvas 座標に変換してポリラインに追加
        for (var i = 0; i < data.Count; i++)
        {
            var x = i * stepX;
            // 値を 0〜1 に正規化してから Canvas 高さに変換（上下を反転）
            var normalizedY = (data[i] - minVal) / range;
            // 上下 5px のパディングを確保してクリッピングを防ぐ
            var y = height - (normalizedY * (height - 10)) - 5;
            polyline.Points.Add(new Point(x, y));
        }

        MemoryChart.Children.Add(polyline);

        // 最新値（末尾の値）を右上にラベル表示
        var latest = data[^1];  // C# 8.0 のインデックス演算子で末尾要素を取得
        var latestLabel = new TextBlock
        {
            Text = $"{latest:F1} MB",  // 小数点1桁で MB 表示
            Foreground = FindResource("OnSurfaceBrush") as Brush ?? Brushes.White,
            FontSize = 10,
            FontFamily = new FontFamily("Consolas")  // 等幅フォントで数値を揃える
        };
        // ラベルを Canvas の右上に配置
        Canvas.SetRight(latestLabel, 4);
        Canvas.SetTop(latestLabel, 4);
        MemoryChart.Children.Add(latestLabel);
    }
}
