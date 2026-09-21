namespace Axrone.Utility.Concurrency;

/// <summary>
/// Lightweight boolean flag with test-and-set/clear, mirroring std::atomic_flag.
/// </summary>
/// <remarks>
/// Same copy discipline as <see cref="Atomic{T}"/>: share fields through <see cref="AtomicRef{T}"/> instead.
/// </remarks>
public struct AtomicFlag
{
    private byte _flag;

    /// <summary>Creates a flag.</summary>
    public AtomicFlag(bool initialState = false) => _flag = initialState ? (byte)1 : (byte)0;

    /// <summary>Sets the flag, returning the previous value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TestAndSet(MemoryOrder order = MemoryOrder.SequentiallyConsistent)
    {
        byte expected = 0;
        return !AtomicCoreOps.CompareExchange(ref _flag, ref expected, (byte)1, order, order);
    }

    /// <summary>Clears the flag.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(MemoryOrder order = MemoryOrder.SequentiallyConsistent) =>
        AtomicCoreOps.Store(ref _flag, (byte)0, order);

    /// <summary>Reads the flag.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Test(MemoryOrder order = MemoryOrder.SequentiallyConsistent) =>
        AtomicCoreOps.Load(ref _flag, order) != 0;

    /// <inheritdoc/>
    public override string ToString() => Test().ToString();
}
