namespace Axrone.Simd;

public static unsafe partial class SimdFloat64
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void TransformLinear(ReadOnlySpan<double> source, double multiplier, double offset, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;

        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            Vector512<double> vMul = Vector512.Create(multiplier);
            Vector512<double> vOff = Vector512.Create(offset);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<double> r0 = (Vector512.LoadUnsafe(in src, i) * vMul) + vOff;
                Vector512<double> r1 = (Vector512.LoadUnsafe(in src, i + step) * vMul) + vOff;
                r0.StoreUnsafe(ref dst, i);
                r1.StoreUnsafe(ref dst, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector512.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            Vector256<double> vMul = Vector256.Create(multiplier);
            Vector256<double> vOff = Vector256.Create(offset);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<double> r0 = (Vector256.LoadUnsafe(in src, i) * vMul) + vOff;
                Vector256<double> r1 = (Vector256.LoadUnsafe(in src, i + step) * vMul) + vOff;
                r0.StoreUnsafe(ref dst, i);
                r1.StoreUnsafe(ref dst, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector256.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            Vector128<double> vMul = Vector128.Create(multiplier);
            Vector128<double> vOff = Vector128.Create(offset);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<double> r0 = (Vector128.LoadUnsafe(in src, i) * vMul) + vOff;
                Vector128<double> r1 = (Vector128.LoadUnsafe(in src, i + step) * vMul) + vOff;
                r0.StoreUnsafe(ref dst, i);
                r1.StoreUnsafe(ref dst, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector128.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dst, (nint)(i + 0)) = (Unsafe.Add(ref src, (nint)(i + 0)) * multiplier) + offset;
            Unsafe.Add(ref dst, (nint)(i + 1)) = (Unsafe.Add(ref src, (nint)(i + 1)) * multiplier) + offset;
            Unsafe.Add(ref dst, (nint)(i + 2)) = (Unsafe.Add(ref src, (nint)(i + 2)) * multiplier) + offset;
            Unsafe.Add(ref dst, (nint)(i + 3)) = (Unsafe.Add(ref src, (nint)(i + 3)) * multiplier) + offset;
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dst, (nint)i) = (Unsafe.Add(ref src, (nint)i) * multiplier) + offset;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAdd(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<double> r0 = Vector512.LoadUnsafe(in lRef, i) + Vector512.LoadUnsafe(in rRef, i);
                Vector512<double> r1 = Vector512.LoadUnsafe(in lRef, i + step) + Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector512.LoadUnsafe(in lRef, i) + Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<double> r0 = Vector256.LoadUnsafe(in lRef, i) + Vector256.LoadUnsafe(in rRef, i);
                Vector256<double> r1 = Vector256.LoadUnsafe(in lRef, i + step) + Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector256.LoadUnsafe(in lRef, i) + Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<double> r0 = Vector128.LoadUnsafe(in lRef, i) + Vector128.LoadUnsafe(in rRef, i);
                Vector128<double> r1 = Vector128.LoadUnsafe(in lRef, i + step) + Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector128.LoadUnsafe(in lRef, i) + Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = Unsafe.Add(ref lRef, (nint)(i + 0)) + Unsafe.Add(ref rRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = Unsafe.Add(ref lRef, (nint)(i + 1)) + Unsafe.Add(ref rRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = Unsafe.Add(ref lRef, (nint)(i + 2)) + Unsafe.Add(ref rRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = Unsafe.Add(ref lRef, (nint)(i + 3)) + Unsafe.Add(ref rRef, (nint)(i + 3));
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) + Unsafe.Add(ref rRef, (nint)i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorSubtract(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512<double> r0 = Vector512.LoadUnsafe(in lRef, i) - Vector512.LoadUnsafe(in rRef, i);
                Vector512<double> r1 = Vector512.LoadUnsafe(in lRef, i + step) - Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector512.LoadUnsafe(in lRef, i) - Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256<double> r0 = Vector256.LoadUnsafe(in lRef, i) - Vector256.LoadUnsafe(in rRef, i);
                Vector256<double> r1 = Vector256.LoadUnsafe(in lRef, i + step) - Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector256.LoadUnsafe(in lRef, i) - Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128<double> r0 = Vector128.LoadUnsafe(in lRef, i) - Vector128.LoadUnsafe(in rRef, i);
                Vector128<double> r1 = Vector128.LoadUnsafe(in lRef, i + step) - Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector128.LoadUnsafe(in lRef, i) - Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = Unsafe.Add(ref lRef, (nint)(i + 0)) - Unsafe.Add(ref rRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = Unsafe.Add(ref lRef, (nint)(i + 1)) - Unsafe.Add(ref rRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = Unsafe.Add(ref lRef, (nint)(i + 2)) - Unsafe.Add(ref rRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = Unsafe.Add(ref lRef, (nint)(i + 3)) - Unsafe.Add(ref rRef, (nint)(i + 3));
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) - Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorMultiply(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<double> r0 = Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i);
                Vector512<double> r1 = Vector512.LoadUnsafe(in lRef, i + step) * Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<double> r0 = Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i);
                Vector256<double> r1 = Vector256.LoadUnsafe(in lRef, i + step) * Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<double> r0 = Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i);
                Vector128<double> r1 = Vector128.LoadUnsafe(in lRef, i + step) * Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = Unsafe.Add(ref lRef, (nint)(i + 0)) * Unsafe.Add(ref rRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = Unsafe.Add(ref lRef, (nint)(i + 1)) * Unsafe.Add(ref rRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = Unsafe.Add(ref lRef, (nint)(i + 2)) * Unsafe.Add(ref rRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = Unsafe.Add(ref lRef, (nint)(i + 3)) * Unsafe.Add(ref rRef, (nint)(i + 3));
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) * Unsafe.Add(ref rRef, (nint)i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorDivide(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512<double> r0 = Vector512.LoadUnsafe(in lRef, i) / Vector512.LoadUnsafe(in rRef, i);
                Vector512<double> r1 = Vector512.LoadUnsafe(in lRef, i + step) / Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector512.LoadUnsafe(in lRef, i) / Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256<double> r0 = Vector256.LoadUnsafe(in lRef, i) / Vector256.LoadUnsafe(in rRef, i);
                Vector256<double> r1 = Vector256.LoadUnsafe(in lRef, i + step) / Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector256.LoadUnsafe(in lRef, i) / Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128<double> r0 = Vector128.LoadUnsafe(in lRef, i) / Vector128.LoadUnsafe(in rRef, i);
                Vector128<double> r1 = Vector128.LoadUnsafe(in lRef, i + step) / Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector128.LoadUnsafe(in lRef, i) / Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = Unsafe.Add(ref lRef, (nint)(i + 0)) / Unsafe.Add(ref rRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = Unsafe.Add(ref lRef, (nint)(i + 1)) / Unsafe.Add(ref rRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = Unsafe.Add(ref lRef, (nint)(i + 2)) / Unsafe.Add(ref rRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = Unsafe.Add(ref lRef, (nint)(i + 3)) / Unsafe.Add(ref rRef, (nint)(i + 3));
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) / Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorNegate(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;

        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector512<double>.Zero - Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector512<double>.Zero - Vector512.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector512<double>.Zero - Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector256<double>.Zero - Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector256<double>.Zero - Vector256.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector256<double>.Zero - Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector128<double>.Zero - Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector128<double>.Zero - Vector128.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector128<double>.Zero - Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dst, (nint)(i + 0)) = -Unsafe.Add(ref src, (nint)(i + 0));
            Unsafe.Add(ref dst, (nint)(i + 1)) = -Unsafe.Add(ref src, (nint)(i + 1));
            Unsafe.Add(ref dst, (nint)(i + 2)) = -Unsafe.Add(ref src, (nint)(i + 2));
            Unsafe.Add(ref dst, (nint)(i + 3)) = -Unsafe.Add(ref src, (nint)(i + 3));
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dst, (nint)i) = -Unsafe.Add(ref src, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAbs(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;

        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<ulong> absMask = Vector512.Create(0x7FFFFFFFFFFFFFFFUL);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector512.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble().StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<ulong> absMask = Vector256.Create(0x7FFFFFFFFFFFFFFFUL);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector256.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble().StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<ulong> absMask = Vector128.Create(0x7FFFFFFFFFFFFFFFUL);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector128.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble().StoreUnsafe(ref dst, i);
        }

        for (; i < length; ++i)
            Unsafe.Add(ref dst, (nint)i) = Math.Abs(Unsafe.Add(ref src, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFma(ReadOnlySpan<double> a, ReadOnlySpan<double> b, ReadOnlySpan<double> c, Span<double> destination)
    {
        if (a.Length != b.Length || a.Length != c.Length || destination.Length < a.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)a.Length;
        if (length == 0) return;

        ref double aRef = ref MemoryMarshal.GetReference(a);
        ref double bRef = ref MemoryMarshal.GetReference(b);
        ref double cRef = ref MemoryMarshal.GetReference(c);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<double> r0 = (Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) + Vector512.LoadUnsafe(in cRef, i);
                Vector512<double> r1 = (Vector512.LoadUnsafe(in aRef, i + step) * Vector512.LoadUnsafe(in bRef, i + step)) + Vector512.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) + Vector512.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<double> r0 = (Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) + Vector256.LoadUnsafe(in cRef, i);
                Vector256<double> r1 = (Vector256.LoadUnsafe(in aRef, i + step) * Vector256.LoadUnsafe(in bRef, i + step)) + Vector256.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) + Vector256.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<double> r0 = (Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) + Vector128.LoadUnsafe(in cRef, i);
                Vector128<double> r1 = (Vector128.LoadUnsafe(in aRef, i + step) * Vector128.LoadUnsafe(in bRef, i + step)) + Vector128.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) + Vector128.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = (Unsafe.Add(ref aRef, (nint)(i + 0)) * Unsafe.Add(ref bRef, (nint)(i + 0))) + Unsafe.Add(ref cRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = (Unsafe.Add(ref aRef, (nint)(i + 1)) * Unsafe.Add(ref bRef, (nint)(i + 1))) + Unsafe.Add(ref cRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = (Unsafe.Add(ref aRef, (nint)(i + 2)) * Unsafe.Add(ref bRef, (nint)(i + 2))) + Unsafe.Add(ref cRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = (Unsafe.Add(ref aRef, (nint)(i + 3)) * Unsafe.Add(ref bRef, (nint)(i + 3))) + Unsafe.Add(ref cRef, (nint)(i + 3));
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = (Unsafe.Add(ref aRef, (nint)i) * Unsafe.Add(ref bRef, (nint)i)) + Unsafe.Add(ref cRef, (nint)i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFms(ReadOnlySpan<double> a, ReadOnlySpan<double> b, ReadOnlySpan<double> c, Span<double> destination)
    {
        if (a.Length != b.Length || a.Length != c.Length || destination.Length < a.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)a.Length;
        if (length == 0) return;

        ref double aRef = ref MemoryMarshal.GetReference(a);
        ref double bRef = ref MemoryMarshal.GetReference(b);
        ref double cRef = ref MemoryMarshal.GetReference(c);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512<double> r0 = (Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) - Vector512.LoadUnsafe(in cRef, i);
                Vector512<double> r1 = (Vector512.LoadUnsafe(in aRef, i + step) * Vector512.LoadUnsafe(in bRef, i + step)) - Vector512.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) - Vector512.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256<double> r0 = (Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) - Vector256.LoadUnsafe(in cRef, i);
                Vector256<double> r1 = (Vector256.LoadUnsafe(in aRef, i + step) * Vector256.LoadUnsafe(in bRef, i + step)) - Vector256.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) - Vector256.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128<double> r0 = (Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) - Vector128.LoadUnsafe(in cRef, i);
                Vector128<double> r1 = (Vector128.LoadUnsafe(in aRef, i + step) * Vector128.LoadUnsafe(in bRef, i + step)) - Vector128.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) - Vector128.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = (Unsafe.Add(ref aRef, (nint)(i + 0)) * Unsafe.Add(ref bRef, (nint)(i + 0))) - Unsafe.Add(ref cRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = (Unsafe.Add(ref aRef, (nint)(i + 1)) * Unsafe.Add(ref bRef, (nint)(i + 1))) - Unsafe.Add(ref cRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = (Unsafe.Add(ref aRef, (nint)(i + 2)) * Unsafe.Add(ref bRef, (nint)(i + 2))) - Unsafe.Add(ref cRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = (Unsafe.Add(ref aRef, (nint)(i + 3)) * Unsafe.Add(ref bRef, (nint)(i + 3))) - Unsafe.Add(ref cRef, (nint)(i + 3));
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = (Unsafe.Add(ref aRef, (nint)i) * Unsafe.Add(ref bRef, (nint)i)) - Unsafe.Add(ref cRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorLerp(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double t, Span<double> destination)
    {
        if (a.Length != b.Length || destination.Length < a.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)a.Length;
        if (length == 0) return;

        ref double aRef = ref MemoryMarshal.GetReference(a);
        ref double bRef = ref MemoryMarshal.GetReference(b);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vT = Vector512.Create(t);
            Vector512<double> oneMinusT = Vector512.Create(1.0 - t);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
                ((Vector512.LoadUnsafe(in aRef, i) * oneMinusT) + (Vector512.LoadUnsafe(in bRef, i) * vT)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vT = Vector256.Create(t);
            Vector256<double> oneMinusT = Vector256.Create(1.0 - t);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
                ((Vector256.LoadUnsafe(in aRef, i) * oneMinusT) + (Vector256.LoadUnsafe(in bRef, i) * vT)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vT = Vector128.Create(t);
            Vector128<double> oneMinusT = Vector128.Create(1.0 - t);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
                ((Vector128.LoadUnsafe(in aRef, i) * oneMinusT) + (Vector128.LoadUnsafe(in bRef, i) * vT)).StoreUnsafe(ref dRef, i);
        }

        for (; i < length; ++i)
        {
            double aVal = Unsafe.Add(ref aRef, (nint)i);
            Unsafe.Add(ref dRef, (nint)i) = (aVal * (1.0 - t)) + (Unsafe.Add(ref bRef, (nint)i) * t);
        }
    }
}
