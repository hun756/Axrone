namespace Axrone.Memory.Lifetime;

/// <summary>
/// Centralizes cold-path throw sites for the singleton engine so that the hot paths
/// stay free of exception-construction code and the original stack traces of factory
/// failures can be preserved through <see cref="ExceptionDispatchInfo"/>.
/// </summary>
internal static class SingletonThrowHelper
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowNotInitialized<T>()
    {
        throw new InvalidOperationException(
            "The singleton has not been initialized. Accessing Instance before the factory has produced a value is not allowed.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowFaulted(ExceptionDispatchInfo fault)
    {
        fault.Throw();
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowDisposed<T>()
    {
        throw new ObjectDisposedException(
            "Singleton",
            "The singleton engine has been disposed and can no longer serve instances or leases.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowLeaseNotValid<T>()
    {
        throw new InvalidOperationException(
            "The singleton lease is not valid. It may have already been released, or the engine was disposed.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowLeaseAcquisitionFailed<T>(LeaseAcquisitionStatus status)
    {
        throw new InvalidOperationException(
            $"Acquiring a lease on the singleton failed with status '{status}'.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowBufferTooSmall(int required, int actual)
    {
        throw new ArgumentException(
            $"The destination buffer is too small to hold the metric sequence; required {required} entries but only {actual} were provided.",
            nameof(required));
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidDrainTimeout(TimeSpan drainTimeout)
    {
        throw new ArgumentOutOfRangeException(
            nameof(drainTimeout),
            drainTimeout,
            "The drain timeout must be a positive, finite TimeSpan.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidMetricCapacity(int metricCapacity)
    {
        throw new ArgumentOutOfRangeException(
            nameof(metricCapacity),
            metricCapacity,
            "The metric capacity must be greater than zero.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowInvalidFactoryResult<T>()
    {
        throw new InvalidOperationException(
            "The singleton factory returned null. Factories must produce a non-null instance.");
    }
}
