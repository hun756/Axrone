namespace Axrone.Memory;

/// <summary>
/// Snapshot of memory pool performance metrics.
/// </summary>
public readonly record struct MemoryPoolMetrics
{
    /// <summary>Total buffers rented from the pool since creation or last reset.</summary>
    public long TotalRentedBuffers { get; }

    /// <summary>Total buffers returned to the pool since creation or last reset.</summary>
    public long TotalReturnedBuffers { get; }

    /// <summary>Currently active (rented but not returned) buffers.</summary>
    public long ActiveBuffers { get; }

    /// <summary>Buffers currently sitting idle in pool buckets.</summary>
    public long PooledBuffers { get; }

    /// <summary>Times a rent was satisfied from an existing pooled buffer.</summary>
    public long Hits { get; }

    /// <summary>Times a rent required a new allocation.</summary>
    public long Misses { get; }

    internal MemoryPoolMetrics(
        long totalRentedBuffers,
        long totalReturnedBuffers,
        long activeBuffers,
        long pooledBuffers,
        long hits,
        long misses)
    {
        TotalRentedBuffers = totalRentedBuffers;
        TotalReturnedBuffers = totalReturnedBuffers;
        ActiveBuffers = activeBuffers;
        PooledBuffers = pooledBuffers;
        Hits = hits;
        Misses = misses;
    }
}
