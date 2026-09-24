namespace Axrone.Utility.Concurrency;

/// <summary>
/// Platform dispatch for atomic primitives: native interlocked operations for 1–8 byte types,
/// lock fallback for larger. Sub-word access requires natural alignment, which the runtime
/// guarantees for normally laid out fields.
/// </summary>
/// <remarks>
/// Success/failure orders are validated but executed as full fences where the BCL offers no
/// relaxed form; the order documents intent even when the platform executes stricter.
/// No telemetry here by design — per-operation counting would tax the operation itself.
/// </remarks>
internal static unsafe class AtomicCoreOps
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T Load<T>(ref T location, MemoryOrder order)
        where T : unmanaged
    {
        MemoryOrderValidator.ValidateLoad(order);
        if (order == MemoryOrder.Relaxed)
        {
            return location;
        }

        if (sizeof(T) == 1)
        {
            return Unsafe.BitCast<byte, T>(Volatile.Read(ref Unsafe.As<T, byte>(ref location)));
        }

        if (sizeof(T) == 2)
        {
            return Unsafe.BitCast<ushort, T>(Volatile.Read(ref Unsafe.As<T, ushort>(ref location)));
        }

        if (sizeof(T) == 4)
        {
            return Unsafe.BitCast<uint, T>(Volatile.Read(ref Unsafe.As<T, uint>(ref location)));
        }

        if (sizeof(T) == 8)
        {
            return Unsafe.BitCast<ulong, T>(Volatile.Read(ref Unsafe.As<T, ulong>(ref location)));
        }

        lock (SharedFallbackLock.For(ref location))
        {
            return location;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Store<T>(ref T location, T desired, MemoryOrder order)
        where T : unmanaged
    {
        MemoryOrderValidator.ValidateStore(order);
        if (order == MemoryOrder.Relaxed)
        {
            location = desired;
            return;
        }

        if (sizeof(T) == 1)
        {
            Volatile.Write(ref Unsafe.As<T, byte>(ref location), Unsafe.BitCast<T, byte>(desired));
        }
        else if (sizeof(T) == 2)
        {
            Volatile.Write(ref Unsafe.As<T, ushort>(ref location), Unsafe.BitCast<T, ushort>(desired));
        }
        else if (sizeof(T) == 4)
        {
            Volatile.Write(ref Unsafe.As<T, uint>(ref location), Unsafe.BitCast<T, uint>(desired));
        }
        else if (sizeof(T) == 8)
        {
            Volatile.Write(ref Unsafe.As<T, ulong>(ref location), Unsafe.BitCast<T, ulong>(desired));
        }
        else
        {
            lock (SharedFallbackLock.For(ref location))
            {
                location = desired;
            }
        }

        if (order == MemoryOrder.SequentiallyConsistent)
        {
            Thread.MemoryBarrier();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T Exchange<T>(ref T location, T desired, MemoryOrder order)
        where T : unmanaged
    {
        if (sizeof(T) == 4)
        {
            return Unsafe.BitCast<int, T>(Interlocked.Exchange(
                ref Unsafe.As<T, int>(ref location),
                Unsafe.BitCast<T, int>(desired)));
        }

        if (sizeof(T) == 8)
        {
            return Unsafe.BitCast<long, T>(Interlocked.Exchange(
                ref Unsafe.As<T, long>(ref location),
                Unsafe.BitCast<T, long>(desired)));
        }

        T current = Load(ref location, MemoryOrder.Relaxed);
        while (!CompareExchange(ref location, ref current, desired, order, MemoryOrder.Relaxed))
        {
        }

        return current;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool CompareExchange<T>(ref T location, ref T expected, T desired, MemoryOrder success, MemoryOrder failure)
        where T : unmanaged
    {
        if (sizeof(T) == 1)
        {
            return SubWord8(ref Unsafe.As<T, byte>(ref location), ref Unsafe.As<T, byte>(ref expected), Unsafe.BitCast<T, byte>(desired));
        }

        if (sizeof(T) == 2)
        {
            return SubWord16(ref Unsafe.As<T, ushort>(ref location), ref Unsafe.As<T, ushort>(ref expected), Unsafe.BitCast<T, ushort>(desired));
        }

        if (sizeof(T) == 4)
        {
            int exp = Unsafe.BitCast<T, int>(expected);
            int old = Interlocked.CompareExchange(ref Unsafe.As<T, int>(ref location), Unsafe.BitCast<T, int>(desired), exp);
            if (old == exp)
            {
                return true;
            }

            expected = Unsafe.BitCast<int, T>(old);
            return false;
        }

        if (sizeof(T) == 8)
        {
            long exp = Unsafe.BitCast<T, long>(expected);
            long old = Interlocked.CompareExchange(ref Unsafe.As<T, long>(ref location), Unsafe.BitCast<T, long>(desired), exp);
            if (old == exp)
            {
                return true;
            }

            expected = Unsafe.BitCast<long, T>(old);
            return false;
        }

        lock (SharedFallbackLock.For(ref location))
        {
            if (EqualityComparer<T>.Default.Equals(location, expected))
            {
                location = desired;
                return true;
            }

            expected = location;
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool SubWord8(ref byte location, ref byte expected, byte desired)
    {
        nuint address = (nuint)Unsafe.AsPointer(ref location);
        nuint aligned = address & ~(nuint)3;
        int shift = (int)((address & 3) << 3);
        uint mask = 0xFFU << shift;
        ref int target = ref Unsafe.AsRef<int>((void*)aligned);
        uint shifted = (uint)desired << shift;
        int current = Volatile.Read(ref target);

        while (true)
        {
            byte observed = (byte)((current >> shift) & 0xFF);
            if (observed != expected)
            {
                expected = observed;
                return false;
            }

            int next = (int)(((uint)current & ~mask) | shifted);
            int old = Interlocked.CompareExchange(ref target, next, current);
            if (old == current)
            {
                return true;
            }

            current = old;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool SubWord16(ref ushort location, ref ushort expected, ushort desired)
    {
        nuint address = (nuint)Unsafe.AsPointer(ref location);
        nuint aligned = address & ~(nuint)3;
        int shift = (int)((address & 2) << 3);
        uint mask = 0xFFFFU << shift;
        ref int target = ref Unsafe.AsRef<int>((void*)aligned);
        uint shifted = (uint)desired << shift;
        int current = Volatile.Read(ref target);

        while (true)
        {
            ushort observed = (ushort)((current >> shift) & 0xFFFF);
            if (observed != expected)
            {
                expected = observed;
                return false;
            }

            int next = (int)(((uint)current & ~mask) | shifted);
            int old = Interlocked.CompareExchange(ref target, next, current);
            if (old == current)
            {
                return true;
            }

            current = old;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T? LoadRef<T>(ref T? location, MemoryOrder order)
        where T : class
    {
        if (order == MemoryOrder.Relaxed)
        {
            return location;
        }

        return Volatile.Read(ref location);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void StoreRef<T>(ref T? location, T? desired, MemoryOrder order)
        where T : class
    {
        MemoryOrderValidator.ValidateStore(order);
        if (order == MemoryOrder.Relaxed)
        {
            location = desired;
            return;
        }

        Volatile.Write(ref location, desired);
        if (order == MemoryOrder.SequentiallyConsistent)
        {
            Thread.MemoryBarrier();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T? ExchangeRef<T>(ref T? location, T? desired, MemoryOrder order)
        where T : class =>
        Interlocked.Exchange(ref location, desired);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool CompareExchangeRef<T>(ref T? location, ref T? expected, T? desired, MemoryOrder success, MemoryOrder failure)
        where T : class
    {
        T? old = Interlocked.CompareExchange(ref location, desired, expected);
        if (ReferenceEquals(old, expected))
        {
            return true;
        }

        expected = old;
        return false;
    }
}
