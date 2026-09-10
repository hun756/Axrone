namespace Axrone.Simd;

public static unsafe class SimdFloat32
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAdd(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512<float> r0 = Vector512.LoadUnsafe(in lRef, i) + Vector512.LoadUnsafe(in rRef, i);
                Vector512<float> r1 = Vector512.LoadUnsafe(in lRef, i + step) + Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector512.LoadUnsafe(in lRef, i) + Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256<float> r0 = Vector256.LoadUnsafe(in lRef, i) + Vector256.LoadUnsafe(in rRef, i);
                Vector256<float> r1 = Vector256.LoadUnsafe(in lRef, i + step) + Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector256.LoadUnsafe(in lRef, i) + Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128<float> r0 = Vector128.LoadUnsafe(in lRef, i) + Vector128.LoadUnsafe(in rRef, i);
                Vector128<float> r1 = Vector128.LoadUnsafe(in lRef, i + step) + Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector128.LoadUnsafe(in lRef, i) + Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
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
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) + Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorSubtract(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512<float> r0 = Vector512.LoadUnsafe(in lRef, i) - Vector512.LoadUnsafe(in rRef, i);
                Vector512<float> r1 = Vector512.LoadUnsafe(in lRef, i + step) - Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector512.LoadUnsafe(in lRef, i) - Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256<float> r0 = Vector256.LoadUnsafe(in lRef, i) - Vector256.LoadUnsafe(in rRef, i);
                Vector256<float> r1 = Vector256.LoadUnsafe(in lRef, i + step) - Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector256.LoadUnsafe(in lRef, i) - Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128<float> r0 = Vector128.LoadUnsafe(in lRef, i) - Vector128.LoadUnsafe(in rRef, i);
                Vector128<float> r1 = Vector128.LoadUnsafe(in lRef, i + step) - Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
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
    public static void VectorMultiply(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512<float> r0 = Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i);
                Vector512<float> r1 = Vector512.LoadUnsafe(in lRef, i + step) * Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256<float> r0 = Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i);
                Vector256<float> r1 = Vector256.LoadUnsafe(in lRef, i + step) * Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128<float> r0 = Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i);
                Vector128<float> r1 = Vector128.LoadUnsafe(in lRef, i + step) * Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
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
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) * Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorDivide(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512<float> r0 = Vector512.LoadUnsafe(in lRef, i) / Vector512.LoadUnsafe(in rRef, i);
                Vector512<float> r1 = Vector512.LoadUnsafe(in lRef, i + step) / Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector512.LoadUnsafe(in lRef, i) / Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256<float> r0 = Vector256.LoadUnsafe(in lRef, i) / Vector256.LoadUnsafe(in rRef, i);
                Vector256<float> r1 = Vector256.LoadUnsafe(in lRef, i + step) / Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector256.LoadUnsafe(in lRef, i) / Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128<float> r0 = Vector128.LoadUnsafe(in lRef, i) / Vector128.LoadUnsafe(in rRef, i);
                Vector128<float> r1 = Vector128.LoadUnsafe(in lRef, i + step) / Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
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
    public static void VectorNegate(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;

        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector512<float>.Zero - Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector512<float>.Zero - Vector512.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector512<float>.Zero - Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector256<float>.Zero - Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector256<float>.Zero - Vector256.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector256<float>.Zero - Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector128<float>.Zero - Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector128<float>.Zero - Vector128.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector128<float>.Zero - Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
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
    public static void VectorAbs(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;

        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<uint> absMask = Vector512.Create(0x7FFFFFFFu);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector512.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle().StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<uint> absMask = Vector256.Create(0x7FFFFFFFu);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector256.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle().StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<uint> absMask = Vector128.Create(0x7FFFFFFFu);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector128.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle().StoreUnsafe(ref dst, i);
        }

        for (; i < length; ++i)
            Unsafe.Add(ref dst, (nint)i) = MathF.Abs(Unsafe.Add(ref src, (nint)i));
    }
}
