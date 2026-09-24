namespace Axrone.Utility.Concurrency;

/// <summary>
/// By-ref atomic overlay over an existing field. Stack-only; copies share the reference, so
/// unlike <see cref="Atomic{T}"/> a copied <see cref="AtomicRef{T}"/> stays atomic.
/// </summary>
/// <typeparam name="T">Value type.</typeparam>
public readonly ref struct AtomicRef<T> : IAtomicWaitNotify<T>
    where T : unmanaged
{
    private readonly ref T _location;

    /// <summary>Overlays the field.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AtomicRef(ref T location) => _location = ref location;

    /// <summary>Whether the location updates without locks.</summary>
    public bool IsLockFree
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Unsafe.SizeOf<T>() <= 8;
    }

    /// <summary>Reads the field.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T Load(MemoryOrder order = MemoryOrder.SequentiallyConsistent) => AtomicCoreOps.Load(ref _location, order);

    /// <summary>Writes the field.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Store(T desired, MemoryOrder order = MemoryOrder.SequentiallyConsistent) =>
        AtomicCoreOps.Store(ref _location, desired, order);

    /// <summary>Writes and returns the previous value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T Exchange(T desired, MemoryOrder order = MemoryOrder.SequentiallyConsistent) =>
        AtomicCoreOps.Exchange(ref _location, desired, order);

    /// <summary>Conditional write; refreshes <paramref name="expected"/> on mismatch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool CompareExchange(ref T expected, T desired, MemoryOrder order = MemoryOrder.SequentiallyConsistent) =>
        AtomicCoreOps.CompareExchange(ref _location, ref expected, desired, order, order);

    /// <summary>Conditional write with split success/failure orders; refreshes <paramref name="expected"/> on mismatch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool CompareExchange(ref T expected, T desired, MemoryOrder success, MemoryOrder failure) =>
        AtomicCoreOps.CompareExchange(ref _location, ref expected, desired, success, failure);

    /// <inheritdoc/>
    public void Wait(T comparand, MemoryOrder order = MemoryOrder.SequentiallyConsistent) =>
        FutexEngine.Wait(ref _location, comparand, order);

    /// <inheritdoc/>
    public ValueTask WaitAsync(T comparand, CancellationToken cancellationToken = default) =>
        FutexEngine.WaitAsync(ref _location, comparand, cancellationToken);

    /// <inheritdoc/>
    public void NotifyOne() => FutexEngine.NotifyOne(ref _location);

    /// <inheritdoc/>
    public void NotifyAll() => FutexEngine.NotifyAll(ref _location);
}
