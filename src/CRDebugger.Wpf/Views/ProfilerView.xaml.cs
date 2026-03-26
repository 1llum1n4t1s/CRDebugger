using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CRDebugger.Core.ViewModels;

namespace CRDebugger.Wpf.Views;

/// <summary>
/// プロファイラービュー - メモリ・GC情報と簡易グラフ
/// </summary>
public partial class ProfilerView : UserControl
{
    public ProfilerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ProfilerViewModel oldVm)
        {
            oldVm.MemoryHistory.CollectionChanged -= OnMemoryHistoryChanged;
        }

        if (e.NewValue is ProfilerViewModel vm)
        {
            vm.MemoryHistory.CollectionChanged += OnMemoryHistoryChanged;
        }
    }

    private void OnMemoryHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is ObservableCollection<double> history)
        {
            Dispatcher.InvokeAsync(() => DrawChart(history));
        }
    }

    private void DrawChart(ObservableCollection<double> data)
    {
        MemoryChart.Children.Clear();

        if (data.Count < 2)
            return;

        var width = MemoryChart.ActualWidth;
        var height = MemoryChart.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        var maxVal = data.Max();
        var minVal = data.Min();
        var range = maxVal - minVal;
        if (range < 0.1) range = 1;

        var stepX = width / (data.Count - 1);

        // ポリラインでメモリ使用量を描画
        var polyline = new Polyline
        {
            Stroke = FindResource("PrimaryBrush") as Brush ?? Brushes.CornflowerBlue,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };

        for (var i = 0; i < data.Count; i++)
        {
            var x = i * stepX;
            var normalizedY = (data[i] - minVal) / range;
            var y = height - (normalizedY * (height - 10)) - 5;
            polyline.Points.Add(new Point(x, y));
        }

        MemoryChart.Children.Add(polyline);

        // 最新値をラベル表示
        var latest = data[^1];
        var latestLabel = new TextBlock
        {
            Text = $"{latest:F1} MB",
            Foreground = FindResource("OnSurfaceBrush") as Brush ?? Brushes.White,
            FontSize = 10,
            FontFamily = new FontFamily("Consolas")
        };
        Canvas.SetRight(latestLabel, 4);
        Canvas.SetTop(latestLabel, 4);
        MemoryChart.Children.Add(latestLabel);
    }
}
