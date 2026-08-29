using System.IO;
using Avalonia.Threading;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;

namespace CRDebugger.Avalonia;

/// <summary>
/// <see cref="IDebuggerWindow"/> の Avalonia 実装。
/// デバッガーウィンドウの生成・表示・非表示・テーマ適用を担当する。
/// </summary>
public sealed class AvaloniaDebuggerWindow : IDebuggerWindow
{
    /// <summary>実際に表示する Avalonia ウィンドウのインスタンス（未表示時は null）</summary>
    private Windows.DebuggerWindow? _window;

    /// <summary>
    /// ウィンドウが現在表示中かどうかを取得する。
    /// </summary>
    public bool IsVisible => _window?.IsVisible ?? false;

    /// <summary>
    /// 指定した ViewModel でデバッガーウィンドウを表示する。
    /// ウィンドウが未作成の場合だけ生成し、非表示の既存ウィンドウは再利用する。
    /// </summary>
    /// <param name="viewModel">ウィンドウにバインドする DebuggerViewModel</param>
    public void Show(DebuggerViewModel viewModel)
    {
        if (_window == null)
        {
            var window = new Windows.DebuggerWindow { DataContext = viewModel };
            // ウィンドウが実際に閉じられたら参照をクリアして次回 Show で再生成可能にする
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(_window, window))
                    _window = null;
            };
            _window = window;
        }
        else
        {
            _window.DataContext = viewModel;
        }

        _window.Show();
    }

    /// <summary>
    /// デバッガーウィンドウを非表示にする。
    /// ウィンドウが存在しない場合は何もしない。
    /// </summary>
    public void Hide() => _window?.Hide();

    /// <summary>
    /// テーマカラーをウィンドウに適用する。
    /// <para>
    /// <b>Avalonia 版は意図的に何もしない（ダーク配色固定）。</b>
    /// FluentTheme の <c>SystemAccentColor</c>（＝ユーザーの OS 設定）が背景へ流入するのを避けるため、
    /// Avalonia の各 AXAML は不透明リテラル色と <c>ControlTheme</c> によるテンプレート差し替えで配色を固定している
    /// （CLAUDE.md「Avalonia 色指定の鉄則」を参照）。そのため実行時のテーマ切り替えには対応していない。
    /// WPF / WinForms 版はこのメソッドで実際にウィンドウ配色を更新する。
    /// </para>
    /// </summary>
    /// <param name="colors">適用するテーマカラー情報（Avalonia 版では未使用）</param>
    public void ApplyTheme(ThemeColors colors)
    {
        // 意図的に空実装。理由は上記 XML doc を参照。
    }

    /// <summary>
    /// ウィンドウのスクリーンショットを PNG バイト配列として非同期に取得する。
    /// Avalonia の <see cref="global::Avalonia.Media.Imaging.RenderTargetBitmap"/> を使ってウィンドウをレンダリングし PNG エンコードする。
    /// ウィンドウが存在しない・サイズが不正・例外発生時は null を返す。
    /// </summary>
    /// <returns>PNG バイト配列。取得失敗時は null。</returns>
    public async Task<byte[]?> CaptureScreenshotAsync()
    {
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
                return CaptureScreenshotOnUiThread();

            return await Dispatcher.UIThread.InvokeAsync(CaptureScreenshotOnUiThread);
        }
        catch
        {
            // スクリーンショット取得失敗時は null を返す（例外を伝播させない）
            return null;
        }
    }

    /// <summary>UI スレッド上でウィンドウをレンダリングして PNG バイト列を返す。</summary>
    private byte[]? CaptureScreenshotOnUiThread()
    {
        var window = _window;
        if (window == null) return null;

        var width = (int)window.ClientSize.Width;
        var height = (int)window.ClientSize.Height;
        if (width <= 0 || height <= 0) return null;

        var size = new global::Avalonia.PixelSize(width, height);
        var dpi = new global::Avalonia.Vector(96, 96);
        using var rtb = new global::Avalonia.Media.Imaging.RenderTargetBitmap(size, dpi);
        rtb.Render(window);
        using var ms = new MemoryStream();
        rtb.Save(ms);
        return ms.ToArray();
    }
}
