using System.ComponentModel;
using CRDebugger.Core;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.WinForms.Controls;
using CRDebugger.WinForms.Panels;

namespace CRDebugger.WinForms.Forms;

/// <summary>
/// モダンデザインのメインデバッガーフォーム。
/// 左サイドバー（ヘッダー + タブボタン）と右コンテンツパネルで構成される2ペインレイアウト。
/// サイドバーボタンでタブを切り替え、対応するパネルをコンテンツエリアに表示する。
/// テーマ変更・タブ切り替え・コンソールバッジ更新に ViewModel のイベントで対応する。
/// </summary>
public sealed class DebuggerForm : Form
{
    /// <summary>デバッガー全体の状態とロジックを持つ ViewModel。</summary>
    private readonly DebuggerViewModel _viewModel;

    // ---- サイドバー関連フィールド ----

    /// <summary>左側のサイドバー領域（タブボタンとヘッダーを含む）。</summary>
    private readonly Panel _sidebarPanel;

    /// <summary>すべてのサイドバーボタンのリスト（バッジ更新・選択状態管理に使用）。</summary>
    private readonly List<SidebarButton> _sidebarButtons = new();

    // ---- コンテンツエリア関連フィールド ----

    /// <summary>タブごとのパネルを重ねて表示するコンテンツエリア。</summary>
    private readonly Panel _contentPanel;

    /// <summary>ウィンドウを最前面に固定するピンボタン。状態に応じて色が変わる。</summary>
    private Button? _pinButton;

    /// <summary>システム情報タブのパネル。</summary>
    private readonly SystemInfoPanel _systemInfoPanel;

    /// <summary>コンソールログタブのパネル。</summary>
    private readonly ConsolePanel _consolePanel;

    /// <summary>オプション設定タブのパネル。</summary>
    private readonly OptionsPanel _optionsPanel;

    /// <summary>プロファイラータブのパネル。</summary>
    private readonly ProfilerPanel _profilerPanel;

    /// <summary>バグレポートタブのパネル。</summary>
    private readonly BugReporterPanel _bugReporterPanel;

    /// <summary>現在適用中のテーマカラー。</summary>
    private ThemeColors _colors;

    /// <summary>
    /// サイドバーボタンのタブ定義（タブ種別、アイコン文字、ラベルテキストの対応表）。
    /// 上から順にサイドバーに並べられる。
    /// </summary>
    private static readonly (CRTab Tab, string Icon, string Label)[] TabDefs =
    {
        (CRTab.System, "\u2139", "System"),       // ℹ システム情報
        (CRTab.Console, "\u25B6", "Console"),     // ▶ コンソール
        (CRTab.Options, "\u2699", "Options"),     // ⚙ オプション
        (CRTab.Profiler, "\u2261", "Profiler"),   // ≡ プロファイラー
        (CRTab.BugReporter, "\u2709", "Bug Report"), // ✉ バグレポート
    };

    /// <summary>サイドバーの幅（ピクセル）。</summary>
    private const int SidebarWidth = 76;

    /// <summary>サイドバーのヘッダー領域の高さ（ピクセル）。</summary>
    private const int HeaderHeight = 52;

    /// <summary>
    /// <see cref="DebuggerForm"/> を初期化してUIコントロールを構築する。
    /// タブパネルの生成、サイドバーボタンの配置、ViewModel イベントの購読、
    /// 初期タブの表示、テーマの適用を行う。
    /// </summary>
    /// <param name="viewModel">デバッガー全体の状態とロジックを持つ <see cref="DebuggerViewModel"/>。</param>
    public DebuggerForm(DebuggerViewModel viewModel)
    {
        _viewModel = viewModel;
        _colors = viewModel.ThemeColors;

        // フォーム基本設定
        Text = "CRDebugger";
        Size = new Size(900, 600);
        MinimumSize = new Size(640, 400);
        StartPosition = FormStartPosition.Manual;
        // 初期位置: プライマリスクリーンの右端から20px内側、上端から20px下
        Location = new Point(
            Screen.PrimaryScreen!.WorkingArea.Right - 920,
            Screen.PrimaryScreen.WorkingArea.Top + 20);
        TopMost = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = true;
        DoubleBuffered = true;
        Font = new Font("Segoe UI", 9);

        // 左サイドバーパネル（カスタム描画でヘッダーテキストを描画）
        _sidebarPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = SidebarWidth,
        };
        // Paint イベントでサイドバーのヘッダーテキストと区切り線を描画
        _sidebarPanel.Paint += PaintSidebar;
        Controls.Add(_sidebarPanel);

