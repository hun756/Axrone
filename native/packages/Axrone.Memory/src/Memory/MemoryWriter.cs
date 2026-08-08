namespace Axrone.Memory;

/// <summary>
/// Sequential write cursor over a pooled <see cref="IMemoryOwner{T}"/> buffer.
/// Implements <see cref="IBufferWriter{T}"/> for zero-allocation buffered writing.
/// </summary>
/// <typeparam name="T">Element type of the underlying buffer.</typeparam>
public sealed class MemoryWriter<T> : IBufferWriter<T>, IDisposable
{
    private readonly IMemoryOwner<T> _owner;
    private readonly int _capacity;
    private int _position;

    /// <summary>Creates a writer over the specified pooled buffer.</summary>
    /// <param name="owner">The rented buffer to write into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public MemoryWriter(IMemoryOwner<T> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        _capacity = owner.Memory.Length;
        _position = 0;
    }

    /// <summary>Gets the number of elements written so far.</summary>
    /// <returns>The count of elements written via <see cref="Advance"/>.</returns>
    public int WrittenCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _position;
    }

    /// <summary>Gets the number of elements that can still be written.</summary>
    /// <returns>Remaining writable element count.</returns>
    public int RemainingCapacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _capacity - _position;
    }

    /// <summary>Gets the written portion of the buffer as a <see cref="Span{T}"/>.</summary>
    /// <returns>A span covering elements from index 0 to <see cref="WrittenCount"/>.</returns>
    public Span<T> WrittenSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _owner.Span.Slice(0, _position);
    }

    /// <summary>Gets the written portion of the buffer as a <see cref="Memory{T}"/>.</summary>
    /// <returns>A memory region covering elements from index 0 to <see cref="WrittenCount"/>.</returns>
    public Memory<T> WrittenMemory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _owner.Memory.Slice(0, _position);
    }

    /// <summary>Returns a <see cref="Span{T}"/> to write to, starting at the current position.</summary>
    /// <param name="sizeHint">Minimum number of elements required. 0 means any available space.</param>
    /// <exception cref="InvalidOperationException">The requested size exceeds remaining capacity.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan(int sizeHint = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        int available = _capacity - _position;
        int effectiveHint = sizeHint == 0 ? Math.Max(1, available) : sizeHint;

        if (effectiveHint > available)
            MemoryWriterThrowHelper.ThrowInsufficientCapacity(effectiveHint, available);

        return _owner.Span.Slice(_position);
    }

    /// <summary>Returns a <see cref="Memory{T}"/> to write to, starting at the current position.</summary>
    /// <param name="sizeHint">Minimum number of elements required. 0 means any available space.</param>
    /// <exception cref="InvalidOperationException">The requested size exceeds remaining capacity.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        int available = _capacity - _position;
        int effectiveHint = sizeHint == 0 ? Math.Max(1, available) : sizeHint;

        if (effectiveHint > available)
            MemoryWriterThrowHelper.ThrowInsufficientCapacity(effectiveHint, available);

        return _owner.Memory.Slice(_position);
    }

    /// <summary>Advances the write position by <paramref name="count"/> elements.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="count"/> exceeds remaining capacity.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        int newPosition = _position + count;
        if (newPosition > _capacity)
            MemoryWriterThrowHelper.ThrowAdvanceBeyondCapacity(count, _capacity - _position);

        _position = newPosition;
    }

    /// <summary>Disposes the writer and returns the underlying buffer to the pool.</summary>
    public void Dispose()
    {
        _owner?.Dispose();
    }
}

internal static class MemoryWriterThrowHelper
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInsufficientCapacity(int requested, int available) =>
        throw new InvalidOperationException(
            $"Requested capacity {requested} exceeds remaining buffer space {available}.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowAdvanceBeyondCapacity(int count, int remaining) =>
        throw new InvalidOperationException(
            $"Cannot advance by {count} elements; only {remaining} remaining.");
}
