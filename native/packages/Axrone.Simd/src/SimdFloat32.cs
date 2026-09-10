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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void TransformLinear(ReadOnlySpan<float> source, float multiplier, float offset, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            Vector512<float> vMul = Vector512.Create(multiplier), vOff = Vector512.Create(offset);
            nuint step = (nuint)Vector512<float>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector512.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
                ((Vector512.LoadUnsafe(in src, i + step) * vMul) + vOff).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector512.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            Vector256<float> vMul = Vector256.Create(multiplier), vOff = Vector256.Create(offset);
            nuint step = (nuint)Vector256<float>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector256.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
                ((Vector256.LoadUnsafe(in src, i + step) * vMul) + vOff).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector256.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            Vector128<float> vMul = Vector128.Create(multiplier), vOff = Vector128.Create(offset);
            nuint step = (nuint)Vector128<float>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector128.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
                ((Vector128.LoadUnsafe(in src, i + step) * vMul) + vOff).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector128.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
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
            Unsafe.Add(ref dst, (nint)i) = (Unsafe.Add(ref src, (nint)i) * multiplier) + offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFma(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> destination)
    {
        if (a.Length != b.Length || a.Length != c.Length || destination.Length < a.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)a.Length;
        if (length == 0) return;
        ref float aRef = ref MemoryMarshal.GetReference(a);
        ref float bRef = ref MemoryMarshal.GetReference(b);
        ref float cRef = ref MemoryMarshal.GetReference(c);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) + Vector512.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
                ((Vector512.LoadUnsafe(in aRef, i + step) * Vector512.LoadUnsafe(in bRef, i + step)) + Vector512.LoadUnsafe(in cRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) + Vector512.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) + Vector256.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
                ((Vector256.LoadUnsafe(in aRef, i + step) * Vector256.LoadUnsafe(in bRef, i + step)) + Vector256.LoadUnsafe(in cRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) + Vector256.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) + Vector128.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
                ((Vector128.LoadUnsafe(in aRef, i + step) * Vector128.LoadUnsafe(in bRef, i + step)) + Vector128.LoadUnsafe(in cRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) + Vector128.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
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
            Unsafe.Add(ref dRef, (nint)i) = (Unsafe.Add(ref aRef, (nint)i) * Unsafe.Add(ref bRef, (nint)i)) + Unsafe.Add(ref cRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFms(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> destination)
    {
        if (a.Length != b.Length || a.Length != c.Length || destination.Length < a.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)a.Length;
        if (length == 0) return;
        ref float aRef = ref MemoryMarshal.GetReference(a);
        ref float bRef = ref MemoryMarshal.GetReference(b);
        ref float cRef = ref MemoryMarshal.GetReference(c);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) - Vector512.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
                ((Vector512.LoadUnsafe(in aRef, i + step) * Vector512.LoadUnsafe(in bRef, i + step)) - Vector512.LoadUnsafe(in cRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) - Vector512.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) - Vector256.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
                ((Vector256.LoadUnsafe(in aRef, i + step) * Vector256.LoadUnsafe(in bRef, i + step)) - Vector256.LoadUnsafe(in cRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) - Vector256.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) - Vector128.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
                ((Vector128.LoadUnsafe(in aRef, i + step) * Vector128.LoadUnsafe(in bRef, i + step)) - Vector128.LoadUnsafe(in cRef, i + step)).StoreUnsafe(ref dRef, i + step);
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
    public static void VectorLerp(ReadOnlySpan<float> a, ReadOnlySpan<float> b, float t, Span<float> destination)
    {
        if (a.Length != b.Length || destination.Length < a.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)a.Length;
        if (length == 0) return;
        ref float aRef = ref MemoryMarshal.GetReference(a);
        ref float bRef = ref MemoryMarshal.GetReference(b);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vT = Vector512.Create(t), oneMinusT = Vector512.Create(1.0f - t);
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                ((Vector512.LoadUnsafe(in aRef, i) * oneMinusT) + (Vector512.LoadUnsafe(in bRef, i) * vT)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vT = Vector256.Create(t), oneMinusT = Vector256.Create(1.0f - t);
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                ((Vector256.LoadUnsafe(in aRef, i) * oneMinusT) + (Vector256.LoadUnsafe(in bRef, i) * vT)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vT = Vector128.Create(t), oneMinusT = Vector128.Create(1.0f - t);
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                ((Vector128.LoadUnsafe(in aRef, i) * oneMinusT) + (Vector128.LoadUnsafe(in bRef, i) * vT)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
        {
            float aVal = Unsafe.Add(ref aRef, (nint)i);
            Unsafe.Add(ref dRef, (nint)i) = (aVal * (1.0f - t)) + (Unsafe.Add(ref bRef, (nint)i) * t);
        }
    }
}
