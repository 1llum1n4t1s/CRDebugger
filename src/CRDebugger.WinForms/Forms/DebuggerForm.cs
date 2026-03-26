using System.ComponentModel;
using CRDebugger.Core;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.WinForms.Controls;
using CRDebugger.WinForms.Panels;

namespace CRDebugger.WinForms.Forms;

/// <summary>
/// モダンデザインのメインデバッガーフォーム
/// 左サイドバー（ヘッダー + タブ） + 右コンテンツパネルのレイアウト
/// </summary>
public sealed class DebuggerForm : Form
{
    private readonly DebuggerViewModel _viewModel;

    // サイドバー
    private readonly Panel _sidebarPanel;
    private readonly List<SidebarButton> _sidebarButtons = new();

    // コンテンツパネル
    private readonly Panel _contentPanel;
    private readonly SystemInfoPanel _systemInfoPanel;
    private readonly ConsolePanel _consolePanel;
    private readonly OptionsPanel _optionsPanel;
    private readonly ProfilerPanel _profilerPanel;
    private readonly BugReporterPanel _bugReporterPanel;

    private ThemeColors _colors;

    // サイドバーボタンのアイコン定義
    private static readonly (CRTab Tab, string Icon, string Label)[] TabDefs =
    {
        (CRTab.System, "\u2139", "System"),       // ℹ
        (CRTab.Console, "\u25B6", "Console"),     // ▶
        (CRTab.Options, "\u2699", "Options"),     // ⚙
        (CRTab.Profiler, "\u2261", "Profiler"),   // ≡
        (CRTab.BugReporter, "\u2709", "Bug Report"), // ✉
    };

    private const int SidebarWidth = 76;
    private const int HeaderHeight = 52;

    public DebuggerForm(DebuggerViewModel viewModel)
    {
        _viewModel = viewModel;
        _colors = viewModel.ThemeColors;

        // フォーム基本設定
        Text = "CRDebugger";
        Size = new Size(900, 600);
        MinimumSize = new Size(640, 400);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(
            Screen.PrimaryScreen!.WorkingArea.Right - 920,
            Screen.PrimaryScreen.WorkingArea.Top + 20);
        TopMost = true;
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = true;
        DoubleBuffered = true;
        Font = new Font("Segoe UI", 9);

        // サイドバーパネル
        _sidebarPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = SidebarWidth,
        };
        _sidebarPanel.Paint += PaintSidebar;
        Controls.Add(_sidebarPanel);

