namespace Axrone.Ecs;

/// <summary>
/// Cache-isolated frame counter.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct PaddedAtomicLong
{
    [FieldOffset(64)]
    private long _value;

    /// <summary>Current value.</summary>
    public long Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _value);
    }

    /// <summary>Increments.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Increment() => Interlocked.Increment(ref _value);

    /// <summary>Resets.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => Volatile.Write(ref _value, 0L);
}