        // 右コンテンツパネル（タブパネルを重ねて Visible で切り替える）
        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
        };
        Controls.Add(_contentPanel);

        // 各タブパネルを生成（ViewModel の対応する子 ViewModel を渡す）
        _systemInfoPanel = new SystemInfoPanel(viewModel.SystemInfo, _colors);
        _consolePanel = new ConsolePanel(viewModel.Console, _colors);
        _optionsPanel = new OptionsPanel(viewModel.Options, _colors);
        _profilerPanel = new ProfilerPanel(viewModel.Profiler, _colors);
        _bugReporterPanel = new BugReporterPanel(viewModel.BugReporter, _colors);

        // 全タブパネルをコンテンツエリアに追加（初期状態はすべて非表示）
        foreach (var panel in AllPanels())
        {
            panel.Dock = DockStyle.Fill;
            panel.Visible = false;
            _contentPanel.Controls.Add(panel);
        }

        // コンテンツエリア右上にピンボタンと閉じるボタンを配置
        CreateHeaderButtons();

        // サイドバーにタブ切り替えボタンを配置
        CreateSidebarButtons();

        // ViewModelのプロパティ変更イベントを購読（タブ切り替え・テーマ変更）
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        // コンソール ViewModel のカウント変更イベントを購読（バッジ更新）
        _viewModel.Console.PropertyChanged += OnConsolePropertyChanged;

        // 初期タブを表示してテーマを適用
        SwitchToTab(_viewModel.SelectedTab);
        ApplyTheme(_colors);

        // UIスレッドマーシャリング用コントロールを設定
        if (_viewModel.ThemeManager != null)
        {
            var uiThread = FindUiThread();
            uiThread?.SetMarshalControl(this);
        }
    }

    /// <summary>
    /// 指定したテーマカラーをフォーム全体に適用する。
    /// 背景色、サイドバー色、各タブパネルのテーマを一括更新する。
    /// </summary>
    /// <param name="colors">適用するテーマカラー情報。</param>
    public void ApplyTheme(ThemeColors colors)
    {
        _colors = colors;
        BackColor = OptionControlFactory.ArgbToColor(colors.Background);
        _sidebarPanel.BackColor = OptionControlFactory.ArgbToColor(colors.SidebarBackground);
        _contentPanel.BackColor = OptionControlFactory.ArgbToColor(colors.Background);

        // 各タブパネルにテーマを適用
        _systemInfoPanel.ApplyTheme(colors);
        _consolePanel.ApplyTheme(colors);
        _optionsPanel.ApplyTheme(colors);
        _profilerPanel.ApplyTheme(colors);
        _bugReporterPanel.ApplyTheme(colors);

        // サイドバーボタンの色を更新
        foreach (var btn in _sidebarButtons)
        {
            btn.UpdateColors(colors);
        }

        // サイドバーパネルとフォーム全体を再描画
        _sidebarPanel.Invalidate();
        Invalidate(true);
    }

    /// <summary>
    /// コンテンツエリア右上にピンボタンと閉じるボタンを生成して配置する。
    /// 閉じるボタンはフォームを非表示にし、ピンボタンは TopMost を切り替える。
    /// </summary>
    private void CreateHeaderButtons()
    {
        // 閉じるボタン（フォームを破棄せず非表示にする）
        var closeBtn = new Button
        {
            Text = "✕",
            FlatStyle = FlatStyle.Flat,
            Size = new Size(28, 28),
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(96, 96, 117),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            // ウィンドウリサイズ時に右上に追従するアンカー設定
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        closeBtn.FlatAppearance.BorderSize = 0;
        closeBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 255, 255, 255);
        // コンテンツパネルの右端から 34px 内側に配置
        closeBtn.Location = new Point(_contentPanel.Width - 34, 6);
        // クリックでフォームを非表示（破棄しない）
        closeBtn.Click += (_, _) => Hide();
        _contentPanel.Controls.Add(closeBtn);
        // ボタンを最前面に表示してタブパネルに隠れないようにする
        closeBtn.BringToFront();

        // ピンボタン（TopMost を切り替えてウィンドウを最前面に固定/解除）
        _pinButton = new Button
        {
            Text = "📌",
            FlatStyle = FlatStyle.Flat,
            Size = new Size(28, 28),
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(96, 96, 117),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _pinButton.FlatAppearance.BorderSize = 0;
        _pinButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 255, 255, 255);
        // 閉じるボタンの左隣（30px 左）に配置
        _pinButton.Location = new Point(_contentPanel.Width - 64, 6);
        _pinButton.Click += (_, _) =>
        {
            // TopMost を反転してピン状態を切り替え
            TopMost = !TopMost;
            // ピン状態に応じてボタンの色を切り替え（固定中 = アクセント色、解除中 = グレー）
            _pinButton.ForeColor = TopMost
                ? Color.FromArgb(124, 143, 255)
                : Color.FromArgb(96, 96, 117);
        };
        _contentPanel.Controls.Add(_pinButton);
        _pinButton.BringToFront();
    }

    /// <summary>
    /// タブ定義に基づいてサイドバーにカスタムボタンを生成して配置する。
    /// ヘッダー領域の下から順に縦に並べ、クリック時にタブを切り替える。
    /// </summary>
    private void CreateSidebarButtons()
    {
        // ヘッダー領域の下端 + 8px のマージンからボタンを縦に配置
        var y = HeaderHeight + 8;
        foreach (var (tab, icon, label) in TabDefs)
        {
            var btn = new SidebarButton(tab, icon, label, _colors)
            {
                Location = new Point(0, y),
                Size = new Size(SidebarWidth, 60),
                // 初期選択状態を ViewModel の現在タブと一致させる
                IsSelected = tab == _viewModel.SelectedTab,
            };

            // クリック時に ViewModel の SelectedTab を更新してタブを切り替える
            btn.Click += (_, _) =>
            {
                _viewModel.SelectedTab = tab;
            };

            _sidebarPanel.Controls.Add(btn);
            _sidebarButtons.Add(btn);
            // 次のボタンの Y 座標（ボタン高さ60 + 間隔2）
            y += 62;
        }
    }

    /// <summary>
    /// 指定したタブに切り替える。
    /// 全パネルを非表示にしてから対象タブのパネルのみ表示し、
    /// サイドバーボタンの選択状態を更新する。
    /// </summary>
    /// <param name="tab">切り替え先の <see cref="CRTab"/>。</param>
    private void SwitchToTab(CRTab tab)
    {
        // 全タブパネルを非表示にする
        foreach (var panel in AllPanels())
            panel.Visible = false;

        // 対象タブのパネルのみ表示する
        GetPanelForTab(tab).Visible = true;

        // サイドバーボタンの選択状態を更新する
        foreach (var btn in _sidebarButtons)
            btn.IsSelected = btn.Tab == tab;
    }

    /// <summary>
    /// 指定した <see cref="CRTab"/> に対応するパネルコントロールを返す。
    /// 未知のタブはシステム情報パネルにフォールバックする。
    /// </summary>
    /// <param name="tab">パネルを取得するタブ種別。</param>
    /// <returns>対応するパネル <see cref="Control"/>。</returns>
    private Control GetPanelForTab(CRTab tab) => tab switch
    {
        CRTab.System => _systemInfoPanel,
        CRTab.Console => _consolePanel,
        CRTab.Options => _optionsPanel,
        CRTab.Profiler => _profilerPanel,
        CRTab.BugReporter => _bugReporterPanel,
        // 不明なタブはシステム情報パネルにフォールバック
        _ => _systemInfoPanel,
    };

    /// <summary>
    /// すべてのタブパネルを列挙して返す。
    /// 全パネルへの一括操作（非表示化・テーマ適用など）に使用する。
    /// </summary>
    /// <returns>全タブパネルを yield return で返す列挙子。</returns>
    private IEnumerable<Control> AllPanels()
    {
        yield return _systemInfoPanel;
        yield return _consolePanel;
        yield return _optionsPanel;
        yield return _profilerPanel;
        yield return _bugReporterPanel;
    }

    /// <summary>
    /// ViewModel のプロパティ変更イベントハンドラー。
    /// SelectedTab 変更時はタブを切り替え、ThemeColors 変更時はテーマを適用する。
    /// UIスレッド以外からの呼び出しは Invoke でマーシャリングする。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">プロパティ変更イベント引数。</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DebuggerViewModel.SelectedTab))
        {
            // タブ切り替えをUIスレッドで実行
            if (InvokeRequired)
                Invoke(() => SwitchToTab(_viewModel.SelectedTab));
            else
                SwitchToTab(_viewModel.SelectedTab);
        }
        else if (e.PropertyName == nameof(DebuggerViewModel.ThemeColors))
        {
            // テーマ変更をUIスレッドで実行
            if (InvokeRequired)
                Invoke(() => ApplyTheme(_viewModel.ThemeColors));
            else
                ApplyTheme(_viewModel.ThemeColors);
        }
    }

    /// <summary>
    /// コンソール ViewModel のプロパティ変更イベントハンドラー。
    /// エラー・警告カウント変更時にコンソールタブのサイドバーバッジを更新する。
    /// UIスレッド以外からの呼び出しは Invoke でマーシャリングする。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">プロパティ変更イベント引数。</param>
    private void OnConsolePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // エラー/警告/情報/デバッグのいずれかのカウントが変わったらバッジを更新
        if (e.PropertyName is nameof(ConsoleViewModel.ErrorCount)
            or nameof(ConsoleViewModel.WarningCount)
            or nameof(ConsoleViewModel.InfoCount)
            or nameof(ConsoleViewModel.DebugCount))
        {
            // コンソールタブのサイドバーボタンを検索
            var consoleBtn = _sidebarButtons.Find(b => b.Tab == CRTab.Console);
            if (consoleBtn != null)
            {
                var errorCount = _viewModel.Console.ErrorCount;
                var warnCount = _viewModel.Console.WarningCount;

                // バッジ更新をUIスレッドで実行
                if (InvokeRequired)
                    Invoke(() => consoleBtn.SetBadge(errorCount, warnCount));
                else
                    consoleBtn.SetBadge(errorCount, warnCount);
            }
        }
    }

    /// <summary>
    /// サイドバーのカスタム描画イベントハンドラー。
    /// "CR" をアクセント色の大きなフォントで、"DEBUGGER" をミュートカラーの小さなフォントで描画し、
    /// ヘッダー下に区切り線、サイドバー右端に境界線を描画する。
    /// </summary>
    /// <param name="sender">イベント発生元オブジェクト。</param>
    /// <param name="e">Paint イベント引数。</param>
    private void PaintSidebar(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        // アンチエイリアスと ClearType でテキスト・図形を滑らかに描画
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // ヘッダーフォントとブラシを定義（"CR" は大きく太く、"DEBUGGER" は小さく細く）
        using var accentFont = new Font("Segoe UI", 13, FontStyle.Bold);
        using var mutedFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var accentBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.SelectedTab));
        using var mutedBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.OnSurfaceMuted));

        // "CR" をサイドバー幅の中央に描画
        var crText = "CR";
        var crSize = g.MeasureString(crText, accentFont);
        var crX = (_sidebarPanel.Width - crSize.Width) / 2;
        g.DrawString(crText, accentFont, accentBrush, crX, 10);

        // "DEBUGGER" をその下（Y=30）に描画
        var dbgText = "DEBUGGER";
        var dbgSize = g.MeasureString(dbgText, mutedFont);
        var dbgX = (_sidebarPanel.Width - dbgSize.Width) / 2;
        g.DrawString(dbgText, mutedFont, mutedBrush, dbgX, 30);

        // ヘッダー下に横区切り線を描画（半透明の白、左右12pxマージン）
        using var separatorPen = new Pen(Color.FromArgb(20, 255, 255, 255), 1);
        g.DrawLine(separatorPen, 12, HeaderHeight, _sidebarPanel.Width - 12, HeaderHeight);

        // サイドバーの右端に縦境界線を描画（コンテンツエリアとの視覚的分離）
        using var borderPen = new Pen(Color.FromArgb(12, 255, 255, 255), 1);
        g.DrawLine(borderPen,
            _sidebarPanel.Width - 1, 0,
            _sidebarPanel.Width - 1, _sidebarPanel.Height);
    }

    /// <summary>
    /// WinFormsUiThread インスタンスを検索して返す。
    /// 現在の実装では DebuggerWindow 側で設定するため null を返す。
    /// </summary>
    /// <returns>
    /// <see cref="WinFormsUiThread"/> インスタンス。現在は常に null を返す。
    /// </returns>
    private WinFormsUiThread? FindUiThread()
    {
        // WinFormsUiThreadインスタンスを見つけてマーシャルコントロールを設定
        // UseWinForms()で設定済みのインスタンスを探す
        return null; // DebuggerWindow側で設定する
    }

    /// <summary>
    /// フォームクローズ試行時のイベントハンドラー。
    /// ユーザーによるクローズ操作の場合はフォームを破棄せず非表示にする。
    /// これによりデバッガーの状態が保持され、再表示が高速に行える。
    /// </summary>
    /// <param name="e">フォームクローズイベント引数。キャンセル可能。</param>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // ユーザーによる閉じる操作の場合はキャンセルして非表示にする（破棄しない）
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    /// <summary>
    /// リソースを解放する。ViewModel のイベント購読を解除してメモリリークを防ぐ。
    /// </summary>
    /// <param name="disposing">マネージドリソースを解放する場合は true。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // ViewModel のイベント購読を解除
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Console.PropertyChanged -= OnConsolePropertyChanged;
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// サイドバーに配置するカスタムボタンコントロール。
/// テキストシンボルアイコン・ラベル・バッジ（エラー数/警告数）を描画する。
/// 選択時は左端に3pxのアクセントバーを描画し、ホバー時は背景を薄く着色する。
/// </summary>
internal sealed class SidebarButton : Control
{
    /// <summary>このボタンが対応する <see cref="CRTab"/> の識別子。</summary>
    public CRTab Tab { get; }

