using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using CRDebugger.Core;
using CRDebugger.Core.ViewModels;

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

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ApplyDarkWindowChrome();
    }

    /// <summary>
    /// WindowsのシステムアクセントカラーがFluentテーマに流入してUI全体が黄色化するのを防止する。
    /// Application.Resources に CRDebugger 独自の紫青系アクセントカラーを強制設定する。
    /// </summary>
    private static void OverrideSystemAccentColor()
    {
        var app = Application.Current;
        if (app == null) return;

        var accent = Color.Parse("#7C8FFF");
        app.Resources["SystemAccentColor"] = accent;
        app.Resources["SystemAccentColorLight1"] = Color.Parse("#9EAAFF");
        app.Resources["SystemAccentColorLight2"] = Color.Parse("#BFCAFF");
        app.Resources["SystemAccentColorLight3"] = Color.Parse("#DFE4FF");
        app.Resources["SystemAccentColorDark1"] = Color.Parse("#5A6FD9");
        app.Resources["SystemAccentColorDark2"] = Color.Parse("#3F52B3");
        app.Resources["SystemAccentColorDark3"] = Color.Parse("#2B3A8C");
        app.Resources["SystemAccentColorBrush"] = new SolidColorBrush(accent);
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
    /// 閉じるボタンがクリックされた際の処理。
    /// ウィンドウを破棄せず非表示にすることで次回表示を高速化する。
    /// </summary>
    /// <param name="sender">クリックされたボタン</param>
    /// <param name="e">ルーティングイベント引数</param>
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    /// <summary>
    /// ウィンドウを閉じる操作（Alt+F4 など）をキャンセルし、代わりに非表示にする。
    /// これによりデバッガーの状態を保持したまま再表示できる。
    /// </summary>
    /// <param name="e">ウィンドウ閉じるイベント引数（Cancel を true に設定する）</param>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // ウィンドウを実際には閉じず非表示にする（状態を保持するため）
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
