namespace Axrone.Utility.Singleton.Internal;

/// <summary>
/// Tracks <see cref="IDisposable"/> singleton instances for reverse-creation-order teardown.
/// Call <see cref="ShutdownAsync"/> from application exit to deterministically release all tracked singletons.
/// </summary>
[SkipLocalsInit]
internal static class SingletonShutdownRegistry
{
    private static readonly List<(Type Type, object Instance)> _tracked = [];
    private static readonly HashSet<object> _seen = new(ReferenceEqualityComparer.Instance);
    private static readonly object _lock = new();
    private static int _shutDown;

    /// <summary>Register an instance for shutdown disposal. Idempotent per instance reference.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Register<T>(T instance) where T : class
    {
        if (instance is not IDisposable && instance is not IAsyncDisposable) return;

        lock (_lock)
        {
            if (!_seen.Add(instance)) return;
            _tracked.Add((typeof(T), instance));
        }
    }

    /// <summary>
    /// Dispose all tracked instances in reverse creation order.
    /// Safe to call multiple times — subsequent calls are no-ops.
    /// </summary>
    public static async ValueTask ShutdownAsync()
    {
        if (Interlocked.Exchange(ref _shutDown, 1) != 0) return;

        List<(Type Type, object Instance)> snapshot;
        lock (_lock)
        {
            snapshot = new List<(Type, object)>(_tracked);
            _tracked.Clear();
            _seen.Clear();
        }

        for (var i = snapshot.Count - 1; i >= 0; i--)
        {
            var entry = snapshot[i];
            try
            {
                if (entry.Instance is IAsyncDisposable asyncDisp)
                    await asyncDisp.DisposeAsync().ConfigureAwait(false);
                else if (entry.Instance is IDisposable disp)
                    disp.Dispose();
            }
            catch (Exception ex)
            {
                SingletonDiagnostics.RecordShutdownError(entry.Type, ex);
            }
        }
    }

    /// <summary>Number of currently tracked instances.</summary>
    public static int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            lock (_lock) return _tracked.Count;
        }
    }
}
