using System.Diagnostics;

namespace Axrone.Utility.Singleton;

/// <summary>
/// Instrumented singleton — tracks creation time, access time, and access count for diagnostics.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public sealed class MonitoredSingleton<T> where T : class, new()
{
    private static readonly Lazy<T> _instance = new(
        CreateInstance,
        LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Stopwatch _creationTimer = new();
    private static readonly Stopwatch _accessTimer = new();
    private static long _accessCount;

    /// <summary>Get the singleton instance with timing instrumentation.</summary>
    public static T Instance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            try
            {
                _accessTimer.Start();
                Interlocked.Increment(ref _accessCount);
                return _instance.Value;
            }
            finally
            {
                _accessTimer.Stop();
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T CreateInstance()
    {
        _creationTimer.Start();
        try
        {
            return new T();
        }
        finally
        {
            _creationTimer.Stop();
        }
    }

    /// <summary>Get diagnostic metrics for this singleton.</summary>
    public static SingletonMetrics GetMetrics() => new(
        typeof(T).Name,
        _creationTimer.ElapsedMilliseconds,
        _accessTimer.ElapsedMilliseconds,
        Interlocked.Read(ref _accessCount),
        _instance.IsValueCreated);
}

/// <summary>Diagnostic metrics snapshot for a monitored singleton.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct SingletonMetrics
{
    public readonly string TypeName;
    public readonly long CreationTimeMs;
    public readonly long TotalAccessTimeMs;
    public readonly long AccessCount;
    public readonly bool IsCreated;

    public SingletonMetrics(
        string typeName,
        long creationTimeMs,
        long totalAccessTimeMs,
        long accessCount,
        bool isCreated)
    {
        TypeName = typeName;
        CreationTimeMs = creationTimeMs;
        TotalAccessTimeMs = totalAccessTimeMs;
        AccessCount = accessCount;
        IsCreated = isCreated;
    }

    public double AverageAccessTimeMs =>
        AccessCount > 0 ? (double)TotalAccessTimeMs / AccessCount : 0;
}
