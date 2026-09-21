namespace Axrone.Utility.Concurrency;

/// <summary>
/// Atomic numeric with arithmetic and bitwise operations. Lock-free for sizes up to 8 bytes.
/// </summary>
/// <remarks>
/// Same copy discipline as <see cref="Atomic{T}"/>: the value lives in the struct, so copies
/// diverge. Share fields through <see cref="AtomicRef{T}"/> instead.
/// Add/Subtract/And/Or/Xor return the new value. Native-width integers use single-instruction
/// interlocked operations; everything else falls back to a CAS loop.
/// </remarks>
/// <typeparam name="T">Numeric value type.</typeparam>
public struct AtomicNumber<T> : IAtomicNumber<T>, IAtomicBitwise<T>
    where T : unmanaged, INumber<T>, IBitwiseOperators<T, T, T>
{
    private T _value;

    /// <summary>Wraps an initial value.</summary>
    public AtomicNumber(T value) => _value = value;

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

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T Add(T value, MemoryOrder order = MemoryOrder.SequentiallyConsistent)
    {
        if (typeof(T) == typeof(int))
        {
            int addend = Unsafe.BitCast<T, int>(value);
            return Unsafe.BitCast<int, T>(Interlocked.Add(ref Unsafe.As<T, int>(ref _value), addend));
        }

        if (typeof(T) == typeof(long))
        {
            long addend = Unsafe.BitCast<T, long>(value);
            return Unsafe.BitCast<long, T>(Interlocked.Add(ref Unsafe.As<T, long>(ref _value), addend));
        }

        if (typeof(T) == typeof(uint))
        {
            uint addend = Unsafe.BitCast<T, uint>(value);
            return Unsafe.BitCast<uint, T>(Interlocked.Add(ref Unsafe.As<T, uint>(ref _value), addend));
        }

        if (typeof(T) == typeof(ulong))
        {
            ulong addend = Unsafe.BitCast<T, ulong>(value);
            return Unsafe.BitCast<ulong, T>(Interlocked.Add(ref Unsafe.As<T, ulong>(ref _value), addend));
        }

        T current = Load(MemoryOrder.Relaxed);
        while (true)
        {
            T next = current + value;
            if (CompareExchange(ref current, next, order, MemoryOrder.Relaxed))
            {
                return next;
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T Subtract(T value, MemoryOrder order = MemoryOrder.SequentiallyConsistent)
    {
        // Negating MinValue overflows, so those go through the CAS fallback below.
        if (typeof(T) == typeof(int))
        {
            int subtrahend = Unsafe.BitCast<T, int>(value);
            if (subtrahend != int.MinValue)
            {
                return Unsafe.BitCast<int, T>(Interlocked.Add(ref Unsafe.As<T, int>(ref _value), -subtrahend));
            }
        }

        if (typeof(T) == typeof(long))
        {
            long subtrahend = Unsafe.BitCast<T, long>(value);
            if (subtrahend != long.MinValue)
            {
                return Unsafe.BitCast<long, T>(Interlocked.Add(ref Unsafe.As<T, long>(ref _value), -subtrahend));
            }
        }

        T current = Load(MemoryOrder.Relaxed);
        while (true)
        {
            T next = current - value;
            if (CompareExchange(ref current, next, order, MemoryOrder.Relaxed))
            {
                return next;
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Increment() => Add(T.One);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Decrement() => Subtract(T.One);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T And(T mask, MemoryOrder order = MemoryOrder.SequentiallyConsistent)
    {
        if (typeof(T) == typeof(int))
        {
            int bits = Unsafe.BitCast<T, int>(mask);
            return Unsafe.BitCast<int, T>(Interlocked.And(ref Unsafe.As<T, int>(ref _value), bits) & bits);
        }

        if (typeof(T) == typeof(long))
        {
            long bits = Unsafe.BitCast<T, long>(mask);
            return Unsafe.BitCast<long, T>(Interlocked.And(ref Unsafe.As<T, long>(ref _value), bits) & bits);
        }

        T current = Load(MemoryOrder.Relaxed);
        while (true)
        {
            T next = current & mask;
            if (CompareExchange(ref current, next, order, MemoryOrder.Relaxed))
            {
                return next;
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T Or(T mask, MemoryOrder order = MemoryOrder.SequentiallyConsistent)
    {
        if (typeof(T) == typeof(int))
        {
            int bits = Unsafe.BitCast<T, int>(mask);
            return Unsafe.BitCast<int, T>(Interlocked.Or(ref Unsafe.As<T, int>(ref _value), bits) | bits);
        }

        if (typeof(T) == typeof(long))
        {
            long bits = Unsafe.BitCast<T, long>(mask);
            return Unsafe.BitCast<long, T>(Interlocked.Or(ref Unsafe.As<T, long>(ref _value), bits) | bits);
        }

        T current = Load(MemoryOrder.Relaxed);
        while (true)
        {
            T next = current | mask;
            if (CompareExchange(ref current, next, order, MemoryOrder.Relaxed))
            {
                return next;
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T Xor(T mask, MemoryOrder order = MemoryOrder.SequentiallyConsistent)
    {
        T current = Load(MemoryOrder.Relaxed);
        while (true)
        {
            T next = current ^ mask;
            if (CompareExchange(ref current, next, order, MemoryOrder.Relaxed))
            {
                return next;
            }
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Load().ToString() ?? string.Empty;

    /// <summary>Reads the value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator T(AtomicNumber<T> atomic) => atomic.Load();

    /// <summary>Increments and returns the struct (assign back to keep the value).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AtomicNumber<T> operator ++(AtomicNumber<T> atomic)
    {
        atomic.Increment();
        return atomic;
    }

    /// <summary>Decrements and returns the struct (assign back to keep the value).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AtomicNumber<T> operator --(AtomicNumber<T> atomic)
    {
        atomic.Decrement();
        return atomic;
    }

    /// <summary>Adds and returns the new value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T operator +(AtomicNumber<T> atomic, T value) => atomic.Add(value);

    /// <summary>Subtracts and returns the new value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T operator -(AtomicNumber<T> atomic, T value) => atomic.Subtract(value);
}
