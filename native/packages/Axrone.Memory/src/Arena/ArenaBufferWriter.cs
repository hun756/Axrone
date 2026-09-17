namespace Axrone.Memory.Arena;

public sealed class ArenaBufferWriter<T, TBackoff> : IBufferWriter<T>
    where T : unmanaged
    where TBackoff : struct, IBackoffPolicy
{
    private readonly ArenaMemoryRing<T, TBackoff> _ring;
    private Memory<T> _currentBuffer;
    private int _written;

    public ArenaBufferWriter(ArenaMemoryRing<T, TBackoff> ring)
    {
        if (ring is null) ThrowHelper.ThrowArgumentNullException(nameof(ring));
        _ring = ring;
    }

    public int WrittenCount => _written;

    public void Advance(int count)
    {
        if (count < 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
        if (_written + count > _currentBuffer.Length) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count), "Advance exceeded reserved buffer length.");
        _written += count;
    }

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        if (sizeHint < 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(sizeHint));
        if (sizeHint == 0) sizeHint = Math.Max(1, _ring.Capacity / 4);

        int reserved = _ring.ReserveWriteBatch(Math.Min(sizeHint, _ring.Capacity), out Span<T> span);
        if (reserved <= 0)
        {
            ThrowHelper.ThrowInvalidOperationException("Arena ring is full; cannot reserve write batch.");
        }

        _currentBuffer = _ring is ArenaMemoryRing<T, TBackoff> r
            ? GetMemoryFromSpan(span, reserved)
            : default;
        _written = 0;
        return _currentBuffer;
    }

    public Span<T> GetSpan(int sizeHint = 0)
    {
        return GetMemory(sizeHint).Span;
    }

    public void Commit()
    {
        if (_written > 0)
        {
            _ring.CommitWrite(_written);
            _written = 0;
        }
    }

    private static Memory<T> GetMemoryFromSpan(Span<T> span, int length)
    {
        return span.Slice(0, length).ToArray();
    }
}
