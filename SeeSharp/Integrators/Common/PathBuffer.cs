namespace SeeSharp.Integrators.Common;

/// <summary>
/// A container optimized to be frequently and repeatedly re-used to store small
/// batches of data (e.g., the vertices along the currently traced path).
/// </summary>
public class PathBuffer<T>(int expectedLength) {
    T[] buffer = new T[expectedLength];
    int next = 0;

    public void Add(in T value) {
        if (next == buffer.Length) {
            T[] bigger = new T[buffer.Length * 2];
            buffer.CopyTo(bigger, 0);
            buffer = bigger;
        }
        buffer[next++] = value;
    }

    public void Clear() => next = 0;

    public int Count => next;

    public ref T this[int i] => ref buffer[i];

    public ReadOnlySpan<T> AsSpan() => buffer.AsSpan(0, next);
}