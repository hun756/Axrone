namespace Axrone.Batching;

/// <summary>
/// Adaptive-stride bounds for the time-sliced pipeline.
/// </summary>
/// <remarks>
/// Carries the calibrator's tuning as a value instead of scattering magic numbers through the
/// execution loop. The default matches the pipeline's measured tuning.
/// </remarks>
public readonly record struct BatchStride
{
    /// <summary>Minimum stride of the house tuning.</summary>
    public const int DefaultMin = 16;

    /// <summary>Maximum stride of the house tuning.</summary>
    public const int DefaultMax = 1024;

    /// <summary>Smallest chunk.</summary>
    public int Min { get; }

    /// <summary>Largest chunk.</summary>
    public int Max { get; }

    /// <summary>House tuning.</summary>
    public static BatchStride Default => new(DefaultMin, DefaultMax);

    /// <summary>
    /// Low-latency tuning: narrow ceiling so a slice never holds the frame hostage.
    /// </summary>
    /// <remarks>Ceiling 128 keeps the worst single chunk under ~125µs for kernels at or below the
    /// measured per-item costs; see the pipeline calibrator.</remarks>
    public static BatchStride LowLatency => new(DefaultMin, 128);

    /// <summary>
    /// Throughput tuning: raised floor so large batches amortize per-chunk clock reads.
    /// </summary>
    public static BatchStride Throughput => new(64, DefaultMax);

    /// <summary>Creates bounds.</summary>
    /// <param name="min">Smallest chunk; must be positive and at most <paramref name="max"/>.</param>
    /// <param name="max">Largest chunk.</param>
    /// <exception cref="ArgumentOutOfRangeException">Bounds are not positive or not ordered.</exception>
    public BatchStride(int min, int max)
    {
        if (min <= 0 || max < min)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(min));
        }

        Min = min;
        Max = max;
    }

    /// <summary>Clamps <paramref name="value"/> into range.</summary>
    /// <param name="value">Stride to clamp.</param>
    public int Clamp(int value) => value < Min ? Min : value > Max ? Max : value;
}
