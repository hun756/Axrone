namespace Axrone.Memory;

public static class GlobalMemoryPools
{
    private static readonly Lazy<TieredMemoryPool<byte>> s_sharedBytePool =
        new(() => new TieredMemoryPool<byte>(new BufferPoolOptions
        {
            MinimumBlockSize = 64,
            MaximumBlockSize = 4194304,
            GlobalQueueCapacity = 512,
            ClearMode = MemoryClearMode.Never,
            BackingStrategy = MemoryBackingStrategy.Managed
        }), LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<TieredMemoryPool<byte>> s_sharedNativeBytePool =
        new(() => new TieredMemoryPool<byte>(
            new BufferPoolOptions
            {
                MinimumBlockSize = 64,
                MaximumBlockSize = 4194304,
                GlobalQueueCapacity = 512,
                ClearMode = MemoryClearMode.Never,
                BackingStrategy = MemoryBackingStrategy.NativeAligned
            },
            NativeAlignedBlockAllocator<byte>.Instance), LazyThreadSafetyMode.ExecutionAndPublication);

    public static TieredMemoryPool<byte> Shared => s_sharedBytePool.Value;

    public static TieredMemoryPool<byte> SharedNative => s_sharedNativeBytePool.Value;

    public static TieredMemoryPool<T> CreatePartitionedPool<T>(
        BufferPoolOptions? options = null,
        IBlockAllocator<T>? allocator = null) =>
        new(options, allocator);

    public static TieredMemoryPool<T> CreateNativePool<T>(
        BufferPoolOptions? options = null) where T : unmanaged =>
        new(options, NativeAlignedBlockAllocator<T>.Instance);

    public static ContiguousSlabPool<T> CreateSlabPool<T>(
        int blockSize,
        int blockCount,
        MemoryClearMode clearMode = MemoryClearMode.Never) where T : unmanaged =>
        new(blockSize, blockCount, clearMode);

    public static MonotonicArenaBuffer CreateArena(nint initialChunkSize = 1048576) =>
        new(initialChunkSize);

    public static PooledBufferWriter<T> CreateWriter<T>(
        TieredMemoryPool<T> pool,
        int initialCapacity = 256) =>
        new(pool, initialCapacity);
}
