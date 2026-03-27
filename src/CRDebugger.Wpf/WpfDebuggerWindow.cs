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
        _viewModel = viewModel;

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

        // テーマ変更を監視して ViewModel の ThemeColors が変わった際に自動再適用
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DebuggerViewModel.ThemeColors))
            {
                _window?.ApplyThemeColors(viewModel.ThemeColors);
            }
        };

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

        byte[]? result = null;

        // UI スレッドで RenderTargetBitmap を使ってウィンドウを描画キャプチャ
        await _window.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                // ウィンドウの子孫要素全体の境界を取得（未使用だが将来のクロップ用に取得）
                var bounds = VisualTreeHelper.GetDescendantBounds(_window);
                var width = (int)_window.ActualWidth;
                var height = (int)_window.ActualHeight;

                // ウィンドウサイズが無効な場合はスキップ
                if (width <= 0 || height <= 0)
                    return;

                // DPI スケールを考慮した実ピクセル数で RenderTargetBitmap を生成
                var dpi = VisualTreeHelper.GetDpi(_window);
                var renderTarget = new RenderTargetBitmap(
                    (int)(width * dpi.DpiScaleX),   // DPI スケールを掛けた実幅
                    (int)(height * dpi.DpiScaleY),  // DPI スケールを掛けた実高さ
                    dpi.PixelsPerInchX,              // 水平 DPI
                    dpi.PixelsPerInchY,              // 垂直 DPI
                    PixelFormats.Pbgra32);           // アルファチャンネル付き 32bit フォーマット

                // ウィンドウ全体をビットマップにレンダリング
                renderTarget.Render(_window);

                // PNG エンコーダーでビットマップを PNG に変換
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));

                // MemoryStream を使って PNG バイト配列を取得
                using var stream = new MemoryStream();
                encoder.Save(stream);
                result = stream.ToArray();
            }
            catch
            {
                // スクリーンショット取得に失敗した場合は null を返す（例外は握りつぶす）
            }
        });

        return result;
    }
}
