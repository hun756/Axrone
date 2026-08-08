namespace Axrone.Memory;

/// <summary>
/// Comprehensive diagnostic snapshot of the memory pool's internal state.
/// </summary>
public readonly record struct MemoryPoolDiagnostics
{
    /// <summary>Per-bucket diagnostic information keyed by buffer size.</summary>
    public IReadOnlyDictionary<int, BucketDiagnostics> BucketInfo { get; }

    /// <summary>Total bytes currently managed by the pool (active + pooled).</summary>
    public long TotalMemory { get; }

    /// <summary>Peak bytes ever managed by the pool.</summary>
    public long MaxMemory { get; }

    /// <summary>Bytes allocated but wasted due to size-class rounding.</summary>
    public long WastedMemory { get; }

    /// <summary>Time elapsed since the pool was created.</summary>
    public TimeSpan Runtime { get; }

    /// <summary>Largest single allocation request size in elements.</summary>
    public int LargestAllocation { get; }

    /// <summary>Smallest single allocation request size in elements.</summary>
    public int SmallestAllocation { get; }

    /// <summary>Most frequently requested allocation size in elements.</summary>
    public int MostFrequentAllocation { get; }

    /// <summary>Average allocation request size in elements.</summary>
    public float AverageAllocationSize { get; }

    /// <summary>Statistical variance of allocation request sizes.</summary>
    public float AllocationVariance { get; }

    internal MemoryPoolDiagnostics(
        IReadOnlyDictionary<int, BucketDiagnostics> bucketInfo,
        long totalMemory,
        long maxMemory,
        long wastedMemory,
        TimeSpan runtime,
        int largestAllocation,
        int smallestAllocation,
        int mostFrequentAllocation,
        float averageAllocationSize,
        float allocationVariance)
    {
        BucketInfo = bucketInfo;
        TotalMemory = totalMemory;
        MaxMemory = maxMemory;
        WastedMemory = wastedMemory;
        Runtime = runtime;
        LargestAllocation = largestAllocation;
        SmallestAllocation = smallestAllocation;
        MostFrequentAllocation = mostFrequentAllocation;
        AverageAllocationSize = averageAllocationSize;
        AllocationVariance = allocationVariance;
    }
}
