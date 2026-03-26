using System.Collections;

namespace CRDebugger.Core.Logging;

/// <summary>
/// 固定容量の循環バッファ。容量超過時は最古の要素を上書きする。
/// スレッドセーフではない（呼び出し側で同期が必要）。
/// </summary>
internal sealed class CircularBuffer<T> : IReadOnlyList<T>
{
    private readonly T[] _buffer;
    private int _head;
    private int _count;

    public CircularBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _buffer = new T[capacity];
    }

    public int Capacity => _buffer.Length;
    public int Count => _count;

    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _buffer[(_head + index) % _buffer.Length];
        }
    }

    public void Add(T item)
    {
        var tail = (_head + _count) % _buffer.Length;
        _buffer[tail] = item;

        if (_count == _buffer.Length)
            _head = (_head + 1) % _buffer.Length;
        else
            _count++;
    }

    public void Clear()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
        _head = 0;
        _count = 0;
    }

    public List<T> ToList()
    {
        var list = new List<T>(_count);
        for (var i = 0; i < _count; i++)
            list.Add(this[i]);
        return list;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < _count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
