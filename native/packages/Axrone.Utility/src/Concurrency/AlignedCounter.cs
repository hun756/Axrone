namespace Axrone.Utility;

/// <summary>
/// A cache-line-aligned 64-bit atomic counter that avoids false sharing
/// by isolating the backing storage on its own 128-byte aligned allocation.
/// </summary>
public sealed unsafe class AlignedCounter : IDisposable
{
    private long* _value;
    private DisposalTracker _tracker;

    public long Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref *_value);
    }

    public AlignedCounter(long initialValue = 0)
    {
        _value = (long*)NativeMemory.AlignedAlloc(128, 128);
        NativeMemory.Clear(_value, 128);
        *_value = initialValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Increment() => Interlocked.Increment(ref *_value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Add(long value) => Interlocked.Add(ref *_value, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CompareExchange(long expected, long next) => Interlocked.CompareExchange(ref *_value, next, expected) == expected;

    ~AlignedCounter()
    {
        Release();
    }

    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    private void Release()
    {
        if (_tracker.TryDispose())
        {
            long* ptr = _value;
            _value = null;
            if (ptr != null)
            {
                NativeMemory.AlignedFree(ptr);
            }
        }
    }
}