        // コンテンツパネル
        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
        };
        Controls.Add(_contentPanel);

        // 各タブパネルを生成
        _systemInfoPanel = new SystemInfoPanel(viewModel.SystemInfo, _colors);
        _consolePanel = new ConsolePanel(viewModel.Console, _colors);
        _optionsPanel = new OptionsPanel(viewModel.Options, _colors);
        _profilerPanel = new ProfilerPanel(viewModel.Profiler, _colors);
        _bugReporterPanel = new BugReporterPanel(viewModel.BugReporter, _colors);

        // コンテンツパネルに全パネルを追加（非表示で）
        foreach (var panel in AllPanels())
        {
            panel.Dock = DockStyle.Fill;
            panel.Visible = false;
            _contentPanel.Controls.Add(panel);
        }

        // サイドバーボタンを生成
        CreateSidebarButtons();

        // ViewModelのイベントを購読
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.Console.PropertyChanged += OnConsolePropertyChanged;

        // 初期タブを表示
        SwitchToTab(_viewModel.SelectedTab);
        ApplyTheme(_colors);

        // UIスレッド設定
        if (_viewModel.ThemeManager != null)
        {
            var uiThread = FindUiThread();
            uiThread?.SetMarshalControl(this);
        }
    }

    /// <summary>
    /// テーマカラーを適用する
    /// </summary>
    public void ApplyTheme(ThemeColors colors)
    {
        _colors = colors;
        BackColor = OptionControlFactory.ArgbToColor(colors.Background);
        _sidebarPanel.BackColor = OptionControlFactory.ArgbToColor(colors.SidebarBackground);
        _contentPanel.BackColor = OptionControlFactory.ArgbToColor(colors.Background);

        // 各パネルにテーマ適用
        _systemInfoPanel.ApplyTheme(colors);
        _consolePanel.ApplyTheme(colors);
        _optionsPanel.ApplyTheme(colors);
        _profilerPanel.ApplyTheme(colors);
        _bugReporterPanel.ApplyTheme(colors);

        // サイドバーボタンを更新
        foreach (var btn in _sidebarButtons)
        {
            btn.UpdateColors(colors);
        }

        _sidebarPanel.Invalidate();
        Invalidate(true);
    }

    private void CreateSidebarButtons()
    {
        // ヘッダー領域の下からボタンを配置
        var y = HeaderHeight + 8;
        foreach (var (tab, icon, label) in TabDefs)
        {
            var btn = new SidebarButton(tab, icon, label, _colors)
            {
                Location = new Point(0, y),
                Size = new Size(SidebarWidth, 60),
                IsSelected = tab == _viewModel.SelectedTab,
            };

            btn.Click += (_, _) =>
            {
                _viewModel.SelectedTab = tab;
            };

            _sidebarPanel.Controls.Add(btn);
            _sidebarButtons.Add(btn);
            y += 62;
        }
    }

    private void SwitchToTab(CRTab tab)
    {
        foreach (var panel in AllPanels())
            panel.Visible = false;

        GetPanelForTab(tab).Visible = true;

        foreach (var btn in _sidebarButtons)
            btn.IsSelected = btn.Tab == tab;
    }

    private Control GetPanelForTab(CRTab tab) => tab switch
    {
        CRTab.System => _systemInfoPanel,
        CRTab.Console => _consolePanel,
        CRTab.Options => _optionsPanel,
        CRTab.Profiler => _profilerPanel,
        CRTab.BugReporter => _bugReporterPanel,
        _ => _systemInfoPanel,
    };

    private IEnumerable<Control> AllPanels()
    {
        yield return _systemInfoPanel;
        yield return _consolePanel;
        yield return _optionsPanel;
        yield return _profilerPanel;
        yield return _bugReporterPanel;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DebuggerViewModel.SelectedTab))
        {
            if (InvokeRequired)
                Invoke(() => SwitchToTab(_viewModel.SelectedTab));
            else
                SwitchToTab(_viewModel.SelectedTab);
        }
        else if (e.PropertyName == nameof(DebuggerViewModel.ThemeColors))
        {
            if (InvokeRequired)
                Invoke(() => ApplyTheme(_viewModel.ThemeColors));
            else
                ApplyTheme(_viewModel.ThemeColors);
        }
    }

    private void OnConsolePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // ログカウント変更時にサイドバーバッジを更新
        if (e.PropertyName is nameof(ConsoleViewModel.ErrorCount)
            or nameof(ConsoleViewModel.WarningCount)
            or nameof(ConsoleViewModel.InfoCount)
            or nameof(ConsoleViewModel.DebugCount))
        {
            var consoleBtn = _sidebarButtons.Find(b => b.Tab == CRTab.Console);
            if (consoleBtn != null)
            {
                var errorCount = _viewModel.Console.ErrorCount;
                var warnCount = _viewModel.Console.WarningCount;

                if (InvokeRequired)
                    Invoke(() => consoleBtn.SetBadge(errorCount, warnCount));
                else
                    consoleBtn.SetBadge(errorCount, warnCount);
            }
        }
    }

    private void PaintSidebar(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // ヘッダー: "CR" をアクセント色 + "DEBUGGER" をミュート色で描画
        using var accentFont = new Font("Segoe UI", 13, FontStyle.Bold);
        using var mutedFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var accentBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.SelectedTab));
        using var mutedBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.OnSurfaceMuted));

        // "CR" を中央に描画
        var crText = "CR";
        var crSize = g.MeasureString(crText, accentFont);
        var crX = (_sidebarPanel.Width - crSize.Width) / 2;
        g.DrawString(crText, accentFont, accentBrush, crX, 10);

        // "DEBUGGER" をその下に描画
        var dbgText = "DEBUGGER";
        var dbgSize = g.MeasureString(dbgText, mutedFont);
        var dbgX = (_sidebarPanel.Width - dbgSize.Width) / 2;
        g.DrawString(dbgText, mutedFont, mutedBrush, dbgX, 30);

        // ヘッダー下に区切り線
        using var separatorPen = new Pen(Color.FromArgb(20, 255, 255, 255), 1);
        g.DrawLine(separatorPen, 12, HeaderHeight, _sidebarPanel.Width - 12, HeaderHeight);

        // サイドバーの右端に区切り線を描画
        using var borderPen = new Pen(Color.FromArgb(12, 255, 255, 255), 1);
        g.DrawLine(borderPen,
            _sidebarPanel.Width - 1, 0,
            _sidebarPanel.Width - 1, _sidebarPanel.Height);
    }

    private WinFormsUiThread? FindUiThread()
    {
        // WinFormsUiThreadインスタンスを見つけてマーシャルコントロールを設定
        // UseWinForms()で設定済みのインスタンスを探す
        return null; // DebuggerWindow側で設定する
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // 閉じるボタンで非表示にする（破棄しない）
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Console.PropertyChanged -= OnConsolePropertyChanged;
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// サイドバーのカスタムボタンコントロール
/// テキストシンボルアイコン + ラベル + バッジ表示
/// 選択時は左端に3pxアクセントバーを描画
/// </summary>
internal sealed class SidebarButton : Control
{
    public CRTab Tab { get; }

    private readonly string _icon;
    private readonly string _label;
    private bool _isSelected;
    private bool _isHovered;
    private int _errorBadge;
    private int _warnBadge;
    private ThemeColors _colors;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            Invalidate();
        }
    }

    public SidebarButton(CRTab tab, string icon, string label, ThemeColors colors)
    {
        Tab = tab;
        _icon = icon;
        _label = label;
        _colors = colors;

        SetStyle(ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.SupportsTransparentBackColor, true);

        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
    }

    public void UpdateColors(ThemeColors colors)
    {
        _colors = colors;
        Invalidate();
    }

    public void SetBadge(int errorCount, int warnCount)
    {
        _errorBadge = errorCount;
        _warnBadge = warnCount;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _isHovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _isHovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // 背景
        Color bgColor;
        if (_isSelected)
            bgColor = Color.FromArgb(25, OptionControlFactory.ArgbToColor(_colors.SelectedTab));
        else if (_isHovered)
            bgColor = Color.FromArgb(15, 255, 255, 255);
        else
            bgColor = Color.Transparent;

        if (bgColor != Color.Transparent)
        {
            using var bgBrush = new SolidBrush(bgColor);
            var rect = new Rectangle(4, 2, Width - 8, Height - 4);
            var radius = 8;
            using var path = CreateRoundedRectPath(rect, radius);
            g.FillPath(bgBrush, path);
        }

        // 選択インジケーター（左端に3pxアクセントバー）
        if (_isSelected)
        {
            using var indicatorBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.SelectedTab));
            var barRect = new Rectangle(0, 10, 3, Height - 20);
            using var barPath = CreateRoundedRectPath(barRect, 2);
            g.FillPath(indicatorBrush, barPath);
        }

        // アイコン
        var iconColor = _isSelected
            ? OptionControlFactory.ArgbToColor(_colors.SelectedTab)
            : (_isHovered
                ? OptionControlFactory.ArgbToColor(_colors.OnBackground)
                : OptionControlFactory.ArgbToColor(_colors.SidebarText));

        using var iconFont = new Font("Segoe UI Symbol", 16, FontStyle.Regular);
        using var iconBrush = new SolidBrush(iconColor);
        var iconSize = g.MeasureString(_icon, iconFont);
        var iconX = (Width - iconSize.Width) / 2;
        g.DrawString(_icon, iconFont, iconBrush, iconX, 8);

        // ラベル
        var labelColor = _isSelected
            ? OptionControlFactory.ArgbToColor(_colors.OnBackground)
            : (_isHovered
                ? OptionControlFactory.ArgbToColor(_colors.OnSurface)
                : OptionControlFactory.ArgbToColor(_colors.SidebarText));

        using var labelFont = new Font("Segoe UI", 7.5f, FontStyle.Regular);
        using var labelBrush = new SolidBrush(labelColor);
        var labelSize = g.MeasureString(_label, labelFont);
        var labelX = (Width - labelSize.Width) / 2;
        g.DrawString(_label, labelFont, labelBrush, labelX, 34);

        // バッジ表示
        DrawBadges(g);
    }

    private void DrawBadges(Graphics g)
    {
        if (_errorBadge <= 0 && _warnBadge <= 0) return;

        using var badgeFont = new Font("Segoe UI", 6.5f, FontStyle.Bold);
        var x = Width - 4;

        if (_errorBadge > 0)
        {
            var text = _errorBadge > 99 ? "99+" : _errorBadge.ToString();
            var size = g.MeasureString(text, badgeFont);
            var badgeWidth = Math.Max(size.Width + 6, 16);
            var badgeRect = new RectangleF(x - badgeWidth, 2, badgeWidth, 14);

            using var badgeBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.LogError));
            using var badgePath = CreateRoundedRectPath(
                Rectangle.Round(badgeRect), 7);
            g.FillPath(badgeBrush, badgePath);

            using var textBrush = new SolidBrush(Color.White);
            var textX = badgeRect.X + (badgeRect.Width - size.Width) / 2;
            g.DrawString(text, badgeFont, textBrush, textX, badgeRect.Y);

            x -= (int)badgeWidth + 2;
        }

        if (_warnBadge > 0)
        {
            var text = _warnBadge > 99 ? "99+" : _warnBadge.ToString();
            var size = g.MeasureString(text, badgeFont);
            var badgeWidth = Math.Max(size.Width + 6, 16);
            var badgeRect = new RectangleF(x - badgeWidth, 2, badgeWidth, 14);

            using var badgeBrush = new SolidBrush(OptionControlFactory.ArgbToColor(_colors.LogWarning));
            using var badgePath = CreateRoundedRectPath(
                Rectangle.Round(badgeRect), 7);
            g.FillPath(badgeBrush, badgePath);

            using var textBrush = new SolidBrush(Color.Black);
            var textX = badgeRect.X + (badgeRect.Width - size.Width) / 2;
            g.DrawString(text, badgeFont, textBrush, textX, badgeRect.Y);
        }
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }
}
