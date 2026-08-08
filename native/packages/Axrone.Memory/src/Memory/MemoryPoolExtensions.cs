namespace Axrone.Memory;

/// <summary>
/// Extension methods for <see cref="IMemoryPool{T}"/> and <see cref="IMemoryOwner{T}"/>.
/// </summary>
public static class MemoryPoolExtensions
{
    /// <summary>Rents a buffer with exactly the specified length, throwing on failure.</summary>
    /// <param name="pool">The pool to rent from.</param>
    /// <param name="exactLength">Exact number of elements required.</param>
    /// <returns>An <see cref="IMemoryOwner{T}"/> with exactly the requested length.</returns>
    /// <exception cref="OutOfMemoryException">The pool could not satisfy the exact size request.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IMemoryOwner<T> RentExact<T>(this IMemoryPool<T> pool, int exactLength) where T : struct
    {
        ArgumentNullException.ThrowIfNull(pool);
        if (!pool.TryRentExact(exactLength, out var owner))
            throw new OutOfMemoryException($"Pool could not rent an exact buffer of size {exactLength}.");
        return owner;
    }

    /// <summary>Creates a <see cref="ByteMemoryWriter"/> on any <see cref="IMemoryPool{T}"/> of bytes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ByteMemoryWriter GetWriter(this IMemoryPool<byte> pool, int initialCapacity = 4096)
    {
        ArgumentNullException.ThrowIfNull(pool);
        return new ByteMemoryWriter(pool, initialCapacity);
    }

    /// <summary>Creates a <see cref="CharMemoryWriter"/> on any <see cref="IMemoryPool{T}"/> of chars.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CharMemoryWriter GetCharWriter(this IMemoryPool<char> pool, int initialCapacity = 1024)
    {
        ArgumentNullException.ThrowIfNull(pool);
        return new CharMemoryWriter(pool, initialCapacity);
    }

    /// <summary>Full reset: trims all idle buffers and resets statistics.</summary>
    public static void Reset<T>(this IMemoryPool<T> pool) where T : struct
    {
        ArgumentNullException.ThrowIfNull(pool);
        pool.Trim(1.0);
        pool.ResetStatistics();
    }

    /// <summary>Returns the underlying <see cref="Memory{T}"/> from an owner.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(this IMemoryOwner<T> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return owner.Memory;
    }

    /// <summary>Returns the underlying <see cref="Span{T}"/> from an owner.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this IMemoryOwner<T> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return owner.Span;
    }

    /// <summary>Returns the underlying <see cref="ReadOnlyMemory{T}"/> from an owner.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this IMemoryOwner<T> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return owner.ReadOnlyMemory;
    }

    /// <summary>Returns the underlying <see cref="ReadOnlySpan{T}"/> from an owner.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this IMemoryOwner<T> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return owner.ReadOnlySpan;
    }
}
