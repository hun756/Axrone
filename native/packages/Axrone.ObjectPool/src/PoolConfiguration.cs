namespace Axrone.ObjectPool;

/// <summary>
/// Immutable configuration for an object pool, covering capacity, strategy, diagnostics, and memory behavior.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct PoolConfiguration
{
    /// <summary>
    /// Number of objects pre-created when the pool is initialized.
    /// </summary>
    public int InitialCapacity { get; init; }

    /// <summary>
    /// Maximum number of objects the pool may hold before rejecting or blocking new borrows.
    /// </summary>
    public int MaximumCapacity { get; init; }

    /// <summary>
    /// Duration an object may remain idle before it becomes eligible for eviction.
    /// </summary>
    public TimeSpan IdleTimeout { get; init; }

    /// <summary>
    /// The concurrency and access strategy the pool uses to distribute objects.
    /// </summary>
    public PoolingStrategy Strategy { get; init; }

    /// <summary>
    /// When true, the pool tracks borrowed objects and reports leaks for unreleased instances.
    /// </summary>
    public bool TrackLeaks { get; init; }

    /// <summary>
    /// When true, object state is reset or cleared each time it is returned to the pool.
    /// </summary>
    public bool ClearOnReturn { get; init; }

    /// <summary>
    /// When true, an exception is thrown if the pool is exhausted instead of blocking or returning null.
    /// </summary>
    public bool ThrowOnExhaustion { get; init; }

    /// <summary>
    /// Interval in milliseconds between background scavenging passes that remove idle objects.
    /// </summary>
    public int ScavengeIntervalMs { get; init; }

    /// <summary>
    /// Policy governing when and how the pool allocates new object instances.
    /// </summary>
    public AllocationPolicy AllocationPolicy { get; init; }

    /// <summary>
    /// When true, the pool uses <c>Monitor</c>-based slim synchronization primitives to reduce overhead.
    /// </summary>
    public bool UseSlimSynchronization { get; init; }

    /// <summary>
    /// When true, the pool is allowed to grow beyond <see cref="MaximumCapacity"/> under extreme demand.
    /// </summary>
    public bool AllowPoolExpansion { get; init; }

    /// <summary>
    /// Target pool utilization percentage that triggers growth or shrinkage decisions.
    /// </summary>
    public int TargetUtilizationPercentage { get; init; }

    /// <summary>
    /// Verbosity level of diagnostic information emitted by the pool.
    /// </summary>
    public DiagnosticsLevel DiagnosticsLevel { get; init; }

    /// <summary>
    /// Expected degree of concurrent access the pool must support.
    /// </summary>
    public ConcurrencyLevel ConcurrencyLevel { get; init; }

    /// <summary>
    /// Frequency at which the pool samples internal performance metrics.
    /// </summary>
    public MetricsSamplingRate MetricsSamplingRate { get; init; }

    /// <summary>
    /// Policy for removing idle or excess objects from the pool.
    /// </summary>
    public EvictionStrategy EvictionStrategy { get; init; }

    /// <summary>
    /// Trade-off level between speed and memory consumption.
    /// </summary>
    public MemoryOptimizationLevel MemoryOptimization { get; init; }

    /// <summary>
    /// General-purpose configuration balanced for typical workloads.
    /// </summary>
    public static readonly PoolConfiguration Default = new()
    {
        InitialCapacity = Environment.ProcessorCount * 2,
        MaximumCapacity = Environment.ProcessorCount * 32,
        IdleTimeout = TimeSpan.FromMinutes(5),
        Strategy = PoolingStrategy.ThreadLocal,
        TrackLeaks = false,
        ClearOnReturn = true,
        ThrowOnExhaustion = false,
        ScavengeIntervalMs = 30000,
        AllocationPolicy = AllocationPolicy.GrowWhenNeeded,
        UseSlimSynchronization = true,
        AllowPoolExpansion = true,
        TargetUtilizationPercentage = 80,
        DiagnosticsLevel = DiagnosticsLevel.Basic,
        ConcurrencyLevel = ConcurrencyLevel.Default,
        MetricsSamplingRate = MetricsSamplingRate.Medium,
        EvictionStrategy = EvictionStrategy.LeastRecentlyUsed,
        MemoryOptimization = MemoryOptimizationLevel.Balanced,
    };

    /// <summary>
    /// Throughput-optimized configuration that pre-allocates objects and minimizes diagnostics overhead.
    /// </summary>
    public static readonly PoolConfiguration HighPerformance = new()
    {
        InitialCapacity = Environment.ProcessorCount * 4,
        MaximumCapacity = Environment.ProcessorCount * 64,
        IdleTimeout = TimeSpan.FromMinutes(10),
        Strategy = PoolingStrategy.ThreadLocalWithPartitioning,
        TrackLeaks = false,
        ClearOnReturn = false,
        ThrowOnExhaustion = false,
        ScavengeIntervalMs = 60000,
        AllocationPolicy = AllocationPolicy.PreallocateAll,
        UseSlimSynchronization = true,
        AllowPoolExpansion = true,
        TargetUtilizationPercentage = 90,
        DiagnosticsLevel = DiagnosticsLevel.Minimal,
        ConcurrencyLevel = ConcurrencyLevel.Maximum,
        MetricsSamplingRate = MetricsSamplingRate.Low,
        EvictionStrategy = EvictionStrategy.None,
        MemoryOptimization = MemoryOptimizationLevel.Speed,
    };

    /// <summary>
    /// Memory-conserving configuration that minimizes footprint with strict leak tracking and aggressive eviction.
    /// </summary>
    public static readonly PoolConfiguration MemoryConserving = new()
    {
        InitialCapacity = Environment.ProcessorCount,
        MaximumCapacity = Environment.ProcessorCount * 16,
        IdleTimeout = TimeSpan.FromMinutes(2),
        Strategy = PoolingStrategy.CentralizedQueue,
        TrackLeaks = true,
        ClearOnReturn = true,
        ThrowOnExhaustion = true,
        ScavengeIntervalMs = 15000,
        AllocationPolicy = AllocationPolicy.LazyAllocation,
        UseSlimSynchronization = false,
        AllowPoolExpansion = false,
        TargetUtilizationPercentage = 95,
        DiagnosticsLevel = DiagnosticsLevel.Full,
        ConcurrencyLevel = ConcurrencyLevel.Minimal,
        MetricsSamplingRate = MetricsSamplingRate.High,
        EvictionStrategy = EvictionStrategy.LeastRecentlyUsed,
        MemoryOptimization = MemoryOptimizationLevel.Memory,
    };

    /// <summary>
    /// Configuration tuned for Native AOT deployment, avoiding reflection-heavy patterns and trimming metadata.
    /// </summary>
    public static readonly PoolConfiguration NativeAotOptimized = new()
    {
        InitialCapacity = Environment.ProcessorCount * 2,
        MaximumCapacity = Environment.ProcessorCount * 24,
        IdleTimeout = TimeSpan.FromMinutes(5),
        Strategy = PoolingStrategy.ThreadLocalWithPartitioning,
        TrackLeaks = false,
        ClearOnReturn = true,
        ThrowOnExhaustion = false,
        ScavengeIntervalMs = 45000,
        AllocationPolicy = AllocationPolicy.GrowWhenNeeded,
        UseSlimSynchronization = true,
        AllowPoolExpansion = true,
        TargetUtilizationPercentage = 85,
        DiagnosticsLevel = DiagnosticsLevel.Basic,
        ConcurrencyLevel = ConcurrencyLevel.Default,
        MetricsSamplingRate = MetricsSamplingRate.Medium,
        EvictionStrategy = EvictionStrategy.TimeBased,
        MemoryOptimization = MemoryOptimizationLevel.NativeAot,
    };
}
