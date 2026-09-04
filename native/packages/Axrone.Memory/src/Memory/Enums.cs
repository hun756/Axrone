namespace Axrone.Memory;

public enum MemoryBackingStrategy : byte
{
    Managed = 0,
    PinnedManaged = 1,
    NativeAligned = 2
}

public enum MemoryClearMode : byte
{
    Never = 0,
    OnReturn = 1,
    OnRent = 2
}

public readonly record struct PoolDiagnosticsSnapshot(
    long TotalAllocatedBytes,
    long TotalRentedBytes,
    long ActiveAllocations,
    long Tier1Hits,
    long Tier2Hits,
    long AllocatorMisses);
