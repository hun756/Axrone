namespace Axrone.Memory;

/// <summary>
/// Configuration for <see cref="MemoryPool{T}"/> with fluent builder methods.
/// </summary>
/// <typeparam name="T">Element type of buffers managed by the pool.</typeparam>
public sealed class MemoryPoolOptions<T>
{
    /// <summary>Default buffer size in elements when no size is specified. Default: 4096.</summary>
    public int DefaultBufferSize { get; set; } = 4096;

    /// <summary>Maximum buffer size the pool will allocate. Default: 1MB in elements.</summary>
    public int MaxBufferSize { get; set; } = 1024 * 1024;

    /// <summary>Minimum buffer size (smallest bucket). Default: 16.</summary>
    public int MinBufferSize { get; set; } = 16;

    /// <summary>When buffers are zeroed. Default: <see cref="ClearMode.OnReturn"/>.</summary>
    public ClearMode ClearMode { get; set; } = ClearMode.OnReturn;

    /// <summary>Maximum number of idle buffers per bucket. Default: 32.</summary>
    public int MaxBuffersPerBucket { get; set; } = 32;

    /// <summary>Number of per-thread cached buffers for fast TLS access. Default: 8.</summary>
    public int TlsCacheSize { get; set; } = 8;

    /// <summary>Maximum total memory in bytes the pool may manage. Default: <see cref="long.MaxValue"/>.</summary>
    public long MaxTotalMemory { get; set; } = long.MaxValue;

    /// <summary>Bucket sizing strategy. Default: <see cref="AllocationStrategy.PowerOfTwo"/>.</summary>
    public AllocationStrategy AllocationStrategy { get; set; } = AllocationStrategy.PowerOfTwo;

    /// <summary>Feature toggle flags. Default: <see cref="MemoryPoolFlags.EnableThreadSafety"/>.</summary>
    public MemoryPoolFlags Flags { get; set; } = MemoryPoolFlags.EnableThreadSafety;

    /// <summary>Alignment for buffer sizes (power of two). Null means no alignment. Default: null.</summary>
    public int? AllocationAlignment { get; set; }

    /// <summary>Chunk size for <see cref="AllocationStrategy.Chunked"/>. Default: 128.</summary>
    public int ChunkSize { get; set; } = 128;

    /// <summary>Number of buffers to pre-allocate on pool creation. Default: 0.</summary>
    public int PreallocationCount { get; set; }

    /// <summary>Guard padding bytes for buffer overflow detection. Default: 16.</summary>
    public int MemoryGuardPadding { get; set; } = 16;

    /// <summary>Interval between automatic trim operations. Default: 2 minutes.</summary>
    public TimeSpan TrimInterval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Percentage of idle buffers to remove during auto-trim. Default: 25.</summary>
    public int AutoTrimPercentage { get; set; } = 25;

    /// <summary>Batch size for RentMultiple/ReturnMultiple operations. Default: 16.</summary>
    public int BatchOperationSize { get; set; } = 16;

    /// <summary>Maximum number of allocations allowed. -1 means unlimited. Default: -1.</summary>
    public int AllocationQuota { get; set; } = -1;

    /// <summary>Factor for over-allocation sizing. 1.0 means no over-allocation. Default: 1.0.</summary>
    public float OverAllocationFactor { get; set; } = 1.0f;

    /// <summary>Custom bucket sizes. When non-empty, overrides strategy-generated sizes.</summary>
    public List<int> CustomBucketSizes { get; set; } = [];

    public MemoryPoolOptions<T> WithDefaultBufferSize(int size)
    {
        DefaultBufferSize = size;
        return this;
    }

    public MemoryPoolOptions<T> WithMaxBufferSize(int size)
    {
        MaxBufferSize = size;
        return this;
    }

    public MemoryPoolOptions<T> WithMinBufferSize(int size)
    {
        MinBufferSize = size;
        return this;
    }

    public MemoryPoolOptions<T> WithClearMode(ClearMode mode)
    {
        ClearMode = mode;
        return this;
    }

    public MemoryPoolOptions<T> WithMaxBuffersPerBucket(int count)
    {
        MaxBuffersPerBucket = count;
        return this;
    }

    public MemoryPoolOptions<T> WithAllocationStrategy(AllocationStrategy strategy)
    {
        AllocationStrategy = strategy;
        return this;
    }

