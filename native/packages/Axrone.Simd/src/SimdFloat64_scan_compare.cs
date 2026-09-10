namespace Axrone.Simd;

public static unsafe partial class SimdFloat64
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstGreaterThan(ReadOnlySpan<double> source, double threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return -1;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vThresh = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<double> mask = Vector512.GreaterThan(Vector512.LoadUnsafe(in src, i), vThresh);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                if (bits != 0)
                {
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
                }
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vThresh = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<double> mask = Vector256.GreaterThan(Vector256.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                if (bits != 0)
                {
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vThresh = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<double> mask = Vector128.GreaterThan(Vector128.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                if (bits != 0)
                {
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
                }
            }
        }

        for (; i < length; ++i)
        {
            if (Unsafe.Add(ref src, (nint)i) > threshold)
            {
                return (int)i;
            }
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountGreaterThan(ReadOnlySpan<double> source, double threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint count = 0;
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vThresh = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<double> mask = Vector512.GreaterThan(Vector512.LoadUnsafe(in src, i), vThresh);
                count += (nuint)BitOperations.PopCount(Vector512.ExtractMostSignificantBits(mask));
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vThresh = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<double> mask = Vector256.GreaterThan(Vector256.LoadUnsafe(in src, i), vThresh);
                count += (nuint)BitOperations.PopCount(Vector256.ExtractMostSignificantBits(mask));
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vThresh = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<double> mask = Vector128.GreaterThan(Vector128.LoadUnsafe(in src, i), vThresh);
                count += (nuint)BitOperations.PopCount(Vector128.ExtractMostSignificantBits(mask));
            }
        }

        for (; i < length; ++i)
        {
            if (Unsafe.Add(ref src, (nint)i) > threshold)
            {
                count++;
            }
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterGreaterThan(ReadOnlySpan<double> source, double threshold, Span<double> destination)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return ScanResult.Success(0);

        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint dstCap = (nuint)destination.Length;
        nuint written = 0;
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vThresh = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;

            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;

            for (; i < safeLimit; i += step)
            {
                Vector512<double> mask = Vector512.GreaterThan(Vector512.LoadUnsafe(in src, i), vThresh);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }

            for (; i < limit; i += step)
            {
                Vector512<double> mask = Vector512.GreaterThan(Vector512.LoadUnsafe(in src, i), vThresh);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    if (written >= dstCap) return ScanResult.Overflow(written);
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vThresh = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;

            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;

            for (; i < safeLimit; i += step)
            {
                Vector256<double> mask = Vector256.GreaterThan(Vector256.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }

            for (; i < limit; i += step)
            {
                Vector256<double> mask = Vector256.GreaterThan(Vector256.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    if (written >= dstCap) return ScanResult.Overflow(written);
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vThresh = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;

            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;

            for (; i < safeLimit; i += step)
            {
                Vector128<double> mask = Vector128.GreaterThan(Vector128.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }

            for (; i < limit; i += step)
            {
                Vector128<double> mask = Vector128.GreaterThan(Vector128.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    if (written >= dstCap) return ScanResult.Overflow(written);
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }
        }

        for (; i < length; ++i)
        {
            double val = Unsafe.Add(ref src, (nint)i);
            if (val > threshold)
            {
                if (written >= dstCap) return ScanResult.Overflow(written);
                Unsafe.Add(ref dst, (nint)written++) = val;
            }
        }

        return ScanResult.Success(written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstLessThan(ReadOnlySpan<double> source, double threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return -1;
        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vT = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                ulong bits = Vector512.ExtractMostSignificantBits(Vector512.LessThan(Vector512.LoadUnsafe(in src, i), vT));
                if (bits != 0) return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vT = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                uint bits = Vector256.ExtractMostSignificantBits(Vector256.LessThan(Vector256.LoadUnsafe(in src, i), vT));
                if (bits != 0) return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vT = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                uint bits = Vector128.ExtractMostSignificantBits(Vector128.LessThan(Vector128.LoadUnsafe(in src, i), vT));
                if (bits != 0) return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        for (; i < length; ++i) if (Unsafe.Add(ref src, (nint)i) < threshold) return (int)i;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstEqual(ReadOnlySpan<double> source, double value)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return -1;
        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vV = Vector512.Create(value);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                ulong bits = Vector512.ExtractMostSignificantBits(Vector512.Equals(Vector512.LoadUnsafe(in src, i), vV));
                if (bits != 0) return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vV = Vector256.Create(value);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                uint bits = Vector256.ExtractMostSignificantBits(Vector256.Equals(Vector256.LoadUnsafe(in src, i), vV));
                if (bits != 0) return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vV = Vector128.Create(value);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                uint bits = Vector128.ExtractMostSignificantBits(Vector128.Equals(Vector128.LoadUnsafe(in src, i), vV));
                if (bits != 0) return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        for (; i < length; ++i) if (Unsafe.Add(ref src, (nint)i) == value) return (int)i;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountLessThan(ReadOnlySpan<double> source, double threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0;
        ref double src = ref MemoryMarshal.GetReference(source);
        nuint count = 0, i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vT = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                count += (nuint)uint.PopCount((uint)Vector512.ExtractMostSignificantBits(Vector512.LessThan(Vector512.LoadUnsafe(in src, i), vT)));
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vT = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                count += (nuint)uint.PopCount(Vector256.ExtractMostSignificantBits(Vector256.LessThan(Vector256.LoadUnsafe(in src, i), vT)));
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vT = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                count += (nuint)uint.PopCount(Vector128.ExtractMostSignificantBits(Vector128.LessThan(Vector128.LoadUnsafe(in src, i), vT)));
        }
        for (; i < length; ++i) if (Unsafe.Add(ref src, (nint)i) < threshold) ++count;
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountEqual(ReadOnlySpan<double> source, double value)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0;
        ref double src = ref MemoryMarshal.GetReference(source);
        nuint count = 0, i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vV = Vector512.Create(value);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                count += (nuint)uint.PopCount((uint)Vector512.ExtractMostSignificantBits(Vector512.Equals(Vector512.LoadUnsafe(in src, i), vV)));
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vV = Vector256.Create(value);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                count += (nuint)uint.PopCount(Vector256.ExtractMostSignificantBits(Vector256.Equals(Vector256.LoadUnsafe(in src, i), vV)));
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vV = Vector128.Create(value);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                count += (nuint)uint.PopCount(Vector128.ExtractMostSignificantBits(Vector128.Equals(Vector128.LoadUnsafe(in src, i), vV)));
        }
        for (; i < length; ++i) if (Unsafe.Add(ref src, (nint)i) == value) ++count;
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterLessThan(ReadOnlySpan<double> source, double threshold, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return ScanResult.Success(0);
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint dstCap = (nuint)destination.Length, written = 0, i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vT = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;
            for (; i < safeLimit; i += step)
            {
                ulong bits = Vector512.ExtractMostSignificantBits(Vector512.LessThan(Vector512.LoadUnsafe(in src, i), vT));
                while (bits != 0) { int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
            for (; i < limit; i += step)
            {
                ulong bits = Vector512.ExtractMostSignificantBits(Vector512.LessThan(Vector512.LoadUnsafe(in src, i), vT));
                while (bits != 0) { if (written >= dstCap) return ScanResult.Overflow(written); int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vT = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;
            for (; i < safeLimit; i += step)
            {
                uint bits = Vector256.ExtractMostSignificantBits(Vector256.LessThan(Vector256.LoadUnsafe(in src, i), vT));
                while (bits != 0) { int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
            for (; i < limit; i += step)
            {
                uint bits = Vector256.ExtractMostSignificantBits(Vector256.LessThan(Vector256.LoadUnsafe(in src, i), vT));
                while (bits != 0) { if (written >= dstCap) return ScanResult.Overflow(written); int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vT = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;
            for (; i < safeLimit; i += step)
            {
                uint bits = Vector128.ExtractMostSignificantBits(Vector128.LessThan(Vector128.LoadUnsafe(in src, i), vT));
                while (bits != 0) { int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
            for (; i < limit; i += step)
            {
                uint bits = Vector128.ExtractMostSignificantBits(Vector128.LessThan(Vector128.LoadUnsafe(in src, i), vT));
                while (bits != 0) { if (written >= dstCap) return ScanResult.Overflow(written); int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
        }
        for (; i < length; ++i)
        {
            double val = Unsafe.Add(ref src, (nint)i);
            if (val < threshold) { if (written >= dstCap) return ScanResult.Overflow(written); Unsafe.Add(ref dst, (nint)written++) = val; }
        }
        return ScanResult.Success(written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterEqual(ReadOnlySpan<double> source, double value, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return ScanResult.Success(0);
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint dstCap = (nuint)destination.Length, written = 0, i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vV = Vector512.Create(value);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;
            for (; i < safeLimit; i += step)
            {
                ulong bits = Vector512.ExtractMostSignificantBits(Vector512.Equals(Vector512.LoadUnsafe(in src, i), vV));
                while (bits != 0) { int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
            for (; i < limit; i += step)
            {
                ulong bits = Vector512.ExtractMostSignificantBits(Vector512.Equals(Vector512.LoadUnsafe(in src, i), vV));
                while (bits != 0) { if (written >= dstCap) return ScanResult.Overflow(written); int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vV = Vector256.Create(value);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;
            for (; i < safeLimit; i += step)
            {
                uint bits = Vector256.ExtractMostSignificantBits(Vector256.Equals(Vector256.LoadUnsafe(in src, i), vV));
                while (bits != 0) { int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
            for (; i < limit; i += step)
            {
                uint bits = Vector256.ExtractMostSignificantBits(Vector256.Equals(Vector256.LoadUnsafe(in src, i), vV));
                while (bits != 0) { if (written >= dstCap) return ScanResult.Overflow(written); int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vV = Vector128.Create(value);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;
            for (; i < safeLimit; i += step)
            {
                uint bits = Vector128.ExtractMostSignificantBits(Vector128.Equals(Vector128.LoadUnsafe(in src, i), vV));
                while (bits != 0) { int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
            for (; i < limit; i += step)
            {
                uint bits = Vector128.ExtractMostSignificantBits(Vector128.Equals(Vector128.LoadUnsafe(in src, i), vV));
                while (bits != 0) { if (written >= dstCap) return ScanResult.Overflow(written); int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
        }
        for (; i < length; ++i)
        {
            double val = Unsafe.Add(ref src, (nint)i);
            if (val == value) { if (written >= dstCap) return ScanResult.Overflow(written); Unsafe.Add(ref dst, (nint)written++) = val; }
        }
        return ScanResult.Success(written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Select(ReadOnlySpan<double> condition, ReadOnlySpan<double> trueValues, ReadOnlySpan<double> falseValues, Span<double> destination)
    {
        if (condition.Length != trueValues.Length || condition.Length != falseValues.Length || destination.Length < condition.Length)
            ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)condition.Length;
        if (length == 0) return;
        ref double cRef = ref MemoryMarshal.GetReference(condition);
        ref double tRef = ref MemoryMarshal.GetReference(trueValues);
        ref double fRef = ref MemoryMarshal.GetReference(falseValues);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.ConditionalSelect(Vector512.LoadUnsafe(in cRef, i), Vector512.LoadUnsafe(in tRef, i), Vector512.LoadUnsafe(in fRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.ConditionalSelect(Vector256.LoadUnsafe(in cRef, i), Vector256.LoadUnsafe(in tRef, i), Vector256.LoadUnsafe(in fRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.ConditionalSelect(Vector128.LoadUnsafe(in cRef, i), Vector128.LoadUnsafe(in tRef, i), Vector128.LoadUnsafe(in fRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref cRef, (nint)i) != 0.0 ? Unsafe.Add(ref tRef, (nint)i) : Unsafe.Add(ref fRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Select(ReadOnlySpan<double> condition, double trueValue, double falseValue, Span<double> destination)
    {
        if (destination.Length < condition.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)condition.Length;
        if (length == 0) return;
        ref double cRef = ref MemoryMarshal.GetReference(condition);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vT = Vector512.Create(trueValue), vF = Vector512.Create(falseValue);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.ConditionalSelect(Vector512.LoadUnsafe(in cRef, i), vT, vF).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vT = Vector256.Create(trueValue), vF = Vector256.Create(falseValue);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.ConditionalSelect(Vector256.LoadUnsafe(in cRef, i), vT, vF).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vT = Vector128.Create(trueValue), vF = Vector128.Create(falseValue);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.ConditionalSelect(Vector128.LoadUnsafe(in cRef, i), vT, vF).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref cRef, (nint)i) != 0.0 ? trueValue : falseValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareLessThan(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.LessThan(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.LessThan(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.LessThan(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) < Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int64BitsToDouble(-1L) : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareGreaterThan(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.GreaterThan(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.GreaterThan(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.GreaterThan(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) > Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int64BitsToDouble(-1L) : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.Equals(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.Equals(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.Equals(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) == Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int64BitsToDouble(-1L) : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareNotEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                (~Vector512.Equals(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i))).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                (~Vector256.Equals(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i))).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                (~Vector128.Equals(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i))).AsDouble().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) != Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int64BitsToDouble(-1L) : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareLessThanOrEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.LessThanOrEqual(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.LessThanOrEqual(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.LessThanOrEqual(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) <= Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int64BitsToDouble(-1L) : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareGreaterThanOrEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.GreaterThanOrEqual(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.GreaterThanOrEqual(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.GreaterThanOrEqual(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) >= Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int64BitsToDouble(-1L) : 0.0;
    }
}
