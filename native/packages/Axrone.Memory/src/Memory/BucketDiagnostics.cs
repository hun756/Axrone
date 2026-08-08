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

    /// <summary>Total bytes managed by this bucket (BufferSize * CurrentCount).</summary>
    public long TotalMemory { get; }

    internal BucketDiagnostics(int bufferSize, int currentCount, long totalMemory)
    {
        BufferSize = bufferSize;
        CurrentCount = currentCount;
        TotalMemory = totalMemory;
    }
}
