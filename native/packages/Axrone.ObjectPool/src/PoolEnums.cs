namespace Axrone.ObjectPool;

/// <summary>
/// Defines the strategy used by the object pool to manage concurrent access and object distribution.
/// </summary>
public enum PoolingStrategy
{
    /// <summary>
    /// All threads share a single central queue for borrowing and returning objects.
    /// </summary>
    CentralizedQueue,

    /// <summary>
    /// Each thread maintains its own local pool to minimize contention.
    /// </summary>
    ThreadLocal,

    /// <summary>
    /// Thread-local pools with automatic partitioning to balance load across threads.
    /// </summary>
    ThreadLocalWithPartitioning,

    /// <summary>
    /// Uses a bounded channel to cap the number of in-flight objects and apply backpressure.
    /// </summary>
    BoundedChannel,

    /// <summary>
    /// Dynamically adjusts partition sizes based on observed thread demand.
    /// </summary>
    AdaptivePartitioning,

    /// <summary>
    /// Combines multiple strategies and selects the best one at runtime based on workload.
    /// </summary>
    Hybrid,

    /// <summary>
    /// Uses lock-free data structures to eliminate synchronization overhead entirely.
    /// </summary>
    LockFree,

    /// <summary>
    /// Divides the pool into stripes to reduce contention while keeping a shared backing store.
    /// </summary>
    Striped,

    /// <summary>
    /// Assigns each thread a dedicated shard, providing the strongest isolation.
    /// </summary>
    ShardedByThread,
}

/// <summary>
/// Controls when and how the pool allocates new object instances.
/// </summary>
public enum AllocationPolicy
{
    /// <summary>
    /// Objects are allocated only on the first request.
    /// </summary>
    LazyAllocation,

    /// <summary>
    /// The pool grows incrementally as demand exceeds current capacity.
    /// </summary>
    GrowWhenNeeded,

    /// <summary>
    /// All objects up to maximum capacity are allocated at pool initialization.
    /// </summary>
    PreallocateAll,

    /// <summary>
    /// Growth rate adjusts automatically based on recent utilization trends.
    /// </summary>
    AdaptiveGrowth,

    /// <summary>
    /// A background task proactively replenishes the pool before it runs dry.
    /// </summary>
    BackgroundReplenishment,
}

/// <summary>
/// Specifies the expected degree of concurrent access the pool must support.
/// </summary>
public enum ConcurrencyLevel
{
    /// <summary>
    /// Optimized for single-threaded or very low contention scenarios.
    /// </summary>
    Minimal = 1,

    /// <summary>
    /// Suitable for a small number of concurrent consumers.
    /// </summary>
    Low = 2,

    /// <summary>
    /// Balanced setting for typical multi-threaded workloads.
    /// </summary>
    Default = 4,

    /// <summary>
    /// Tuned for workloads with significant parallel access.
    /// </summary>
    High = 8,

    /// <summary>
    /// Supports heavily contended scenarios with many concurrent consumers.
    /// </summary>
    Maximum = 16,

    /// <summary>
    /// No practical upper bound on concurrent access; maximizes internal partitioning.
    /// </summary>
    Unbounded = 32,
}

/// <summary>
/// Controls the verbosity of diagnostic information emitted by the pool.
/// </summary>
public enum DiagnosticsLevel
{
    /// <summary>
    /// No diagnostic output is produced.
    /// </summary>
    None,

    /// <summary>
    /// Only critical diagnostic events are recorded.
    /// </summary>
    Minimal,

    /// <summary>
    /// Standard diagnostic events such as borrow and return are recorded.
    /// </summary>
    Basic,

    /// <summary>
    /// Detailed diagnostics including allocation, eviction, and resize events.
    /// </summary>
    Full,

    /// <summary>
    /// Maximum verbosity intended for development and troubleshooting only.
    /// </summary>
    Debug,
}

/// <summary>
/// Determines how frequently the pool samples internal metrics.
/// </summary>
public enum MetricsSamplingRate
{
    /// <summary>
    /// Metrics collection is disabled.
    /// </summary>
    None,

    /// <summary>
    /// Metrics are sampled infrequently to minimize overhead.
    /// </summary>
    Low,

    /// <summary>
    /// Moderate sampling rate balancing accuracy and overhead.
    /// </summary>
    Medium,

    /// <summary>
    /// Frequent sampling for near-real-time metric visibility.
    /// </summary>
    High,

    /// <summary>
    /// Every operation is sampled; highest accuracy but greatest overhead.
    /// </summary>
    Extreme,
}

/// <summary>
/// Defines the policy for removing idle or excess objects from the pool.
/// </summary>
public enum EvictionStrategy
{
    /// <summary>
    /// Objects are never evicted; the pool only grows.
    /// </summary>
    None,

    /// <summary>
    /// The least recently used objects are evicted first when capacity is exceeded.
    /// </summary>
    LeastRecentlyUsed,

    /// <summary>
    /// Objects idle longer than a configured timeout are evicted.
    /// </summary>
    TimeBased,

    /// <summary>
    /// Eviction is triggered when internal pressure indicators exceed thresholds.
    /// </summary>
    PressureBased,

    /// <summary>
    /// The pool dynamically scales its size up or down based on utilization.
    /// </summary>
    AdaptiveScaling,

    /// <summary>
    /// Eviction is driven by the host process memory pressure notifications.
    /// </summary>
    MemoryPressure,

    /// <summary>
    /// The most recently used objects are evicted first, preserving older entries.
    /// </summary>
    MostRecentlyUsed,
}

/// <summary>
/// Specifies the trade-off between speed and memory consumption for the pool.
/// </summary>
public enum MemoryOptimizationLevel
{
    /// <summary>
    /// No memory optimizations are applied.
    /// </summary>
    None,

    /// <summary>
    /// Balanced trade-off between throughput and memory footprint.
    /// </summary>
    Balanced,

    /// <summary>
    /// Prioritizes throughput at the cost of higher memory usage.
    /// </summary>
    Speed,

    /// <summary>
    /// Prioritizes minimal memory usage at the cost of throughput.
    /// </summary>
    Memory,

    /// <summary>
    /// Optimized for Native AOT deployment with trimmed metadata and reduced reflection.
    /// </summary>
    NativeAot,
}
