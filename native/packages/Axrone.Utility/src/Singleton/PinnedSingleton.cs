namespace Axrone.Utility.Singleton;

/// <summary>
/// Pinned singleton — holds a <see cref="GCHandle"/> to prevent GC collection.
/// Useful for interop scenarios where native code holds a reference.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public abstract class PinnedSingleton<TSelf> : IDisposable where TSelf : PinnedSingleton<TSelf>, new()
{
    private static TSelf? _instance;
    private static int _initialized;
    private GCHandle _handle;

    /// <summary>
    /// Initializes a new instance. Allocates a <see cref="GCHandle"/> to pin this instance.
    /// </summary>
    protected PinnedSingleton()
    {
        _handle = GCHandle.Alloc(this, GCHandleType.Normal);
    }

    /// <summary>Get the pinned singleton instance.</summary>
    public static TSelf Instance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            var inst = Volatile.Read(ref _instance);
            if (inst is not null) return inst;

            if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 0)
            {
                Volatile.Write(ref _instance, new TSelf());
                return Volatile.Read(ref _instance)!;
            }

            SpinWait spinner = default;
            while (Volatile.Read(ref _initialized) != 1)
                spinner.SpinOnce();

            return Volatile.Read(ref _instance)!;
        }
    }

    /// <summary>Free the GC handle. Call when native references are no longer needed.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Release managed and native resources.</summary>
    /// <param name="disposing"><c>true</c> from Dispose; <c>false</c> from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_handle.IsAllocated)
            _handle.Free();
    }
}
