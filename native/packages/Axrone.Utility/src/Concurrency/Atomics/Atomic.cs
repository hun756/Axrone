namespace Axrone.Utility.Concurrency;

/// <summary>
/// Generic atomic over any unmanaged value type. Lock-free for sizes up to 8 bytes.
/// </summary>
/// <remarks>
/// A struct holding the value: copying an <see cref="Atomic{T}"/> forks the location — the copy
/// and the original diverge silently. Never pass by value, capture, or duplicate. For shared
/// fields prefer <see cref="AtomicRef{T}"/>, which overlays the field and stays atomic across
/// copies because copies share the reference.
/// </remarks>
/// <typeparam name="T">Value type.</typeparam>
public struct Atomic<T> : IAtomic<T>
    where T : unmanaged
{
    private T _value;

    /// <summary>Wraps an initial value.</summary>
    public Atomic(T value) => _value = value;

    /// <inheritdoc/>
    public T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Load();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Store(value);
    }

    /// <inheritdoc/>
    public bool IsLockFree
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Unsafe.SizeOf<T>() <= 8;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T Load(MemoryOrder order = MemoryOrder.SequentiallyConsistent) => AtomicCoreOps.Load(ref _value, order);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Store(T desired, MemoryOrder order = MemoryOrder.SequentiallyConsistent) =>
        AtomicCoreOps.Store(ref _value, desired, order);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T Exchange(T desired, MemoryOrder order = MemoryOrder.SequentiallyConsistent) =>
        AtomicCoreOps.Exchange(ref _value, desired, order);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool CompareExchange(ref T expected, T desired, MemoryOrder order = MemoryOrder.SequentiallyConsistent) =>
        AtomicCoreOps.CompareExchange(ref _value, ref expected, desired, order, order);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool CompareExchange(ref T expected, T desired, MemoryOrder success, MemoryOrder failure) =>
        AtomicCoreOps.CompareExchange(ref _value, ref expected, desired, success, failure);

    /// <summary>Zero-allocation CAS loop computing the next value from the current one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T TransformAndGet<TTransformer>()
        where TTransformer : struct, IAtomicTransformer<T, T>
    {
        T current = Load(MemoryOrder.Relaxed);
        while (true)
        {
            T next = TTransformer.Transform(in current);
            if (CompareExchange(ref current, next, MemoryOrder.SequentiallyConsistent, MemoryOrder.Relaxed))
            {
                return next;
            }
        }
    }

    /// <summary>Zero-allocation CAS loop combining the current value with an argument.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T MutateAndGet<TMutator>(in T argument)
        where TMutator : struct, IAtomicMutator<T>
    {
        T current = Load(MemoryOrder.Relaxed);
        while (true)
        {
            T next = TMutator.Mutate(in current, in argument);
            if (CompareExchange(ref current, next, MemoryOrder.SequentiallyConsistent, MemoryOrder.Relaxed))
            {
                return next;
            }
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Load().ToString() ?? string.Empty;

    /// <summary>Reads the value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator T(Atomic<T> atomic) => atomic.Load();
}
