using System.Windows;
using System.Windows.Controls;
using CRDebugger.Core;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.Wpf.Converters;

namespace CRDebugger.Wpf.Windows;

/// <summary>
/// SRDebugger風のメインデバッガーウィンドウ
/// </summary>
public partial class DebuggerWindow : Window
{
    private DebuggerViewModel? _viewModel;

    public DebuggerWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is DebuggerViewModel vm)
        {
            _viewModel = vm;
            SystemInfoContent.DataContext = vm.SystemInfo;
            ConsoleContent.DataContext = vm.Console;
            OptionsContent.DataContext = vm.Options;
            ProfilerContent.DataContext = vm.Profiler;
            BugReporterContent.DataContext = vm.BugReporter;

            SelectTab(vm.SelectedTab);

            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(DebuggerViewModel.SelectedTab))
                {
                    SelectTab(vm.SelectedTab);
                }
            };
        }
    }

    private void SelectTab(CRTab tab)
    {
        // サイドバーのラジオボタンを連動
        switch (tab)
        {
            case CRTab.System:
                TabSystem.IsChecked = true;
                break;
            case CRTab.Console:
                TabConsole.IsChecked = true;
                break;
            case CRTab.Options:
                TabOptions.IsChecked = true;
                break;
            case CRTab.Profiler:
                TabProfiler.IsChecked = true;
                break;
            case CRTab.BugReporter:
                TabBugReporter.IsChecked = true;
                break;
        }

        ShowContent(tab);
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        PinButton.Opacity = Topmost ? 1.0 : 0.4;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnTabChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb)
        {
            var tab = rb.Name switch
            {
                nameof(TabSystem) => CRTab.System,
                nameof(TabConsole) => CRTab.Console,
                nameof(TabOptions) => CRTab.Options,
                nameof(TabProfiler) => CRTab.Profiler,
                nameof(TabBugReporter) => CRTab.BugReporter,
                _ => CRTab.Console
            };

            if (_viewModel != null)
                _viewModel.SelectedTab = tab;

            ShowContent(tab);
        }
    }

    private void ShowContent(CRTab tab)
    {
        SystemInfoContent.Visibility = tab == CRTab.System ? Visibility.Visible : Visibility.Collapsed;
        ConsoleContent.Visibility = tab == CRTab.Console ? Visibility.Visible : Visibility.Collapsed;
        OptionsContent.Visibility = tab == CRTab.Options ? Visibility.Visible : Visibility.Collapsed;
        ProfilerContent.Visibility = tab == CRTab.Profiler ? Visibility.Visible : Visibility.Collapsed;
        BugReporterContent.Visibility = tab == CRTab.BugReporter ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// テーマカラーを動的に適用する
    /// </summary>
    public void ApplyThemeColors(ThemeColors colors)
    {
        var themePath = IsLightTheme(colors)
            ? "/CRDebugger.Wpf;component/Themes/Light.xaml"
            : "/CRDebugger.Wpf;component/Themes/Dark.xaml";

        var dict = new ResourceDictionary
        {
            Source = new Uri(themePath, UriKind.Relative)
        };

        // 動的にリソースディクショナリを差し替え
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(dict);

        // テーマカラーでブラシを上書き（カスタムカラー対応）
        Resources["BackgroundBrush"] = ThemeColorConverter.UintToBrush(colors.Background);
        Resources["SurfaceBrush"] = ThemeColorConverter.UintToBrush(colors.Surface);
        Resources["SurfaceAltBrush"] = ThemeColorConverter.UintToBrush(colors.SurfaceAlt);
        Resources["PrimaryBrush"] = ThemeColorConverter.UintToBrush(colors.Primary);
        Resources["OnBackgroundBrush"] = ThemeColorConverter.UintToBrush(colors.OnBackground);
        Resources["OnSurfaceBrush"] = ThemeColorConverter.UintToBrush(colors.OnSurface);
        Resources["OnSurfaceMutedBrush"] = ThemeColorConverter.UintToBrush(colors.OnSurfaceMuted);
        Resources["BorderBrush"] = ThemeColorConverter.UintToBrush(colors.Border);
        Resources["SidebarBackgroundBrush"] = ThemeColorConverter.UintToBrush(colors.SidebarBackground);
        Resources["SidebarTextBrush"] = ThemeColorConverter.UintToBrush(colors.SidebarText);
        Resources["SelectedTabBrush"] = ThemeColorConverter.UintToBrush(colors.SelectedTab);
        Resources["LogDebugBrush"] = ThemeColorConverter.UintToBrush(colors.LogDebug);
        Resources["LogInfoBrush"] = ThemeColorConverter.UintToBrush(colors.LogInfo);
        Resources["LogWarningBrush"] = ThemeColorConverter.UintToBrush(colors.LogWarning);
        Resources["LogErrorBrush"] = ThemeColorConverter.UintToBrush(colors.LogError);
        Resources["SuccessBrush"] = ThemeColorConverter.UintToBrush(colors.Success);
    }

    private static bool IsLightTheme(ThemeColors colors)
    {
        // Backgroundの明度で判定
        var r = (byte)((colors.Background >> 16) & 0xFF);
        var g = (byte)((colors.Background >> 8) & 0xFF);
        var b = (byte)(colors.Background & 0xFF);
        var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        return luminance > 0.5;
    }
}
