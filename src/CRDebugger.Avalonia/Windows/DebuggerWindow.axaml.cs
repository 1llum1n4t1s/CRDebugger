using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using CRDebugger.Core;
using CRDebugger.Core.Input;
using CRDebugger.Core.ViewModels;
// namespace CRDebugger.Avalonia.Windows と type CRDebugger.Core.CRDebugger の曖昧解決
using CRDebuggerFacade = CRDebugger.Core.CRDebugger;

namespace CRDebugger.Avalonia.Windows;

/// <summary>
/// CRDebugger のメインウィンドウ（Avalonia Window）。
/// サイドバーのタブ切り替え・ピン固定・閉じるボタンのイベントハンドラーを実装する。
/// DataContext には <see cref="DebuggerViewModel"/> を設定する。
/// </summary>
public partial class DebuggerWindow : Window
{
    /// <summary>
    /// DebuggerWindow を初期化し、AXAML で定義したコンポーネントをロードする。
    /// </summary>
    public DebuggerWindow()
    {
        InitializeComponent();
        OverrideSystemAccentColor();
    }

    /// <summary>
    /// アプリケーション終了処理中フラグ。
    /// true のときは <see cref="OnClosing"/> が e.Cancel をスキップして実際に閉じる。
    /// </summary>
    private bool _isShuttingDown;

