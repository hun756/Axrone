namespace Axrone.Utility.Concurrency;

using Axrone.Utility.Backoff.SpinPolicies;

/// <summary>
/// Blocking wait/notify rendezvous over shared roots. Spin/yield phases use the house backoff;
/// blocking uses OS wait-on-address for 4-byte locations on Windows, Monitor pulses elsewhere.
/// </summary>
/// <remarks>
/// Rendezvous is by address on the same roots the over-8-byte fallback locks use, so an update
/// and a wait on one location always meet. Like every address-keyed primitive, relocating the
/// location mid-wait (GC move of an unpinned field) can miss a wakeup: pin first for hard
/// guarantees. All waits re-check the condition, so spurious wakeups are harmless but a value
/// change without a matching notify never wakes — pair every observed store with a notify.
/// </remarks>
internal static class FutexEngine
{
    private const int SpinYieldIterations = 24;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Wait<T>(ref T location, T comparand, MemoryOrder order)
        where T : unmanaged
    {
        ProgressiveSpinBackoff.Initialize(out int backoff);
        for (int i = 0; i < SpinYieldIterations; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(AtomicCoreOps.Load(ref location, order), comparand))
            {
                return;
            }

            ProgressiveSpinBackoff.Advance(ref backoff);
        }

        while (true)
        {
            if (!EqualityComparer<T>.Default.Equals(AtomicCoreOps.Load(ref location, order), comparand))
            {
                return;
            }

            if (OsFutex.IsSupported && sizeof(T) == 4)
            {
                ref int address = ref Unsafe.As<T, int>(ref location);
                int expected = Unsafe.As<T, int>(ref comparand);
                if (Unsafe.As<T, int>(ref location) == expected)
                {
                    OsFutex.TryWait(ref address, ref expected, Timeout.Infinite);
                }

                continue;
            }

            BlockOnRoot<T>(ref location, comparand, order);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void WaitRef<T>(ref T? location, T? comparand, MemoryOrder order)
        where T : class
    {
        ProgressiveSpinBackoff.Initialize(out int backoff);
        for (int i = 0; i < SpinYieldIterations; i++)
        {
            if (!ReferenceEquals(AtomicCoreOps.LoadRef(ref location, order), comparand))
            {
                return;
            }

            ProgressiveSpinBackoff.Advance(ref backoff);
        }

        while (true)
        {
            if (!ReferenceEquals(AtomicCoreOps.LoadRef(ref location, order), comparand))
            {
                return;
            }

            BlockOnRootRef(ref location, comparand, order);
        }
    }

    private static unsafe void BlockOnRoot<T>(ref T location, T comparand, MemoryOrder order)
        where T : unmanaged
    {
        fixed (T* ptr = &location)
        {
            object root = SharedFallbackLock.ForAddress(ptr);
            lock (root)
            {
                if (!EqualityComparer<T>.Default.Equals(AtomicCoreOps.Load(ref location, order), comparand))
                {
                    return;
                }

                Monitor.Wait(root);
            }
        }
    }

    private static unsafe void BlockOnRootRef<T>(ref T? location, T? comparand, MemoryOrder order)
        where T : class
    {
        void* address = Unsafe.AsPointer(ref Unsafe.AsRef(in location));
        object root = SharedFallbackLock.ForAddress(address);
        lock (root)
        {
            if (!ReferenceEquals(AtomicCoreOps.LoadRef(ref location, order), comparand))
            {
                return;
            }

            Monitor.Wait(root);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void NotifyOne<T>(ref T location)
        where T : unmanaged
    {
        fixed (T* ptr = &location)
        {
            if (OsFutex.IsSupported && sizeof(T) == 4)
            {
                try
                {
                    OsFutex.WakeOne(ref Unsafe.As<T, int>(ref location));
                }
                catch (Exception)
                {
                    // OS wake is best-effort; the Monitor pulse below still reaches fallback waiters.
                }
            }

            object root = SharedFallbackLock.ForAddress(ptr);
            lock (root)
            {
                Monitor.Pulse(root);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void NotifyAll<T>(ref T location)
        where T : unmanaged
    {
        fixed (T* ptr = &location)
        {
            if (OsFutex.IsSupported && sizeof(T) == 4)
            {
                try
                {
                    OsFutex.WakeAll(ref Unsafe.As<T, int>(ref location));
                }
                catch (Exception)
                {
                    // OS wake is best-effort; the Monitor pulse below still reaches fallback waiters.
                }
            }

            object root = SharedFallbackLock.ForAddress(ptr);
            lock (root)
            {
                Monitor.PulseAll(root);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void NotifyOneRef<T>(ref T? location)
        where T : class
    {
        void* address = Unsafe.AsPointer(ref Unsafe.AsRef(in location));
        object root = SharedFallbackLock.ForAddress(address);
        lock (root)
        {
            Monitor.Pulse(root);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void NotifyAllRef<T>(ref T? location)
        where T : class
    {
        void* address = Unsafe.AsPointer(ref Unsafe.AsRef(in location));
        object root = SharedFallbackLock.ForAddress(address);
        lock (root)
        {
            Monitor.PulseAll(root);
        }
    }
}
