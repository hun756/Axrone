namespace Axrone.ObjectPool;

public enum PoolingStrategy
{
    CentralizedQueue,
    ThreadLocal,
    ThreadLocalWithPartitioning,
    BoundedChannel,
    AdaptivePartitioning,
    Hybrid,
    LockFree,
    Striped,
    ShardedByThread,
}

public enum AllocationPolicy
{
    LazyAllocation,
    GrowWhenNeeded,
    PreallocateAll,
    AdaptiveGrowth,
    BackgroundReplenishment,
}

public enum ConcurrencyLevel
{
    Minimal = 1,
    Low = 2,
    Default = 4,
    High = 8,
    Maximum = 16,
    Unbounded = 32,
}

public enum DiagnosticsLevel
{
    None,
    Minimal,
    Basic,
    Full,
    Debug,
}

public enum MetricsSamplingRate
{
    None,
    Low,
    Medium,
    High,
    Extreme,
}

public enum EvictionStrategy
{
    None,
    LeastRecentlyUsed,
    TimeBased,
    PressureBased,
    AdaptiveScaling,
    MemoryPressure,
    MostRecentlyUsed,
}

public enum MemoryOptimizationLevel
{
    None,
    Balanced,
    Speed,
    Memory,
    NativeAot,
}
