namespace Axrone.Memory;

public sealed class PooledBufferWriter<T> : IBufferWriter<T>, IDisposable
{
    private readonly TieredMemoryPool<T> _pool;
    private ValueMemoryLease<T> _currentLease;
    private int _elementsWritten;
    private DisposalTracker _tracker;

    public PooledBufferWriter(TieredMemoryPool<T> pool, int initialCapacity = 256)
    {
        _pool = pool;
        _currentLease = _pool.RentLease(initialCapacity);
        _elementsWritten = 0;
    }

    public int WrittenCount => _elementsWritten;
    public int Capacity => _currentLease.Length;
    public int FreeCapacity => _currentLease.Length - _elementsWritten;
    public ReadOnlySpan<T> WrittenSpan => _currentLease.Span.Slice(0, _elementsWritten);
    public ReadOnlyMemory<T> WrittenMemory => _currentLease.Memory.Slice(0, _elementsWritten);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        _tracker.ThrowIfDisposed(nameof(PooledBufferWriter<T>));
        if (count < 0 || _elementsWritten + count > _currentLease.Length) throw new ArgumentOutOfRangeException(nameof(count));
        _elementsWritten += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        _tracker.ThrowIfDisposed(nameof(PooledBufferWriter<T>));
        EnsureCapacity(sizeHint);
        return _currentLease.Memory.Slice(_elementsWritten);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan(int sizeHint = 0)
    {
        _tracker.ThrowIfDisposed(nameof(PooledBufferWriter<T>));
        EnsureCapacity(sizeHint);
        return _currentLease.Span.Slice(_elementsWritten);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void EnsureCapacity(int sizeHint)
    {
        int required = sizeHint <= 0 ? 1 : sizeHint;
        int available = _currentLease.Length - _elementsWritten;
        if (available >= required) return;

        int newCapacity = Math.Max(_currentLease.Length * 2, _elementsWritten + required);
        ValueMemoryLease<T> newLease = _pool.RentLease(newCapacity);
        _currentLease.Span.Slice(0, _elementsWritten).CopyTo(newLease.Span);
        _currentLease.Dispose();
        _currentLease = newLease;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _tracker.ThrowIfDisposed(nameof(PooledBufferWriter<T>));
        _elementsWritten = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _tracker.ThrowIfDisposed(nameof(PooledBufferWriter<T>));
        _currentLease.Span.Slice(0, _elementsWritten).Clear();
        _elementsWritten = 0;
    }

    public void Dispose()
    {
        if (_tracker.TryDispose())
        {
            _currentLease.Dispose();
            _elementsWritten = 0;
        }
    }
}
