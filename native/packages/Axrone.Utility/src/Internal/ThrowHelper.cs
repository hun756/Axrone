namespace Axrone.Utility.Internal;

using Axrone.Utility.Builders;

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
    public static void ThrowBufferFaultedException(ExceptionDispatchInfo edi) =>
        edi.Throw();

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowFaulted(Exception? inner) =>
        throw new InvalidOperationException("Buffer pipeline is in a faulted terminal state.", inner);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TReturn ThrowArgumentZero<TReturn>() =>
        throw new ArgumentOutOfRangeException("length", "Length parameter must be non-zero.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowSpanLengthExceeded(nuint length) =>
        throw new InvalidOperationException($"Element count {length} exceeds maximum 32-bit Span threshold {int.MaxValue}.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowIndexOutOfRange(nuint index, nuint length) =>
        throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of bounds for buffer length {length}.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowRangeInvalid(nuint offset, nuint count, nuint capacity) =>
        throw new ArgumentOutOfRangeException(nameof(count), $"Range [{offset}..{offset + count}) exceeds total capacity {capacity}.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowByteRangeInvalid(nuint offset, nuint count, nuint capacity) =>
        throw new ArgumentOutOfRangeException(nameof(count), $"Byte range [{offset}..{offset + count}) exceeds total byte capacity {capacity}.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowAcquisitionFailed(long controlWord) =>
        throw new InvalidOperationException($"Lease acquisition failed under current state control word: 0x{controlWord:X16}.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowCounterOverflow() =>
        throw new OverflowException("Concurrent lease registration overflowed 32-bit ceiling.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowCounterUnderflow() =>
        throw new InvalidOperationException("Unmatched lease release detected: active reference count is zero.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TReturn ThrowInvalidAlignment<TReturn>(nuint alignment) =>
        throw new ArgumentException($"Alignment {alignment} must be a valid power of two multiple of pointer word.", nameof(alignment));

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TReturn ThrowInvalidState<TReturn>(long state) =>
        throw new InvalidOperationException($"Invalid state machine transition or control word: 0x{state:X16}.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TReturn ThrowDrainingLeaseAccess<TReturn>() =>
        throw new InvalidOperationException("Cannot acquire lease: buffer is currently draining.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TReturn ThrowDrainedLeaseAccess<TReturn>() =>
        throw new InvalidOperationException("Cannot acquire lease: buffer has already completed draining.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TReturn ThrowDisposedLeaseAccess<TReturn>() =>
        throw new ObjectDisposedException("NativeBuffer", "Cannot access lease: buffer is already disposed.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TReturn ThrowFaultedLeaseAccess<TReturn>() =>
        throw new InvalidOperationException("Cannot acquire lease: buffer is in a faulted terminal state.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentException(string message)
    {
        throw new ArgumentException(message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentException(string paramName, string message)
    {
        throw new ArgumentException(message, paramName);
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
    public static void ThrowCountOutOfRange()
    {
        throw new ArgumentOutOfRangeException("count", "Count cannot exceed Int32.MaxValue.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowBufferTooSmall()
    {
        throw new InvalidOperationException("Buffer length is smaller than target type size.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNotActive(int state, ExceptionDispatchInfo? fault)
    {
        fault?.Throw();

        throw state switch
        {
            1 => new InvalidOperationException("Arena is currently reclaiming memory."),
            2 => new InvalidOperationException("Arena is in draining state."),
            3 => new InvalidOperationException("Arena is in faulted state."),
            4 => new ObjectDisposedException("Arena", "Arena has been disposed."),
            _ => new InvalidOperationException($"Invalid arena state: {state}")
        };
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
    public static void ThrowUnalignedPointer(nuint address, nuint alignment)
    {
        throw new InvalidOperationException($"Pointer 0x{address:X} violates the required alignment of {alignment} bytes.");
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
        throw new ArgumentOutOfRangeException("Gather/Scatter index is outside the bounds of the source or destination span.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidCapacity(nuint capacity)
    {
        throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be an integral power of two >= 2.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowDoubleCommit()
    {
        throw new InvalidOperationException("Batch memory reservation has already been committed.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowLifecycleTerminated()
    {
        throw new InvalidOperationException("Operation failed: Memory arena is completing, drained, or faulted.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidMarker()
    {
        throw new InvalidOperationException("Specified marker is invalid or out of sequence.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNoReservation()
    {
        throw new InvalidOperationException("No reservation exists to advance.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidAdvance()
    {
        throw new ArgumentOutOfRangeException("count", "Advance count exceeds reservation size.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowBuilderFailed(in BuilderDiagnostic diagnostic)
    {
        throw new InvalidOperationException($"Build failed [{diagnostic.Code}]: {diagnostic.Message}");
    }
}
