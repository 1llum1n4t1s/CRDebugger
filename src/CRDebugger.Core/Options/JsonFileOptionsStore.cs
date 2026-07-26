using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace CRDebugger.Core.Options;

/// <summary>
/// JSON ファイルに Options を永続化するデフォルト実装。
/// プロセス起動時にファイルから読み込み、Save 毎にメモリ更新、Flush でファイル書き出し。
/// </summary>
public sealed class JsonFileOptionsStore : IOptionsStore
{
    private readonly string _filePath;
    private readonly ConcurrentDictionary<string, string> _values;
    private readonly object _writeLock = new();

    /// <summary>
    /// 未保存の変更があるかどうか。Save は任意のスレッド、Flush は Shutdown スレッドから呼ばれるため、
    /// ロック外での早期判定が確実に最新値を読むよう volatile にする。
    /// </summary>
    private volatile bool _dirty;

    /// <summary>
    /// 指定ファイルパスで永続化ストアを生成する。
    /// </summary>
    /// <param name="filePath">JSON 保存先（例: "crdebugger-options.json"）</param>
    public JsonFileOptionsStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("ファイルパスを指定してください", nameof(filePath));

        _filePath = filePath;
        _values = LoadFromFile(filePath);
    }

    /// <inheritdoc/>
    public string? Load(string key) =>
        _values.TryGetValue(key, out var value) ? value : null;

    /// <inheritdoc/>
    public void Save(string key, string value)
    {
        _values[key] = value;
        _dirty = true;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _values.Clear();
        _dirty = true;
    }

    /// <inheritdoc/>
    public void Flush()
    {
        // 変更が無ければファイル I/O をスキップ
        if (!_dirty) return;

        lock (_writeLock)
        {
            if (!_dirty) return;

            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                // スナップショットを先に確定させ、_dirty も先に落とす。
                // 書き出し中に届いた Save は必ず「未保存」として次回 Flush の対象になる
                // （後で落とすと、その Save がスナップショットにも入らず _dirty=false で埋もれる）。
                var snapshot = new Dictionary<string, string>(_values);
                _dirty = false;

                var json = JsonSerializer.Serialize(snapshot, JsonOpts);

                // 一時ファイルへ書いてから置換することで、書き込み中の異常終了でも
                // 既存の設定ファイルが 0 バイト／途中切れにならないようにする（同一ボリュームの Move は原子的）。
                var tempPath = _filePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _filePath, overwrite: true);
            }
            catch (Exception)
            {
                // 永続化失敗はホストアプリを巻き込まない（CRDebugger の哲学に従う）。
                // ただし未保存であることは事実なので _dirty を戻し、次回の Flush で再試行できるようにする。
                _dirty = true;
            }
        }
    }

    private static ConcurrentDictionary<string, string> LoadFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return new ConcurrentDictionary<string, string>();

            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json)) return new ConcurrentDictionary<string, string>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOpts)
                       ?? new Dictionary<string, string>();
            return new ConcurrentDictionary<string, string>(dict);
        }
        catch (Exception)
        {
            // 破損ファイルは無視して空ストアで起動
            return new ConcurrentDictionary<string, string>();
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
