namespace Axrone.Utility.Internal;

public static class ThrowHelper
{
    private const int MaxBatchCapacity = 0x3FFFFFFF;
    private const uint MaxBufferCapacity = 1u << 30;

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentNullException(string paramName)
    {
        throw new ArgumentNullException(paramName);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowArgumentNullException<T>(string paramName)
    {
        throw new ArgumentNullException(paramName);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentOutOfRangeException(string paramName)
    {
        throw new ArgumentOutOfRangeException(paramName);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentOutOfRangeException(string paramName, string message)
    {
        throw new ArgumentOutOfRangeException(paramName, message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowObjectDisposedException(string objectName)
    {
        throw new ObjectDisposedException(objectName);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidOperationException(string message)
    {
        throw new InvalidOperationException(message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNotSupportedException(string message)
    {
        throw new NotSupportedException(message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidCapacity(int value)
    {
        throw new ArgumentOutOfRangeException(nameof(value), value, $"Capacity {value} must be between 1 and {MaxBatchCapacity}.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidBufferCapacity(uint capacity)
    {
        throw new ArgumentOutOfRangeException(nameof(capacity), capacity, $"Buffer capacity {capacity} must be a power of 2 between 2 and {MaxBufferCapacity}.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowObjectDisposed()
    {
        throw new ObjectDisposedException("The object has been disposed.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowConcurrentWaitNotSupported()
    {
        throw new InvalidOperationException("Concurrent wait operations are not supported. Only a single waiter is allowed at a time.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateBinarySpans<T1, T2>(ReadOnlySpan<T1> left, ReadOnlySpan<T1> right, ReadOnlySpan<T2> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowMismatchedSpans();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateTernarySpans<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, ReadOnlySpan<T> c, ReadOnlySpan<T> destination)
    {
        if (a.Length != b.Length || a.Length != c.Length || destination.Length < a.Length) ThrowMismatchedSpans();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateDestinationSpan<T1, T2>(ReadOnlySpan<T1> source, ReadOnlySpan<T2> destination)
    {
        if (destination.Length < source.Length) ThrowDestinationTooSmall();
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowDestinationTooSmall()
    {
        throw new ArgumentException("Destination span is insufficiently sized for vectorized output.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowMismatchedSpans()
    {
        throw new ArgumentException("Span operands must possess equal lengths for lock-step vectorized operations.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowEmptySequence()
    {
        throw new InvalidOperationException("Cannot compute a reduction over an empty sequence.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInsufficientMemory(string message)
    {
        throw new InsufficientMemoryException(message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidAlignment(uint value)
    {
        throw new ArgumentOutOfRangeException(nameof(value), value, "Alignment must be a non-zero power of two.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowTypeTooLargeForCacheLine(int typeSize, int maxAllowed)
    {
        throw new InvalidOperationException($"Type size of {typeSize} bytes exceeds the maximum allowable cacheline constraint of {maxAllowed} bytes.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowOutOfMemory(nuint bytes, nuint alignment)
    {
        throw new InsufficientMemoryException($"Failed to allocate {bytes} bytes with alignment boundary {alignment}.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNegativeCount()
    {
        throw new ArgumentOutOfRangeException("count", "Count cannot be negative.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowDestinationTooShort()
    {
        throw new ArgumentException("Destination span is shorter than the source span.", "destinations");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowObjectDisposed(string objectName)
    {
        throw new ObjectDisposedException(objectName, "The aligned memory resource has already been disposed.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowIndexOutOfRange()
    {
        throw new IndexOutOfRangeException("Gather/Scatter index is outside the bounds of the source or destination span.");
    }
}
