namespace Axrone.Memory;

#if DEBUG
internal sealed class LeakTracker : IDisposable
{
    private static long s_activeTrackers;
    private static long s_totalLeaks;

    private readonly string _allocationStackTrace;
    private readonly string _slotType;
    private int _isDisposed;

    public LeakTracker(string slotType)
    {
        _slotType = slotType;
        _allocationStackTrace = Environment.StackTrace;
        Interlocked.Increment(ref s_activeTrackers);
    }

    public static long ActiveTrackers => Volatile.Read(ref s_activeTrackers);
    public static long TotalLeaks => Volatile.Read(ref s_totalLeaks);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            Interlocked.Decrement(ref s_activeTrackers);
            GC.SuppressFinalize(this);
        }
    }

    ~LeakTracker()
    {
        if (Volatile.Read(ref _isDisposed) == 0)
        {
            Interlocked.Increment(ref s_totalLeaks);
            System.Diagnostics.Debug.WriteLine(
                $"[LEAK DETECTED] PooledBufferSlot<{_slotType}> was not properly disposed.\n" +
                $"Allocation stack trace:\n{_allocationStackTrace}");
        }
    }
}
#endif

internal static class LeakDiagnostics
{
#if DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LeakTracker? CreateTracker<T>() => new(typeof(T).Name);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DisposeTracker(LeakTracker? tracker) => tracker?.Dispose();

    public static (long active, long totalLeaks) GetStats() =>
        (LeakTracker.ActiveTrackers, LeakTracker.TotalLeaks);
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? CreateTracker<T>() => null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DisposeTracker(object? tracker) { }

    public static (long active, long totalLeaks) GetStats() => (0, 0);
#endif
}
