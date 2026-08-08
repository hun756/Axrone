using Axrone.Utility.Singleton;

namespace Axrone.Utility.Singleton.Internal;

/// <summary>
/// Internal diagnostics registry — tracks creation time, access count, and access duration
/// for singleton types marked with <c>[Singleton(Monitored = true)]</c>.
/// Replaces the former public <c>MonitoredSingleton&lt;T&gt;</c>.
/// </summary>
[SkipLocalsInit]
internal static class SingletonDiagnostics
{
    private static readonly Dictionary<Type, SingletonMetrics> _metrics = [];
    private static readonly Dictionary<Type, long> _creationTimes = [];
    private static readonly object _lock = new();

    /// <summary>Record the start of singleton creation for timing.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordCreationStart<T>() where T : class
    {
        if (!SingletonAttributeCache<T>.Monitored) return;
        lock (_lock) _creationTimes[typeof(T)] = Stopwatch.GetTimestamp();
    }

    /// <summary>Record completion of singleton creation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordCreationEnd<T>() where T : class
    {
        if (!SingletonAttributeCache<T>.Monitored) return;
        lock (_lock)
        {
            if (_creationTimes.Remove(typeof(T), out var start))
            {
                var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                if (_metrics.TryGetValue(typeof(T), out var existing))
                {
                    _metrics[typeof(T)] = existing with { CreationTimeMs = elapsed, IsCreated = true };
                }
                else
                {
                    _metrics[typeof(T)] = new SingletonMetrics(typeof(T).Name, elapsed, 0, 0, true);
                }
            }
        }
    }

    /// <summary>Record a single access to the singleton.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordAccess<T>() where T : class
    {
        if (!SingletonAttributeCache<T>.Monitored) return;
        lock (_lock)
        {
            if (_metrics.TryGetValue(typeof(T), out var existing))
            {
                _metrics[typeof(T)] = existing with { AccessCount = existing.AccessCount + 1 };
            }
            else
            {
                _metrics[typeof(T)] = new SingletonMetrics(typeof(T).Name, 0, 0, 1, false);
            }
        }
    }

    /// <summary>Get metrics snapshot for a monitored singleton type.</summary>
    public static SingletonMetrics GetMetrics<T>() where T : class
    {
        lock (_lock)
        {
            return _metrics.TryGetValue(typeof(T), out var m) ? m : default;
        }
    }

    /// <summary>Get metrics for all monitored types.</summary>
    public static Dictionary<string, SingletonMetrics> GetAllMetrics()
    {
        lock (_lock)
        {
            var result = new Dictionary<string, SingletonMetrics>(_metrics.Count);
            foreach (var (type, metrics) in _metrics)
                result[type.Name] = metrics;
            return result;
        }
    }

    /// <summary>Record a shutdown error for diagnostics.</summary>
    internal static void RecordShutdownError(Type type, Exception exception)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[SingletonShutdown] Error disposing {type.Name}: {exception.Message}");
    }
}
