namespace Axrone.Memory.Arena;

public readonly record struct HealthReport(
    bool IsHealthy,
    bool IsDisposed,
    bool IsDraining,
    long Capacity,
    long ActiveWriteReservations,
    long ActiveReadReservations,
    double UtilizationPercent);
