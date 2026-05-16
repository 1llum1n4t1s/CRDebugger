using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.Wpf.Windows;

namespace CRDebugger.Wpf;

/// <summary>
/// IDebuggerWindow インターフェースの WPF 実装クラス。
/// DebuggerWindow のライフサイクル管理（生成・表示・非表示）とテーマ適用、
/// スクリーンショット取得を担当する。
/// </summary>
public sealed class WpfDebuggerWindow : IDebuggerWindow
{
    /// <summary>実際の WPF ウィンドウインスタンス（未表示時は null）</summary>
    private DebuggerWindow? _window;

    /// <summary>現在バインドされている DebuggerViewModel の参照</summary>
    private DebuggerViewModel? _viewModel;

    /// <summary>テーマ変更監視用の PropertyChanged ハンドラ（重複購読防止用）</summary>
    private PropertyChangedEventHandler? _themeChangedHandler;

    /// <summary>
    /// デバッガーウィンドウが現在画面上に表示されているかどうかを取得する
    /// </summary>
    public bool IsVisible => _window?.IsVisible ?? false;

    /// <summary>
    /// デバッガーウィンドウを表示する。
    /// ウィンドウが未生成またはアンロード済みの場合は新規生成する。
    /// テーマ変更を監視して自動的に再適用する。
    /// </summary>
    /// <param name="viewModel">ウィンドウに設定する DebuggerViewModel</param>
    public void Show(DebuggerViewModel viewModel)
    {
        // ウィンドウが未生成またはすでにアンロードされている場合は再生成
        if (_window == null || !_window.IsLoaded)
        {
            _window = new DebuggerWindow();
            // ウィンドウが閉じられたら参照をクリアしてメモリリークを防ぐ
            _window.Closed += (_, _) => _window = null;
        }

        // DataContext を設定してからテーマカラーを適用
        _window.DataContext = viewModel;
        _window.ApplyThemeColors(viewModel.ThemeColors);

        // 旧 ViewModel に対する PropertyChanged ハンドラを解除してメモリリークを防ぐ
        // ※ _viewModel 代入よりも前にやらないと旧 ViewModel への参照が失われて解除できなくなる
        if (_viewModel != null && _themeChangedHandler != null)
        {
            _viewModel.PropertyChanged -= _themeChangedHandler;
        }

        // 旧 ViewModel の解除が終わってから新 ViewModel に切り替える
        _viewModel = viewModel;

        // テーマ変更を監視して ViewModel の ThemeColors が変わった際に自動再適用
        _themeChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(DebuggerViewModel.ThemeColors))
            {
                _window?.ApplyThemeColors(viewModel.ThemeColors);
            }
        };
        viewModel.PropertyChanged += _themeChangedHandler;

        // ウィンドウを表示してフォーカスを当てる
        _window.Show();
        _window.Activate();
    }

    /// <summary>
    /// デバッガーウィンドウを非表示にする。
    /// Close() ではなく Hide() を使いインスタンスを保持して再利用可能にする。
    /// </summary>
    public void Hide()
    {
        _window?.Hide();
    }

    /// <summary>
    /// テーマカラーを適用する
    /// </summary>
    /// <param name="colors">適用するテーマカラー群</param>
    public void ApplyTheme(ThemeColors colors)
    {
        _window?.ApplyThemeColors(colors);
    }

    /// <summary>
    /// デバッガーウィンドウのスクリーンショットを PNG バイト配列として非同期に取得する。
    /// ウィンドウが非表示または未生成の場合は null を返す。
    /// </summary>
    /// <returns>PNG エンコードされたスクリーンショットのバイト配列、取得失敗時は null</returns>
    public async Task<byte[]?> CaptureScreenshotAsync()
    {
        // ウィンドウが存在しない・非表示の場合はスクリーンショット取得不可
        if (_window == null || !_window.IsVisible)
            return null;

        // ----- Phase 1: UI スレッドで描画してピクセル配列を取り出す -----
        // RenderTargetBitmap.Render は UI スレッドからしか呼べないためここまでは Dispatcher 上で実行する
        var pixelData = await _window.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var width = (int)_window.ActualWidth;
                var height = (int)_window.ActualHeight;

                // ウィンドウサイズが無効な場合はスキップ
                if (width <= 0 || height <= 0) return null;

                // DPI スケールを考慮した実ピクセル数で RenderTargetBitmap を生成
                var dpi = VisualTreeHelper.GetDpi(_window);
                var pixelWidth = (int)(width * dpi.DpiScaleX);
                var pixelHeight = (int)(height * dpi.DpiScaleY);

                var renderTarget = new RenderTargetBitmap(
                    pixelWidth,
                    pixelHeight,
                    dpi.PixelsPerInchX,
                    dpi.PixelsPerInchY,
                    PixelFormats.Pbgra32);

                // ウィンドウ全体をビットマップにレンダリング（UI スレッド必須）
                renderTarget.Render(_window);

                // バックグラウンドで再利用できるようピクセル配列に焼き出して凍結する
                int stride = pixelWidth * 4; // Pbgra32 は 4 bytes/pixel
                var buffer = new byte[stride * pixelHeight];
                renderTarget.CopyPixels(buffer, stride, 0);

                return new PixelSnapshot(buffer, pixelWidth, pixelHeight, stride,
                    dpi.PixelsPerInchX, dpi.PixelsPerInchY);
            }
            catch
            {
                // 描画に失敗した場合は null を返す（例外は握りつぶす）
                return null;
            }
        });

        if (pixelData == null) return null;

        // ----- Phase 2: PNG エンコードをバックグラウンドスレッドにオフロード -----
        // PngBitmapEncoder.Save は CPU バウンドで重いので UI スレッドを離す
        return await Task.Run<byte[]?>(() =>
        {
            try
            {
                var bitmap = BitmapSource.Create(
                    pixelData.PixelWidth,
                    pixelData.PixelHeight,
                    pixelData.DpiX,
                    pixelData.DpiY,
                    PixelFormats.Pbgra32,
                    null,
                    pixelData.Buffer,
                    pixelData.Stride);
                // BitmapSource をスレッド境界を越えて使えるように凍結
                bitmap.Freeze();

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using var stream = new MemoryStream();
                encoder.Save(stream);
                return stream.ToArray();
            }
            catch
            {
                // エンコードに失敗した場合は null を返す
                return null;
            }
        });
    }

    /// <summary>
    /// UI スレッドで描画したピクセルバッファをバックグラウンドエンコードに引き渡すための DTO。
    /// </summary>
    private sealed record PixelSnapshot(
        byte[] Buffer,
        int PixelWidth,
        int PixelHeight,
        int Stride,
        double DpiX,
        double DpiY);
}
