namespace Axrone.Memory.Lifetime;

/// <summary>
/// Configuration options for a singleton engine. Instances are immutable after
/// construction; the builder produces a new options value for each modification.
/// </summary>
public sealed class SingletonOptions
{
    private TimeSpan _drainTimeout = TimeSpan.FromSeconds(30.0);
    private int _metricCapacity = 64;

    /// <summary>
    /// Gets or sets the maximum time a drain attempt waits for held leases to release
    /// before giving up. Must be a positive, finite <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan DrainTimeout
    {
        get => _drainTimeout;
        init => _drainTimeout = ValidateDrainTimeout(value);
    }

    /// <summary>
    /// Gets or sets the registration name used for diagnostics and metric correlation.
    /// </summary>
    public InstanceName InstanceName { get; init; } = InstanceName.Empty;

    /// <summary>
    /// Gets or sets the number of metric records the engine retains. Must be greater
    /// than zero.
    /// </summary>
    public int MetricCapacity
    {
        get => _metricCapacity;
        init => _metricCapacity = ValidateMetricCapacity(value);
    }

    /// <summary>Validates that the drain timeout is positive and finite.</summary>
    private static TimeSpan ValidateDrainTimeout(TimeSpan drainTimeout)
    {
        if (drainTimeout <= TimeSpan.Zero || drainTimeout == TimeSpan.MaxValue)
        {
            SingletonThrowHelper.ThrowInvalidDrainTimeout(drainTimeout);
        }

        return drainTimeout;
    }

    /// <summary>Validates that the metric capacity is greater than zero.</summary>
    private static int ValidateMetricCapacity(int metricCapacity)
    {
        if (metricCapacity <= 0)
        {
            SingletonThrowHelper.ThrowInvalidMetricCapacity(metricCapacity);
        }

        return metricCapacity;
    }
}

/// <summary>
/// Cache-line-padded container for the engine's hot lifecycle counters. The explicit
/// layout pins the counters to the start of a dedicated 64-byte region so that state
/// belonging to one engine does not share a cache line with state of another engine
/// accessed concurrently on a different core.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
internal struct PaddedState
{
    [FieldOffset(0)] public int Status;
    [FieldOffset(8)] public long Epoch;
    [FieldOffset(16)] public long LeaseCount;
    [FieldOffset(24)] public long Iteration;
    [FieldOffset(32)] public int Disposed;
}
