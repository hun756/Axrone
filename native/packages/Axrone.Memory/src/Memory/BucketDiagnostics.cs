namespace Axrone.Memory;

/// <summary>
/// Per-bucket diagnostic snapshot for a single size class in the pool.
/// </summary>
public readonly record struct BucketDiagnostics
{
    /// <summary>The buffer size class for this bucket.</summary>
    public int BufferSize { get; }

    /// <summary>Number of idle buffers currently in this bucket.</summary>
    public int CurrentCount { get; }

    /// <summary>Maximum number of idle buffers allowed in this bucket.</summary>
    public int MaxCount { get; }

    /// <summary>Total number of rent operations from this bucket.</summary>
    public long TotalRents { get; }

    /// <summary>Total number of return operations to this bucket.</summary>
    public long TotalReturns { get; }

    /// <summary>Total bytes managed by this bucket (BufferSize * CurrentCount).</summary>
    public long TotalMemory { get; }

    /// <summary>Average rent time in nanoseconds (requires profiling enabled).</summary>
    public double AverageRentTimeNs { get; }

    /// <summary>Average return time in nanoseconds (requires profiling enabled).</summary>
    public double AverageReturnTimeNs { get; }

    internal BucketDiagnostics(int bufferSize, int currentCount, long totalMemory)
    {
        BufferSize = bufferSize;
        CurrentCount = currentCount;
        MaxCount = 0;
        TotalRents = 0;
        TotalReturns = 0;
        TotalMemory = totalMemory;
        AverageRentTimeNs = 0;
        AverageReturnTimeNs = 0;
    }

    internal BucketDiagnostics(
        int bufferSize,
        int currentCount,
        int maxCount,
        long totalRents,
        long totalReturns,
        long totalMemory,
        double averageRentTimeNs,
        double averageReturnTimeNs)
    {
        BufferSize = bufferSize;
        CurrentCount = currentCount;
        MaxCount = maxCount;
        TotalRents = totalRents;
        TotalReturns = totalReturns;
        TotalMemory = totalMemory;
        AverageRentTimeNs = averageRentTimeNs;
        AverageReturnTimeNs = averageReturnTimeNs;
    }
}
