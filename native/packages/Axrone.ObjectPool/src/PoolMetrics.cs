namespace Axrone.ObjectPool;

public readonly struct PoolMetrics
{
    public long TotalCreated { get; init; }
    public long TotalRented { get; init; }
    public long TotalReturned { get; init; }
    public long TotalDestroyed { get; init; }
    public int CurrentCount { get; init; }
    public int AvailableCount { get; init; }
    public int RentedCount { get; init; }
    public int MaximumCapacity { get; init; }
    public double AverageRentDurationMs { get; init; }
    public double AverageCreateDurationMs { get; init; }
    public double AverageResetDurationMs { get; init; }
    public double P95RentDurationMs { get; init; }
    public double P99RentDurationMs { get; init; }
    public double HitRatio { get; init; }
    public double Utilization { get; init; }
    public double TurnoverRate { get; init; }
    public double GrowthRate { get; init; }
    public double MemoryUsageBytes { get; init; }
    public double MemoryPressureLevel { get; init; }
    public double AverageThroughputPerSecond { get; init; }
    public double CurrentThroughputPerSecond { get; init; }
    public int PeakUtilization { get; init; }
    public DateTime PeakTime { get; init; }
    public DateTime LastResetTime { get; init; }
    public DateTime LastScavengeTime { get; init; }
}

public readonly struct PoolDiagnostics
{
    public TimeSpan Uptime { get; init; }
    public int ConfigurationChanges { get; init; }
    public int ScavengeCount { get; init; }
    public int ExpansionCount { get; init; }
    public int ShrinkCount { get; init; }
    public int FailedValidationCount { get; init; }
    public int ExhaustionCount { get; init; }
    public int InitialCapacity { get; init; }
    public int MaximumCapacity { get; init; }
    public int AllocFailureCount { get; init; }
    public int DisposeFailureCount { get; init; }
    public int RecycleFailureCount { get; init; }
    public List<KeyValuePair<DateTime, string>> Events { get; init; }
    public Dictionary<string, double> CustomMetrics { get; init; }
    public Exception? LastException { get; init; }
}
