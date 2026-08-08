namespace Axrone.ObjectPool;

[StructLayout(LayoutKind.Auto)]
public readonly struct PoolConfiguration
{
    public int InitialCapacity { get; init; }
    public int MaximumCapacity { get; init; }
    public TimeSpan IdleTimeout { get; init; }
    public PoolingStrategy Strategy { get; init; }
    public bool TrackLeaks { get; init; }
    public bool ClearOnReturn { get; init; }
    public bool ThrowOnExhaustion { get; init; }
    public int ScavengeIntervalMs { get; init; }
    public AllocationPolicy AllocationPolicy { get; init; }
    public bool UseSlimSynchronization { get; init; }
    public bool AllowPoolExpansion { get; init; }
    public int TargetUtilizationPercentage { get; init; }
    public DiagnosticsLevel DiagnosticsLevel { get; init; }
    public ConcurrencyLevel ConcurrencyLevel { get; init; }
    public MetricsSamplingRate MetricsSamplingRate { get; init; }
    public EvictionStrategy EvictionStrategy { get; init; }
    public MemoryOptimizationLevel MemoryOptimization { get; init; }

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
