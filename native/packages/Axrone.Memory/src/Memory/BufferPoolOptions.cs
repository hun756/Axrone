namespace Axrone.Memory;

public sealed record BufferPoolOptions
{
    public int MinimumBlockSize { get; init; } = 64;
    public int MaximumBlockSize { get; init; } = 4194304;
    public int GlobalQueueCapacity { get; init; } = 256;
    public MemoryClearMode ClearMode { get; init; } = MemoryClearMode.Never;
    public MemoryBackingStrategy BackingStrategy { get; init; } = MemoryBackingStrategy.Managed;
    public TimeSpan TrimInterval { get; init; } = TimeSpan.Zero;
    public int AutoTrimPercentage { get; init; }
}
