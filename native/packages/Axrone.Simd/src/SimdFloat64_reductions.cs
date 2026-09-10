namespace Axrone.Simd;

public static unsafe partial class SimdFloat64
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorClamp(ReadOnlySpan<double> source, double min, double max, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;

        ref double sRef = ref MemoryMarshal.GetReference(source);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vMin = Vector512.Create(min);
            Vector512<double> vMax = Vector512.Create(max);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<double> val = Vector512.LoadUnsafe(in sRef, i);
                Vector512.Min(Vector512.Max(val, vMin), vMax).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vMin = Vector256.Create(min);
            Vector256<double> vMax = Vector256.Create(max);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<double> val = Vector256.LoadUnsafe(in sRef, i);
                Vector256.Min(Vector256.Max(val, vMin), vMax).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vMin = Vector128.Create(min);
            Vector128<double> vMax = Vector128.Create(max);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<double> val = Vector128.LoadUnsafe(in sRef, i);
                Vector128.Min(Vector128.Max(val, vMin), vMax).StoreUnsafe(ref dRef, i);
            }
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = Math.Clamp(Unsafe.Add(ref sRef, (nint)i), min, max);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMax(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
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
                Vector512.Max(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector512.Max(Vector512.LoadUnsafe(in lRef, i + step), Vector512.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                Vector512.Max(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256.Max(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector256.Max(Vector256.LoadUnsafe(in lRef, i + step), Vector256.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                Vector256.Max(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128.Max(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector128.Max(Vector128.LoadUnsafe(in lRef, i + step), Vector128.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                Vector128.Max(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }

        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = double.Max(Unsafe.Add(ref lRef, (nint)i), Unsafe.Add(ref rRef, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMin(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
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
                Vector512.Min(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector512.Min(Vector512.LoadUnsafe(in lRef, i + step), Vector512.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                Vector512.Min(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256.Min(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector256.Min(Vector256.LoadUnsafe(in lRef, i + step), Vector256.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                Vector256.Min(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128.Min(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector128.Min(Vector128.LoadUnsafe(in lRef, i + step), Vector128.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                Vector128.Min(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }

        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = double.Min(Unsafe.Add(ref lRef, (nint)i), Unsafe.Add(ref rRef, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeSum(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 4)
        {
            Vector512<double> a0 = Vector512<double>.Zero;
            Vector512<double> a1 = Vector512<double>.Zero;
            Vector512<double> a2 = Vector512<double>.Zero;
            Vector512<double> a3 = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 4) + 1;

            for (; i < limit; i += step * 4)
            {
                a0 += Vector512.LoadUnsafe(in src, i);
                a1 += Vector512.LoadUnsafe(in src, i + step);
                a2 += Vector512.LoadUnsafe(in src, i + (step * 2));
                a3 += Vector512.LoadUnsafe(in src, i + (step * 3));
            }

            Vector512<double> acc = (a0 + a1) + (a2 + a3);
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector512.LoadUnsafe(in src, i);
            }

            return Vector512.Sum(acc) + ScalarTailSum(ref src, i, length);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 4)
        {
            Vector256<double> a0 = Vector256<double>.Zero;
            Vector256<double> a1 = Vector256<double>.Zero;
            Vector256<double> a2 = Vector256<double>.Zero;
            Vector256<double> a3 = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 4) + 1;

            for (; i < limit; i += step * 4)
            {
                a0 += Vector256.LoadUnsafe(in src, i);
                a1 += Vector256.LoadUnsafe(in src, i + step);
                a2 += Vector256.LoadUnsafe(in src, i + (step * 2));
                a3 += Vector256.LoadUnsafe(in src, i + (step * 3));
            }

            Vector256<double> acc = (a0 + a1) + (a2 + a3);
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector256.LoadUnsafe(in src, i);
            }

            return Vector256.Sum(acc) + ScalarTailSum(ref src, i, length);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 4)
        {
            Vector128<double> a0 = Vector128<double>.Zero;
            Vector128<double> a1 = Vector128<double>.Zero;
            Vector128<double> a2 = Vector128<double>.Zero;
            Vector128<double> a3 = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 4) + 1;

            for (; i < limit; i += step * 4)
            {
                a0 += Vector128.LoadUnsafe(in src, i);
                a1 += Vector128.LoadUnsafe(in src, i + step);
                a2 += Vector128.LoadUnsafe(in src, i + (step * 2));
                a3 += Vector128.LoadUnsafe(in src, i + (step * 3));
            }

            Vector128<double> acc = (a0 + a1) + (a2 + a3);
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
    private static double ScalarTailSum(ref double src, nuint start, nuint length)
    {
        double s0 = 0.0, s1 = 0.0, s2 = 0.0, s3 = 0.0;
        nuint i = start;
        nuint limit = length >= 4 ? length - 3 : 0;

        for (; i < limit; i += 4)
        {
            s0 += Unsafe.Add(ref src, (nint)(i + 0));
            s1 += Unsafe.Add(ref src, (nint)(i + 1));
            s2 += Unsafe.Add(ref src, (nint)(i + 2));
            s3 += Unsafe.Add(ref src, (nint)(i + 3));
        }

        double acc = (s0 + s1) + (s2 + s3);
        for (; i < length; ++i)
        {
            acc += Unsafe.Add(ref src, (nint)i);
        }
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeProduct(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 1.0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> acc = Vector512.Create(1.0);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step) acc *= Vector512.LoadUnsafe(in src, i);
            double result = 1.0;
            for (int lane = 0; lane < Vector512<double>.Count; ++lane) result *= acc.GetElement(lane);
            for (; i < length; ++i) result *= Unsafe.Add(ref src, (nint)i);
            return result;
        }
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> acc = Vector256.Create(1.0);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step) acc *= Vector256.LoadUnsafe(in src, i);
            double result = 1.0;
            for (int lane = 0; lane < Vector256<double>.Count; ++lane) result *= acc.GetElement(lane);
            for (; i < length; ++i) result *= Unsafe.Add(ref src, (nint)i);
            return result;
        }
        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> acc = Vector128.Create(1.0);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step) acc *= Vector128.LoadUnsafe(in src, i);
            double result = 1.0;
            for (int lane = 0; lane < Vector128<double>.Count; ++lane) result *= acc.GetElement(lane);
            for (; i < length; ++i) result *= Unsafe.Add(ref src, (nint)i);
            return result;
        }

        double product = 1.0;
        for (; i < length; ++i) product *= Unsafe.Add(ref src, (nint)i);
        return product;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeSumOfSquares(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            Vector512<double> a0 = Vector512<double>.Zero, a1 = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512<double> v0 = Vector512.LoadUnsafe(in src, i);
                Vector512<double> v1 = Vector512.LoadUnsafe(in src, i + step);
                a0 += v0 * v0; a1 += v1 * v1;
            }
            Vector512<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) { Vector512<double> v = Vector512.LoadUnsafe(in src, i); acc += v * v; }
            return Vector512.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
        }
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            Vector256<double> a0 = Vector256<double>.Zero, a1 = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256<double> v0 = Vector256.LoadUnsafe(in src, i);
                Vector256<double> v1 = Vector256.LoadUnsafe(in src, i + step);
                a0 += v0 * v0; a1 += v1 * v1;
            }
            Vector256<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) { Vector256<double> v = Vector256.LoadUnsafe(in src, i); acc += v * v; }
            return Vector256.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
        }
        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            Vector128<double> a0 = Vector128<double>.Zero, a1 = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128<double> v0 = Vector128.LoadUnsafe(in src, i);
                Vector128<double> v1 = Vector128.LoadUnsafe(in src, i + step);
                a0 += v0 * v0; a1 += v1 * v1;
            }
            Vector128<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) { Vector128<double> v = Vector128.LoadUnsafe(in src, i); acc += v * v; }
            return Vector128.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
        }
        return ScalarTailSquareSum(ref src, 0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double ScalarTailSquareSum(ref double src, nuint start, nuint length)
    {
        double s0 = 0.0, s1 = 0.0;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;

        for (; i < limit; i += 2)
        {
            double v0 = Unsafe.Add(ref src, (nint)(i + 0));
            double v1 = Unsafe.Add(ref src, (nint)(i + 1));
            s0 += v0 * v0;
            s1 += v1 * v1;
        }

        double acc = s0 + s1;
        for (; i < length; ++i)
        {
            double v = Unsafe.Add(ref src, (nint)i);
            acc += v * v;
        }
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeMean(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0;
        return ComputeSum(source) / (double)length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeVariance(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length <= 1) return 0.0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            Vector512<double> s0 = Vector512<double>.Zero, s1 = Vector512<double>.Zero;
            Vector512<double> q0 = Vector512<double>.Zero, q1 = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512<double> v0 = Vector512.LoadUnsafe(in src, i);
                Vector512<double> v1 = Vector512.LoadUnsafe(in src, i + step);
                s0 += v0; s1 += v1; q0 += v0 * v0; q1 += v1 * v1;
            }
            Vector512<double> accS = s0 + s1; Vector512<double> accQ = q0 + q1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512<double> v = Vector512.LoadUnsafe(in src, i);
                accS += v; accQ += v * v;
            }
            double sum = Vector512.Sum(accS) + ScalarTailSumD(ref src, i, length);
            double sumSq = Vector512.Sum(accQ) + ScalarTailSquareSumD(ref src, i, length);
            double n = (double)length; double mean = sum / n;
            return (sumSq / n) - (mean * mean);
        }
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            Vector256<double> s0 = Vector256<double>.Zero, s1 = Vector256<double>.Zero;
            Vector256<double> q0 = Vector256<double>.Zero, q1 = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256<double> v0 = Vector256.LoadUnsafe(in src, i);
                Vector256<double> v1 = Vector256.LoadUnsafe(in src, i + step);
                s0 += v0; s1 += v1; q0 += v0 * v0; q1 += v1 * v1;
            }
            Vector256<double> accS = s0 + s1; Vector256<double> accQ = q0 + q1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256<double> v = Vector256.LoadUnsafe(in src, i);
                accS += v; accQ += v * v;
            }
            double sum = Vector256.Sum(accS) + ScalarTailSumD(ref src, i, length);
            double sumSq = Vector256.Sum(accQ) + ScalarTailSquareSumD(ref src, i, length);
            double n = (double)length; double mean = sum / n;
            return (sumSq / n) - (mean * mean);
        }
        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            Vector128<double> s0 = Vector128<double>.Zero, s1 = Vector128<double>.Zero;
            Vector128<double> q0 = Vector128<double>.Zero, q1 = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128<double> v0 = Vector128.LoadUnsafe(in src, i);
                Vector128<double> v1 = Vector128.LoadUnsafe(in src, i + step);
                s0 += v0; s1 += v1; q0 += v0 * v0; q1 += v1 * v1;
            }
            Vector128<double> accS = s0 + s1; Vector128<double> accQ = q0 + q1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector128<double> v = Vector128.LoadUnsafe(in src, i);
                accS += v; accQ += v * v;
            }
            double sum = Vector128.Sum(accS) + ScalarTailSumD(ref src, i, length);
            double sumSq = Vector128.Sum(accQ) + ScalarTailSquareSumD(ref src, i, length);
            double n = (double)length; double mean = sum / n;
            return (sumSq / n) - (mean * mean);
        }

        double sSum = 0.0, sSq = 0.0;
        for (i = 0; i < length; ++i)
        {
            double v = Unsafe.Add(ref src, (nint)i);
            sSum += v; sSq += v * v;
        }
        double sn = (double)length; double sm = sSum / sn;
        return (sSq / sn) - (sm * sm);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double ScalarTailSumD(ref double src, nuint start, nuint length)
    {
        double s0 = 0.0, s1 = 0.0;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;
        for (; i < limit; i += 2) { s0 += Unsafe.Add(ref src, (nint)(i + 0)); s1 += Unsafe.Add(ref src, (nint)(i + 1)); }
        double acc = s0 + s1;
        for (; i < length; ++i) acc += Unsafe.Add(ref src, (nint)i);
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double ScalarTailSquareSumD(ref double src, nuint start, nuint length)
    {
        double s0 = 0.0, s1 = 0.0;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;
        for (; i < limit; i += 2)
        {
            double v0 = Unsafe.Add(ref src, (nint)(i + 0)); double v1 = Unsafe.Add(ref src, (nint)(i + 1));
            s0 += v0 * v0; s1 += v1 * v1;
        }
        double acc = s0 + s1;
        for (; i < length; ++i) { double v = Unsafe.Add(ref src, (nint)i); acc += v * v; }
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeStdDev(ReadOnlySpan<double> source) => Math.Sqrt(ComputeVariance(source));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeDotProduct(ReadOnlySpan<double> left, ReadOnlySpan<double> right)
    {
        if (left.Length != right.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return 0.0;

        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            Vector512<double> a0 = Vector512<double>.Zero;
            Vector512<double> a1 = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i);
                a1 += Vector512.LoadUnsafe(in lRef, i + step) * Vector512.LoadUnsafe(in rRef, i + step);
            }

            Vector512<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i);
            }

            return Vector512.Sum(acc) + ScalarTailDot(ref lRef, ref rRef, i, length);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            Vector256<double> a0 = Vector256<double>.Zero;
            Vector256<double> a1 = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i);
                a1 += Vector256.LoadUnsafe(in lRef, i + step) * Vector256.LoadUnsafe(in rRef, i + step);
            }

            Vector256<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i);
            }

            return Vector256.Sum(acc) + ScalarTailDot(ref lRef, ref rRef, i, length);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            Vector128<double> a0 = Vector128<double>.Zero;
            Vector128<double> a1 = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i);
                a1 += Vector128.LoadUnsafe(in lRef, i + step) * Vector128.LoadUnsafe(in rRef, i + step);
            }

            Vector128<double> acc = a0 + a1;
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
    private static double ScalarTailDot(ref double lRef, ref double rRef, nuint start, nuint length)
    {
        double d0 = 0.0, d1 = 0.0;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;

        for (; i < limit; i += 2)
        {
            d0 += Unsafe.Add(ref lRef, (nint)(i + 0)) * Unsafe.Add(ref rRef, (nint)(i + 0));
            d1 += Unsafe.Add(ref lRef, (nint)(i + 1)) * Unsafe.Add(ref rRef, (nint)(i + 1));
        }

        double acc = d0 + d1;
        for (; i < length; ++i)
        {
            acc += Unsafe.Add(ref lRef, (nint)i) * Unsafe.Add(ref rRef, (nint)i);
        }
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeL2Norm(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            Vector512<double> a0 = Vector512<double>.Zero;
            Vector512<double> a1 = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<double> v0 = Vector512.LoadUnsafe(in src, i);
                Vector512<double> v1 = Vector512.LoadUnsafe(in src, i + step);
                a0 += v0 * v0;
                a1 += v1 * v1;
            }

            Vector512<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512<double> v = Vector512.LoadUnsafe(in src, i);
                acc += v * v;
            }

            double sumOfSquares = Vector512.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
            return Math.Sqrt(sumOfSquares);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            Vector256<double> a0 = Vector256<double>.Zero;
            Vector256<double> a1 = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<double> v0 = Vector256.LoadUnsafe(in src, i);
                Vector256<double> v1 = Vector256.LoadUnsafe(in src, i + step);
                a0 += v0 * v0;
                a1 += v1 * v1;
            }

            Vector256<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256<double> v = Vector256.LoadUnsafe(in src, i);
                acc += v * v;
            }

            double sumOfSquares = Vector256.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
            return Math.Sqrt(sumOfSquares);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            Vector128<double> a0 = Vector128<double>.Zero;
            Vector128<double> a1 = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<double> v0 = Vector128.LoadUnsafe(in src, i);
                Vector128<double> v1 = Vector128.LoadUnsafe(in src, i + step);
                a0 += v0 * v0;
                a1 += v1 * v1;
            }

            Vector128<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector128<double> v = Vector128.LoadUnsafe(in src, i);
                acc += v * v;
            }

            double sumOfSquares = Vector128.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
            return Math.Sqrt(sumOfSquares);
        }

        return Math.Sqrt(ScalarTailSquareSum(ref src, 0, length));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeL1Norm(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            Vector512<ulong> absMask = Vector512.Create(0x7FFFFFFFFFFFFFFFUL);
            Vector512<double> a0 = Vector512<double>.Zero, a1 = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                a0 += (Vector512.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
                a1 += (Vector512.LoadUnsafe(in src, i + step).AsUInt64() & absMask).AsDouble();
            }
            Vector512<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                acc += (Vector512.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
            return Vector512.Sum(acc) + ScalarTailAbsSum(ref src, i, length);
        }
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            Vector256<ulong> absMask = Vector256.Create(0x7FFFFFFFFFFFFFFFUL);
            Vector256<double> a0 = Vector256<double>.Zero, a1 = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                a0 += (Vector256.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
                a1 += (Vector256.LoadUnsafe(in src, i + step).AsUInt64() & absMask).AsDouble();
            }
            Vector256<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                acc += (Vector256.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
            return Vector256.Sum(acc) + ScalarTailAbsSum(ref src, i, length);
        }
        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            Vector128<ulong> absMask = Vector128.Create(0x7FFFFFFFFFFFFFFFUL);
            Vector128<double> a0 = Vector128<double>.Zero, a1 = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                a0 += (Vector128.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
                a1 += (Vector128.LoadUnsafe(in src, i + step).AsUInt64() & absMask).AsDouble();
            }
            Vector128<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                acc += (Vector128.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
            return Vector128.Sum(acc) + ScalarTailAbsSum(ref src, i, length);
        }
        return ScalarTailAbsSum(ref src, 0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double ScalarTailAbsSum(ref double src, nuint start, nuint length)
    {
        double s0 = 0.0, s1 = 0.0;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;
        for (; i < limit; i += 2) { s0 += Math.Abs(Unsafe.Add(ref src, (nint)(i + 0))); s1 += Math.Abs(Unsafe.Add(ref src, (nint)(i + 1))); }
        double acc = s0 + s1;
        for (; i < length; ++i) acc += Math.Abs(Unsafe.Add(ref src, (nint)i));
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeLinfNorm(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<ulong> absMask = Vector512.Create(0x7FFFFFFFFFFFFFFFUL);
            Vector512<double> vMax = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<double> abs = (Vector512.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
                vMax = Vector512.Max(vMax, abs);
            }
            double result = 0.0;
            for (int lane = 0; lane < Vector512<double>.Count; ++lane) result = double.Max(result, vMax.GetElement(lane));
            for (; i < length; ++i) result = double.Max(result, Math.Abs(Unsafe.Add(ref src, (nint)i)));
            return result;
        }
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<ulong> absMask = Vector256.Create(0x7FFFFFFFFFFFFFFFUL);
            Vector256<double> vMax = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<double> abs = (Vector256.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
                vMax = Vector256.Max(vMax, abs);
            }
            double result = 0.0;
            for (int lane = 0; lane < Vector256<double>.Count; ++lane) result = double.Max(result, vMax.GetElement(lane));
            for (; i < length; ++i) result = double.Max(result, Math.Abs(Unsafe.Add(ref src, (nint)i)));
            return result;
        }
        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<ulong> absMask = Vector128.Create(0x7FFFFFFFFFFFFFFFUL);
            Vector128<double> vMax = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<double> abs = (Vector128.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
                vMax = Vector128.Max(vMax, abs);
            }
            double result = 0.0;
            for (int lane = 0; lane < Vector128<double>.Count; ++lane) result = double.Max(result, vMax.GetElement(lane));
            for (; i < length; ++i) result = double.Max(result, Math.Abs(Unsafe.Add(ref src, (nint)i)));
            return result;
        }

        double sMax = 0.0;
        for (; i < length; ++i) sMax = double.Max(sMax, Math.Abs(Unsafe.Add(ref src, (nint)i)));
        return sMax;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ExtremaPair<double> ComputeExtrema(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            Vector512<double> vMin0 = Vector512.LoadUnsafe(in src, 0);
            Vector512<double> vMax0 = vMin0;
            Vector512<double> vMin1 = Vector512.LoadUnsafe(in src, step);
            Vector512<double> vMax1 = vMin1;
            nuint limit = length - (step * 2) + 1;

            for (i = step * 2; i < limit; i += step * 2)
            {
                Vector512<double> v0 = Vector512.LoadUnsafe(in src, i);
                Vector512<double> v1 = Vector512.LoadUnsafe(in src, i + step);
                vMin0 = Vector512.Min(vMin0, v0);
                vMax0 = Vector512.Max(vMax0, v0);
                vMin1 = Vector512.Min(vMin1, v1);
                vMax1 = Vector512.Max(vMax1, v1);
            }

            Vector512<double> vMin = Vector512.Min(vMin0, vMin1);
            Vector512<double> vMax = Vector512.Max(vMax0, vMax1);

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512<double> val = Vector512.LoadUnsafe(in src, i);
                vMin = Vector512.Min(vMin, val);
                vMax = Vector512.Max(vMax, val);
            }

            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            for (int lane = 0; lane < Vector512<double>.Count; ++lane)
            {
                min = double.Min(min, vMin.GetElement(lane));
                max = double.Max(max, vMax.GetElement(lane));
            }

            for (; i < length; ++i)
            {
                double v = Unsafe.Add(ref src, (nint)i);
                min = double.Min(min, v);
                max = double.Max(max, v);
            }
            return new ExtremaPair<double>(min, max);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            Vector256<double> vMin0 = Vector256.LoadUnsafe(in src, 0);
            Vector256<double> vMax0 = vMin0;
            Vector256<double> vMin1 = Vector256.LoadUnsafe(in src, step);
            Vector256<double> vMax1 = vMin1;
            nuint limit = length - (step * 2) + 1;

            for (i = step * 2; i < limit; i += step * 2)
            {
                Vector256<double> v0 = Vector256.LoadUnsafe(in src, i);
                Vector256<double> v1 = Vector256.LoadUnsafe(in src, i + step);
                vMin0 = Vector256.Min(vMin0, v0);
                vMax0 = Vector256.Max(vMax0, v0);
                vMin1 = Vector256.Min(vMin1, v1);
                vMax1 = Vector256.Max(vMax1, v1);
            }

            Vector256<double> vMin = Vector256.Min(vMin0, vMin1);
            Vector256<double> vMax = Vector256.Max(vMax0, vMax1);

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256<double> val = Vector256.LoadUnsafe(in src, i);
                vMin = Vector256.Min(vMin, val);
                vMax = Vector256.Max(vMax, val);
            }

            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            for (int lane = 0; lane < Vector256<double>.Count; ++lane)
            {
                min = double.Min(min, vMin.GetElement(lane));
                max = double.Max(max, vMax.GetElement(lane));
            }

            for (; i < length; ++i)
            {
                double v = Unsafe.Add(ref src, (nint)i);
                min = double.Min(min, v);
                max = double.Max(max, v);
            }
            return new ExtremaPair<double>(min, max);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vMin = Vector128.LoadUnsafe(in src, 0);
            Vector128<double> vMax = vMin;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;

            for (i = step; i < limit; i += step)
            {
                Vector128<double> val = Vector128.LoadUnsafe(in src, i);
                vMin = Vector128.Min(vMin, val);
                vMax = Vector128.Max(vMax, val);
            }

            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            for (int lane = 0; lane < Vector128<double>.Count; ++lane)
            {
                min = double.Min(min, vMin.GetElement(lane));
                max = double.Max(max, vMax.GetElement(lane));
            }

            for (; i < length; ++i)
            {
                double v = Unsafe.Add(ref src, (nint)i);
                min = double.Min(min, v);
                max = double.Max(max, v);
            }
            return new ExtremaPair<double>(min, max);
        }

        double sMin = Unsafe.Add(ref src, 0);
        double sMax = sMin;
        for (i = 1; i < length; ++i)
        {
            double v = Unsafe.Add(ref src, (nint)i);
            sMin = double.Min(sMin, v);
            sMax = double.Max(sMax, v);
        }
        return new ExtremaPair<double>(sMin, sMax);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint ComputeArgMin(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();
        ref double src = ref MemoryMarshal.GetReference(source);
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            Vector512<double> vMin = Vector512.LoadUnsafe(in src, 0);
            for (nuint j = step; j < limit; j += step) vMin = Vector512.Min(vMin, Vector512.LoadUnsafe(in src, j));
            double minVal = double.PositiveInfinity;
            for (int lane = 0; lane < Vector512<double>.Count; ++lane) minVal = double.Min(minVal, vMin.GetElement(lane));
            for (nuint j = 0; j < length; ++j) if (Unsafe.Add(ref src, (nint)j) == minVal) return j;
            return 0;
        }
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            Vector256<double> vMin = Vector256.LoadUnsafe(in src, 0);
            for (nuint j = step; j < limit; j += step) vMin = Vector256.Min(vMin, Vector256.LoadUnsafe(in src, j));
            double minVal = double.PositiveInfinity;
            for (int lane = 0; lane < Vector256<double>.Count; ++lane) minVal = double.Min(minVal, vMin.GetElement(lane));
            for (nuint j = 0; j < length; ++j) if (Unsafe.Add(ref src, (nint)j) == minVal) return j;
            return 0;
        }
        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            Vector128<double> vMin = Vector128.LoadUnsafe(in src, 0);
            for (nuint j = step; j < limit; j += step) vMin = Vector128.Min(vMin, Vector128.LoadUnsafe(in src, j));
            double minVal = double.PositiveInfinity;
            for (int lane = 0; lane < Vector128<double>.Count; ++lane) minVal = double.Min(minVal, vMin.GetElement(lane));
            for (nuint j = 0; j < length; ++j) if (Unsafe.Add(ref src, (nint)j) == minVal) return j;
            return 0;
        }
        double sMin = Unsafe.Add(ref src, 0); nuint idx = 0;
        for (nuint j = 1; j < length; ++j) { double v = Unsafe.Add(ref src, (nint)j); if (v < sMin) { sMin = v; idx = j; } }
        return idx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint ComputeArgMax(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();
        ref double src = ref MemoryMarshal.GetReference(source);
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            Vector512<double> vMax = Vector512.LoadUnsafe(in src, 0);
            for (nuint j = step; j < limit; j += step) vMax = Vector512.Max(vMax, Vector512.LoadUnsafe(in src, j));
            double maxVal = double.NegativeInfinity;
            for (int lane = 0; lane < Vector512<double>.Count; ++lane) maxVal = double.Max(maxVal, vMax.GetElement(lane));
            for (nuint j = 0; j < length; ++j) if (Unsafe.Add(ref src, (nint)j) == maxVal) return j;
            return 0;
        }
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            Vector256<double> vMax = Vector256.LoadUnsafe(in src, 0);
            for (nuint j = step; j < limit; j += step) vMax = Vector256.Max(vMax, Vector256.LoadUnsafe(in src, j));
            double maxVal = double.NegativeInfinity;
            for (int lane = 0; lane < Vector256<double>.Count; ++lane) maxVal = double.Max(maxVal, vMax.GetElement(lane));
            for (nuint j = 0; j < length; ++j) if (Unsafe.Add(ref src, (nint)j) == maxVal) return j;
            return 0;
        }
        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            Vector128<double> vMax = Vector128.LoadUnsafe(in src, 0);
            for (nuint j = step; j < limit; j += step) vMax = Vector128.Max(vMax, Vector128.LoadUnsafe(in src, j));
            double maxVal = double.NegativeInfinity;
            for (int lane = 0; lane < Vector128<double>.Count; ++lane) maxVal = double.Max(maxVal, vMax.GetElement(lane));
            for (nuint j = 0; j < length; ++j) if (Unsafe.Add(ref src, (nint)j) == maxVal) return j;
            return 0;
        }
        double sMax = Unsafe.Add(ref src, 0); nuint idx = 0;
        for (nuint j = 1; j < length; ++j) { double v = Unsafe.Add(ref src, (nint)j); if (v > sMax) { sMax = v; idx = j; } }
        return idx;
    }
}
