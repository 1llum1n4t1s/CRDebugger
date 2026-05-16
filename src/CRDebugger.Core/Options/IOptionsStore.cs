namespace CRDebugger.Core.Options;

/// <summary>
/// Options タブで管理されるオプション値の永続化を担う抽象。
/// CRDebuggerOptions.OptionsStore に実装を渡すと、UI で変更された値を保存・復元できる。
/// </summary>
public interface IOptionsStore
{
    /// <summary>指定キーの値を読み込む（存在しない場合は null を返す）</summary>
    /// <param name="key">"ContainerType.PropertyName" 形式の一意キー</param>
    /// <returns>保存されていた値（文字列表現）。存在しない場合は null</returns>
    string? Load(string key);

    /// <summary>指定キーに値を保存する</summary>
    /// <param name="key">"ContainerType.PropertyName" 形式の一意キー</param>
    /// <param name="value">値の文字列表現</param>
    void Save(string key, string value);

    /// <summary>全ての保存内容を破棄する</summary>
    void Clear();

    /// <summary>保留中の変更を永続化メディアにフラッシュする（ファイル書き込み等）</summary>
    void Flush();
}
