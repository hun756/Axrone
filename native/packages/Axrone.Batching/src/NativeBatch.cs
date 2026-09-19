namespace Axrone.Batching;

/// <summary>
/// A contiguous range of unmanaged elements addressed by pointer and length.
/// </summary>
/// <typeparam name="T">Element type. Unmanaged so a batch can address native memory directly.</typeparam>
/// <remarks>
/// <para>
/// <see cref="Span{T}"/> cannot be stored in a heap object, but a pipeline that picks up a batch in
/// one frame and keeps working through it in the next needs a field that outlives the span it came
/// from. This is that: a span-shaped view with a lifetime the caller controls.
/// </para>
/// <para>
/// It borrows, never owns. Nothing here allocates or frees, and whoever produced the batch must
/// keep it alive for as long as any copy is in use — handing a batch to a kernel is safe, holding
/// one across a buffer rotation is not.
/// </para>
/// </remarks>
public readonly unsafe partial struct NativeBatch<T>
    where T : unmanaged
{
    private readonly T* _data;
    private readonly int _length;

    /// <summary>Wraps <paramref name="length"/> elements starting at <paramref name="data"/>.</summary>
    /// <param name="data">First element. May be <see langword="null"/> only when <paramref name="length"/> is zero.</param>
    /// <param name="length">Element count.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/> for a non-empty batch.</exception>
    public NativeBatch(T* data, int length)
    {
        if (length < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(length));
        }

        if (data is null && length > 0)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(data));
        }

        _data = data;
        _length = length;
    }

    /// <summary>Number of elements in the batch.</summary>
    public readonly int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _length;
    }

    /// <summary>Whether the batch holds no elements.</summary>
    public readonly bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _length == 0;
    }

    /// <summary>A mutable view over the batch's elements.</summary>
    public readonly Span<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_data, _length);
    }

    /// <summary>A read-only view over the batch's elements.</summary>
    public readonly ReadOnlySpan<T> ReadOnlySpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_data, _length);
    }

    /// <summary>References the element at <paramref name="index"/>.</summary>
    /// <param name="index">Zero-based element position.</param>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the batch.</exception>
    /// <remarks>
    /// Throws what <see cref="Span{T}"/> throws rather than what <see cref="System.Collections.Generic.List{T}"/>
    /// throws: this is a span-shaped view, and matching the type it delegates to is the less
    /// surprising contract.
    /// </remarks>
    public readonly ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Span[index];
    }

    /// <summary>
    /// Returns the sub-range starting at <paramref name="start"/> and spanning <paramref name="length"/> elements.
    /// </summary>
    /// <param name="start">Offset of the first element in the new batch.</param>
    /// <param name="length">Element count of the new batch.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range does not fit inside this batch.</exception>
    /// <remarks>
    /// Bounds are checked in unsigned arithmetic, so <c>start + length</c> cannot overflow its way
    /// past the end of the batch.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly NativeBatch<T> Slice(int start, int length)
    {
        if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(start));
        }

        return new NativeBatch<T>(_data + start, length);
    }

    /// <summary>
    /// Copies the batch into <paramref name="destination"/>, which must be at least as long.
    /// </summary>
    /// <param name="destination">Target range.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than this batch.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(NativeBatch<T> destination) => Span.CopyTo(destination.Span);

    /// <summary>
    /// Copies the batch into <paramref name="destination"/> when it fits, without throwing if it does not.
    /// </summary>
    /// <param name="destination">Target range.</param>
    /// <returns><see langword="true"/> when every element was copied.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryCopyTo(NativeBatch<T> destination) => Span.TryCopyTo(destination.Span);

    /// <summary>Zeroes every element of the batch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Clear() => Span.Clear();

    /// <summary>Overwrites every element with <paramref name="value"/>.</summary>
    /// <param name="value">Value to write.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Fill(T value) => Span.Fill(value);

    /// <summary>
    /// Address of the first element. Internal on purpose — callers outside this assembly go
    /// through <see cref="Span"/>, which carries the bounds with the pointer.
    /// </summary>
    internal readonly T* DataPointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _data;
    }

    /// <summary>
    /// Whether this batch's byte range intersects <c>[other, other + otherLength)</c>.
    /// </summary>
    /// <param name="other">First element of the other range.</param>
    /// <param name="otherLength">Element count of the other range.</param>
    /// <remarks>
    /// Ranges are compared in bytes using pointer-sized arithmetic, so a long batch cannot wrap its
    /// way past the comparison and look disjoint when it is not.
    /// </remarks>
    internal readonly bool Overlaps(T* other, int otherLength)
    {
        if (_length == 0 || otherLength == 0)
        {
            return false;
        }

        var selfStart = (nuint)_data;
        var selfEnd = selfStart + ((nuint)(uint)_length * (nuint)sizeof(T));
        var otherStart = (nuint)other;
        var otherEnd = otherStart + ((nuint)(uint)otherLength * (nuint)sizeof(T));

        return otherStart < selfEnd && selfStart < otherEnd;
    }
}
