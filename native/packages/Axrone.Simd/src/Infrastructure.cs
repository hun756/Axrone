namespace Axrone.Simd;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct BatchCapacity
{
    public const int MaximumCapacity = 0x3FFFFFFF;
    public int Value { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public BatchCapacity(int value)
    {
        if (value <= 0 || value > MaximumCapacity)
        {
            ThrowHelper.ThrowInvalidCapacity(value);
        }
        Value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(BatchCapacity capacity) => capacity.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator nuint(BatchCapacity capacity) => (nuint)capacity.Value;
}

[StructLayout(LayoutKind.Sequential, Pack = sizeof(nuint))]
public readonly record struct MemoryAlignment
{
    public nuint Value { get; }

    public static readonly MemoryAlignment CacheLine64 = new(64);
    public static readonly MemoryAlignment CacheLine128 = new(128);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public MemoryAlignment(nuint value)
    {
        if (value < (nuint)sizeof(nuint) || (value & (value - 1)) != 0)
        {
            ThrowHelper.ThrowInvalidAlignment(value);
        }
        Value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator nuint(MemoryAlignment alignment) => alignment.Value;
}

internal static class ThrowHelper
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidCapacity(int value)
    {
        throw new ArgumentOutOfRangeException(nameof(value), value, "Batch capacity out of valid hardware range.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidAlignment(nuint value)
    {
        throw new ArgumentException("Memory alignment must be a power of two and word-sized.", nameof(value));
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowDestinationTooSmall()
    {
        throw new ArgumentException("Destination span is insufficiently sized for SIMD vectorized output.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowMismatchedSpans()
    {
        throw new ArgumentException("Span operands must possess equal lengths for lock-step vectorization.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowEmptySequence()
    {
        throw new InvalidOperationException("Cannot compute horizontal SIMD reduction over an empty sequence.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowObjectDisposed()
    {
        throw new ObjectDisposedException("AlignedMemoryBlock", "Memory block is disposed or unmapped.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowConcurrentWaitNotSupported()
    {
        throw new InvalidOperationException("Concurrent async awaits on single-producer single-consumer signaler are not supported.");
    }
}
