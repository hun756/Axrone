namespace Axrone.Memory;

/// <summary>
/// Auto-resizing <see cref="IBufferWriter{T}"/> for <see cref="char"/> buffers backed by a memory pool.
/// Grows by doubling capacity when more space is needed.
/// </summary>
public sealed class CharMemoryWriter : IBufferWriter<char>, IDisposable
{
    private readonly IMemoryPool<char> _pool;
    private readonly int _minimumSegmentSize;
    private IMemoryOwner<char>? _owner;
    private int _written;

    /// <summary>Creates a writer backed by the specified pool.</summary>
    /// <param name="pool">The memory pool to rent buffers from.</param>
    /// <param name="initialCapacity">Initial buffer capacity in chars.</param>
    public CharMemoryWriter(IMemoryPool<char> pool, int initialCapacity = 1024)
    {
        ArgumentNullException.ThrowIfNull(pool);
        _pool = pool;
        _minimumSegmentSize = Math.Max(initialCapacity, 512);
        _owner = pool.Rent(_minimumSegmentSize);
        _written = 0;
    }

    /// <summary>Gets the number of chars written so far.</summary>
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
    public ReadOnlyMemory<char> WrittenMemory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _owner?.Memory.Slice(0, _written) ?? ReadOnlyMemory<char>.Empty;
    }

    /// <summary>Gets the written portion as <see cref="ReadOnlySpan{T}"/>.</summary>
    public ReadOnlySpan<char> WrittenSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _owner is not null ? _owner.Span.Slice(0, _written) : ReadOnlySpan<char>.Empty;
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
    public Memory<char> GetMemory(int sizeHint = 0)
    {
        CheckAndResizeBuffer(sizeHint);
        return _owner!.Memory.Slice(_written);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<char> GetSpan(int sizeHint = 0)
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

    /// <summary>Writes a single char.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(char value)
    {
        CheckAndResizeBuffer(1);
        _owner!.Span[_written] = value;
        _written++;
    }

    /// <summary>Writes a span of chars.</summary>
    public void Write(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty) return;
        CheckAndResizeBuffer(value.Length);
        value.CopyTo(_owner!.Span.Slice(_written));
        _written += value.Length;
    }

    /// <summary>Writes a string (null/empty-safe).</summary>
    public void Write(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        Write(value.AsSpan());
    }

    /// <summary>Returns the written content as a new string.</summary>
    public override string ToString() => _written == 0 ? string.Empty : new string(WrittenSpan);

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
