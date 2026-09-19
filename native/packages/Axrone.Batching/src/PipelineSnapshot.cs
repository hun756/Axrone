namespace Axrone.Batching;

/// <summary>
/// Point-in-time pipeline inspection snapshot for orchestrators and diagnostics.
/// </summary>
/// <remarks>
/// Best-effort consistency: fields are read without locking, so a concurrent slice may move
/// between reads — the same guarantee level as the queue counters in
/// <c>Axrone.Collections</c>. Good enough for health probes and dashboards, not for control flow.
/// </remarks>
public readonly record struct PipelineSnapshot(
    int Capacity,
    int PendingItems,
    int CalibratedStride,
    long DroppedItems,
    bool HasRemainingWork);
