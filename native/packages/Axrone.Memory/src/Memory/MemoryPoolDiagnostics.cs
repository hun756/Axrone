namespace Axrone.Memory;

/// <summary>
/// Comprehensive diagnostic snapshot of the memory pool's internal state.
/// </summary>
public readonly record struct MemoryPoolDiagnostics
{
    /// <summary>Total bytes currently managed by the pool (active + pooled).</summary>
    public long TotalMemory { get; }

    /// <summary>Peak bytes ever managed by the pool.</summary>
    public long MaxMemory { get; }

    /// <summary>Bytes allocated but wasted due to size-class rounding.</summary>
    public long WastedMemory { get; }

    /// <summary>Number of active bucket size classes.</summary>
    public int BucketCount { get; }

    /// <summary>Currently active (rented but not returned) buffers.</summary>
    public long ActiveBuffers { get; }

    /// <summary>Buffers currently sitting idle in pool buckets.</summary>
    public long PooledBuffers { get; }

    internal MemoryPoolDiagnostics(
        long totalMemory,
        long maxMemory,
        long wastedMemory,
        int bucketCount,
        long activeBuffers,
        long pooledBuffers)
    {
        TotalMemory = totalMemory;
        MaxMemory = maxMemory;
        WastedMemory = wastedMemory;
        BucketCount = bucketCount;
        ActiveBuffers = activeBuffers;
        PooledBuffers = pooledBuffers;
    }
}
