namespace Axrone.Simd;

public static unsafe partial class SimdFloat32
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeDotProduct(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length != right.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return 0.0f;

        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            Vector512<float> a0 = Vector512<float>.Zero;
            Vector512<float> a1 = Vector512<float>.Zero;
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i);
                a1 += Vector512.LoadUnsafe(in lRef, i + step) * Vector512.LoadUnsafe(in rRef, i + step);
            }

            Vector512<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i);
            }

            return Vector512.Sum(acc) + ScalarTailDot(ref lRef, ref rRef, i, length);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            Vector256<float> a0 = Vector256<float>.Zero;
            Vector256<float> a1 = Vector256<float>.Zero;
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i);
                a1 += Vector256.LoadUnsafe(in lRef, i + step) * Vector256.LoadUnsafe(in rRef, i + step);
            }

            Vector256<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i);
            }

            return Vector256.Sum(acc) + ScalarTailDot(ref lRef, ref rRef, i, length);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            Vector128<float> a0 = Vector128<float>.Zero;
            Vector128<float> a1 = Vector128<float>.Zero;
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i);
                a1 += Vector128.LoadUnsafe(in lRef, i + step) * Vector128.LoadUnsafe(in rRef, i + step);
            }

            Vector128<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i);
            }

            return Vector128.Sum(acc) + ScalarTailDot(ref lRef, ref rRef, i, length);
        }

        return ScalarTailDot(ref lRef, ref rRef, 0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float ScalarTailDot(ref float lRef, ref float rRef, nuint start, nuint length)
    {
        float d0 = 0.0f, d1 = 0.0f;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;

        for (; i < limit; i += 2)
        {
            d0 += Unsafe.Add(ref lRef, (nint)(i + 0)) * Unsafe.Add(ref rRef, (nint)(i + 0));
            d1 += Unsafe.Add(ref lRef, (nint)(i + 1)) * Unsafe.Add(ref rRef, (nint)(i + 1));
        }

        float acc = d0 + d1;
        for (; i < length; ++i)
        {
            acc += Unsafe.Add(ref lRef, (nint)i) * Unsafe.Add(ref rRef, (nint)i);
        }
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeL2Norm(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0f;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            Vector512<float> a0 = Vector512<float>.Zero;
            Vector512<float> a1 = Vector512<float>.Zero;
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

            float sumOfSquares = Vector512.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
            return MathF.Sqrt(sumOfSquares);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            Vector256<float> a0 = Vector256<float>.Zero;
            Vector256<float> a1 = Vector256<float>.Zero;
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

            float sumOfSquares = Vector256.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
            return MathF.Sqrt(sumOfSquares);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            Vector128<float> a0 = Vector128<float>.Zero;
            Vector128<float> a1 = Vector128<float>.Zero;
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

            float sumOfSquares = Vector128.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
            return MathF.Sqrt(sumOfSquares);
        }

        return MathF.Sqrt(ScalarTailSquareSum(ref src, 0, length));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float ScalarTailSquareSum(ref float src, nuint start, nuint length)
    {
        float s0 = 0.0f, s1 = 0.0f;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;

        for (; i < limit; i += 2)
        {
            float v0 = Unsafe.Add(ref src, (nint)(i + 0));
            float v1 = Unsafe.Add(ref src, (nint)(i + 1));
            s0 += v0 * v0;
            s1 += v1 * v1;
        }

        float acc = s0 + s1;
        for (; i < length; ++i)
        {
            float v = Unsafe.Add(ref src, (nint)i);
            acc += v * v;
        }
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ExtremaPair<float> ComputeExtrema(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count;
            Vector512<float> vMin0 = Vector512.LoadUnsafe(in src, 0);
            Vector512<float> vMax0 = vMin0;
            Vector512<float> vMin1 = Vector512.LoadUnsafe(in src, step);
            Vector512<float> vMax1 = vMin1;
            nuint limit = length - (step * 2) + 1;

            for (i = step * 2; i < limit; i += step * 2)
            {
                Vector512<float> v0 = Vector512.LoadUnsafe(in src, i);
                Vector512<float> v1 = Vector512.LoadUnsafe(in src, i + step);
                vMin0 = Vector512.Min(vMin0, v0);
                vMax0 = Vector512.Max(vMax0, v0);
                vMin1 = Vector512.Min(vMin1, v1);
                vMax1 = Vector512.Max(vMax1, v1);
            }

            Vector512<float> vMin = Vector512.Min(vMin0, vMin1);
            Vector512<float> vMax = Vector512.Max(vMax0, vMax1);

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512<float> val = Vector512.LoadUnsafe(in src, i);
                vMin = Vector512.Min(vMin, val);
                vMax = Vector512.Max(vMax, val);
            }

            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int lane = 0; lane < Vector512<float>.Count; ++lane)
            {
                min = float.Min(min, vMin.GetElement(lane));
                max = float.Max(max, vMax.GetElement(lane));
            }

            for (; i < length; ++i)
            {
                float v = Unsafe.Add(ref src, (nint)i);
                min = float.Min(min, v);
                max = float.Max(max, v);
            }
            return new ExtremaPair<float>(min, max);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            Vector256<float> vMin0 = Vector256.LoadUnsafe(in src, 0);
            Vector256<float> vMax0 = vMin0;
            Vector256<float> vMin1 = Vector256.LoadUnsafe(in src, step);
            Vector256<float> vMax1 = vMin1;
            nuint limit = length - (step * 2) + 1;

            for (i = step * 2; i < limit; i += step * 2)
            {
                Vector256<float> v0 = Vector256.LoadUnsafe(in src, i);
                Vector256<float> v1 = Vector256.LoadUnsafe(in src, i + step);
                vMin0 = Vector256.Min(vMin0, v0);
                vMax0 = Vector256.Max(vMax0, v0);
                vMin1 = Vector256.Min(vMin1, v1);
                vMax1 = Vector256.Max(vMax1, v1);
            }

            Vector256<float> vMin = Vector256.Min(vMin0, vMin1);
            Vector256<float> vMax = Vector256.Max(vMax0, vMax1);

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256<float> val = Vector256.LoadUnsafe(in src, i);
                vMin = Vector256.Min(vMin, val);
                vMax = Vector256.Max(vMax, val);
            }

            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int lane = 0; lane < Vector256<float>.Count; ++lane)
            {
                min = float.Min(min, vMin.GetElement(lane));
                max = float.Max(max, vMax.GetElement(lane));
            }

            for (; i < length; ++i)
            {
                float v = Unsafe.Add(ref src, (nint)i);
                min = float.Min(min, v);
                max = float.Max(max, v);
            }
            return new ExtremaPair<float>(min, max);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vMin = Vector128.LoadUnsafe(in src, 0);
            Vector128<float> vMax = vMin;
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (i = step; i < limit; i += step)
            {
                Vector128<float> val = Vector128.LoadUnsafe(in src, i);
                vMin = Vector128.Min(vMin, val);
                vMax = Vector128.Max(vMax, val);
            }

            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int lane = 0; lane < Vector128<float>.Count; ++lane)
            {
                min = float.Min(min, vMin.GetElement(lane));
                max = float.Max(max, vMax.GetElement(lane));
            }

            for (; i < length; ++i)
            {
                float v = Unsafe.Add(ref src, (nint)i);
                min = float.Min(min, v);
                max = float.Max(max, v);
            }
            return new ExtremaPair<float>(min, max);
        }

        float sMin = Unsafe.Add(ref src, 0);
        float sMax = sMin;
        for (i = 1; i < length; ++i)
        {
            float v = Unsafe.Add(ref src, (nint)i);
            sMin = float.Min(sMin, v);
            sMax = float.Max(sMax, v);
        }
        return new ExtremaPair<float>(sMin, sMax);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeL1Norm(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0f;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            Vector512<uint> absMask = Vector512.Create(0x7FFFFFFFu);
            Vector512<float> a0 = Vector512<float>.Zero, a1 = Vector512<float>.Zero;
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += (Vector512.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
                a1 += (Vector512.LoadUnsafe(in src, i + step).AsUInt32() & absMask).AsSingle();
            }

            Vector512<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += (Vector512.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
            }

            return Vector512.Sum(acc) + ScalarTailAbsSum(ref src, i, length);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            Vector256<uint> absMask = Vector256.Create(0x7FFFFFFFu);
            Vector256<float> a0 = Vector256<float>.Zero, a1 = Vector256<float>.Zero;
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += (Vector256.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
                a1 += (Vector256.LoadUnsafe(in src, i + step).AsUInt32() & absMask).AsSingle();
            }

            Vector256<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += (Vector256.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
            }

            return Vector256.Sum(acc) + ScalarTailAbsSum(ref src, i, length);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            Vector128<uint> absMask = Vector128.Create(0x7FFFFFFFu);
            Vector128<float> a0 = Vector128<float>.Zero, a1 = Vector128<float>.Zero;
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += (Vector128.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
                a1 += (Vector128.LoadUnsafe(in src, i + step).AsUInt32() & absMask).AsSingle();
            }

            Vector128<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += (Vector128.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
            }

            return Vector128.Sum(acc) + ScalarTailAbsSum(ref src, i, length);
        }

        return ScalarTailAbsSum(ref src, 0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeLinfNorm(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0f;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<uint> absMask = Vector512.Create(0x7FFFFFFFu);
            Vector512<float> vMax = Vector512<float>.Zero;
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<float> abs = (Vector512.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
                vMax = Vector512.Max(vMax, abs);
            }

            float result = 0.0f;
            for (int lane = 0; lane < Vector512<float>.Count; ++lane)
                result = float.Max(result, vMax.GetElement(lane));

            for (; i < length; ++i)
                result = float.Max(result, MathF.Abs(Unsafe.Add(ref src, (nint)i)));
            return result;
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<uint> absMask = Vector256.Create(0x7FFFFFFFu);
            Vector256<float> vMax = Vector256<float>.Zero;
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<float> abs = (Vector256.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
                vMax = Vector256.Max(vMax, abs);
            }

            float result = 0.0f;
            for (int lane = 0; lane < Vector256<float>.Count; ++lane)
                result = float.Max(result, vMax.GetElement(lane));

            for (; i < length; ++i)
                result = float.Max(result, MathF.Abs(Unsafe.Add(ref src, (nint)i)));
            return result;
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<uint> absMask = Vector128.Create(0x7FFFFFFFu);
            Vector128<float> vMax = Vector128<float>.Zero;
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<float> abs = (Vector128.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
                vMax = Vector128.Max(vMax, abs);
            }

            float result = 0.0f;
            for (int lane = 0; lane < Vector128<float>.Count; ++lane)
                result = float.Max(result, vMax.GetElement(lane));

            for (; i < length; ++i)
                result = float.Max(result, MathF.Abs(Unsafe.Add(ref src, (nint)i)));
            return result;
        }

        float sMax = 0.0f;
        for (; i < length; ++i)
            sMax = float.Max(sMax, MathF.Abs(Unsafe.Add(ref src, (nint)i)));
        return sMax;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float ScalarTailAbsSum(ref float src, nuint start, nuint length)
    {
        float s0 = 0.0f, s1 = 0.0f;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;

        for (; i < limit; i += 2)
        {
            s0 += MathF.Abs(Unsafe.Add(ref src, (nint)(i + 0)));
            s1 += MathF.Abs(Unsafe.Add(ref src, (nint)(i + 1)));
        }

        float acc = s0 + s1;
        for (; i < length; ++i)
            acc += MathF.Abs(Unsafe.Add(ref src, (nint)i));
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint ComputeArgMin(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();

        ref float src = ref MemoryMarshal.GetReference(source);

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;
            Vector512<float> vMin = Vector512.LoadUnsafe(in src, 0);
            nuint i = step;

            for (; i < limit; i += step)
                vMin = Vector512.Min(vMin, Vector512.LoadUnsafe(in src, i));

            float minVal = float.PositiveInfinity;
            for (int lane = 0; lane < Vector512<float>.Count; ++lane)
                minVal = float.Min(minVal, vMin.GetElement(lane));

            for (i = 0; i < length; ++i)
            {
                if (Unsafe.Add(ref src, (nint)i) == minVal) return i;
            }
            return 0;
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;
            Vector256<float> vMin = Vector256.LoadUnsafe(in src, 0);
            nuint i = step;

            for (; i < limit; i += step)
                vMin = Vector256.Min(vMin, Vector256.LoadUnsafe(in src, i));

            float minVal = float.PositiveInfinity;
            for (int lane = 0; lane < Vector256<float>.Count; ++lane)
                minVal = float.Min(minVal, vMin.GetElement(lane));

            for (i = 0; i < length; ++i)
            {
                if (Unsafe.Add(ref src, (nint)i) == minVal) return i;
            }
            return 0;
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;
            Vector128<float> vMin = Vector128.LoadUnsafe(in src, 0);
            nuint i = step;

            for (; i < limit; i += step)
                vMin = Vector128.Min(vMin, Vector128.LoadUnsafe(in src, i));

            float minVal = float.PositiveInfinity;
            for (int lane = 0; lane < Vector128<float>.Count; ++lane)
                minVal = float.Min(minVal, vMin.GetElement(lane));

            for (i = 0; i < length; ++i)
            {
                if (Unsafe.Add(ref src, (nint)i) == minVal) return i;
            }
            return 0;
        }

        float sMin = Unsafe.Add(ref src, 0);
        nuint idx = 0;
        for (nuint j = 1; j < length; ++j)
        {
            float v = Unsafe.Add(ref src, (nint)j);
            if (v < sMin) { sMin = v; idx = j; }
        }
        return idx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint ComputeArgMax(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();

        ref float src = ref MemoryMarshal.GetReference(source);

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;
            Vector512<float> vMax = Vector512.LoadUnsafe(in src, 0);
            nuint i = step;

            for (; i < limit; i += step)
                vMax = Vector512.Max(vMax, Vector512.LoadUnsafe(in src, i));

            float maxVal = float.NegativeInfinity;
            for (int lane = 0; lane < Vector512<float>.Count; ++lane)
                maxVal = float.Max(maxVal, vMax.GetElement(lane));

            for (i = 0; i < length; ++i)
            {
                if (Unsafe.Add(ref src, (nint)i) == maxVal) return i;
            }
            return 0;
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;
            Vector256<float> vMax = Vector256.LoadUnsafe(in src, 0);
            nuint i = step;

            for (; i < limit; i += step)
                vMax = Vector256.Max(vMax, Vector256.LoadUnsafe(in src, i));

            float maxVal = float.NegativeInfinity;
            for (int lane = 0; lane < Vector256<float>.Count; ++lane)
                maxVal = float.Max(maxVal, vMax.GetElement(lane));

            for (i = 0; i < length; ++i)
            {
                if (Unsafe.Add(ref src, (nint)i) == maxVal) return i;
            }
            return 0;
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;
            Vector128<float> vMax = Vector128.LoadUnsafe(in src, 0);
            nuint i = step;

            for (; i < limit; i += step)
                vMax = Vector128.Max(vMax, Vector128.LoadUnsafe(in src, i));

            float maxVal = float.NegativeInfinity;
            for (int lane = 0; lane < Vector128<float>.Count; ++lane)
                maxVal = float.Max(maxVal, vMax.GetElement(lane));

            for (i = 0; i < length; ++i)
            {
                if (Unsafe.Add(ref src, (nint)i) == maxVal) return i;
            }
            return 0;
        }

        float sMax = Unsafe.Add(ref src, 0);
        nuint idx = 0;
        for (nuint j = 1; j < length; ++j)
        {
            float v = Unsafe.Add(ref src, (nint)j);
            if (v > sMax) { sMax = v; idx = j; }
        }
        return idx;
    }
}
