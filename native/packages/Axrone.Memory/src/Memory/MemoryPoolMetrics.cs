namespace Axrone.Memory;

/// <summary>
/// Snapshot of memory pool performance metrics.
/// </summary>
public readonly record struct MemoryPoolMetrics
{
    /// <summary>Total bytes currently allocated by the pool (active + pooled).</summary>
    public long TotalAllocatedBytes { get; }

    /// <summary>Total buffers rented from the pool since creation or last reset.</summary>
    public long TotalRentedBuffers { get; }

    /// <summary>Total buffers returned to the pool since creation or last reset.</summary>
    public long TotalReturnedBuffers { get; }

    /// <summary>Currently active (rented but not returned) buffers.</summary>
    public long ActiveBuffers { get; }

    /// <summary>Buffers currently sitting idle in pool buckets.</summary>
    public long PooledBuffers { get; }

    /// <summary>Times a rent required a new allocation.</summary>
    public long Misses { get; }

    /// <summary>Times a rent was satisfied from an existing pooled buffer.</summary>
    public long Hits { get; }

    /// <summary>Number of active bucket size classes.</summary>
    public int BucketCount { get; }

    /// <summary>Bytes wasted by size-class rounding across pooled buffers.</summary>
    public long FragmentationBytes { get; }

    /// <summary>Time elapsed since the pool was created.</summary>
    public TimeSpan ElapsedTime { get; }

    /// <summary>Average time in nanoseconds to complete a rent operation.</summary>
    public double AverageRentTime { get; }

    /// <summary>Average time in nanoseconds to complete a return operation.</summary>
    public double AverageReturnTime { get; }

    /// <summary>Total number of individual buffer allocations (new backing arrays).</summary>
    public long TotalAllocations { get; }

    /// <summary>Total number of buffer deallocations (released backing arrays).</summary>
    public long TotalDeallocations { get; }

    /// <summary>Ratio of cache hits to total rent operations [0.0, 1.0].</summary>
    public float HitRatio { get; }

    /// <summary>Average bytes per allocation request.</summary>
    public int BytesPerAllocation { get; }

    internal MemoryPoolMetrics(
        long totalAllocatedBytes,
        long totalRentedBuffers,
        long totalReturnedBuffers,
        long activeBuffers,
        long pooledBuffers,
        long misses,
        long hits,
        int bucketCount,
        long fragmentationBytes,
        TimeSpan elapsedTime,
        double averageRentTime,
        double averageReturnTime,
        long totalAllocations,
        long totalDeallocations,
        float hitRatio,
        int bytesPerAllocation)
    {
        TotalAllocatedBytes = totalAllocatedBytes;
        TotalRentedBuffers = totalRentedBuffers;
        TotalReturnedBuffers = totalReturnedBuffers;
        ActiveBuffers = activeBuffers;
        PooledBuffers = pooledBuffers;
        Misses = misses;
        Hits = hits;
        BucketCount = bucketCount;
        FragmentationBytes = fragmentationBytes;
        ElapsedTime = elapsedTime;
        AverageRentTime = averageRentTime;
        AverageReturnTime = averageReturnTime;
        TotalAllocations = totalAllocations;
        TotalDeallocations = totalDeallocations;
        HitRatio = hitRatio;
        BytesPerAllocation = bytesPerAllocation;
    }
}
