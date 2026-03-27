using System.Windows;
using System.Windows.Controls;
using CRDebugger.Core;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.Wpf.Converters;

namespace CRDebugger.Wpf.Windows;

/// <summary>
/// SRDebugger 風のメインデバッガーウィンドウ。
/// サイドバーのタブ切り替えで System・Console・Options・Profiler・BugReporter の
/// 各コンテンツビューを表示し、ピン留め・閉じるボタンを提供する。
/// </summary>
public partial class DebuggerWindow : Window
{
    /// <summary>現在バインドされている DebuggerViewModel の参照</summary>
    private DebuggerViewModel? _viewModel;

    /// <summary>
    /// DebuggerWindow を初期化し、DataContext 変更イベントを購読する
    /// </summary>
    public DebuggerWindow()
    {
        InitializeComponent();
        // DataContext が差し替わった際に各ビューの DataContext を更新する
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// DataContext が変更されたときのイベントハンドラ。
    /// DebuggerViewModel の各サブ ViewModel を各コンテンツビューの DataContext に設定し、
    /// SelectedTab の変更を購読してサイドバーのラジオボタンと連動させる。
    /// </summary>
    /// <param name="sender">イベント送信元</param>
    /// <param name="e">旧値と新値を含む DependencyPropertyChangedEventArgs</param>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is DebuggerViewModel vm)
        {
            _viewModel = vm;

            // 各コンテンツビューに対応するサブ ViewModel を DataContext として設定
            SystemInfoContent.DataContext = vm.SystemInfo;
            ConsoleContent.DataContext = vm.Console;
            OptionsContent.DataContext = vm.Options;
            ProfilerContent.DataContext = vm.Profiler;
            BugReporterContent.DataContext = vm.BugReporter;

            // 初期タブを選択状態にする
            SelectTab(vm.SelectedTab);

            // SelectedTab プロパティの変更を購読してサイドバーのラジオボタンと同期
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(DebuggerViewModel.SelectedTab))
                {
                    SelectTab(vm.SelectedTab);
                }
            };
        }
    }

    /// <summary>
    /// 指定したタブをアクティブにする。
    /// サイドバーの対応するラジオボタンをチェック状態にし、コンテンツを表示する。
    /// </summary>
    /// <param name="tab">アクティブにする CRTab 値</param>
    private void SelectTab(CRTab tab)
    {
        // サイドバーのラジオボタンを指定タブに対応するものに切り替える
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

        // 選択タブに対応するコンテンツを表示し、他を非表示にする
        ShowContent(tab);
    }

    /// <summary>
    /// ピン留めボタンのクリックイベントハンドラ。
    /// ウィンドウの常前面表示（Topmost）を切り替え、ボタンの透明度を変化させる。
    /// </summary>
    /// <param name="sender">ピン留めボタン</param>
    /// <param name="e">ルーティングイベント引数</param>
    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        // Topmost フラグを反転してピン留め状態をトグル
        Topmost = !Topmost;
        // ピン留め中は完全不透明、解除時は半透明でボタンの状態を視覚的に表現
        PinButton.Opacity = Topmost ? 1.0 : 0.4;
    }

    /// <summary>
    /// 閉じるボタンのクリックイベントハンドラ。
    /// ウィンドウを閉じずに非表示にしてインスタンスを保持する。
    /// </summary>
    /// <param name="sender">閉じるボタン</param>
    /// <param name="e">ルーティングイベント引数</param>
    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        // Close() ではなく Hide() でウィンドウを非表示にして再利用可能な状態を保つ
        Hide();
    }

    /// <summary>
    /// サイドバーのタブ（ラジオボタン）が変更されたときのイベントハンドラ。
    /// クリックされたラジオボタンの名前から CRTab を解決して ViewModel と表示を更新する。
    /// </summary>
    /// <param name="sender">クリックされた RadioButton</param>
    /// <param name="e">ルーティングイベント引数</param>
    private void OnTabChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb)
        {
            // ラジオボタンの名前から対応する CRTab を解決
            var tab = rb.Name switch
            {
                nameof(TabSystem) => CRTab.System,
                nameof(TabConsole) => CRTab.Console,
                nameof(TabOptions) => CRTab.Options,
                nameof(TabProfiler) => CRTab.Profiler,
                nameof(TabBugReporter) => CRTab.BugReporter,
                _ => CRTab.Console  // 未知の名前はコンソールをデフォルトに
            };

            // ViewModel の SelectedTab を更新して双方向同期を維持
            if (_viewModel != null)
                _viewModel.SelectedTab = tab;

            // コンテンツの表示切り替えを実行
            ShowContent(tab);
        }
    }

    /// <summary>
    /// 指定したタブに対応するコンテンツのみを表示し、他を非表示にする
    /// </summary>
    /// <param name="tab">表示する CRTab 値</param>
    private void ShowContent(CRTab tab)
    {
        // 各コンテンツの Visibility を選択タブと一致するかどうかで切り替え
        SystemInfoContent.Visibility = tab == CRTab.System ? Visibility.Visible : Visibility.Collapsed;
        ConsoleContent.Visibility = tab == CRTab.Console ? Visibility.Visible : Visibility.Collapsed;
        OptionsContent.Visibility = tab == CRTab.Options ? Visibility.Visible : Visibility.Collapsed;
        ProfilerContent.Visibility = tab == CRTab.Profiler ? Visibility.Visible : Visibility.Collapsed;
        BugReporterContent.Visibility = tab == CRTab.BugReporter ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// テーマカラーを動的に適用する。
    /// 背景の輝度でライト/ダークテーマを自動判定し、対応する ResourceDictionary を読み込んだ後、
    /// ThemeColors の各値でブラシリソースを上書きしてカスタムカラーに対応する。
    /// </summary>
    /// <param name="colors">適用するテーマカラー群</param>
    public void ApplyThemeColors(ThemeColors colors)
    {
        // 背景色の輝度でライト/ダークテーマの XAML ファイルパスを決定
        var themePath = IsLightTheme(colors)
            ? "/CRDebugger.Wpf;component/Themes/Light.xaml"
            : "/CRDebugger.Wpf;component/Themes/Dark.xaml";

        // 選択したテーマの ResourceDictionary を生成
        var dict = new ResourceDictionary
        {
            Source = new Uri(themePath, UriKind.Relative)
        };

        // 既存のリソースディクショナリをクリアして新テーマに差し替え
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(dict);

        // テーマカラーの各値でブラシリソースを上書き（カスタムカラー対応）
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

    /// <summary>
    /// 背景色の輝度に基づいてライトテーマかどうかを判定する。
    /// ITU-R BT.601 の輝度係数（Y = 0.299R + 0.587G + 0.114B）を使用する。
    /// </summary>
    /// <param name="colors">判定に使用するテーマカラー</param>
    /// <returns>ライトテーマの場合は true、ダークテーマの場合は false</returns>
    private static bool IsLightTheme(ThemeColors colors)
    {
        // Background カラーの R・G・B チャンネルをビットシフトで抽出
        var r = (byte)((colors.Background >> 16) & 0xFF);
        var g = (byte)((colors.Background >> 8) & 0xFF);
        var b = (byte)(colors.Background & 0xFF);
        // ITU-R BT.601 輝度式で相対輝度を計算し、0.5 を閾値にライト/ダークを判定
        var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        return luminance > 0.5;
    }
}
