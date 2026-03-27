using System.Collections.ObjectModel;
using System.Windows.Input;
using CRDebugger.Core.SystemInfo;

namespace CRDebugger.Core.ViewModels;

/// <summary>
/// システム情報タブのViewModel。
/// OS・ランタイム・プロセス・アプリケーション等の情報を
/// <see cref="SystemInfoCollector"/> から収集し、カテゴリ別にグループ化して表示する。
/// </summary>
public sealed class SystemInfoViewModel : ViewModelBase
{
    /// <summary>システム情報の収集処理を担うコレクター</summary>
    private readonly SystemInfoCollector _collector;

    /// <summary>
    /// カテゴリ別にグループ化されたシステム情報の一覧。
    /// UIのリストコントロールにバインドされる。
    /// </summary>
    public ObservableCollection<SystemInfoGroup> Groups { get; } = new();

    /// <summary>
    /// システム情報を再収集して表示を更新するコマンド。
    /// UIの「更新」ボタンに対応する。
    /// </summary>
    public ICommand RefreshCommand { get; }

    /// <summary>
    /// <see cref="SystemInfoViewModel"/> のインスタンスを生成する
    /// </summary>
    /// <param name="collector">OSやランタイムからシステム情報を収集するコレクター</param>
    public SystemInfoViewModel(SystemInfoCollector collector)
    {
        _collector = collector;
        RefreshCommand = new RelayCommand(Refresh);

        // 初期表示用にシステム情報を収集・構築
        Refresh();
    }

    /// <summary>
    /// システム情報を再収集し、<see cref="Groups"/> を再構築して表示を更新する。
    /// 既存のグループをクリアしてから収集・グループ化・変換を行う。
    /// </summary>
    public void Refresh()
    {
        // 既存のグループ一覧をクリア
        Groups.Clear();

        // コレクターから全情報エントリを収集
        var entries = _collector.CollectAll();
        // カテゴリ名でグループ化
        var groups = entries.GroupBy(e => e.Category);
        foreach (var group in groups)
        {
            // 各エントリをSystemInfoItemに変換してグループとしてコレクションに追加
            Groups.Add(new SystemInfoGroup(
                group.Key,
                group.Select(e => new SystemInfoItem(e.Key, e.Value)).ToList()
            ));
        }
    }
}

/// <summary>
/// カテゴリ別にグループ化されたシステム情報。
/// UIのグループヘッダーと項目リストの単位として使用される。
/// </summary>
/// <param name="Category">カテゴリ名（例: "System"、"Runtime"、"Process"）</param>
/// <param name="Items">このカテゴリに属するシステム情報項目のリスト</param>
public sealed record SystemInfoGroup(string Category, IReadOnlyList<SystemInfoItem> Items);

/// <summary>
/// システム情報の個別項目を表すキーバリューペア。
/// UIの各行に対応する。
/// </summary>
/// <param name="Key">項目名（例: "OS"、"Machine Name"、".NET Version"）</param>
/// <param name="Value">値（例: "Windows 11 Pro"、"DESKTOP-XXXX"）</param>
public sealed record SystemInfoItem(string Key, string Value);
