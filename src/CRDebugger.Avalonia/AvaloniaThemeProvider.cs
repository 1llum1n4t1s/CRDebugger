using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using CRDebugger.Core.Abstractions;

namespace CRDebugger.Avalonia;

/// <summary>
/// Avalonia の <see cref="IPlatformSettings"/> を使った OS ダークモード検出の実装。
/// システムのカラーテーマ変更を監視し、コールバックで通知する。
/// </summary>
public sealed class AvaloniaThemeProvider : IThemeProvider
{
    /// <summary>テーマ変更時に呼び出すコールバック（監視停止時は null）</summary>
    private Action<bool>? _callback;

    /// <summary>
    /// 現在の OS テーマがダークモードかどうかを返す。
    /// Avalonia アプリケーションが未初期化の場合は false を返す。
    /// </summary>
    /// <returns>ダークモードなら true、ライトモードなら false</returns>
    public bool IsSystemDarkMode()
    {
        // Avalonia アプリケーションのインスタンスを取得する
        var app = Application.Current;
        if (app == null) return false;

        // PlatformSettings からカラーテーマのバリアントを取得する
        var variant = app.PlatformSettings?.GetColorValues().ThemeVariant;
        return variant == PlatformThemeVariant.Dark;
    }

    /// <summary>
    /// OS テーマ変更の監視を開始する。
    /// 変更が検出されるたびに <paramref name="onSystemThemeChanged"/> がUIスレッドで呼び出される。
    /// </summary>
    /// <param name="onSystemThemeChanged">テーマ変更時のコールバック（true = ダークモード）</param>
    public void StartMonitoring(Action<bool> onSystemThemeChanged)
    {
        // コールバックを保持する
        _callback = onSystemThemeChanged;
        var app = Application.Current;
        if (app?.PlatformSettings != null)
        {
            // PlatformSettings のカラー変更イベントにハンドラーを登録する
            app.PlatformSettings.ColorValuesChanged += OnColorValuesChanged;
        }
    }

    /// <summary>
    /// OS テーマ変更の監視を停止し、コールバックを解除する。
    /// </summary>
    public void StopMonitoring()
    {
        var app = Application.Current;
        if (app?.PlatformSettings != null)
        {
            // イベントハンドラーを解除してメモリリークを防ぐ
            app.PlatformSettings.ColorValuesChanged -= OnColorValuesChanged;
        }
        // コールバック参照をクリアする
        _callback = null;
    }

    /// <summary>
    /// OS カラーテーマ変更イベントのハンドラー。
    /// UIスレッドにポストしてコールバックを呼び出す。
    /// </summary>
    /// <param name="sender">イベント送信元（通常は PlatformSettings）</param>
    /// <param name="e">新しいカラー値（ThemeVariant を含む）</param>
    private void OnColorValuesChanged(object? sender, PlatformColorValues e)
    {
        // 新しいテーマがダークかどうかを判定する
        var isDark = e.ThemeVariant == PlatformThemeVariant.Dark;
        if (_callback != null)
        {
            // UIスレッドでコールバックを実行する
            Dispatcher.UIThread.Post(() => _callback(isDark));
        }
    }
}
