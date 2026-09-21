namespace Axrone.Batching;

/// <summary>
/// Two parallel streams of equal length, addressed as separate ranges.
/// </summary>
/// <remarks>
/// Splitting fields into their own streams lets a kernel touch one of them without dragging the
/// other across cache lines. Like <see cref="NativeBatch{T}"/> this borrows both ranges and keeps
/// neither alive.
/// </remarks>
public readonly unsafe struct NativeSoABatch2<T1, T2>
    where T1 : unmanaged
    where T2 : unmanaged
{
    private readonly T1* _stream1;
    private readonly T2* _stream2;
    private readonly int _length;

    /// <summary>Wraps <paramref name="length"/> elements in each stream.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    /// <exception cref="ArgumentNullException">A stream is null while <paramref name="length"/> is non-zero.</exception>
    public NativeSoABatch2(T1* stream1, T2* stream2, int length)
    {
        if (length < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(length));
        }

        if (length > 0)
        {
            if (stream1 is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(stream1));
            }

            if (stream2 is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(stream2));
            }
        }

        _stream1 = stream1;
        _stream2 = stream2;
        _length = length;
    }

    /// <summary>Element count, shared by both streams.</summary>
    public readonly int Length => _length;

    /// <summary>Whether either stream holds no elements.</summary>
    public readonly bool IsEmpty => _length == 0;

    /// <summary>Mutable view of the first stream.</summary>
    public readonly Span<T1> Span1 => new(_stream1, _length);

    /// <summary>Mutable view of the second stream.</summary>
    public readonly Span<T2> Span2 => new(_stream2, _length);

    /// <summary>Read-only view of the first stream.</summary>
    public readonly ReadOnlySpan<T1> ReadOnlySpan1 => new(_stream1, _length);

    /// <summary>Read-only view of the second stream.</summary>
    public readonly ReadOnlySpan<T2> ReadOnlySpan2 => new(_stream2, _length);

    /// <summary>
    /// Returns the same sub-range of every stream.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The range does not fit inside the batch.</exception>
    public readonly NativeSoABatch2<T1, T2> Slice(int start, int length)
    {
        if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(start));
        }

        return new NativeSoABatch2<T1, T2>(_stream1 + start, _stream2 + start, length);
    }
}

/// <summary>
/// Three parallel streams of equal length, addressed as separate ranges.
/// </summary>
public readonly unsafe struct NativeSoABatch3<T1, T2, T3>
    where T1 : unmanaged
    where T2 : unmanaged
    where T3 : unmanaged
{
    private readonly T1* _stream1;
    private readonly T2* _stream2;
    private readonly T3* _stream3;
    private readonly int _length;

    /// <summary>Wraps <paramref name="length"/> elements in each stream.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    /// <exception cref="ArgumentNullException">A stream is null while <paramref name="length"/> is non-zero.</exception>
    public NativeSoABatch3(T1* stream1, T2* stream2, T3* stream3, int length)
    {
        if (length < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(length));
        }

        if (length > 0)
        {
            if (stream1 is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(stream1));
            }

            if (stream2 is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(stream2));
            }

            if (stream3 is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(stream3));
            }
        }

        _stream1 = stream1;
        _stream2 = stream2;
        _stream3 = stream3;
        _length = length;
    }

    /// <summary>Element count, shared by all streams.</summary>
    public readonly int Length => _length;

    /// <summary>Whether any stream holds no elements.</summary>
    public readonly bool IsEmpty => _length == 0;

    /// <summary>Mutable view of the first stream.</summary>
    public readonly Span<T1> Span1 => new(_stream1, _length);

    /// <summary>Mutable view of the second stream.</summary>
    public readonly Span<T2> Span2 => new(_stream2, _length);

    /// <summary>Mutable view of the third stream.</summary>
    public readonly Span<T3> Span3 => new(_stream3, _length);

    /// <summary>Read-only view of the first stream.</summary>
    public readonly ReadOnlySpan<T1> ReadOnlySpan1 => new(_stream1, _length);

    /// <summary>Read-only view of the second stream.</summary>
    public readonly ReadOnlySpan<T2> ReadOnlySpan2 => new(_stream2, _length);

    /// <summary>Read-only view of the third stream.</summary>
    public readonly ReadOnlySpan<T3> ReadOnlySpan3 => new(_stream3, _length);

    /// <summary>
    /// Returns the same sub-range of every stream.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The range does not fit inside the batch.</exception>
    public readonly NativeSoABatch3<T1, T2, T3> Slice(int start, int length)
    {
        if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(start));
        }

        return new NativeSoABatch3<T1, T2, T3>(_stream1 + start, _stream2 + start, _stream3 + start, length);
    }
}

/// <summary>
/// Four parallel streams of equal length, addressed as separate ranges.
/// </summary>
public readonly unsafe struct NativeSoABatch4<T1, T2, T3, T4>
    where T1 : unmanaged
    where T2 : unmanaged
    where T3 : unmanaged
    where T4 : unmanaged
{
    private readonly T1* _stream1;
    private readonly T2* _stream2;
    private readonly T3* _stream3;
    private readonly T4* _stream4;
    private readonly int _length;

    /// <summary>Wraps <paramref name="length"/> elements in each stream.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    /// <exception cref="ArgumentNullException">A stream is null while <paramref name="length"/> is non-zero.</exception>
    public NativeSoABatch4(T1* stream1, T2* stream2, T3* stream3, T4* stream4, int length)
    {
        if (length < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(length));
        }

        if (length > 0)
        {
            if (stream1 is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(stream1));
            }

            if (stream2 is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(stream2));
            }

            if (stream3 is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(stream3));
            }

            if (stream4 is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(stream4));
            }
        }

        _stream1 = stream1;
        _stream2 = stream2;
        _stream3 = stream3;
        _stream4 = stream4;
        _length = length;
    }

    /// <summary>Element count, shared by all streams.</summary>
    public readonly int Length => _length;

    /// <summary>Whether any stream holds no elements.</summary>
    public readonly bool IsEmpty => _length == 0;

    /// <summary>Mutable view of the first stream.</summary>
    public readonly Span<T1> Span1 => new(_stream1, _length);

    /// <summary>Mutable view of the second stream.</summary>
    public readonly Span<T2> Span2 => new(_stream2, _length);

    /// <summary>Mutable view of the third stream.</summary>
    public readonly Span<T3> Span3 => new(_stream3, _length);

    /// <summary>Mutable view of the fourth stream.</summary>
    public readonly Span<T4> Span4 => new(_stream4, _length);

    /// <summary>Read-only view of the first stream.</summary>
    public readonly ReadOnlySpan<T1> ReadOnlySpan1 => new(_stream1, _length);

    /// <summary>Read-only view of the second stream.</summary>
    public readonly ReadOnlySpan<T2> ReadOnlySpan2 => new(_stream2, _length);

    /// <summary>Read-only view of the third stream.</summary>
    public readonly ReadOnlySpan<T3> ReadOnlySpan3 => new(_stream3, _length);

    /// <summary>Read-only view of the fourth stream.</summary>
    public readonly ReadOnlySpan<T4> ReadOnlySpan4 => new(_stream4, _length);

    /// <summary>
    /// Returns the same sub-range of every stream.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The range does not fit inside the batch.</exception>
    public readonly NativeSoABatch4<T1, T2, T3, T4> Slice(int start, int length)
    {
        if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(start));
        }

        return new NativeSoABatch4<T1, T2, T3, T4>(
            _stream1 + start, _stream2 + start, _stream3 + start, _stream4 + start, length);
    }
}
