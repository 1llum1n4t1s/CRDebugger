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
    /// ウィンドウが未作成または非表示の場合は新たに生成して DataContext を設定する。
    /// </summary>
    /// <param name="viewModel">ウィンドウにバインドする DebuggerViewModel</param>
    public void Show(DebuggerViewModel viewModel)
    {
        // ウィンドウが存在しない、または既に閉じられている場合は新規作成する
        if (_window == null || !_window.IsVisible)
        {
            _window = new Windows.DebuggerWindow { DataContext = viewModel };
            // ウィンドウが実際に閉じられたら参照をクリアして次回 Show で再生成可能にする
            _window.Closed += (_, _) => _window = null;
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
    public Task<byte[]?> CaptureScreenshotAsync()
    {
        if (_window == null) return Task.FromResult<byte[]?>(null);
        try
        {
            var width = (int)_window.ClientSize.Width;
            var height = (int)_window.ClientSize.Height;
            // クライアントサイズが不正な場合（最小化など）はスキップ
            if (width <= 0 || height <= 0) return Task.FromResult<byte[]?>(null);

            var size = new global::Avalonia.PixelSize(width, height);
            var dpi = new global::Avalonia.Vector(96, 96);
            using var rtb = new global::Avalonia.Media.Imaging.RenderTargetBitmap(size, dpi);
            rtb.Render(_window);
            using var ms = new MemoryStream();
            rtb.Save(ms);
            return Task.FromResult<byte[]?>(ms.ToArray());
        }
        catch
        {
            // スクリーンショット取得失敗時は null を返す（例外を伝播させない）
            return Task.FromResult<byte[]?>(null);
        }
    }
}
