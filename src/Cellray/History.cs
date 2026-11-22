using System.Collections;

namespace Cellray;

/// <summary>Fixed-capacity rolling series of samples (oldest first), for feeding <see cref="Gui.Graph"/>.</summary>
public sealed class History : IReadOnlyList<float>
{
    private readonly float[] _buf;
    private int _start, _count;

    public History(int capacity) => _buf = new float[Math.Max(1, capacity)];

    public int Count => _count;
    public int Capacity => _buf.Length;

    public float this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            return _buf[(_start + index) % _buf.Length];
        }
    }

    /// <summary>Appends a sample, dropping the oldest when full.</summary>
    public void Push(float value)
    {
        if (_count < _buf.Length) _buf[(_start + _count++) % _buf.Length] = value;
        else { _buf[_start] = value; _start = (_start + 1) % _buf.Length; }
    }

    public void Clear() { _start = 0; _count = 0; }

    public IEnumerator<float> GetEnumerator()
    {
        for (int i = 0; i < _count; i++) yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
