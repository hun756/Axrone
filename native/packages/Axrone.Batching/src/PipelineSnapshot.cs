namespace Axrone.Batching;

/// <summary>
/// Point-in-time pipeline inspection snapshot for orchestrators and diagnostics.
/// </summary>
/// <remarks>
/// Best-effort consistency: fields are read without locking, so a concurrent slice may move
/// between reads — the same guarantee level as the queue counters in
/// <c>Axrone.Collections</c>. Good enough for health probes and dashboards, not for control flow.
/// Rates are derived, not measured twice: throughput comes from the calibrator's smoothed
/// per-item cost, so reporting adds no clock reads.
/// </remarks>
public readonly record struct PipelineSnapshot(
    int Capacity,
    int TotalItems,
    int ProcessedItems,
    int PendingItems,
    int CalibratedStride,
    double TicksPerItem,
    long DroppedItems,
    bool HasRemainingWork)
{
    /// <summary>Share of the active batch consumed, in [0, 100]. Zero when idle.</summary>
    public double ProgressPercentage =>
        TotalItems > 0 ? Math.Min(100d, (double)ProcessedItems / TotalItems * 100d) : 0d;

    /// <summary>Estimated items per second at the smoothed cost. Zero before any sample.</summary>
    public double ItemsPerSecond =>
        TicksPerItem > 0d ? Stopwatch.Frequency / TicksPerItem : 0d;

    /// <summary>Estimated seconds to drain the remainder. Negative when the rate is unknown.</summary>
    public double RemainingEstimatedSeconds =>
        ItemsPerSecond > 0d ? PendingItems / ItemsPerSecond : -1d;
}
