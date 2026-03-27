using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.WinForms.Forms;

namespace CRDebugger.WinForms;

/// <summary>
/// WinForms用デバッガーウィンドウ実装。
/// <see cref="IDebuggerWindow"/> インターフェースを実装し、
/// <see cref="DebuggerForm"/> のライフサイクル（生成・表示・非表示・破棄）を管理する。
/// フォームが既に存在する場合は再利用し、閉じられた場合は次回 Show 時に再生成する。
/// </summary>
public sealed class WinFormsDebuggerWindow : IDebuggerWindow
{
    /// <summary>管理対象の <see cref="DebuggerForm"/> インスタンス。未生成または破棄済みの場合は null。</summary>
    private DebuggerForm? _form;

    /// <summary>現在バインドされている <see cref="DebuggerViewModel"/>。</summary>
    private DebuggerViewModel? _viewModel;

    /// <summary>
    /// デバッガーウィンドウが現在表示されているかどうかを取得する。
    /// フォームが存在し、Visible が true で、破棄されていない場合に true を返す。
    /// </summary>
    public bool IsVisible => _form != null && _form.Visible && !_form.IsDisposed;

    /// <summary>
    /// デバッガーウィンドウを表示する。
    /// フォームが既に存在する場合は前面に移動し、存在しない場合は新規生成して表示する。
    /// </summary>
    /// <param name="viewModel">デバッガーUIにバインドする <see cref="DebuggerViewModel"/>。</param>
    public void Show(DebuggerViewModel viewModel)
    {
        // ViewModelを保持
        _viewModel = viewModel;

        // 既存フォームが生きている場合は前面に表示して終了
        if (_form != null && !_form.IsDisposed)
        {
            _form.Show();
            _form.BringToFront();
            return;
        }

        // フォームを新規生成してフォームクローズイベントを購読
        _form = new DebuggerForm(viewModel);
        _form.FormClosed += OnFormClosed;
        _form.Show();
    }

    /// <summary>
    /// デバッガーウィンドウを非表示にする。
    /// フォームが存在し破棄されていない場合のみ非表示にする。
    /// </summary>
    public void Hide()
    {
        if (_form != null && !_form.IsDisposed)
        {
            _form.Hide();
        }
    }

    /// <summary>
    /// 指定したテーマカラーをデバッガーウィンドウに適用する。
    /// フォームが存在し破棄されていない場合のみ適用する。
    /// </summary>
    /// <param name="colors">適用するテーマカラー情報。</param>
    public void ApplyTheme(ThemeColors colors)
    {
        if (_form != null && !_form.IsDisposed)
        {
            _form.ApplyTheme(colors);
        }
    }

    /// <summary>
    /// デバッガーウィンドウのスクリーンショットをPNG形式のバイト配列として非同期に取得する。
    /// フォームが存在しない場合や取得に失敗した場合は null を返す。
    /// </summary>
    /// <returns>PNGバイト配列。取得できない場合は null。</returns>
    public Task<byte[]?> CaptureScreenshotAsync()
    {
        // フォームが存在しない場合は null を返す
        if (_form == null || _form.IsDisposed)
            return Task.FromResult<byte[]?>(null);

        try
        {
            // フォームの境界矩形を取得してビットマップに描画
            var bounds = _form.Bounds;
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            _form.DrawToBitmap(bitmap, new Rectangle(0, 0, bounds.Width, bounds.Height));

            // MemoryStream に PNG 形式で保存してバイト配列に変換
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return Task.FromResult<byte[]?>(ms.ToArray());
        }
        catch
        {
            // スクリーンショット取得失敗時は null を返す
            return Task.FromResult<byte[]?>(null);
        }
    }

    /// <summary>
    /// フォームが閉じられたときのイベントハンドラー。
    /// イベントの購読を解除してフォーム参照を null にクリアする。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">フォームクローズイベント引数。</param>
    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        if (_form != null)
        {
            // イベント購読を解除してメモリリークを防ぐ
            _form.FormClosed -= OnFormClosed;
            _form = null;
        }
    }
}