    /// <summary>
    /// ウィンドウ強制クローズを許可するフラグを立てる。
    /// CRDebugger.Shutdown() などホスト側からの明示的破棄時に呼ぶ。
    /// </summary>
    internal void RequestShutdown()
    {
        _isShuttingDown = true;
    }

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ApplyDarkWindowChrome();
    }

    /// <summary>
    /// WindowsのシステムアクセントカラーがFluentテーマに流入してUI全体が黄色化するのを防止する。
    /// 本ウィンドウ自身の <see cref="Window.Resources"/> に CRDebugger 独自の紫青系アクセントカラーを設定し、
    /// Application スコープを汚染せずホストアプリのアクセントカラーを保護する。
    /// </summary>
    private void OverrideSystemAccentColor()
    {
        var accent = Color.Parse("#7C8FFF");
        // Window スコープに閉じ込めてホストアプリの Application.Resources を破壊しない
        this.Resources["SystemAccentColor"] = accent;
        this.Resources["SystemAccentColorLight1"] = Color.Parse("#9EAAFF");
        this.Resources["SystemAccentColorLight2"] = Color.Parse("#BFCAFF");
        this.Resources["SystemAccentColorLight3"] = Color.Parse("#DFE4FF");
        this.Resources["SystemAccentColorDark1"] = Color.Parse("#5A6FD9");
        this.Resources["SystemAccentColorDark2"] = Color.Parse("#3F52B3");
        this.Resources["SystemAccentColorDark3"] = Color.Parse("#2B3A8C");
        this.Resources["SystemAccentColorBrush"] = new SolidColorBrush(accent);
    }

    // ────────── Win32 DWM API（Windows 11 ウィンドウボーダー色の強制指定）──────────

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref uint attrValue, int attrSize);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    /// <summary>DWMWA_BORDER_COLOR (34) — ウィンドウのボーダー色を COLORREF で指定</summary>
    private const int DWMWA_BORDER_COLOR = 34;
    /// <summary>DWMWA_CAPTION_COLOR (35) — タイトルバー背景色を COLORREF で指定</summary>
    private const int DWMWA_CAPTION_COLOR = 35;
    /// <summary>DWMWA_USE_IMMERSIVE_DARK_MODE (20) — ダークモードのタイトルバーを使用</summary>
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    /// <summary>
    /// Win32 DWM API を使って、Windows 11 が描画するウィンドウボーダーとタイトルバーの色を
    /// ダークブルー (#12121C) に強制変更する。
    /// これにより SystemAccentColor（黄色等）がウィンドウ外観に影響するのを完全に防止する。
    /// Windows 以外のプラットフォームでは何もしない。
    /// </summary>
    private void ApplyDarkWindowChrome()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var handle = TryGetPlatformHandle();
        if (handle == null) return;

        var hwnd = handle.Handle;

        // COLORREF は BGR 形式: 0x00BBGGRR
        // #12121C → R=0x12, G=0x12, B=0x1C → COLORREF=0x001C1212
        uint darkColor = 0x001C1212;

        // ウィンドウボーダーをダークブルーに
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref darkColor, sizeof(uint));
        // タイトルバー背景をダークブルーに
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref darkColor, sizeof(uint));
        // ダークモードのキャプションボタン（最小化・最大化・閉じる）を有効化
        uint useDarkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(uint));

        // タイトルバーのアイコンを削除
        // 1. WS_EX_DLGMODALFRAME 拡張スタイルを追加（アイコン表示を無効化）
        const int GWL_EXSTYLE = -20;
        const int WS_EX_DLGMODALFRAME = 0x0001;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_DLGMODALFRAME);

        // 2. フレーム変更を反映させる
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_FRAMECHANGED = 0x0020;
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

        // 3. アイコンハンドルを null に設定
        const uint WM_SETICON = 0x0080;
        SendMessage(hwnd, WM_SETICON, IntPtr.Zero, IntPtr.Zero);
        SendMessage(hwnd, WM_SETICON, (IntPtr)1, IntPtr.Zero);
    }

    /// <summary>
    /// サイドバーのタブボタンがクリックされた際の処理。
    /// ボタンの Tag プロパティに設定された <see cref="CRTab"/> を ViewModel に反映する。
    /// </summary>
    /// <param name="sender">クリックされたボタン（<see cref="Button"/>）</param>
    /// <param name="e">ルーティングイベント引数</param>
    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        // ボタンの Tag から CRTab を取り出して選択タブを更新する
        if (sender is Button button && button.Tag is CRTab tab && DataContext is DebuggerViewModel vm)
        {
            vm.SelectedTab = tab;
        }
    }

    /// <summary>
    /// ピンボタンがクリックされた際の処理。
    /// Topmost プロパティを切り替えてウィンドウを常に前面に固定／解除する。
    /// ピンアイコンの不透明度で固定状態を視覚的にフィードバックする。
    /// </summary>
    /// <param name="sender">クリックされたボタン</param>
    /// <param name="e">ルーティングイベント引数</param>
    private void OnPinClick(object? sender, RoutedEventArgs e)
    {
        // Topmost を反転して常に前面 / 通常を切り替える
        Topmost = !Topmost;
        // PinIcon テキストブロックの不透明度でピン状態を表示する
        var icon = this.FindControl<global::Avalonia.Controls.TextBlock>("PinIcon");
        if (icon != null)
        {
            // 固定中は不透明（1.0）、解除中は半透明（0.4）にする
            icon.Opacity = Topmost ? 1.0 : 0.4;
        }
    }

    /// <summary>
    /// ウィンドウを閉じる操作（Alt+F4 など）を原則キャンセルし、代わりに非表示にする。
    /// ただし以下のいずれかの条件のときは実際に閉じる（ホストアプリ終了時のハング防止）:
    ///   1. <see cref="_isShuttingDown"/> または <c>CRDebugger.IsInitialized == false</c>（明示シャットダウン）
    ///   2. アプリケーションが <see cref="ShutdownMode.OnLastWindowClose"/> モード
    ///   3. <see cref="IClassicDesktopStyleApplicationLifetime.MainWindow"/> が null（ホストの主ウィンドウなし）
    /// </summary>
    /// <summary>
    /// キー入力を CRDebugger のショートカット機構へ橋渡しする。
    /// Core 側は F1〜F5 のタブ切替と Esc の非表示を既定登録しているが、
    /// UI フレームワークからこのメソッドを呼ばない限り発火しない。
    /// </summary>
    /// <param name="e">キー入力イベント引数</param>
    protected override void OnKeyDown(global::Avalonia.Input.KeyEventArgs e)
    {
        if (CRKeyMap.TryFromName(e.Key.ToString(), out var crKey))
        {
            var modifiers = ToCRModifiers(e.KeyModifiers);
            // ショートカットが実行された場合はイベントを消費して、下位コントロールへ流さない
            if (CRDebuggerFacade.HandleKeyDown(crKey, modifiers))
            {
                e.Handled = true;
                return;
            }
        }

        base.OnKeyDown(e);
    }

    /// <summary>Avalonia の修飾キーフラグを <see cref="CRModifierKeys"/> に変換する</summary>
    /// <param name="modifiers">Avalonia の修飾キーフラグ</param>
    /// <returns>対応する CRDebugger の修飾キーフラグ</returns>
    private static CRModifierKeys ToCRModifiers(global::Avalonia.Input.KeyModifiers modifiers)
    {
        var result = CRModifierKeys.None;
        if (modifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Control)) result |= CRModifierKeys.Ctrl;
        if (modifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Shift)) result |= CRModifierKeys.Shift;
        if (modifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Alt)) result |= CRModifierKeys.Alt;
        return result;
    }

    /// <param name="e">ウィンドウ閉じるイベント引数（Cancel を true に設定する）</param>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (ShouldAllowClose())
        {
            base.OnClosing(e);
            return;
        }

        // 通常時はウィンドウを実際には閉じず非表示にする（状態を保持するため）
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    /// <summary>
    /// 現在のクローズ要求を許可するかどうか判定する。
    /// ホストアプリの終了モードや CRDebugger 初期化状態を見て、ハング回避のため実クローズを通す。
    /// </summary>
    private bool ShouldAllowClose()
    {
        // 明示的なシャットダウン要求、または CRDebugger 初期化解除済みなら通す
        if (_isShuttingDown) return true;
        if (!CRDebuggerFacade.IsInitialized) return true;

        // Avalonia デスクトップライフタイムの状態を見て判定
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 最後のウィンドウが閉じたら全終了するモードでは、Cancel するとホストがハングする
            if (desktop.ShutdownMode == ShutdownMode.OnLastWindowClose) return true;
            // ホストの MainWindow が無い場合（= デバッガが事実上のメイン）は通す
            if (desktop.MainWindow == null) return true;
        }

        return false;
    }
}
