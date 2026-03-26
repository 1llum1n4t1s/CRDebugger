using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.WinForms.Forms;

namespace CRDebugger.WinForms;

/// <summary>
/// WinForms用デバッガーウィンドウ実装
/// DebuggerFormのライフサイクルを管理する
/// </summary>
public sealed class WinFormsDebuggerWindow : IDebuggerWindow
{
    private DebuggerForm? _form;
    private DebuggerViewModel? _viewModel;

    public bool IsVisible => _form != null && _form.Visible && !_form.IsDisposed;

    public void Show(DebuggerViewModel viewModel)
    {
        _viewModel = viewModel;

        if (_form != null && !_form.IsDisposed)
        {
            _form.Show();
            _form.BringToFront();
            return;
        }

        _form = new DebuggerForm(viewModel);
        _form.FormClosed += OnFormClosed;
        _form.Show();
    }

    public void Hide()
    {
        if (_form != null && !_form.IsDisposed)
        {
            _form.Hide();
        }
    }

    public void ApplyTheme(ThemeColors colors)
    {
        if (_form != null && !_form.IsDisposed)
        {
            _form.ApplyTheme(colors);
        }
    }

    public Task<byte[]?> CaptureScreenshotAsync()
    {
        if (_form == null || _form.IsDisposed)
            return Task.FromResult<byte[]?>(null);

        try
        {
            var bounds = _form.Bounds;
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            _form.DrawToBitmap(bitmap, new Rectangle(0, 0, bounds.Width, bounds.Height));

            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return Task.FromResult<byte[]?>(ms.ToArray());
        }
        catch
        {
            return Task.FromResult<byte[]?>(null);
        }
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        if (_form != null)
        {
            _form.FormClosed -= OnFormClosed;
            _form = null;
        }
    }
}
