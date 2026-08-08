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

    /// <summary>Total bytes currently allocated by the pool (active + pooled).</summary>
    public long TotalAllocatedBytes { get; }

    /// <summary>Number of active bucket size classes.</summary>
    public long BucketCount { get; }

    /// <summary>Ratio of cache hits to total rent operations [0.0, 1.0].</summary>
    public float HitRatio { get; }

    /// <summary>Time elapsed since the pool was created.</summary>
    public TimeSpan ElapsedTime { get; }

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
        TotalAllocatedBytes = 0;
        BucketCount = 0;
        HitRatio = 0;
        ElapsedTime = default;
    }

    internal MemoryPoolMetrics(
        long totalRentedBuffers,
        long totalReturnedBuffers,
        long activeBuffers,
        long pooledBuffers,
        long hits,
        long misses,
        long totalAllocatedBytes,
        long bucketCount,
        float hitRatio,
        TimeSpan elapsedTime)
    {
        TotalRentedBuffers = totalRentedBuffers;
        TotalReturnedBuffers = totalReturnedBuffers;
        ActiveBuffers = activeBuffers;
        PooledBuffers = pooledBuffers;
        Hits = hits;
        Misses = misses;
        TotalAllocatedBytes = totalAllocatedBytes;
        BucketCount = bucketCount;
        HitRatio = hitRatio;
        ElapsedTime = elapsedTime;
    }
}
