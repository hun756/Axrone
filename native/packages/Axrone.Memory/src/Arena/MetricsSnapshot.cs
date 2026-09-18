namespace Axrone.Memory.Arena;

public readonly record struct MetricsSnapshot(
    ulong CommittedHead,
    ulong CommittedTail,
    ulong InFlightWrites,
    ulong InFlightReads,
    nuint Capacity,
    uint ActiveLeases,
    double UtilizationPercentage,
    bool IsCompleted,
    bool IsFaulted,
    bool IsHealthy);
