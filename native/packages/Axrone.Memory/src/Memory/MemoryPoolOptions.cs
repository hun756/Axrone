namespace Axrone.Memory;

/// <summary>
/// Configuration for <see cref="MemoryPool{T}"/> with fluent builder methods.
/// </summary>
/// <typeparam name="T">Element type of buffers managed by the pool.</typeparam>
/// <remarks>
/// <para>The <c>With*</c> methods mutate this instance and return <c>this</c> — they do NOT create clones.
/// Use <see cref="Default"/> to obtain a fresh clone before chaining, or call <see cref="Clone"/> explicitly.</para>
/// </remarks>
public sealed class MemoryPoolOptions<T>
{
    private static readonly MemoryPoolOptions<T> s_default = new();

    /// <summary>Shared default options instance. Returns a clone to prevent mutation of the singleton.</summary>
    public static MemoryPoolOptions<T> Default => s_default.Clone();

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
    /// <remarks>Reserved for future use. Currently stored but not applied to buffer sizing.</remarks>
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

    /// <summary>Buffer size threshold for memory compression. Default: 1MB.</summary>
    public int MemoryCompressThreshold { get; set; } = 1024 * 1024;

    /// <summary>Batch size for RentMultiple/ReturnMultiple operations. Default: 16.</summary>
    public int BatchOperationSize { get; set; } = 16;

    /// <summary>Maximum number of allocations allowed. -1 means unlimited. Default: -1.</summary>
    public int AllocationQuota { get; set; } = -1;

    /// <summary>Factor for over-allocation sizing. 1.0 means no over-allocation. Default: 1.0.</summary>
    public float OverAllocationFactor { get; set; } = 1.0f;

    /// <summary>Custom bucket sizes. When non-empty, overrides strategy-generated sizes.</summary>
    public IReadOnlyList<int> CustomBucketSizes { get; set; } = [];

    public MemoryPoolOptions<T> WithDefaultBufferSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);
        DefaultBufferSize = size;
        return this;
    }

    public MemoryPoolOptions<T> WithMaxBufferSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);
        MaxBufferSize = size;
        return this;
    }

    public MemoryPoolOptions<T> WithMinBufferSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);
        MinBufferSize = size;
        return this;
    }

    public MemoryPoolOptions<T> WithClearMode(ClearMode mode)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid ClearMode value.");
        ClearMode = mode;
        return this;
    }

    public MemoryPoolOptions<T> WithMaxBuffersPerBucket(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0);
        MaxBuffersPerBucket = count;
        return this;
    }

    public MemoryPoolOptions<T> WithAllocationStrategy(AllocationStrategy strategy)
    {
        if (!Enum.IsDefined(strategy))
            throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Invalid AllocationStrategy value.");
        AllocationStrategy = strategy;
        return this;
    }

    public MemoryPoolOptions<T> WithChunkSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);
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
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxBytes, 0);
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
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        TlsCacheSize = size;
        return this;
    }

    /// <summary>Sets the number of buffers to pre-allocate on pool creation.</summary>
    public MemoryPoolOptions<T> WithPreallocation(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
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
            MemoryCompressThreshold = MemoryCompressThreshold,
            BatchOperationSize = BatchOperationSize,
            AllocationQuota = AllocationQuota,
            OverAllocationFactor = OverAllocationFactor,
            CustomBucketSizes = [.. CustomBucketSizes],
        };
    }

    /// <summary>Validates all options and throws if any are invalid.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A property value is out of valid range.</exception>
    /// <exception cref="InvalidOperationException">Cross-property invariants are violated.</exception>
    internal void Validate()
    {
        if (MinBufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(MinBufferSize));
        if (MaxBufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxBufferSize));
        if (DefaultBufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(DefaultBufferSize));
        if (MinBufferSize > MaxBufferSize)
            throw new InvalidOperationException("MinBufferSize cannot exceed MaxBufferSize.");
        if (DefaultBufferSize < MinBufferSize || DefaultBufferSize > MaxBufferSize)
            throw new InvalidOperationException("DefaultBufferSize must be between MinBufferSize and MaxBufferSize.");
        if (MaxBuffersPerBucket <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxBuffersPerBucket));
        if (TlsCacheSize < 0)
            throw new ArgumentOutOfRangeException(nameof(TlsCacheSize));
        if (MaxTotalMemory <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxTotalMemory));
        if (ChunkSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(ChunkSize));
        if (PreallocationCount < 0)
            throw new ArgumentOutOfRangeException(nameof(PreallocationCount));
        if (MemoryGuardPadding < 0)
            throw new ArgumentOutOfRangeException(nameof(MemoryGuardPadding));
        if (OverAllocationFactor <= 0)
            throw new ArgumentOutOfRangeException(nameof(OverAllocationFactor));
        if (BatchOperationSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(BatchOperationSize));
        if (!Enum.IsDefined(ClearMode))
            throw new ArgumentOutOfRangeException(nameof(ClearMode), ClearMode, "Invalid ClearMode value.");
        if (!Enum.IsDefined(AllocationStrategy))
            throw new ArgumentOutOfRangeException(nameof(AllocationStrategy), AllocationStrategy, "Invalid AllocationStrategy value.");
    }
}
