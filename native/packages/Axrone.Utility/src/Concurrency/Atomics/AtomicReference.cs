namespace Axrone.Utility.Concurrency;

/// <summary>
/// Atomic reference for class types. A class, so copies share the location — unlike the
/// value-type atomics, aliasing is safe here.
/// </summary>
/// <typeparam name="T">Reference type.</typeparam>
public sealed class AtomicReference<T> : IAtomicWaitNotify<T?>
    where T : class
{
    private T? _value;

    /// <summary>Wraps an initial value.</summary>
    public AtomicReference(T? value = null) => _value = value;

    /// <summary>Current value (sequentially consistent).</summary>
    public T? Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Load();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Store(value);
    }

    /// <summary>Reference CAS is always lock-free.</summary>
    public bool IsLockFree => true;

    /// <summary>Reads the reference.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T? Load(MemoryOrder order = MemoryOrder.SequentiallyConsistent) => AtomicCoreOps.LoadRef(ref _value, order);

    /// <summary>Writes the reference.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Store(T? desired, MemoryOrder order = MemoryOrder.SequentiallyConsistent) =>
        AtomicCoreOps.StoreRef(ref _value, desired, order);

    /// <summary>Writes and returns the previous reference.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T? Exchange(T? desired, MemoryOrder order = MemoryOrder.SequentiallyConsistent) =>
        AtomicCoreOps.ExchangeRef(ref _value, desired, order);

    /// <summary>Conditional write; refreshes <paramref name="expected"/> on mismatch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool CompareExchange(ref T? expected, T? desired, MemoryOrder success = MemoryOrder.SequentiallyConsistent, MemoryOrder failure = MemoryOrder.SequentiallyConsistent) =>
        AtomicCoreOps.CompareExchangeRef(ref _value, ref expected, desired, success, failure);

    /// <summary>Lock-free update: the factory receives the current reference and returns the next.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T? UpdateAndGet(Func<T?, T?> updateFactory)
    {
        ArgumentNullException.ThrowIfNull(updateFactory);

        T? current = Load(MemoryOrder.Relaxed);
        while (true)
        {
            T? next = updateFactory(current);
            if (CompareExchange(ref current, next))
            {
                return next;
            }
        }
    }

    /// <inheritdoc/>
    public void Wait(T? comparand, MemoryOrder order = MemoryOrder.SequentiallyConsistent) =>
        FutexEngine.WaitRef(ref _value, comparand, order);

    /// <inheritdoc/>
    public void NotifyOne() => FutexEngine.NotifyOneRef(ref _value);

    /// <inheritdoc/>
    public void NotifyAll() => FutexEngine.NotifyAllRef(ref _value);

    /// <inheritdoc/>
    public override string ToString() => Load()?.ToString() ?? "null";

    /// <summary>Reads the reference.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator T?(AtomicReference<T> atomic) => atomic.Load();
}
