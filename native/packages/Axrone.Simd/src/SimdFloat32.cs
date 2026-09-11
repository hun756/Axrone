namespace Axrone.Simd;

public static unsafe partial class SimdFloat32
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAdd(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);
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
        ThrowHelper.ValidateBinarySpans(left, right, destination);
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
        ThrowHelper.ValidateBinarySpans(left, right, destination);
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
        ThrowHelper.ValidateBinarySpans(left, right, destination);
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
        ThrowHelper.ValidateDestinationSpan(destination, source);
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
        ThrowHelper.ValidateDestinationSpan(destination, source);
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
        ThrowHelper.ValidateDestinationSpan(destination, source);
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
        ThrowHelper.ValidateTernarySpans(a, b, a, destination);
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
        ThrowHelper.ValidateTernarySpans(a, b, a, destination);
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
        ThrowHelper.ValidateBinarySpans(a, b, destination);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorClamp(ReadOnlySpan<float> source, float min, float max, Span<float> destination)
    {
        ThrowHelper.ValidateDestinationSpan(destination, source);
        nuint length = (nuint)source.Length;
        if (length == 0) return;

        ref float sRef = ref MemoryMarshal.GetReference(source);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vMin = Vector512.Create(min);
            Vector512<float> vMax = Vector512.Create(max);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<float> val = Vector512.LoadUnsafe(in sRef, i);
                Vector512.Min(Vector512.Max(val, vMin), vMax).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vMin = Vector256.Create(min);
            Vector256<float> vMax = Vector256.Create(max);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<float> val = Vector256.LoadUnsafe(in sRef, i);
                Vector256.Min(Vector256.Max(val, vMin), vMax).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vMin = Vector128.Create(min);
            Vector128<float> vMax = Vector128.Create(max);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<float> val = Vector128.LoadUnsafe(in sRef, i);
                Vector128.Min(Vector128.Max(val, vMin), vMax).StoreUnsafe(ref dRef, i);
            }
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = Math.Clamp(Unsafe.Add(ref sRef, (nint)i), min, max);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeSum(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0f;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 4)
        {
            Vector512<float> a0 = Vector512<float>.Zero;
            Vector512<float> a1 = Vector512<float>.Zero;
            Vector512<float> a2 = Vector512<float>.Zero;
            Vector512<float> a3 = Vector512<float>.Zero;
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 4) + 1;

            for (; i < limit; i += step * 4)
            {
                a0 += Vector512.LoadUnsafe(in src, i);
                a1 += Vector512.LoadUnsafe(in src, i + step);
                a2 += Vector512.LoadUnsafe(in src, i + (step * 2));
                a3 += Vector512.LoadUnsafe(in src, i + (step * 3));
            }

            Vector512<float> acc = (a0 + a1) + (a2 + a3);
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector512.LoadUnsafe(in src, i);
            }

            return Vector512.Sum(acc) + ScalarTailSum(ref src, i, length);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 4)
        {
            Vector256<float> a0 = Vector256<float>.Zero;
            Vector256<float> a1 = Vector256<float>.Zero;
            Vector256<float> a2 = Vector256<float>.Zero;
            Vector256<float> a3 = Vector256<float>.Zero;
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 4) + 1;

            for (; i < limit; i += step * 4)
            {
                a0 += Vector256.LoadUnsafe(in src, i);
                a1 += Vector256.LoadUnsafe(in src, i + step);
                a2 += Vector256.LoadUnsafe(in src, i + (step * 2));
                a3 += Vector256.LoadUnsafe(in src, i + (step * 3));
            }

            Vector256<float> acc = (a0 + a1) + (a2 + a3);
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector256.LoadUnsafe(in src, i);
            }

            return Vector256.Sum(acc) + ScalarTailSum(ref src, i, length);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 4)
        {
            Vector128<float> a0 = Vector128<float>.Zero;
            Vector128<float> a1 = Vector128<float>.Zero;
            Vector128<float> a2 = Vector128<float>.Zero;
            Vector128<float> a3 = Vector128<float>.Zero;
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 4) + 1;

            for (; i < limit; i += step * 4)
            {
                a0 += Vector128.LoadUnsafe(in src, i);
                a1 += Vector128.LoadUnsafe(in src, i + step);
                a2 += Vector128.LoadUnsafe(in src, i + (step * 2));
                a3 += Vector128.LoadUnsafe(in src, i + (step * 3));
            }

            Vector128<float> acc = (a0 + a1) + (a2 + a3);
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector128.LoadUnsafe(in src, i);
            }

            return Vector128.Sum(acc) + ScalarTailSum(ref src, i, length);
        }

        return ScalarTailSum(ref src, 0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float ScalarTailSum(ref float src, nuint start, nuint length)
    {
        float s0 = 0.0f, s1 = 0.0f, s2 = 0.0f, s3 = 0.0f;
        nuint i = start;
        nuint limit = length >= 4 ? length - 3 : 0;

        for (; i < limit; i += 4)
        {
            s0 += Unsafe.Add(ref src, (nint)(i + 0));
            s1 += Unsafe.Add(ref src, (nint)(i + 1));
            s2 += Unsafe.Add(ref src, (nint)(i + 2));
            s3 += Unsafe.Add(ref src, (nint)(i + 3));
        }

        float acc = (s0 + s1) + (s2 + s3);
        for (; i < length; ++i)
        {
            acc += Unsafe.Add(ref src, (nint)i);
        }
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMax(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);
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
                Vector512.Max(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector512.Max(Vector512.LoadUnsafe(in lRef, i + step), Vector512.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512.Max(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256.Max(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector256.Max(Vector256.LoadUnsafe(in lRef, i + step), Vector256.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256.Max(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128.Max(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector128.Max(Vector128.LoadUnsafe(in lRef, i + step), Vector128.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector128.Max(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = float.Max(Unsafe.Add(ref lRef, (nint)i), Unsafe.Add(ref rRef, (nint)i));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMin(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);
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
                Vector512.Min(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector512.Min(Vector512.LoadUnsafe(in lRef, i + step), Vector512.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512.Min(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256.Min(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector256.Min(Vector256.LoadUnsafe(in lRef, i + step), Vector256.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256.Min(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128.Min(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector128.Min(Vector128.LoadUnsafe(in lRef, i + step), Vector128.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector128.Min(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = float.Min(Unsafe.Add(ref lRef, (nint)i), Unsafe.Add(ref rRef, (nint)i));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeMean(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0f;

        float sum = ComputeSum(source);
        return sum / (float)length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeVariance(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0f;
        if (length == 1) return 0.0f;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            Vector512<float> s0 = Vector512<float>.Zero, s1 = Vector512<float>.Zero;
            Vector512<float> q0 = Vector512<float>.Zero, q1 = Vector512<float>.Zero;
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<float> v0 = Vector512.LoadUnsafe(in src, i);
                Vector512<float> v1 = Vector512.LoadUnsafe(in src, i + step);
                s0 += v0; s1 += v1;
                q0 += v0 * v0; q1 += v1 * v1;
            }

            Vector512<float> accS = s0 + s1;
            Vector512<float> accQ = q0 + q1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512<float> v = Vector512.LoadUnsafe(in src, i);
                accS += v;
                accQ += v * v;
            }

            float sum = Vector512.Sum(accS) + ScalarTailSum(ref src, i, length);
            float sumSq = Vector512.Sum(accQ) + ScalarTailSquareSum(ref src, i, length);
            float n = (float)length;
            float mean = sum / n;
            return (sumSq / n) - (mean * mean);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            Vector256<float> s0 = Vector256<float>.Zero, s1 = Vector256<float>.Zero;
            Vector256<float> q0 = Vector256<float>.Zero, q1 = Vector256<float>.Zero;
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<float> v0 = Vector256.LoadUnsafe(in src, i);
                Vector256<float> v1 = Vector256.LoadUnsafe(in src, i + step);
                s0 += v0; s1 += v1;
                q0 += v0 * v0; q1 += v1 * v1;
            }

            Vector256<float> accS = s0 + s1;
            Vector256<float> accQ = q0 + q1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256<float> v = Vector256.LoadUnsafe(in src, i);
                accS += v;
                accQ += v * v;
            }

            float sum = Vector256.Sum(accS) + ScalarTailSum(ref src, i, length);
            float sumSq = Vector256.Sum(accQ) + ScalarTailSquareSum(ref src, i, length);
            float n = (float)length;
            float mean = sum / n;
            return (sumSq / n) - (mean * mean);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            Vector128<float> s0 = Vector128<float>.Zero, s1 = Vector128<float>.Zero;
            Vector128<float> q0 = Vector128<float>.Zero, q1 = Vector128<float>.Zero;
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<float> v0 = Vector128.LoadUnsafe(in src, i);
                Vector128<float> v1 = Vector128.LoadUnsafe(in src, i + step);
                s0 += v0; s1 += v1;
                q0 += v0 * v0; q1 += v1 * v1;
            }

            Vector128<float> accS = s0 + s1;
            Vector128<float> accQ = q0 + q1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector128<float> v = Vector128.LoadUnsafe(in src, i);
                accS += v;
                accQ += v * v;
            }

            float sum = Vector128.Sum(accS) + ScalarTailSum(ref src, i, length);
            float sumSq = Vector128.Sum(accQ) + ScalarTailSquareSum(ref src, i, length);
            float n = (float)length;
            float mean = sum / n;
            return (sumSq / n) - (mean * mean);
        }

        float sSum = 0.0f, sSq = 0.0f;
        for (i = 0; i < length; ++i)
        {
            float v = Unsafe.Add(ref src, (nint)i);
            sSum += v;
            sSq += v * v;
        }
        float sn = (float)length;
        float sm = sSum / sn;
        return (sSq / sn) - (sm * sm);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeStdDev(ReadOnlySpan<float> source)
        => MathF.Sqrt(ComputeVariance(source));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeSumOfSquares(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0f;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            Vector512<float> a0 = Vector512<float>.Zero, a1 = Vector512<float>.Zero;
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<float> v0 = Vector512.LoadUnsafe(in src, i);
                Vector512<float> v1 = Vector512.LoadUnsafe(in src, i + step);
                a0 += v0 * v0;
                a1 += v1 * v1;
            }

            Vector512<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512<float> v = Vector512.LoadUnsafe(in src, i);
                acc += v * v;
            }

            return Vector512.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            Vector256<float> a0 = Vector256<float>.Zero, a1 = Vector256<float>.Zero;
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<float> v0 = Vector256.LoadUnsafe(in src, i);
                Vector256<float> v1 = Vector256.LoadUnsafe(in src, i + step);
                a0 += v0 * v0;
                a1 += v1 * v1;
            }

            Vector256<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256<float> v = Vector256.LoadUnsafe(in src, i);
                acc += v * v;
            }

            return Vector256.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            Vector128<float> a0 = Vector128<float>.Zero, a1 = Vector128<float>.Zero;
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<float> v0 = Vector128.LoadUnsafe(in src, i);
                Vector128<float> v1 = Vector128.LoadUnsafe(in src, i + step);
                a0 += v0 * v0;
                a1 += v1 * v1;
            }

            Vector128<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector128<float> v = Vector128.LoadUnsafe(in src, i);
                acc += v * v;
            }

            return Vector128.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
        }

        return ScalarTailSquareSum(ref src, 0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeProduct(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 1.0f;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> acc = Vector512.Create(1.0f);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                acc *= Vector512.LoadUnsafe(in src, i);
            }

            float result = 1.0f;
            for (int lane = 0; lane < Vector512<float>.Count; ++lane)
                result *= acc.GetElement(lane);

            for (; i < length; ++i)
                result *= Unsafe.Add(ref src, (nint)i);
            return result;
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> acc = Vector256.Create(1.0f);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                acc *= Vector256.LoadUnsafe(in src, i);
            }

            float result = 1.0f;
            for (int lane = 0; lane < Vector256<float>.Count; ++lane)
                result *= acc.GetElement(lane);

            for (; i < length; ++i)
                result *= Unsafe.Add(ref src, (nint)i);
            return result;
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> acc = Vector128.Create(1.0f);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                acc *= Vector128.LoadUnsafe(in src, i);
            }

            float result = 1.0f;
            for (int lane = 0; lane < Vector128<float>.Count; ++lane)
                result *= acc.GetElement(lane);

            for (; i < length; ++i)
                result *= Unsafe.Add(ref src, (nint)i);
            return result;
        }

        float product = 1.0f;
        for (; i < length; ++i)
            product *= Unsafe.Add(ref src, (nint)i);
        return product;
    }
}
