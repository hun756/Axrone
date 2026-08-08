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

    /// <summary>Bucket sizing strategy. Default: <see cref="AllocationStrategy.PowerOfTwo"/>.</summary>
    public AllocationStrategy AllocationStrategy { get; set; } = AllocationStrategy.PowerOfTwo;

    /// <summary>Chunk size for <see cref="AllocationStrategy.Chunked"/>. Default: 128.</summary>
    public int ChunkSize { get; set; } = 128;

    /// <summary>Sets the default buffer size in elements.</summary>
    /// <param name="size">Default buffer size.</param>
    /// <returns>This instance for chaining.</returns>
    public MemoryPoolOptions<T> WithDefaultBufferSize(int size)
    {
        DefaultBufferSize = size;
        return this;
    }

    /// <summary>Sets the maximum buffer size the pool will allocate.</summary>
    /// <param name="size">Maximum buffer size in elements.</param>
    /// <returns>This instance for chaining.</returns>
    public MemoryPoolOptions<T> WithMaxBufferSize(int size)
    {
        MaxBufferSize = size;
        return this;
    }

    /// <summary>Sets the minimum buffer size (smallest bucket).</summary>
    /// <param name="size">Minimum buffer size in elements.</param>
    /// <returns>This instance for chaining.</returns>
    public MemoryPoolOptions<T> WithMinBufferSize(int size)
    {
        MinBufferSize = size;
        return this;
    }

    /// <summary>Sets when buffers are zeroed.</summary>
    /// <param name="mode">The clear mode to use.</param>
    /// <returns>This instance for chaining.</returns>
    public MemoryPoolOptions<T> WithClearMode(ClearMode mode)
    {
        ClearMode = mode;
        return this;
    }

    /// <summary>Sets the maximum number of idle buffers per bucket.</summary>
    /// <param name="count">Maximum idle buffers per bucket.</param>
    /// <returns>This instance for chaining.</returns>
    public MemoryPoolOptions<T> WithMaxBuffersPerBucket(int count)
    {
        MaxBuffersPerBucket = count;
        return this;
    }

    /// <summary>Sets the bucket sizing strategy.</summary>
    /// <param name="strategy">The allocation strategy to use.</param>
    /// <returns>This instance for chaining.</returns>
    public MemoryPoolOptions<T> WithAllocationStrategy(AllocationStrategy strategy)
    {
        AllocationStrategy = strategy;
        return this;
    }

    /// <summary>Sets the chunk size for <see cref="AllocationStrategy.Chunked"/>.</summary>
    /// <param name="size">Chunk size in elements.</param>
    /// <returns>This instance for chaining.</returns>
    public MemoryPoolOptions<T> WithChunkSize(int size)
    {
        ChunkSize = size;
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
            AllocationStrategy = AllocationStrategy,
            ChunkSize = ChunkSize,
        };
    }
}
