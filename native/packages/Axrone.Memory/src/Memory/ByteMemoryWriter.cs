namespace Axrone.Memory;

/// <summary>
/// Auto-resizing <see cref="IBufferWriter{T}"/> for <see cref="byte"/> buffers backed by a memory pool.
/// Grows by doubling capacity when more space is needed.
/// </summary>
public sealed class ByteMemoryWriter : IBufferWriter<byte>, IDisposable
{
    private readonly IMemoryPool<byte> _pool;
    private readonly int _minimumSegmentSize;
    private IMemoryOwner<byte>? _owner;
    private int _written;

    /// <summary>Creates a writer backed by the specified pool.</summary>
    /// <param name="pool">The memory pool to rent buffers from.</param>
    /// <param name="initialCapacity">Initial buffer capacity in bytes.</param>
    public ByteMemoryWriter(IMemoryPool<byte> pool, int initialCapacity = 4096)
    {
        ArgumentNullException.ThrowIfNull(pool);
        _pool = pool;
        _minimumSegmentSize = Math.Max(initialCapacity, 1024);
        _owner = pool.Rent(_minimumSegmentSize);
        _written = 0;
    }

    /// <summary>Gets the number of bytes written so far.</summary>
    public int Written
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _written;
    }

    /// <summary>Gets the current underlying buffer capacity.</summary>
    public int Capacity => _owner?.Memory.Length ?? 0;

    /// <summary>Gets the remaining writable capacity.</summary>
    public int FreeCapacity => Capacity - _written;

    /// <summary>Gets the written portion as <see cref="ReadOnlyMemory{T}"/>.</summary>
    public ReadOnlyMemory<byte> WrittenMemory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _owner?.Memory.Slice(0, _written) ?? ReadOnlyMemory<byte>.Empty;
    }

    /// <summary>Gets the written portion as <see cref="ReadOnlySpan{T}"/>.</summary>
    public ReadOnlySpan<byte> WrittenSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _owner is not null ? _owner.Span.Slice(0, _written) : ReadOnlySpan<byte>.Empty;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (_written + count > Capacity)
            MemoryWriterThrowHelper.ThrowAdvanceBeyondCapacity(count, Capacity - _written);
        _written += count;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        CheckAndResizeBuffer(sizeHint);
        return _owner!.Memory.Slice(_written);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        CheckAndResizeBuffer(sizeHint);
        return _owner!.Span.Slice(_written);
    }

    /// <summary>Zeroes the written region and resets the write cursor.</summary>
    public void Clear()
    {
        if (_owner is not null)
            _owner.Span.Slice(0, _written).Clear();
        _written = 0;
    }

    /// <summary>Copies the written bytes to the destination span.</summary>
    public void CopyTo(Span<byte> destination)
    {
        if (destination.Length < _written)
            throw new ArgumentException("Destination is too small.", nameof(destination));
        WrittenSpan.CopyTo(destination);
    }

    /// <summary>Returns a new array containing the written bytes.</summary>
    public byte[] ToArray() => _written == 0 ? [] : WrittenSpan.ToArray();

    /// <summary>Writes a single byte.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(byte value)
    {
        CheckAndResizeBuffer(1);
        _owner!.Span[_written] = value;
        _written++;
    }

    /// <summary>Writes a span of bytes.</summary>
    public void Write(ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty) return;
        CheckAndResizeBuffer(source.Length);
        source.CopyTo(_owner!.Span.Slice(_written));
        _written += source.Length;
    }

    /// <summary>Writes a 16-bit integer in little-endian format.</summary>
    public void WriteInt16(short value)
    {
        CheckAndResizeBuffer(2);
        BitConverter.TryWriteBytes(_owner!.Span.Slice(_written), value);
        _written += 2;
    }

    /// <summary>Writes a 32-bit integer in little-endian format.</summary>
    public void WriteInt32(int value)
    {
        CheckAndResizeBuffer(4);
        BitConverter.TryWriteBytes(_owner!.Span.Slice(_written), value);
        _written += 4;
    }

    /// <summary>Writes a 64-bit integer in little-endian format.</summary>
    public void WriteInt64(long value)
    {
        CheckAndResizeBuffer(8);
        BitConverter.TryWriteBytes(_owner!.Span.Slice(_written), value);
        _written += 8;
    }

    /// <summary>Disposes the writer and returns the buffer to the pool.</summary>
    public void Dispose()
    {
        _owner?.Dispose();
        _owner = null;
        _written = 0;
    }

    private void CheckAndResizeBuffer(int sizeHint)
    {
        int effectiveHint = sizeHint <= 0 ? 1 : sizeHint;
        if (effectiveHint <= FreeCapacity)
            return;

        int currentLength = Capacity;
        int growBy = Math.Max(effectiveHint, currentLength);
        if (_minimumSegmentSize > growBy)
            growBy = _minimumSegmentSize;

        int newSize = currentLength + growBy;
        if (newSize < 0) newSize = int.MaxValue;

        var newOwner = _pool.Rent(newSize);
        _owner!.Span.Slice(0, _written).CopyTo(newOwner.Span);
        _owner.Dispose();
        _owner = newOwner;
    }
}
