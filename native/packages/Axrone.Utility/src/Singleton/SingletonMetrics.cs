namespace Axrone.Utility.Singleton;

/// <summary>Diagnostic metrics snapshot for a monitored singleton.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SingletonMetrics(
    string TypeName,
    double CreationTimeMs,
    double TotalAccessTimeMs,
    long AccessCount,
    bool IsCreated)
{
    /// <summary>Average access duration in milliseconds.</summary>
    public double AverageAccessTimeMs =>
        AccessCount > 0 ? TotalAccessTimeMs / AccessCount : 0;
}