    /// <summary>ボタン中央に表示するアイコン文字（Unicode シンボル）。</summary>
    private readonly string _icon;

    /// <summary>アイコン下部に表示するラベルテキスト（タブ名）。</summary>
    private readonly string _label;

    /// <summary>ボタンが現在選択状態かどうかを示すフラグ。</summary>
    private bool _isSelected;

    /// <summary>マウスが現在このボタン上にあるかどうかを示すフラグ。</summary>
    private bool _isHovered;

    /// <summary>表示するエラーバッジ数（0以下の場合はバッジを非表示）。</summary>
    private int _errorBadge;

    /// <summary>表示する警告バッジ数（0以下の場合はバッジを非表示）。</summary>
    private int _warnBadge;

    /// <summary>現在適用中のテーマカラー。</summary>
    private ThemeColors _colors;

    /// <summary>
    /// ボタンが選択状態かどうかを取得または設定する。
    /// 変更時は自動的に再描画する。
    /// </summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            // 選択状態変更時に再描画をリクエスト
            Invalidate();
        }
    }

    /// <summary>
    /// <see cref="SidebarButton"/> を初期化する。
    /// ダブルバッファリングと透明背景サポートを有効化する。
    /// </summary>
    /// <param name="tab">このボタンが対応する <see cref="CRTab"/>。</param>
    /// <param name="icon">表示するアイコン文字（Unicode シンボル）。</param>
    /// <param name="label">アイコン下部に表示するラベルテキスト。</param>
    /// <param name="colors">初期適用するテーマカラー情報。</param>
    public SidebarButton(CRTab tab, string icon, string label, ThemeColors colors)
    {
        Tab = tab;
        _icon = icon;
        _label = label;
        _colors = colors;

        // ダブルバッファリングと透明背景サポートを有効化してちらつきを防ぐ
        SetStyle(ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.SupportsTransparentBackColor, true);

        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
    }

    /// <summary>
    /// テーマカラーを更新して再描画する。
    /// テーマ変更時に呼び出す。
    /// </summary>
    /// <param name="colors">新しいテーマカラー情報。</param>
    public void UpdateColors(ThemeColors colors)
    {
        _colors = colors;
        Invalidate();
    }

    /// <summary>
    /// バッジに表示するエラー数と警告数を更新して再描画する。
    /// コンソールのログカウント変更時に呼び出す。
    /// </summary>
    /// <param name="errorCount">表示するエラー数。0以下の場合はバッジを非表示。</param>
    /// <param name="warnCount">表示する警告数。0以下の場合はバッジを非表示。</param>
    public void SetBadge(int errorCount, int warnCount)
    {
        _errorBadge = errorCount;
        _warnBadge = warnCount;
        Invalidate();
    }

    /// <summary>
    /// マウスがボタン上に入ったときのイベントハンドラー。
    /// ホバー状態を有効にして再描画する。
    /// </summary>
    /// <param name="e">イベント引数。</param>
    protected override void OnMouseEnter(EventArgs e)
    {
        _isHovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    /// <summary>
    /// マウスがボタン上から離れたときのイベントハンドラー。
    /// ホバー状態を無効にして再描画する。
    /// </summary>
    /// <param name="e">イベント引数。</param>
    protected override void OnMouseLeave(EventArgs e)
    {
        _isHovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    /// <summary>
    /// ボタン全体をカスタム描画する。
    /// 背景（選択/ホバー/通常）、選択インジケーターバー、アイコン、ラベル、バッジを描画する。
    /// </summary>
    /// <param name="e">Paint イベント引数。</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        // アンチエイリアスと ClearType を有効化
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // ---- 背景の描画 ----
        // 選択中 → アクセント色の半透明背景、ホバー → 薄い白の半透明背景、通常 → 透明
        Color bgColor;
        if (_isSelected)
            bgColor = Color.FromArgb(25, OptionControlFactory.ArgbToColor(_colors.SelectedTab));
        else if (_isHovered)
            bgColor = Color.FromArgb(15, 255, 255, 255);
        else
            bgColor = Color.Transparent;

        // 透明以外の場合は角丸矩形で背景を塗りつぶす
        if (bgColor != Color.Transparent)
        {
            using var bgBrush = new SolidBrush(bgColor);
            var rect = new Rectangle(4, 2, Width - 8, Height - 4);
            var radius = 8;
            using var path = CreateRoundedRectPath(rect, radius);
            g.FillPath(bgBrush, path);
        }

        // ---- 選択インジケーターバーの描画（左端に3px幅のアクセントバー）----
        if (_isSelected)
        {
            using var indicatorBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.SelectedTab));
            // 上下に10pxのマージンを設けた縦バーを描画
            var barRect = new Rectangle(0, 10, 3, Height - 20);
            using var barPath = CreateRoundedRectPath(barRect, 2);
            g.FillPath(indicatorBrush, barPath);
        }

        // ---- アイコンの描画 ----
        // 選択中 → アクセント色、ホバー中 → OnBackground色、通常 → SidebarText色
        var iconColor = _isSelected
            ? OptionControlFactory.ArgbToColor(_colors.SelectedTab)
            : (_isHovered
                ? OptionControlFactory.ArgbToColor(_colors.OnBackground)
                : OptionControlFactory.ArgbToColor(_colors.SidebarText));

        using var iconFont = new Font("Segoe UI Symbol", 16, FontStyle.Regular);
        using var iconBrush = new SolidBrush(iconColor);
        // アイコンをサイドバー幅の中央に描画（Y=8）
        var iconSize = g.MeasureString(_icon, iconFont);
        var iconX = (Width - iconSize.Width) / 2;
        g.DrawString(_icon, iconFont, iconBrush, iconX, 8);

        // ---- ラベルの描画 ----
        // 選択中 → OnBackground色、ホバー中 → OnSurface色、通常 → SidebarText色
        var labelColor = _isSelected
            ? OptionControlFactory.ArgbToColor(_colors.OnBackground)
            : (_isHovered
                ? OptionControlFactory.ArgbToColor(_colors.OnSurface)
                : OptionControlFactory.ArgbToColor(_colors.SidebarText));

        using var labelFont = new Font("Segoe UI", 7.5f, FontStyle.Regular);
        using var labelBrush = new SolidBrush(labelColor);
        // ラベルをアイコン下（Y=34）の中央に描画
        var labelSize = g.MeasureString(_label, labelFont);
        var labelX = (Width - labelSize.Width) / 2;
        g.DrawString(_label, labelFont, labelBrush, labelX, 34);

        // ---- バッジの描画（エラー数・警告数）----
        DrawBadges(g);
    }

    /// <summary>
    /// エラーバッジと警告バッジを右上隅に描画する。
    /// どちらも0以下の場合は何も描画しない。
    /// バッジ内の数値が99を超える場合は "99+" と表示する。
    /// </summary>
    /// <param name="g">描画に使用する <see cref="Graphics"/> オブジェクト。</param>
    private void DrawBadges(Graphics g)
    {
        // 両方のカウントが0以下の場合は描画しない
        if (_errorBadge <= 0 && _warnBadge <= 0) return;

        using var badgeFont = new Font("Segoe UI", 6.5f, FontStyle.Bold);
        // バッジを右端から順に左に向かって配置
        var x = Width - 4;

        // エラーバッジの描画（赤色の角丸矩形）
        if (_errorBadge > 0)
        {
            // 99超えは "99+" と表示
            var text = _errorBadge > 99 ? "99+" : _errorBadge.ToString();
            var size = g.MeasureString(text, badgeFont);
            // バッジ幅はテキスト幅+6px か 16px の大きい方
            var badgeWidth = Math.Max(size.Width + 6, 16);
            var badgeRect = new RectangleF(x - badgeWidth, 2, badgeWidth, 14);

            // エラー色の角丸矩形でバッジ背景を描画
            using var badgeBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.LogError));
            using var badgePath = CreateRoundedRectPath(
                Rectangle.Round(badgeRect), 7);
            g.FillPath(badgeBrush, badgePath);

            // バッジ内に白色テキストを中央揃えで描画
            using var textBrush = new SolidBrush(Color.White);
            var textX = badgeRect.X + (badgeRect.Width - size.Width) / 2;
            g.DrawString(text, badgeFont, textBrush, textX, badgeRect.Y);

            // 次のバッジの右端をエラーバッジの左に移動
            x -= (int)badgeWidth + 2;
        }

        // 警告バッジの描画（黄色の角丸矩形）
        if (_warnBadge > 0)
        {
            var text = _warnBadge > 99 ? "99+" : _warnBadge.ToString();
            var size = g.MeasureString(text, badgeFont);
            var badgeWidth = Math.Max(size.Width + 6, 16);
            var badgeRect = new RectangleF(x - badgeWidth, 2, badgeWidth, 14);

            // 警告色の角丸矩形でバッジ背景を描画
            using var badgeBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.LogWarning));
            using var badgePath = CreateRoundedRectPath(
                Rectangle.Round(badgeRect), 7);
            g.FillPath(badgeBrush, badgePath);

            // バッジ内に黒色テキストを中央揃えで描画（黄色背景に対してのコントラスト確保）
            using var textBrush = new SolidBrush(Color.Black);
            var textX = badgeRect.X + (badgeRect.Width - size.Width) / 2;
            g.DrawString(text, badgeFont, textBrush, textX, badgeRect.Y);
        }
    }

    /// <summary>
    /// 角丸矩形の <see cref="System.Drawing.Drawing2D.GraphicsPath"/> を生成する。
    /// 4つの角に指定した半径の円弧を追加して角丸形状を作成する。
    /// </summary>
    /// <param name="rect">角丸矩形の外接矩形。</param>
    /// <param name="radius">角の丸みの半径（ピクセル）。</param>
    /// <returns>角丸矩形の <see cref="System.Drawing.Drawing2D.GraphicsPath"/>。</returns>
    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        // 直径 = 半径の2倍
        var diameter = radius * 2;

        // 左上の円弧（180〜270度）
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        // 右上の円弧（270〜360度）
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        // 右下の円弧（0〜90度）
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        // 左下の円弧（90〜180度）
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        // パスを閉じて完成させる
        path.CloseFigure();

        return path;
    }
}
