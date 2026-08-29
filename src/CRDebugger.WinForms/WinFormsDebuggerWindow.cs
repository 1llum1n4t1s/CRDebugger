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
    /// <summary>フォーム生成後にマーシャル先を接続する UI スレッド実装。</summary>
    private readonly WinFormsUiThread? _uiThread;

    /// <summary>管理対象の <see cref="DebuggerForm"/> インスタンス。未生成または破棄済みの場合は null。</summary>
    private DebuggerForm? _form;

    /// <summary>独立したウィンドウ実装を生成する。</summary>
    public WinFormsDebuggerWindow()
    {
    }

    /// <summary>UI スレッド実装と対で管理されるウィンドウを生成する。</summary>
    internal WinFormsDebuggerWindow(WinFormsUiThread uiThread)
    {
        _uiThread = uiThread;
    }

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
        // 既存フォームが生きている場合は前面に表示して終了
        if (_form != null && !_form.IsDisposed)
        {
            _form.Show();
            _form.BringToFront();
            return;
        }

        // フォームを新規生成してフォームクローズイベントを購読
        _form = new DebuggerForm(viewModel);
        // InvokeRequired が正しく判定できるよう、UI スレッド上でハンドルを先に生成してから接続する
        _ = _form.Handle;
        _uiThread?.SetMarshalControl(_form);
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
    /// DrawToBitmap は UI スレッド必須なので Invoke で UI スレッドに戻して実行し、
    /// PNG エンコードはバックグラウンドスレッドにオフロードして UI のブロックを避ける。
    /// フォームが存在しない場合や取得に失敗した場合は null を返す。
    /// </summary>
    /// <returns>PNGバイト配列。取得できない場合は null。</returns>
    public async Task<byte[]?> CaptureScreenshotAsync()
    {
        // フォームが存在しない場合は null を返す
        if (_form == null || _form.IsDisposed)
            return null;

        Bitmap? bitmap = null;
        try
        {
            // ----- Phase 1: UI スレッドで DrawToBitmap を実行してビットマップを取得 -----
            // DrawToBitmap は WinForms ハンドルにアクセスするため UI スレッドからの呼び出しが必須
            bitmap = await InvokeOnUiThreadAsync(() =>
            {
                if (_form == null || _form.IsDisposed) return null;
                var bounds = _form.Bounds;
                if (bounds.Width <= 0 || bounds.Height <= 0) return null;

                var bmp = new Bitmap(bounds.Width, bounds.Height);
                _form.DrawToBitmap(bmp, new Rectangle(0, 0, bounds.Width, bounds.Height));
                return bmp;
            }).ConfigureAwait(false);

            if (bitmap == null) return null;

            // ----- Phase 2: PNG エンコードをバックグラウンドスレッドにオフロード -----
            var localBitmap = bitmap;
            return await Task.Run<byte[]?>(() =>
            {
                try
                {
                    using var ms = new MemoryStream();
                    localBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return ms.ToArray();
                }
                catch
                {
                    return null;
                }
            }).ConfigureAwait(false);
        }
        catch
        {
            // スクリーンショット取得失敗時は null を返す
            return null;
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    /// <summary>
    /// 指定したデリゲートをフォームの UI スレッド上で実行する。
    /// 既に UI スレッド上なら直接呼び、別スレッドなら <see cref="Control.BeginInvoke(Delegate)"/> でマーシャリングする。
    /// </summary>
    /// <typeparam name="T">戻り値の型。</typeparam>
    /// <param name="func">UI スレッドで実行する関数。</param>
    /// <returns>関数の戻り値を含む <see cref="Task{T}"/>。</returns>
    private Task<T?> InvokeOnUiThreadAsync<T>(Func<T?> func) where T : class
    {
        if (_form == null || _form.IsDisposed)
            return Task.FromResult<T?>(null);

        if (!_form.InvokeRequired)
        {
            return Task.FromResult(func());
        }

        var tcs = new TaskCompletionSource<T?>();
        try
        {
            _form.BeginInvoke(() =>
            {
                try { tcs.SetResult(func()); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }
        return tcs.Task;
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
