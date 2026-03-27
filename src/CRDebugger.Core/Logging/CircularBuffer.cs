using System.Collections;

namespace CRDebugger.Core.Logging;

/// <summary>
/// 固定容量の循環バッファ。容量超過時は最古の要素を上書きする。
/// スレッドセーフではない（呼び出し側で同期が必要）。
/// </summary>
/// <typeparam name="T">バッファに格納する要素の型</typeparam>
internal sealed class CircularBuffer<T> : IReadOnlyList<T>
{
    /// <summary>固定長の内部配列。循環インデックスで管理する</summary>
    private readonly T[] _buffer;
    /// <summary>有効データの先頭インデックス（循環）</summary>
    private int _head;
    /// <summary>現在の有効要素数</summary>
    private int _count;

    /// <summary>
    /// 指定した容量で循環バッファを初期化する
    /// </summary>
    /// <param name="capacity">バッファの最大容量。1以上である必要がある</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> が0以下の場合</exception>
    public CircularBuffer(int capacity)
    {
        // 容量が0以下の場合はバッファとして機能しないため例外を投げる
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _buffer = new T[capacity];
    }

    /// <summary>バッファの最大容量</summary>
    public int Capacity => _buffer.Length;

    /// <summary>現在の有効要素数</summary>
    public int Count => _count;

    /// <summary>
    /// 論理インデックスで要素にアクセスする
    /// </summary>
    /// <param name="index">0始まりの論理インデックス</param>
    /// <returns>指定インデックスの要素</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> が範囲外の場合</exception>
    public T this[int index]
    {
        get
        {
            // 論理インデックスが有効範囲内かチェック
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));
            // 先頭オフセットを加算して実際の配列インデックスに変換（循環）
            return _buffer[(_head + index) % _buffer.Length];
        }
    }

    /// <summary>
    /// 要素をバッファ末尾に追加する。容量超過時は最古の要素を上書きする
    /// </summary>
    /// <param name="item">追加する要素</param>
    public void Add(T item)
    {
        // 書き込み先インデックスを計算（先頭 + 現在件数 を容量で折り返す）
        var tail = (_head + _count) % _buffer.Length;
        _buffer[tail] = item;

        if (_count == _buffer.Length)
            // 満杯の場合は先頭を1つ進めて最古の要素を捨てる
            _head = (_head + 1) % _buffer.Length;
        else
            // まだ空きがある場合は件数をインクリメントするだけ
            _count++;
    }

    /// <summary>
    /// 最後に追加した要素を指定した値で上書きする
    /// </summary>
    /// <param name="item">上書きする新しい値</param>
    /// <exception cref="InvalidOperationException">バッファが空の場合</exception>
    public void UpdateLast(T item)
    {
        if (_count == 0) throw new InvalidOperationException("バッファが空です。");
        // 末尾要素の実インデックスを計算して上書き
        var lastIndex = (_head + _count - 1) % _buffer.Length;
        _buffer[lastIndex] = item;
    }

    /// <summary>
    /// バッファの全要素をクリアして初期状態に戻す
    /// </summary>
    public void Clear()
    {
        // GCへの参照を解放するため配列をゼロクリアする
        Array.Clear(_buffer, 0, _buffer.Length);
        _head = 0;
        _count = 0;
    }

    /// <summary>
    /// バッファの内容を新しいリストとして返す
    /// </summary>
    /// <returns>論理順序で並んだ要素のリスト</returns>
    public List<T> ToList()
    {
        var list = new List<T>(_count);
        // 論理インデックス順にインデクサ経由で取り出す
        for (var i = 0; i < _count; i++)
            list.Add(this[i]);
        return list;
    }

    /// <summary>
    /// 論理順序でバッファを列挙するイテレータを返す
    /// </summary>
    /// <returns>要素を論理順に返す列挙子</returns>
    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < _count; i++)
            yield return this[i];
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
