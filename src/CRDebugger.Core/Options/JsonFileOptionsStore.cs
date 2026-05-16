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
    private bool _dirty;

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

                var snapshot = new Dictionary<string, string>(_values);
                var json = JsonSerializer.Serialize(snapshot, JsonOpts);
                File.WriteAllText(_filePath, json);
                _dirty = false;
            }
            catch (Exception)
            {
                // 永続化失敗はホストアプリを巻き込まない（CRDebugger の哲学に従う）
                // 失敗を InternalError 経由で通知したい場合は呼び出し側で try/catch する
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
