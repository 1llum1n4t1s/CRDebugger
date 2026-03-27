using System.Runtime.InteropServices;
using CRDebugger.Core.Abstractions;
using Microsoft.Win32;

namespace CRDebugger.WinForms;

/// <summary>
/// Windows OSのダークモード検出とテーマ変更監視を行うプロバイダー。
/// レジストリキー <c>Software\Microsoft\Windows\CurrentVersion\Themes\Personalize</c> の
/// <c>AppsUseLightTheme</c> 値を読み取ってダークモードを判定する。
/// レジストリ変更イベントの信頼性が低いため、2秒間隔のポーリングで変更を検出する。
/// </summary>
public sealed class WinFormsThemeProvider : IThemeProvider, IDisposable
{
    /// <summary>テーマ設定が格納されているレジストリキーパス。</summary>
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>アプリのライト/ダークテーマを示すレジストリ値名。0 = ダークモード。</summary>
    private const string RegistryValueName = "AppsUseLightTheme";

    /// <summary>テーマ変更をポーリングするタイマー。監視停止時は null。</summary>
    private System.Threading.Timer? _pollingTimer;

    /// <summary>テーマ変更時に呼び出すコールバック。引数は isDarkMode (true = ダーク)。</summary>
    private Action<bool>? _callback;

    /// <summary>前回のポーリング時のダークモード状態。変更検出に使用する。</summary>
    private bool _lastKnownDarkMode;

    /// <summary>
    /// 現在のシステムがダークモードかどうかをレジストリから判定する。
    /// レジストリアクセスに失敗した場合はライトモード (false) を返す。
    /// </summary>
    /// <returns>ダークモードの場合は true、ライトモードの場合は false。</returns>
    public bool IsSystemDarkMode()
    {
        try
        {
            // レジストリからテーマ設定値を読み取る
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            var value = key?.GetValue(RegistryValueName);
            if (value is int intValue)
                return intValue == 0; // 0 = ダークモード、1 = ライトモード
        }
        catch
        {
            // レジストリアクセス失敗時はライトモードとみなす
        }

        return false;
    }

    /// <summary>
    /// システムテーマの変更監視を開始する。
    /// 2秒間隔のポーリングでレジストリ値の変化を検出し、変化があればコールバックを呼び出す。
    /// </summary>
    /// <param name="onSystemThemeChanged">テーマ変更時に呼ばれるコールバック。引数は isDarkMode。</param>
    public void StartMonitoring(Action<bool> onSystemThemeChanged)
    {
        // コールバックを保持して現在のテーマ状態を記録
        _callback = onSystemThemeChanged;
        _lastKnownDarkMode = IsSystemDarkMode();

        // レジストリ変更イベントが信頼性に欠けるため、ポーリングで監視（2秒間隔）
        _pollingTimer = new System.Threading.Timer(
            PollThemeChange,
            null,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// システムテーマの変更監視を停止する。
    /// ポーリングタイマーを破棄してコールバック参照をクリアする。
    /// </summary>
    public void StopMonitoring()
    {
        // タイマーを破棄して null にリセット
        _pollingTimer?.Dispose();
        _pollingTimer = null;
        _callback = null;
    }

    /// <summary>
    /// タイマーコールバック。レジストリのテーマ状態を確認し、
    /// 前回から変化があればコールバックを呼び出す。
    /// </summary>
    /// <param name="state">タイマー状態（使用しない）。</param>
    private void PollThemeChange(object? state)
    {
        // 現在のダークモード状態を取得して前回と比較
        var currentDarkMode = IsSystemDarkMode();
        if (currentDarkMode != _lastKnownDarkMode)
        {
            // 変化があった場合は最新状態を更新してコールバックを通知
            _lastKnownDarkMode = currentDarkMode;
            _callback?.Invoke(currentDarkMode);
        }
    }

    /// <summary>
    /// リソースを解放する。監視停止を呼び出してタイマーを破棄する。
    /// </summary>
    public void Dispose()
    {
        StopMonitoring();
    }
}
