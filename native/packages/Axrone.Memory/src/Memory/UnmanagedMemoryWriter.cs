namespace Axrone.Memory;

/// <summary>
/// Auto-resizing <see cref="IBufferWriter{T}"/> backed by <see cref="UnmanagedMemoryPool"/>.
/// Writes bytes to native memory with automatic doubling growth strategy.
/// </summary>
public sealed class UnmanagedMemoryWriter : IBufferWriter<byte>, IDisposable
{
    private readonly UnmanagedMemoryPool _pool;
    private UnmanagedMemoryHandle _handle;
    private readonly UnmanagedMemoryManager _manager;
    private int _written;
    private readonly int _minimumSegmentSize;
    private bool _disposed;

    /// <summary>Creates a writer with the specified initial capacity.</summary>
    /// <param name="pool">The unmanaged pool to rent from.</param>
    /// <param name="initialCapacity">Initial buffer capacity in bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pool"/> is null.</exception>
    public unsafe UnmanagedMemoryWriter(UnmanagedMemoryPool pool, int initialCapacity = 4096)
    {
        ArgumentNullException.ThrowIfNull(pool);

        _pool = pool;
        _minimumSegmentSize = Math.Max(initialCapacity, 1024);
        _handle = pool.Rent(_minimumSegmentSize);
        _manager = new UnmanagedMemoryManager(_handle.ToPointer(), _handle.Size);
        _written = 0;
    }

    /// <summary>Gets the number of bytes written so far.</summary>
    public int Written => _written;

    /// <summary>Gets the current buffer capacity in bytes.</summary>
    public int Capacity => _handle.Size;

    /// <summary>Gets the remaining free capacity in bytes.</summary>
    public int FreeCapacity => Capacity - _written;

    /// <summary>Advances the write position by <paramref name="count"/> bytes.</summary>
    /// <param name="count">Number of bytes to advance.</param>
    /// <exception cref="ArgumentOutOfRangeException">Advancing past capacity.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, Capacity - _written);

        _written += count;
    }

    /// <summary>Gets a <see cref="Memory{T}"/> of at least <paramref name="sizeHint"/> bytes for writing.</summary>
    /// <param name="sizeHint">Minimum size required. 0 means any available space.</param>
    /// <returns>A memory region for writing backed by unmanaged memory.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Memory<byte> GetMemory(int sizeHint = 0)
    {
        CheckAndResizeBuffer(sizeHint);
        int remaining = Capacity - _written;
        _manager.Update((byte*)_handle.ToPointer() + _written, remaining);
        return _manager.Memory;
    }

    /// <summary>Gets a <see cref="Span{T}"/> of at least <paramref name="sizeHint"/> bytes for writing.</summary>
    /// <param name="sizeHint">Minimum size required. 0 means any available space.</param>
    /// <returns>A span for writing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Span<byte> GetSpan(int sizeHint = 0)
    {
        CheckAndResizeBuffer(sizeHint);
        return new Span<byte>((byte*)_handle.ToPointer() + _written, Capacity - _written);
    }

    /// <summary>Writes a single byte.</summary>
    /// <param name="value">The byte to write.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(byte value)
    {
        var span = GetSpan(1);
        span[0] = value;
        Advance(1);
    }

    /// <summary>Writes a span of bytes.</summary>
    /// <param name="source">The bytes to write.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty)
            return;

        var destination = GetSpan(source.Length);
        source.CopyTo(destination);
        Advance(source.Length);
    }

    /// <summary>Copies the written data to the destination span.</summary>
    /// <param name="destination">Destination span.</param>
    /// <exception cref="ArgumentOutOfRangeException">Destination is too small.</exception>
    /// <exception cref="ObjectDisposedException">Writer has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (destination.Length < _written)
            throw new ArgumentOutOfRangeException(nameof(destination));

        _handle.AsSpan().Slice(0, _written).CopyTo(destination);
    }

    /// <summary>Returns a copy of the written data as a new byte array.</summary>
    /// <returns>A byte array containing the written data.</returns>
    /// <exception cref="ObjectDisposedException">Writer has been disposed.</exception>
    public byte[] ToArray()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_written == 0)
            return [];

        var result = new byte[_written];
        _handle.AsSpan().Slice(0, _written).CopyTo(result);
        return result;
    }

    /// <summary>Resets the writer, zeroing written data.</summary>
    /// <exception cref="ObjectDisposedException">Writer has been disposed.</exception>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _handle.AsSpan().Slice(0, _written).Clear();
        _written = 0;
    }

    /// <summary>Disposes the writer and returns the underlying buffer to the pool.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _handle.Dispose();
        ((IDisposable)_manager).Dispose();
        _written = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckAndResizeBuffer(int sizeHint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        if (sizeHint == 0)
            sizeHint = 1;

        if (sizeHint <= FreeCapacity)
            return;

        int currentLength = Capacity;
        int growBy = Math.Max(sizeHint, currentLength);

        if (_minimumSegmentSize > growBy)
            growBy = _minimumSegmentSize;

        int newSize = currentLength + growBy;

        if ((uint)newSize > int.MaxValue)
            newSize = int.MaxValue;

        var newHandle = _pool.Rent(newSize);

        try
        {
            _handle.AsSpan().Slice(0, _written).CopyTo(newHandle.AsSpan());
            var oldHandle = _handle;
            _handle = newHandle;
            oldHandle.Dispose();
        }
        catch
        {
            newHandle.Dispose();
            throw;
        }
    }
}
