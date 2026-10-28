namespace Axrone.Simd;

/// <summary>
/// Base class for structure-of-arrays float buffers. Backing storage lives on the pinned
/// object heap so large persistent buffers never move or enter the compaction path; every
/// access surface is a <see cref="Span{T}"/> and steady-state operations allocate nothing.
/// </summary>
public abstract class SoaBuffer : IDisposable
{
    private float[] _data;
    private int _count;

    /// <summary>Creates a buffer.</summary>
    /// <param name="capacity">Element capacity; the buffer grows on demand.</param>
    /// <param name="stride">Floats per element.</param>
    protected SoaBuffer(int capacity, int stride)
    {
        if (capacity < 0)
        {
            ThrowCapacity();
        }

        if (stride <= 0)
        {
            ThrowStride();
        }

        Stride = stride;
        Capacity = capacity;
        _data = capacity == 0
            ? []
            : GC.AllocateUninitializedArray<float>(checked(capacity * stride), pinned: true);
    }

    /// <summary>Gets the number of floats per element.</summary>
    public int Stride { get; }

    /// <summary>Gets the live element count.</summary>
    public int Count => _count;

    /// <summary>Gets the allocated element capacity.</summary>
    public int Capacity { get; private set; }

    /// <summary>Gets the live element range as floats.</summary>
    public Span<float> AsSpan() => _data.AsSpan(0, _count * Stride);

    /// <summary>Gets the entire allocated range as floats.</summary>
    public Span<float> FullSpan => _data.AsSpan();

    /// <summary>Sets the live element count, growing storage when needed.</summary>
    /// <param name="count">New element count.</param>
    public void SetCount(int count)
    {
        if (count < 0)
        {
            ThrowCount();
        }

        EnsureCapacity(count);
        _count = count;
    }

    /// <summary>Ensures room for at least <paramref name="minimum"/> elements.</summary>
    /// <param name="minimum">Required element capacity.</param>
    public void EnsureCapacity(int minimum)
    {
        if (minimum <= Capacity)
        {
            return;
        }

        int target = Math.Max(minimum, Math.Max(4, Capacity * 2));
        float[] next = GC.AllocateUninitializedArray<float>(checked(target * Stride), pinned: true);
        _data.AsSpan(0, _count * Stride).CopyTo(next);
        _data = next;
        Capacity = target;
    }

    /// <summary>Clears the live element range.</summary>
    public void Clear() => _data.AsSpan(0, _count * Stride).Clear();

    /// <summary>Copies one element into another buffer.</summary>
    /// <param name="sourceIndex">Source element.</param>
    /// <param name="destination">Destination buffer.</param>
    /// <param name="destinationIndex">Destination element.</param>
    public void CopyElement(int sourceIndex, SoaBuffer destination, int destinationIndex)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ValidateIndex(sourceIndex);
        destination.ValidateIndex(destinationIndex);

        if (destination.Stride != Stride)
        {
            ThrowStrideMismatch();
        }

        _data.AsSpan(sourceIndex * Stride, Stride).CopyTo(destination._data.AsSpan(destinationIndex * Stride, Stride));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the pinned backing array.</summary>
    /// <param name="disposing">True for deterministic disposal.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _data = [];
        _count = 0;
        Capacity = 0;
    }

    /// <summary>Validates an element index.</summary>
    /// <param name="index">Element index.</param>
    internal void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)_count)
        {
            ThrowIndex();
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCapacity() => throw new ArgumentOutOfRangeException("capacity");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowStride() => throw new ArgumentOutOfRangeException("stride");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCount() => throw new ArgumentOutOfRangeException("count");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIndex() => throw new ArgumentOutOfRangeException("index");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowStrideMismatch() =>
        throw new InvalidOperationException("SOA buffers must share one element stride.");
}
