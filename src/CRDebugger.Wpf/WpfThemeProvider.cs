using CRDebugger.Core.Abstractions;
using Microsoft.Win32;

namespace CRDebugger.Wpf;

/// <summary>
/// Windows レジストリからシステムのダークモード設定を検出し、
/// 変更をリアルタイムで通知する IThemeProvider 実装。
/// </summary>
public sealed class WpfThemeProvider : IThemeProvider
{
    /// <summary>ダークモード設定が格納されているレジストリキーのパス</summary>
    private const string RegistryKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>ライトモード（1）またはダークモード（0）を示すレジストリ値の名前</summary>
    private const string RegistryValueName = "AppsUseLightTheme";

    /// <summary>テーマ変更時に呼び出すコールバック（bool: isDark）</summary>
    private Action<bool>? _callback;

    /// <summary>SystemEvents.UserPreferenceChanged の購読中かどうか</summary>
    private bool _monitoring;

    /// <summary>
    /// システムが現在ダークモードに設定されているかどうかを取得する。
    /// レジストリの AppsUseLightTheme が 0 の場合はダークモードと判定する。
    /// レジストリ読み取りに失敗した場合はライトモード（false）を返す。
    /// </summary>
    /// <returns>ダークモードの場合は true、ライトモードまたは取得失敗の場合は false</returns>
    public bool IsSystemDarkMode()
    {
        try
        {
            // HKCU のパーソナライズキーを開いて AppsUseLightTheme 値を読み取る
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            var value = key?.GetValue(RegistryValueName);
            // 0 = ダークモード, 1 = ライトモード（直感と逆なので注意）
            return value is int intValue && intValue == 0;
        }
        catch
        {
            // レジストリ読み取りエラーの場合はデフォルトでライトモードを返す
            return false;
        }
    }

    /// <summary>
    /// システムテーマの変更監視を開始する。
    /// SystemEvents.UserPreferenceChanged イベントを購読して変更を検知する。
    /// すでに監視中の場合は新しいコールバックを登録するのみでイベントの二重購読は行わない。
    /// </summary>
    /// <param name="onSystemThemeChanged">ダークモード状態（bool: isDark）を引数とするコールバック</param>
    public void StartMonitoring(Action<bool> onSystemThemeChanged)
    {
        _callback = onSystemThemeChanged;

        // 監視中でない場合のみ SystemEvents を購読して二重登録を防ぐ
        if (!_monitoring)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _monitoring = true;
        }
    }

    /// <summary>
    /// システムテーマの変更監視を停止する。
    /// SystemEvents.UserPreferenceChanged のイベント購読を解除する。
    /// </summary>
    public void StopMonitoring()
    {
        // コールバック参照をクリアして呼び出しを無効化
        _callback = null;

        // 監視中の場合のみ SystemEvents の購読を解除
        if (_monitoring)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _monitoring = false;
        }
    }

    /// <summary>
    /// システムのユーザー設定が変更されたときのイベントハンドラ。
    /// General カテゴリの変更（テーマ変更を含む）の場合のみ現在のダークモード状態を確認してコールバックを呼び出す。
    /// </summary>
    /// <param name="sender">イベント送信元</param>
    /// <param name="e">変更カテゴリを含む UserPreferenceChangedEventArgs</param>
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // General カテゴリの変更にテーマ変更が含まれる
        if (e.Category == UserPreferenceCategory.General)
        {
            // 最新のダークモード状態をレジストリから取得してコールバックに通知
            var isDark = IsSystemDarkMode();
            _callback?.Invoke(isDark);
        }
    }
}