    public MemoryPoolOptions<T> WithChunkSize(int size)
    {
        ChunkSize = size;
        return this;
    }

    /// <summary>Sets the feature flags, replacing any existing flags.</summary>
    public MemoryPoolOptions<T> WithFlags(MemoryPoolFlags flags)
    {
        Flags = flags;
        return this;
    }

    /// <summary>Adds a feature flag to the existing set.</summary>
    public MemoryPoolOptions<T> AddFlag(MemoryPoolFlags flag)
    {
        Flags |= flag;
        return this;
    }

    /// <summary>Sets the allocation alignment (must be a power of two).</summary>
    /// <exception cref="ArgumentException"><paramref name="alignment"/> is not a positive power of two.</exception>
    public MemoryPoolOptions<T> WithAlignment(int alignment)
    {
        if (alignment <= 0 || !BitOperations.IsPow2(alignment))
            throw new ArgumentException("Alignment must be a power of two greater than zero.", nameof(alignment));

        AllocationAlignment = alignment;
        return this;
    }

    /// <summary>Enables or disables statistics tracking via the <see cref="MemoryPoolFlags.EnableStatistics"/> flag.</summary>
    public MemoryPoolOptions<T> WithStatistics(bool enabled = true)
    {
        if (enabled)
            Flags |= MemoryPoolFlags.EnableStatistics;
        else
            Flags &= ~MemoryPoolFlags.EnableStatistics;

        return this;
    }

    /// <summary>Sets the maximum total memory the pool may manage.</summary>
    public MemoryPoolOptions<T> WithMaxMemory(long maxBytes)
    {
        MaxTotalMemory = maxBytes;
        return this;
    }

    /// <summary>Enables or disables thread safety via the <see cref="MemoryPoolFlags.EnableThreadSafety"/> flag.</summary>
    public MemoryPoolOptions<T> WithThreadSafety(bool enabled = true)
    {
        if (enabled)
            Flags |= MemoryPoolFlags.EnableThreadSafety;
        else
            Flags &= ~MemoryPoolFlags.EnableThreadSafety;

        return this;
    }

    /// <summary>Enables or disables thread-local caching via the <see cref="MemoryPoolFlags.UseTlsCache"/> flag.</summary>
    public MemoryPoolOptions<T> WithTlsCache(bool enabled = true)
    {
        if (enabled)
            Flags |= MemoryPoolFlags.UseTlsCache;
        else
            Flags &= ~MemoryPoolFlags.UseTlsCache;

        return this;
    }

    /// <summary>Sets the per-thread cache size for thread-local buffering.</summary>
    /// <param name="size">Number of buffers each thread can cache locally.</param>
    public MemoryPoolOptions<T> WithTlsCacheSize(int size)
    {
        TlsCacheSize = Math.Max(0, size);
        return this;
    }

    /// <summary>Sets the number of buffers to pre-allocate on pool creation.</summary>
    public MemoryPoolOptions<T> WithPreallocation(int count)
    {
        PreallocationCount = count;
        return this;
    }

    /// <summary>Sets custom bucket sizes, overriding strategy-generated sizes.</summary>
    public MemoryPoolOptions<T> WithCustomBucketSizes(params int[] sizes)
    {
        CustomBucketSizes = [.. sizes];
        return this;
    }

    /// <summary>Creates a shallow copy of this options instance.</summary>
    public MemoryPoolOptions<T> Clone()
    {
        return new MemoryPoolOptions<T>
        {
            DefaultBufferSize = DefaultBufferSize,
            MaxBufferSize = MaxBufferSize,
            MinBufferSize = MinBufferSize,
            ClearMode = ClearMode,
            MaxBuffersPerBucket = MaxBuffersPerBucket,
            TlsCacheSize = TlsCacheSize,
            MaxTotalMemory = MaxTotalMemory,
            AllocationStrategy = AllocationStrategy,
            Flags = Flags,
            AllocationAlignment = AllocationAlignment,
            ChunkSize = ChunkSize,
            PreallocationCount = PreallocationCount,
            MemoryGuardPadding = MemoryGuardPadding,
            TrimInterval = TrimInterval,
            AutoTrimPercentage = AutoTrimPercentage,
            BatchOperationSize = BatchOperationSize,
            AllocationQuota = AllocationQuota,
            OverAllocationFactor = OverAllocationFactor,
            CustomBucketSizes = [.. CustomBucketSizes],
        };
    }
}
