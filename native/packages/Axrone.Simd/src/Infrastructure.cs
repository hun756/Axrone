namespace Axrone.Simd;

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
