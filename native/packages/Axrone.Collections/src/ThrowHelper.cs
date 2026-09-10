namespace Axrone.Collections;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

internal static class ThrowHelper
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidCapacity(int value)
    {
        throw new ArgumentOutOfRangeException(nameof(value), value, $"Capacity must be between 1 and {BatchCapacity.MaximumCapacity}.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidAlignment(nuint value)
    {
        throw new ArgumentException($"Alignment value {value} must be a power of two and at least {IntPtr.Size}.", nameof(value));
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
}
