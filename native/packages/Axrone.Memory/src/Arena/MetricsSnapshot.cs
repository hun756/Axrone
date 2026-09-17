namespace Axrone.Memory.Arena;

public readonly record struct MetricsSnapshot(
    long TotalWrites,
    long TotalReads,
    long TotalWriteSpins,
    long TotalReadSpins,
    long WriteBatchReservations,
    long ReadBatchReservations,
    long WriteOverflowCount,
    long ReadUnderflowCount);
