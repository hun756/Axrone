using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Lifetime.Internal;

/// <summary>
/// Internal diagnostics registry — tracks creation time, access count, and access duration
/// for singleton types marked with <c>[Singleton(Monitored = true)]</c>.
/// Replaces the former public <c>MonitoredSingleton&lt;T&gt;</c>.
/// </summary>
[SkipLocalsInit]
internal static class SingletonDiagnostics
{
    private sealed class MetricsEntry
    {
        public double CreationTimeMs;
        public long AccessCount;
        public bool IsCreated;
        public string TypeName = "";
    }

    private static readonly ConcurrentDictionary<Type, MetricsEntry> _entries = new();
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
                var entry = _entries.GetOrAdd(typeof(T), static t => new MetricsEntry { TypeName = t.Name });
                entry.CreationTimeMs = elapsed;
                entry.IsCreated = true;
                entry.TypeName = typeof(T).Name;
            }
        }
    }

    /// <summary>Record a single access to the singleton — lock-free hot path.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordAccess<T>() where T : class
    {
        if (!SingletonAttributeCache<T>.Monitored) return;

        if (_entries.TryGetValue(typeof(T), out var entry))
            Interlocked.Increment(ref entry.AccessCount);
    }

    /// <summary>Get metrics snapshot for a monitored singleton type.</summary>
    public static SingletonMetrics GetMetrics<T>() where T : class
    {
        if (!_entries.TryGetValue(typeof(T), out var entry))
            return default;

        return new SingletonMetrics(
            entry.TypeName,
            entry.CreationTimeMs,
            0,
            Interlocked.Read(ref entry.AccessCount),
            entry.IsCreated);
    }

    /// <summary>Get metrics for all monitored types.</summary>
    public static Dictionary<string, SingletonMetrics> GetAllMetrics()
    {
        var result = new Dictionary<string, SingletonMetrics>(_entries.Count);
        foreach (var (type, entry) in _entries)
        {
            result[type.Name] = new SingletonMetrics(
                entry.TypeName,
                entry.CreationTimeMs,
                0,
                Interlocked.Read(ref entry.AccessCount),
                entry.IsCreated);
        }
        return result;
    }

    /// <summary>Record a shutdown error for diagnostics.</summary>
    internal static void RecordShutdownError(Type type, Exception exception)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[SingletonShutdown] Error disposing {type.Name}: {exception.Message}");
    }

}
