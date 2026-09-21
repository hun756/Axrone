namespace Axrone.Utility.Concurrency;

/// <summary>
/// Byte-backed atomic boolean with compare exchange.
/// </summary>
/// <remarks>
/// Same copy discipline as <see cref="Atomic{T}"/>: share fields through <see cref="AtomicRef{T}"/> instead.
/// </remarks>
public struct AtomicBoolean : IAtomic<bool>
{
    private byte _value;

    /// <summary>Wraps an initial value.</summary>
    public AtomicBoolean(bool value) => _value = value ? (byte)1 : (byte)0;

    /// <inheritdoc/>
    public bool Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Load();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Store(value);
    }

    /// <inheritdoc/>
    public bool IsLockFree => true;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Load(MemoryOrder order = MemoryOrder.SequentiallyConsistent) =>
        AtomicCoreOps.Load(ref _value, order) != 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Store(bool desired, MemoryOrder order = MemoryOrder.SequentiallyConsistent) =>
        AtomicCoreOps.Store(ref _value, desired ? (byte)1 : (byte)0, order);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Exchange(bool desired, MemoryOrder order = MemoryOrder.SequentiallyConsistent) =>
        AtomicCoreOps.Exchange(ref _value, desired ? (byte)1 : (byte)0, order) != 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool CompareExchange(ref bool expected, bool desired, MemoryOrder order = MemoryOrder.SequentiallyConsistent)
    {
        byte exp = expected ? (byte)1 : (byte)0;
        bool ok = AtomicCoreOps.CompareExchange(ref _value, ref exp, desired ? (byte)1 : (byte)0, order, order);
        expected = exp != 0;
        return ok;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool CompareExchange(ref bool expected, bool desired, MemoryOrder success, MemoryOrder failure)
    {
        byte exp = expected ? (byte)1 : (byte)0;
        bool ok = AtomicCoreOps.CompareExchange(ref _value, ref exp, desired ? (byte)1 : (byte)0, success, failure);
        expected = exp != 0;
        return ok;
    }

    /// <inheritdoc/>
    public override string ToString() => Load().ToString();

    /// <summary>Reads the value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator bool(AtomicBoolean atomic) => atomic.Load();
}
