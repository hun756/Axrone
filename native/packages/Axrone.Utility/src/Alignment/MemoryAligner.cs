namespace Axrone.Utility.Alignment;

public static unsafe class MemoryAligner
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint AlignUp(nuint address, Alignment alignment) => alignment.AlignUp(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint AlignDown(nuint address, Alignment alignment) => alignment.AlignDown(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAligned(nuint address, Alignment alignment) => alignment.IsAligned(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* AlignUp(void* p, Alignment alignment) => alignment.AlignUp(p);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* AlignDown(void* p, Alignment alignment) => alignment.AlignDown(p);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAligned(void* p, Alignment alignment) => alignment.IsAligned(p);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong AlignUp(ulong address, Alignment alignment) => alignment.AlignUp(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong AlignDown(ulong address, Alignment alignment) => alignment.AlignDown(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAligned(ulong address, Alignment alignment) => alignment.IsAligned(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AlignUp(long address, Alignment alignment) => alignment.AlignUp(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AlignDown(long address, Alignment alignment) => alignment.AlignDown(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAligned(long address, Alignment alignment) => alignment.IsAligned(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint AlignUp(nint address, Alignment alignment) => alignment.AlignUp(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint AlignDown(nint address, Alignment alignment) => alignment.AlignDown(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAligned(nint address, Alignment alignment) => alignment.IsAligned(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint AlignUp<TPolicy>(nuint address) where TPolicy : struct, IAlignmentPolicy<TPolicy> =>
        TPolicy.AlignUp(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint AlignDown<TPolicy>(nuint address) where TPolicy : struct, IAlignmentPolicy<TPolicy> =>
        TPolicy.AlignDown(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAligned<TPolicy>(nuint address) where TPolicy : struct, IAlignmentPolicy<TPolicy> =>
        TPolicy.IsAligned(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Alignment GetNaturalAlignment<T>() where T : unmanaged
    {
        AlignmentDetector<T> probe = default;
        nuint naturalAlignment = (nuint)((byte*)Unsafe.AsPointer(ref probe.Value) - (byte*)Unsafe.AsPointer(ref probe.Dummy));
        return new Alignment((uint)naturalAlignment);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AlignmentDetector<T> where T : unmanaged
    {
        public byte Dummy;
        public T Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static AlignedSpanPartition<T> Partition<T>(Span<T> source, Alignment targetAlignment) where T : struct
    {
        if (source.IsEmpty)
        {
            return default;
        }

        nuint elementSize = (nuint)Unsafe.SizeOf<T>();
        ref T origin = ref MemoryMarshal.GetReference(source);
        nuint address = (nuint)Unsafe.AsPointer(ref origin);

        int prefixCount = 0;
        int length = source.Length;

        if (!targetAlignment.IsAligned(address))
        {
            if (BitOperations.IsPow2((uint)elementSize))
            {
                nuint mask = targetAlignment.Mask;
                nuint misalignedBytes = address & mask;
                nuint bytesNeeded = targetAlignment.Value - misalignedBytes;

                if (bytesNeeded % elementSize == 0)
                {
                    prefixCount = checked((int)(bytesNeeded / elementSize));
                }
                else
                {
                    prefixCount = ScanForFirstAlignedElement<T>(ref origin, length, targetAlignment);
                }
            }
            else
            {
                prefixCount = ScanForFirstAlignedElement<T>(ref origin, length, targetAlignment);
            }
        }

        if (prefixCount >= length)
        {
            return new AlignedSpanPartition<T>(source, Span<T>.Empty, Span<T>.Empty);
        }

        int remaining = length - prefixCount;
        nuint bodyBytes = ((nuint)remaining * elementSize) & ~targetAlignment.Mask;
        int bodyCount = checked((int)(bodyBytes / elementSize));
        int postfixCount = remaining - bodyCount;

        return new AlignedSpanPartition<T>(
            source.Slice(0, prefixCount),
            source.Slice(prefixCount, bodyCount),
            source.Slice(prefixCount + bodyCount, postfixCount)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static ReadOnlyAlignedSpanPartition<T> Partition<T>(ReadOnlySpan<T> source, Alignment targetAlignment) where T : struct
    {
        Span<T> mutable = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(source), source.Length);
        AlignedSpanPartition<T> partition = Partition(mutable, targetAlignment);
        return new ReadOnlyAlignedSpanPartition<T>(partition.Prefix, partition.AlignedBody, partition.Postfix);
    }

    private static int ScanForFirstAlignedElement<T>(ref T origin, int length, Alignment alignment) where T : struct
    {
        for (int i = 0; i < length; i++)
        {
            nuint addr = (nuint)Unsafe.AsPointer(ref Unsafe.Add(ref origin, i));
            if (alignment.IsAligned(addr))
            {
                return i;
            }
        }
        return length;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool AreAllAligned(Alignment alignment, params ReadOnlySpan<nuint> addresses)
    {
        if (addresses.IsEmpty)
        {
            return true;
        }

        ref nuint src = ref MemoryMarshal.GetReference(addresses);
        nuint count = (nuint)addresses.Length;
        nuint mask = alignment.Mask;
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && count >= (nuint)Vector512<nuint>.Count)
        {
            Vector512<nuint> vMask = Vector512.Create(mask);
            Vector512<nuint> vZero = Vector512<nuint>.Zero;
            nuint step = (nuint)Vector512<nuint>.Count;

            while (i <= count - step)
            {
                Vector512<nuint> v = Vector512.LoadUnsafe(ref src, i);
                if ((v & vMask) != vZero)
                {
                    return false;
                }
                i += step;
            }
        }

        if (Vector256.IsHardwareAccelerated && (count - i) >= (nuint)Vector256<nuint>.Count)
        {
            Vector256<nuint> vMask = Vector256.Create(mask);
            Vector256<nuint> vZero = Vector256<nuint>.Zero;
            nuint step = (nuint)Vector256<nuint>.Count;

            while (i <= count - step)
            {
                Vector256<nuint> v = Vector256.LoadUnsafe(ref src, i);
                if ((v & vMask) != vZero)
                {
                    return false;
                }
                i += step;
            }
        }

        if (Vector128.IsHardwareAccelerated && (count - i) >= (nuint)Vector128<nuint>.Count)
        {
            Vector128<nuint> vMask = Vector128.Create(mask);
            Vector128<nuint> vZero = Vector128<nuint>.Zero;
            nuint step = (nuint)Vector128<nuint>.Count;

            while (i <= count - step)
            {
                Vector128<nuint> v = Vector128.LoadUnsafe(ref src, i);
                if ((v & vMask) != vZero)
                {
                    return false;
                }
                i += step;
            }
        }

        while (i + 3 < count)
        {
            nuint acc = (Unsafe.Add(ref src, i) & mask)
                      | (Unsafe.Add(ref src, i + 1) & mask)
                      | (Unsafe.Add(ref src, i + 2) & mask)
                      | (Unsafe.Add(ref src, i + 3) & mask);

            if (acc != 0)
            {
                return false;
            }
            i += 4;
        }

        while (i < count)
        {
            if ((Unsafe.Add(ref src, i) & mask) != 0)
            {
                return false;
            }
            i++;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void BatchAlignUp(ReadOnlySpan<nuint> sources, Span<nuint> destinations, Alignment alignment)
    {
        if (destinations.Length < sources.Length)
        {
            ThrowHelper.ThrowDestinationTooShort();
        }

        if (sources.IsEmpty)
        {
            return;
        }

        ref nuint src = ref MemoryMarshal.GetReference(sources);
        ref nuint dst = ref MemoryMarshal.GetReference(destinations);
        nuint count = (nuint)sources.Length;
        nuint mask = alignment.Mask;
        nuint notMask = ~mask;
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && count >= (nuint)Vector512<nuint>.Count)
        {
            Vector512<nuint> vMask = Vector512.Create(mask);
            Vector512<nuint> vNotMask = Vector512.Create(notMask);
            nuint step = (nuint)Vector512<nuint>.Count;

            while (i <= count - step)
            {
                Vector512<nuint> v = Vector512.LoadUnsafe(ref src, i);
                Vector512<nuint> res = (v + vMask) & vNotMask;
                res.StoreUnsafe(ref dst, i);
                i += step;
            }
        }

        if (Vector256.IsHardwareAccelerated && (count - i) >= (nuint)Vector256<nuint>.Count)
        {
            Vector256<nuint> vMask = Vector256.Create(mask);
            Vector256<nuint> vNotMask = Vector256.Create(notMask);
            nuint step = (nuint)Vector256<nuint>.Count;

            while (i <= count - step)
            {
                Vector256<nuint> v = Vector256.LoadUnsafe(ref src, i);
                Vector256<nuint> res = (v + vMask) & vNotMask;
                res.StoreUnsafe(ref dst, i);
                i += step;
            }
        }

        if (Vector128.IsHardwareAccelerated && (count - i) >= (nuint)Vector128<nuint>.Count)
        {
            Vector128<nuint> vMask = Vector128.Create(mask);
            Vector128<nuint> vNotMask = Vector128.Create(notMask);
            nuint step = (nuint)Vector128<nuint>.Count;

            while (i <= count - step)
            {
                Vector128<nuint> v = Vector128.LoadUnsafe(ref src, i);
                Vector128<nuint> res = (v + vMask) & vNotMask;
                res.StoreUnsafe(ref dst, i);
                i += step;
            }
        }

        while (i + 3 < count)
        {
            Unsafe.Add(ref dst, i) = (Unsafe.Add(ref src, i) + mask) & notMask;
            Unsafe.Add(ref dst, i + 1) = (Unsafe.Add(ref src, i + 1) + mask) & notMask;
            Unsafe.Add(ref dst, i + 2) = (Unsafe.Add(ref src, i + 2) + mask) & notMask;
            Unsafe.Add(ref dst, i + 3) = (Unsafe.Add(ref src, i + 3) + mask) & notMask;
            i += 4;
        }

        while (i < count)
        {
            Unsafe.Add(ref dst, i) = (Unsafe.Add(ref src, i) + mask) & notMask;
            i++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void BatchAlignDown(ReadOnlySpan<nuint> sources, Span<nuint> destinations, Alignment alignment)
    {
        if (destinations.Length < sources.Length)
        {
            ThrowHelper.ThrowDestinationTooShort();
        }

        if (sources.IsEmpty)
        {
            return;
        }

        ref nuint src = ref MemoryMarshal.GetReference(sources);
        ref nuint dst = ref MemoryMarshal.GetReference(destinations);
        nuint count = (nuint)sources.Length;
        nuint notMask = ~alignment.Mask;
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && count >= (nuint)Vector512<nuint>.Count)
        {
            Vector512<nuint> vNotMask = Vector512.Create(notMask);
            nuint step = (nuint)Vector512<nuint>.Count;

            while (i <= count - step)
            {
                Vector512<nuint> v = Vector512.LoadUnsafe(ref src, i);
                (v & vNotMask).StoreUnsafe(ref dst, i);
                i += step;
            }
        }

        if (Vector256.IsHardwareAccelerated && (count - i) >= (nuint)Vector256<nuint>.Count)
        {
            Vector256<nuint> vNotMask = Vector256.Create(notMask);
            nuint step = (nuint)Vector256<nuint>.Count;

            while (i <= count - step)
            {
                Vector256<nuint> v = Vector256.LoadUnsafe(ref src, i);
                (v & vNotMask).StoreUnsafe(ref dst, i);
                i += step;
            }
        }

        if (Vector128.IsHardwareAccelerated && (count - i) >= (nuint)Vector128<nuint>.Count)
        {
            Vector128<nuint> vNotMask = Vector128.Create(notMask);
            nuint step = (nuint)Vector128<nuint>.Count;

            while (i <= count - step)
            {
                Vector128<nuint> v = Vector128.LoadUnsafe(ref src, i);
                (v & vNotMask).StoreUnsafe(ref dst, i);
                i += step;
            }
        }

        while (i + 3 < count)
        {
            Unsafe.Add(ref dst, i) = Unsafe.Add(ref src, i) & notMask;
            Unsafe.Add(ref dst, i + 1) = Unsafe.Add(ref src, i + 1) & notMask;
            Unsafe.Add(ref dst, i + 2) = Unsafe.Add(ref src, i + 2) & notMask;
            Unsafe.Add(ref dst, i + 3) = Unsafe.Add(ref src, i + 3) & notMask;
            i += 4;
        }

        while (i < count)
        {
            Unsafe.Add(ref dst, i) = Unsafe.Add(ref src, i) & notMask;
            i++;
        }
    }
}
