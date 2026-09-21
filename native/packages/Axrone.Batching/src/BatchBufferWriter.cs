namespace Axrone.Batching;

/// <summary>
/// <see cref="IBufferWriter{T}"/> staging writes into a pipeline producer slot.
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
/// <remarks>
/// <para>
/// Binary protocols speak <see cref="IBufferWriter{T}"/>; the pipeline speaks
/// <c>TryWrite/WriteRange</c>. This stages spans locally and moves them across on
/// <see cref="Flush"/>, so a half-filled producer slot never forces a partial protocol frame.
/// </para>
/// <para>
/// With pinned staging the array lives on the Pinned Object Heap, keeping it out of the GC's
/// object-graph scans and safe to address from native code. The array stays fully managed —
/// no handle to free, nothing to dispose.
/// </para>
/// </remarks>
public struct BatchBufferWriter<T> where T : unmanaged
{
    private readonly TimeSlicedPipeline<T> _pipeline;
    private T[] _staging;
    private int _written;

    /// <summary>Creates a writer.</summary>
    /// <param name="pipeline">Target pipeline.</param>
    /// <param name="initialCapacity">Staging capacity; must be positive.</param>
    /// <param name="pinnedStaging">Back staging with the Pinned Object Heap.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCapacity"/> is not positive.</exception>
    public BatchBufferWriter(TimeSlicedPipeline<T> pipeline, int initialCapacity = 256, bool pinnedStaging = false)
    {
        if (initialCapacity <= 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(initialCapacity));
        }

        _pipeline = pipeline;
        _staging = pinnedStaging
            ? GC.AllocateArray<T>(initialCapacity, pinned: true)
            : new T[initialCapacity];
        _written = 0;
    }

    /// <summary>Staged but unflushed elements.</summary>
    public readonly int WrittenCount => _written;

    /// <summary>Staged contents.</summary>
    public readonly ReadOnlySpan<T> WrittenSpan => _staging.AsSpan(0, _written);

    /// <summary>Returns a writable staging span.</summary>
    /// <param name="sizeHint">Minimum writable elements.</param>
    public Span<T> GetSpan(int sizeHint = 0)
    {
        var need = sizeHint <= 0 ? 1 : sizeHint;
        if (_staging.Length - _written < need)
        {
            Array.Resize(ref _staging, Math.Max(_staging.Length * 2, _written + need));
        }

        return _staging.AsSpan(_written);
    }

    /// <summary>Commits staged elements.</summary>
    /// <param name="count">Elements staged since the last call.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is out of range.</exception>
    public void Advance(int count)
    {
        if (count < 0 || _written + count > _staging.Length)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
        }

        _written += count;
    }

    /// <summary>Moves staged items into the producer slot.</summary>
    /// <returns>Elements accepted. When the slot fills mid-flush the remainder stays staged.</returns>
    public int Flush()
    {
        var total = 0;
        while (total < _written)
        {
            var accepted = _pipeline.WriteRange(_staging.AsSpan(total, _written - total));
            if (accepted == 0)
            {
                break;
            }

            total += accepted;
        }

        if (total > 0 && total < _written)
        {
            Array.Copy(_staging, total, _staging, 0, _written - total);
        }

        _written -= total;
        return total;
    }

    /// <summary>Discards staged items without flushing.</summary>
    public void Reset() => _written = 0;
}
