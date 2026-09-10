namespace Axrone.Simd;

public static unsafe partial class SimdFloat32
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Fill(Span<float> destination, float value)
    {
        nuint length = (nuint)destination.Length;
        if (length == 0) return;
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> v = Vector512.Create(value);
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> v = Vector256.Create(value);
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> v = Vector128.Create(value);
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void FillLinear(Span<float> destination, float start, float step)
    {
        nuint length = (nuint)destination.Length;
        if (length == 0) return;
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vStart = Vector512.Create(start);
            Vector512<float> indices = Vector512.Create(0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 13f, 14f, 15f);
            Vector512<float> vStep = Vector512.Create(step);
            nuint vc = (nuint)Vector512<float>.Count;
            Vector512<float> vInc = Vector512.Create((float)vc * step);
            nuint vLimit = (length / vc) * vc;
            for (; i < vLimit; i += vc)
            {
                (vStart + indices * vStep).StoreUnsafe(ref dRef, i);
                vStart += vInc;
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vStart = Vector256.Create(start);
            Vector256<float> indices = Vector256.Create(0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f);
            Vector256<float> vStep = Vector256.Create(step);
            nuint vc = (nuint)Vector256<float>.Count;
            Vector256<float> vInc = Vector256.Create((float)vc * step);
            nuint vLimit = (length / vc) * vc;
            for (; i < vLimit; i += vc)
            {
                (vStart + indices * vStep).StoreUnsafe(ref dRef, i);
                vStart += vInc;
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vStart = Vector128.Create(start);
            Vector128<float> indices = Vector128.Create(0f, 1f, 2f, 3f);
            Vector128<float> vStep = Vector128.Create(step);
            nuint vc = (nuint)Vector128<float>.Count;
            Vector128<float> vInc = Vector128.Create((float)vc * step);
            nuint vLimit = (length / vc) * vc;
            for (; i < vLimit; i += vc)
            {
                (vStart + indices * vStep).StoreUnsafe(ref dRef, i);
                vStart += vInc;
            }
        }
        float val = start + (float)i * step;
        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = val;
            val += step;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Gather(ReadOnlySpan<float> source, ReadOnlySpan<int> indices, Span<float> destination)
    {
        if (destination.Length < indices.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        for (; i < count; ++i)
            Unsafe.Add(ref dst, (nint)i) = Unsafe.Add(ref src, Unsafe.Add(ref idx, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Scatter(ReadOnlySpan<float> source, ReadOnlySpan<int> indices, Span<float> destination)
    {
        if (source.Length > destination.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        for (; i < count; ++i)
            Unsafe.Add(ref dst, Unsafe.Add(ref idx, (nint)i)) = Unsafe.Add(ref src, (nint)i);
    }

    private static Vector512<float> ExpKernel512(Vector512<float> x)
    {
        const float LOG2E = 1.4426950408889634f;
        Vector512<float> clamped = Vector512.Max(Vector512.Min(x, Vector512.Create(87f)), Vector512.Create(-87f));
        Vector512<float> t = clamped * Vector512.Create(LOG2E);
        Vector512<int> k = Vector512.Floor(t).AsInt32() - Vector512.Create(1);
        Vector512<float> f = t - Vector512.ConvertToSingle(k);
        Vector512<int> biased = k + Vector512.Create(127);
        Vector512<float> pow2 = Vector512.ShiftLeft(biased, 23).AsSingle();
        Vector512<float> u = f * Vector512.Create(0.6931471805599453f);
        Vector512<float> c5 = Vector512.Create(0.00833333333333333f);
        Vector512<float> poly = c5;
        poly = poly * u + Vector512.Create(0.0416666666666667f);
        poly = poly * u + Vector512.Create(0.166666666666667f);
        poly = poly * u + Vector512.Create(0.5f);
        poly = poly * u + Vector512.Create(1f);
        return pow2 * (poly * u + Vector512.Create(1f));
    }

    private static float ExpScalar(float x)
    {
        x = Math.Max(-87f, Math.Min(87f, x));
        float t = x * 1.4426950408889634f;
        int k = (int)Math.Floor(t) - 1;
        float f = t - k;
        float u = f * 0.6931471805599453f;
        float poly = ((((0.00833333333333333f * u + 0.0416666666666667f) * u + 0.166666666666667f) * u + 0.5f) * u + 1f) * u + 1f;
        return pow2f(k) * poly;
    }

    private static float pow2f(int k)
    {
        k = Math.Max(-126, Math.Min(127, k));
        return BitConverter.Int32BitsToSingle((k + 127) << 23);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorExp(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) ExpKernel512(Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vLog2E = Vector256.Create(1.4426950408889634f);
            Vector256<float> vLn2 = Vector256.Create(0.6931471805599453f);
            Vector256<float> vClamp = Vector256.Create(87f);
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<float> x = Vector256.Max(Vector256.Min(Vector256.LoadUnsafe(in src, i), vClamp), -vClamp);
                Vector256<float> tt = x * vLog2E;
                Vector256<int> k2 = Vector256.Floor(tt).AsInt32() - Vector256.Create(1);
                Vector256<float> f2 = tt - Vector256.ConvertToSingle(k2);
                Vector256<int> b2 = k2 + Vector256.Create(127);
                Vector256<float> p2 = Vector256.ShiftLeft(b2, 23).AsSingle();
                Vector256<float> u2 = f2 * vLn2;
                Vector256<float> q2 = Vector256.Create(0.00833333333333333f);
                q2 = q2 * u2 + Vector256.Create(0.0416666666666667f);
                q2 = q2 * u2 + Vector256.Create(0.166666666666667f);
                q2 = q2 * u2 + Vector256.Create(0.5f);
                q2 = q2 * u2 + Vector256.Create(1f);
                (p2 * (q2 * u2 + Vector256.Create(1f))).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vLog2E = Vector128.Create(1.4426950408889634f);
            Vector128<float> vLn2 = Vector128.Create(0.6931471805599453f);
            Vector128<float> vClamp = Vector128.Create(87f);
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<float> x = Vector128.Max(Vector128.Min(Vector128.LoadUnsafe(in src, i), vClamp), -vClamp);
                Vector128<float> tt = x * vLog2E;
                Vector128<int> k2 = Vector128.Floor(tt).AsInt32() - Vector128.Create(1);
                Vector128<float> f2 = tt - Vector128.ConvertToSingle(k2);
                Vector128<int> b2 = k2 + Vector128.Create(127);
                Vector128<float> p2 = Vector128.ShiftLeft(b2, 23).AsSingle();
                Vector128<float> u2 = f2 * vLn2;
                Vector128<float> q2 = Vector128.Create(0.00833333333333333f);
                q2 = q2 * u2 + Vector128.Create(0.0416666666666667f);
                q2 = q2 * u2 + Vector128.Create(0.166666666666667f);
                q2 = q2 * u2 + Vector128.Create(0.5f);
                q2 = q2 * u2 + Vector128.Create(1f);
                (p2 * (q2 * u2 + Vector128.Create(1f))).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = ExpScalar(Unsafe.Add(ref src, (nint)i));
    }

    private static Vector512<float> LogKernel512(Vector512<float> x)
    {
        Vector512<float> v = x;
        Vector512<float> n = Vector512<float>.Zero;
        Vector512<float> one = Vector512.Create(1f);
        Vector512<float> two = Vector512.Create(2f);
        Vector512<float> half = Vector512.Create(0.5f);
        for (int iter = 0; iter < 200; ++iter)
        {
            Vector512<float> g = Vector512.GreaterThanOrEqual(v, two);
            if (Vector512.Equals(g, Vector512<int>.Zero)) break;
            v = Vector512.ConditionalSelect(g, v * half, v);
            n += Vector512.ConditionalSelect(g, one, Vector512<float>.Zero);
        }
        for (int iter = 0; iter < 200; ++iter)
        {
            Vector512<float> l = Vector512.LessThan(v, one);
            if (Vector512.Equals(l, Vector512<int>.Zero)) break;
            v = Vector512.ConditionalSelect(l, v * two, v);
            n -= Vector512.ConditionalSelect(l, one, Vector512<float>.Zero);
        }
        Vector512<float> u = v - one;
        Vector512<float> poly = Vector512.Create(-0.020835085f);
        poly = poly * u + Vector512.Create(0.0277272808f);
        poly = poly * u + Vector512.Create(-0.0397820075f);
        poly = poly * u + Vector512.Create(0.0667107478f);
        poly = poly * u + Vector512.Create(-0.117496403f);
        poly = poly * u + Vector512.Create(0.333331568f);
        return n * Vector512.Create(0.6931471805599453f) + (poly * u + Vector512.Create(1f)) * u;
    }

    private static float LogScalar(float x)
    {
        if (x <= 0f) return float.NegativeInfinity;
        float v = x, n = 0f;
        for (int iter = 0; iter < 200 && v >= 2f; ++iter) { v *= 0.5f; n += 1f; }
        for (int iter = 0; iter < 200 && v < 1f; ++iter) { v *= 2f; n -= 1f; }
        float u = v - 1f;
        float poly = (((((-0.020835085f * u + 0.0277272808f) * u - 0.0397820075f) * u + 0.0667107478f) * u - 0.117496403f) * u + 0.333331568f) * u + 1f;
        return n * 0.6931471805599453f + poly * u;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorLog(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) LogKernel512(Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> one = Vector256.Create(1f), two = Vector256.Create(2f), half = Vector256.Create(0.5f);
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<float> v = Vector256.LoadUnsafe(in src, i), nn = Vector256<float>.Zero;
                for (int it = 0; it < 200; ++it) { var g = Vector256.GreaterThanOrEqual(v, two); if (Vector256.Equals(g, Vector256<int>.Zero)) break; v = Vector256.ConditionalSelect(g, v * half, v); nn += Vector256.ConditionalSelect(g, one, Vector256<float>.Zero); }
                for (int it = 0; it < 200; ++it) { var l = Vector256.LessThan(v, one); if (Vector256.Equals(l, Vector256<int>.Zero)) break; v = Vector256.ConditionalSelect(l, v * two, v); nn -= Vector256.ConditionalSelect(l, one, Vector256<float>.Zero); }
                Vector256<float> u2 = v - one;
                Vector256<float> q2 = Vector256.Create(-0.020835085f);
                q2 = q2 * u2 + Vector256.Create(0.0277272808f); q2 = q2 * u2 + Vector256.Create(-0.0397820075f);
                q2 = q2 * u2 + Vector256.Create(0.0667107478f); q2 = q2 * u2 + Vector256.Create(-0.117496403f);
                q2 = q2 * u2 + Vector256.Create(0.333331568f);
                (nn * Vector256.Create(0.6931471805599453f) + (q2 * u2 + one) * u2).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> one = Vector128.Create(1f), two = Vector128.Create(2f), half = Vector128.Create(0.5f);
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<float> v = Vector128.LoadUnsafe(in src, i), nn = Vector128<float>.Zero;
                for (int it = 0; it < 200; ++it) { var g = Vector128.GreaterThanOrEqual(v, two); if (Vector128.Equals(g, Vector128<int>.Zero)) break; v = Vector128.ConditionalSelect(g, v * half, v); nn += Vector128.ConditionalSelect(g, one, Vector128<float>.Zero); }
                for (int it = 0; it < 200; ++it) { var l = Vector128.LessThan(v, one); if (Vector128.Equals(l, Vector128<int>.Zero)) break; v = Vector128.ConditionalSelect(l, v * two, v); nn -= Vector128.ConditionalSelect(l, one, Vector128<float>.Zero); }
                Vector128<float> u2 = v - one;
                Vector128<float> q2 = Vector128.Create(-0.020835085f);
                q2 = q2 * u2 + Vector128.Create(0.0277272808f); q2 = q2 * u2 + Vector128.Create(-0.0397820075f);
                q2 = q2 * u2 + Vector128.Create(0.0667107478f); q2 = q2 * u2 + Vector128.Create(-0.117496403f);
                q2 = q2 * u2 + Vector128.Create(0.333331568f);
                (nn * Vector128.Create(0.6931471805599453f) + (q2 * u2 + one) * u2).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = LogScalar(Unsafe.Add(ref src, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorSigmoid(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<float> neg = Vector512.Create(0f) - Vector512.LoadUnsafe(in src, i);
                Vector512<float> e = ExpKernel512(neg);
                (Vector512.Create(1f) / (Vector512.Create(1f) + e)).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<float> neg = Vector256.Create(0f) - Vector256.LoadUnsafe(in src, i);
                Vector256<float> clamped = Vector256.Max(Vector256.Min(neg, Vector256.Create(87f)), Vector256.Create(-87f));
                Vector256<float> t = clamped * Vector256.Create(1.4426950408889634f);
                Vector256<int> k = Vector256.Floor(t).AsInt32() - Vector256.Create(1);
                Vector256<float> f = t - Vector256.ConvertToSingle(k);
                Vector256<float> p2 = Vector256.ShiftLeft(k + Vector256.Create(127), 23).AsSingle();
                Vector256<float> u = f * Vector256.Create(0.6931471805599453f);
                Vector256<float> poly = Vector256.Create(0.00833333333333333f);
                poly = poly * u + Vector256.Create(0.0416666666666667f); poly = poly * u + Vector256.Create(0.166666666666667f);
                poly = poly * u + Vector256.Create(0.5f); poly = poly * u + Vector256.Create(1f);
                Vector256<float> e = p2 * (poly * u + Vector256.Create(1f));
                (Vector256.Create(1f) / (Vector256.Create(1f) + e)).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<float> neg = Vector128.Create(0f) - Vector128.LoadUnsafe(in src, i);
                Vector128<float> clamped = Vector128.Max(Vector128.Min(neg, Vector128.Create(87f)), Vector128.Create(-87f));
                Vector128<float> t = clamped * Vector128.Create(1.4426950408889634f);
                Vector128<int> k = Vector128.Floor(t).AsInt32() - Vector128.Create(1);
                Vector128<float> f = t - Vector128.ConvertToSingle(k);
                Vector128<float> p2 = Vector128.ShiftLeft(k + Vector128.Create(127), 23).AsSingle();
                Vector128<float> u = f * Vector128.Create(0.6931471805599453f);
                Vector128<float> poly = Vector128.Create(0.00833333333333333f);
                poly = poly * u + Vector128.Create(0.0416666666666667f); poly = poly * u + Vector128.Create(0.166666666666667f);
                poly = poly * u + Vector128.Create(0.5f); poly = poly * u + Vector128.Create(1f);
                Vector128<float> e = p2 * (poly * u + Vector128.Create(1f));
                (Vector128.Create(1f) / (Vector128.Create(1f) + e)).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i)
        {
            float x = Unsafe.Add(ref src, (nint)i);
            Unsafe.Add(ref dst, (nint)i) = 1f / (1f + MathF.Exp(-x));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorTanh(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vClamp = Vector512.Create(9f);
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<float> x = Vector512.Max(Vector512.Min(Vector512.LoadUnsafe(in src, i), vClamp), -vClamp);
                Vector512<float> p = ExpKernel512(x * Vector512.Create(2f));
                ((p - Vector512.Create(1f)) / (p + Vector512.Create(1f))).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<float> x = Vector256.Max(Vector256.Min(Vector256.LoadUnsafe(in src, i), Vector256.Create(9f)), Vector256.Create(-9f));
                Vector256<float> cx = x * Vector256.Create(1.4426950408889634f * 2f);
                Vector256<int> k = Vector256.Floor(cx).AsInt32() - Vector256.Create(1);
                Vector256<float> f = cx - Vector256.ConvertToSingle(k);
                Vector256<float> p2 = Vector256.ShiftLeft(k + Vector256.Create(127), 23).AsSingle();
                Vector256<float> u = f * Vector256.Create(0.6931471805599453f);
                Vector256<float> poly = Vector256.Create(0.00833333333333333f);
                poly = poly * u + Vector256.Create(0.0416666666666667f); poly = poly * u + Vector256.Create(0.166666666666667f);
                poly = poly * u + Vector256.Create(0.5f); poly = poly * u + Vector256.Create(1f);
                Vector256<float> p = p2 * (poly * u + Vector256.Create(1f));
                ((p - Vector256.Create(1f)) / (p + Vector256.Create(1f))).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<float> x = Vector128.Max(Vector128.Min(Vector128.LoadUnsafe(in src, i), Vector128.Create(9f)), Vector128.Create(-9f));
                Vector128<float> cx = x * Vector128.Create(1.4426950408889634f * 2f);
                Vector128<int> k = Vector128.Floor(cx).AsInt32() - Vector128.Create(1);
                Vector128<float> f = cx - Vector128.ConvertToSingle(k);
                Vector128<float> p2 = Vector128.ShiftLeft(k + Vector128.Create(127), 23).AsSingle();
                Vector128<float> u = f * Vector128.Create(0.6931471805599453f);
                Vector128<float> poly = Vector128.Create(0.00833333333333333f);
                poly = poly * u + Vector128.Create(0.0416666666666667f); poly = poly * u + Vector128.Create(0.166666666666667f);
                poly = poly * u + Vector128.Create(0.5f); poly = poly * u + Vector128.Create(1f);
                Vector128<float> p = p2 * (poly * u + Vector128.Create(1f));
                ((p - Vector128.Create(1f)) / (p + Vector128.Create(1f))).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i)
        {
            float x = Unsafe.Add(ref src, (nint)i);
            float e2x = MathF.Exp(2f * Math.Max(-9f, Math.Min(9f, x)));
            Unsafe.Add(ref dst, (nint)i) = (e2x - 1f) / (e2x + 1f);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorReLU(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> zero = Vector512<float>.Zero;
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.Max(Vector512.LoadUnsafe(in src, i), zero).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> zero = Vector256<float>.Zero;
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.Max(Vector256.LoadUnsafe(in src, i), zero).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> zero = Vector128<float>.Zero;
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.Max(Vector128.LoadUnsafe(in src, i), zero).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i)
        {
            float v = Unsafe.Add(ref src, (nint)i);
            Unsafe.Add(ref dst, (nint)i) = v > 0f ? v : 0f;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorRSqrt(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector512.Create(1f) / Vector512.Sqrt(Vector512.LoadUnsafe(in src, i))).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector256.Create(1f) / Vector256.Sqrt(Vector256.LoadUnsafe(in src, i))).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector128.Create(1f) / Vector128.Sqrt(Vector128.LoadUnsafe(in src, i))).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dst, (nint)i) = 1f / MathF.Sqrt(Unsafe.Add(ref src, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorPow(ReadOnlySpan<float> @base, ReadOnlySpan<float> exponent, Span<float> destination)
    {
        if (@base.Length != exponent.Length || destination.Length < @base.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)@base.Length;
        if (length == 0) return;
        ref float bRef = ref MemoryMarshal.GetReference(@base);
        ref float eRef = ref MemoryMarshal.GetReference(exponent);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<float> b = Vector512.LoadUnsafe(in bRef, i);
                Vector512<float> e = Vector512.LoadUnsafe(in eRef, i);
                (ExpKernel512(e * LogKernel512(b))).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<float> b = Vector256.LoadUnsafe(in bRef, i);
                Vector256<float> e = Vector256.LoadUnsafe(in eRef, i);
                Vector256<float> lv = Vector256.Create(1f), nn = Vector256<float>.Zero;
                Vector256<float> two = Vector256.Create(2f), half = Vector256.Create(0.5f);
                for (int it = 0; it < 200; ++it) { var g = Vector256.GreaterThanOrEqual(b, two); if (Vector256.Equals(g, Vector256<int>.Zero)) break; b = Vector256.ConditionalSelect(g, b * half, b); nn += Vector256.ConditionalSelect(g, lv, Vector256<float>.Zero); }
                for (int it = 0; it < 200; ++it) { var l = Vector256.LessThan(b, lv); if (Vector256.Equals(l, Vector256<int>.Zero)) break; b = Vector256.ConditionalSelect(l, b * two, b); nn -= Vector256.ConditionalSelect(l, lv, Vector256<float>.Zero); }
                Vector256<float> u = b - lv;
                Vector256<float> poly = Vector256.Create(-0.020835085f);
                poly = poly * u + Vector256.Create(0.0277272808f); poly = poly * u + Vector256.Create(-0.0397820075f);
                poly = poly * u + Vector256.Create(0.0667107478f); poly = poly * u + Vector256.Create(-0.117496403f);
                poly = poly * u + Vector256.Create(0.333331568f);
                Vector256<float> logB = nn * Vector256.Create(0.6931471805599453f) + (poly * u + lv) * u;
                Vector256<float> x = e * logB;
                Vector256<float> clamped = Vector256.Max(Vector256.Min(x, Vector256.Create(87f)), Vector256.Create(-87f));
                Vector256<float> t = clamped * Vector256.Create(1.4426950408889634f);
                Vector256<int> k = Vector256.Floor(t).AsInt32() - Vector256.Create(1);
                Vector256<float> f = t - Vector256.ConvertToSingle(k);
                Vector256<float> p2 = Vector256.ShiftLeft(k + Vector256.Create(127), 23).AsSingle();
                Vector256<float> uu = f * Vector256.Create(0.6931471805599453f);
                Vector256<float> ep = Vector256.Create(0.00833333333333333f);
                ep = ep * uu + Vector256.Create(0.0416666666666667f); ep = ep * uu + Vector256.Create(0.166666666666667f);
                ep = ep * uu + Vector256.Create(0.5f); ep = ep * uu + Vector256.Create(1f);
                (p2 * (ep * uu + Vector256.Create(1f))).StoreUnsafe(ref dRef, i);
            }
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = MathF.Pow(Unsafe.Add(ref bRef, (nint)i), Unsafe.Add(ref eRef, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Softmax(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        float maxVal = float.NegativeInfinity;
        for (nuint j = 0; j < length; ++j) { float v = Unsafe.Add(ref src, (nint)j); if (v > maxVal) maxVal = v; }
        float sum = 0f;
        for (nuint j = 0; j < length; ++j) { float e = MathF.Exp(Unsafe.Add(ref src, (nint)j) - maxVal); Unsafe.Add(ref dst, (nint)j) = e; sum += e; }
        float inv = 1f / sum;
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vInv = Vector512.Create(inv);
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector512.LoadUnsafe(in dst, i) * vInv).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vInv = Vector256.Create(inv);
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector256.LoadUnsafe(in dst, i) * vInv).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vInv = Vector128.Create(inv);
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector128.LoadUnsafe(in dst, i) * vInv).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) *= inv;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void UnpackHalfToFloat(ReadOnlySpan<byte> source, Span<float> destination)
    {
        nuint count = (nuint)(source.Length / 2);
        if ((nuint)destination.Length < count) ThrowHelper.ThrowDestinationTooSmall();
        if (count == 0) return;
        ref ushort src = ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetReference(source));
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector256.IsHardwareAccelerated && count >= (nuint)Vector256<ushort>.Count)
        {
            nuint step = (nuint)Vector256<ushort>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<ushort> h = Vector256.LoadUnsafe(in src, i);
                Vector256<uint> sign = (h.AsUInt32() & Vector256.Create(0x80008000u)) << 16;
                Vector256<uint> exp = ((h.AsUInt32() & Vector256.Create(0x7C007C00u)) + Vector256.Create(0x38003800u)) & Vector256.Create(0x7F807F80u);
                Vector256<uint> mant = (h.AsUInt32() & Vector256.Create(0x03FF03FFu)) << 13;
                (sign | exp | mant).AsSingle().StoreUnsafe(ref dst, i);
            }
        }
        for (; i < count; ++i)
        {
            uint h = Unsafe.Add(ref src, (nint)i);
            uint f = ((h & 0x8000u) << 16) | (((h & 0x7C00u) + 0x38000000u) & 0x7F800000u) | ((h & 0x03FFu) << 13);
            Unsafe.Add(ref dst, (nint)i) = BitConverter.UInt32BitsToSingle(f);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Mat4x4Multiply(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length < 16 || right.Length < 16 || destination.Length < 16)
            ThrowHelper.ThrowMismatchedSpans();
        ref float l = ref MemoryMarshal.GetReference(left);
        ref float r = ref MemoryMarshal.GetReference(right);
        ref float d = ref MemoryMarshal.GetReference(destination);
        if (Vector128.IsHardwareAccelerated)
        {
            for (int col = 0; col < 4; ++col)
            {
                Vector128<float> rCol = Vector128.Create(
                    Unsafe.Add(ref r, (nint)(col * 4 + 0)), Unsafe.Add(ref r, (nint)(col * 4 + 1)),
                    Unsafe.Add(ref r, (nint)(col * 4 + 2)), Unsafe.Add(ref r, (nint)(col * 4 + 3)));
                Vector128<float> result = Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 0))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4));
                result += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 1))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 4));
                result += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 2))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 8));
                result += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 3))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 12));
                result.StoreUnsafe(ref d, (nuint)(col * 4));
            }
        }
        else
        {
            for (int col = 0; col < 4; ++col)
            {
                for (int row = 0; row < 4; ++row)
                {
                    float sum = 0f;
                    for (int k = 0; k < 4; ++k)
                        sum += Unsafe.Add(ref l, (nint)(k * 4 + col)) * Unsafe.Add(ref r, (nint)(row + k * 4));
                    Unsafe.Add(ref d, (nint)(row + col * 4)) = sum;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CrossProduct(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length < 3 || right.Length < 3 || destination.Length < 3) ThrowHelper.ThrowMismatchedSpans();
        ref float l = ref MemoryMarshal.GetReference(left);
        ref float r = ref MemoryMarshal.GetReference(right);
        float lx = Unsafe.Add(ref l, 0), ly = Unsafe.Add(ref l, 1), lz = Unsafe.Add(ref l, 2);
        float rx = Unsafe.Add(ref r, 0), ry = Unsafe.Add(ref r, 1), rz = Unsafe.Add(ref r, 2);
        ref float d = ref MemoryMarshal.GetReference(destination);
        Unsafe.Add(ref d, 0) = ly * rz - lz * ry;
        Unsafe.Add(ref d, 1) = lz * rx - lx * rz;
        Unsafe.Add(ref d, 2) = lx * ry - ly * rx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void QuaternionMultiply(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length < 4 || right.Length < 4 || destination.Length < 4) ThrowHelper.ThrowMismatchedSpans();
        ref float l = ref MemoryMarshal.GetReference(left);
        ref float r = ref MemoryMarshal.GetReference(right);
        float lx = Unsafe.Add(ref l, 0), ly = Unsafe.Add(ref l, 1), lz = Unsafe.Add(ref l, 2), lw = Unsafe.Add(ref l, 3);
        float rx = Unsafe.Add(ref r, 0), ry = Unsafe.Add(ref r, 1), rz = Unsafe.Add(ref r, 2), rw = Unsafe.Add(ref r, 3);
        ref float d = ref MemoryMarshal.GetReference(destination);
        Unsafe.Add(ref d, 0) = lw * rx + lx * rw + ly * rz - lz * ry;
        Unsafe.Add(ref d, 1) = lw * ry - lx * rz + ly * rw + lz * rx;
        Unsafe.Add(ref d, 2) = lw * rz + lx * ry - ly * rx + lz * rw;
        Unsafe.Add(ref d, 3) = lw * rw - lx * rx - ly * ry - lz * rz;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void QuaternionSlerp(ReadOnlySpan<float> from, ReadOnlySpan<float> to, float t, Span<float> destination)
    {
        if (from.Length < 4 || to.Length < 4 || destination.Length < 4) ThrowHelper.ThrowMismatchedSpans();
        ref float a = ref MemoryMarshal.GetReference(from);
        ref float b = ref MemoryMarshal.GetReference(to);
        float dot = Unsafe.Add(ref a, 0) * Unsafe.Add(ref b, 0) + Unsafe.Add(ref a, 1) * Unsafe.Add(ref b, 1)
                  + Unsafe.Add(ref a, 2) * Unsafe.Add(ref b, 2) + Unsafe.Add(ref a, 3) * Unsafe.Add(ref b, 3);
        float bx = Unsafe.Add(ref b, 0), by = Unsafe.Add(ref b, 1), bz = Unsafe.Add(ref b, 2), bw = Unsafe.Add(ref b, 3);
        if (dot < 0f) { dot = -dot; bx = -bx; by = -by; bz = -bz; bw = -bw; }
        ref float d = ref MemoryMarshal.GetReference(destination);
        if (dot > 0.9995f)
        {
            Unsafe.Add(ref d, 0) = Unsafe.Add(ref a, 0) + t * (bx - Unsafe.Add(ref a, 0));
            Unsafe.Add(ref d, 1) = Unsafe.Add(ref a, 1) + t * (by - Unsafe.Add(ref a, 1));
            Unsafe.Add(ref d, 2) = Unsafe.Add(ref a, 2) + t * (bz - Unsafe.Add(ref a, 2));
            Unsafe.Add(ref d, 3) = Unsafe.Add(ref a, 3) + t * (bw - Unsafe.Add(ref a, 3));
            float len = MathF.Sqrt(Unsafe.Add(ref d, 0) * Unsafe.Add(ref d, 0) + Unsafe.Add(ref d, 1) * Unsafe.Add(ref d, 1)
                                 + Unsafe.Add(ref d, 2) * Unsafe.Add(ref d, 2) + Unsafe.Add(ref d, 3) * Unsafe.Add(ref d, 3));
            float inv = 1f / len;
            Unsafe.Add(ref d, 0) *= inv; Unsafe.Add(ref d, 1) *= inv; Unsafe.Add(ref d, 2) *= inv; Unsafe.Add(ref d, 3) *= inv;
        }
        else
        {
            float theta = MathF.Acos(Math.Clamp(dot, -1f, 1f));
            float sinTheta = MathF.Sin(theta);
            float w0 = MathF.Sin((1f - t) * theta) / sinTheta;
            float w1 = MathF.Sin(t * theta) / sinTheta;
            Unsafe.Add(ref d, 0) = w0 * Unsafe.Add(ref a, 0) + w1 * bx;
            Unsafe.Add(ref d, 1) = w0 * Unsafe.Add(ref a, 1) + w1 * by;
            Unsafe.Add(ref d, 2) = w0 * Unsafe.Add(ref a, 2) + w1 * bz;
            Unsafe.Add(ref d, 3) = w0 * Unsafe.Add(ref a, 3) + w1 * bw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Swizzle(ReadOnlySpan<float> source, ReadOnlySpan<int> indices, Span<float> destination)
    {
        if (destination.Length < indices.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector256.IsHardwareAccelerated && count >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
                Vector256.Shuffle(Vector256.LoadUnsafe(in src, i), Vector256.LoadUnsafe(in idx, i).AsInt32()).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && count >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
                Vector128.Shuffle(Vector128.LoadUnsafe(in src, i), Vector128.LoadUnsafe(in idx, i).AsInt32()).StoreUnsafe(ref dst, i);
        }
        for (; i < count; ++i)
            Unsafe.Add(ref dst, (nint)i) = Unsafe.Add(ref src, Unsafe.Add(ref idx, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Span<byte> PackFloatToHalf(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        byte[] result = GC.AllocateUninitializedArray<byte>((int)length * 2);
        ref ushort dst = ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetArrayDataReference(result));
        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<uint> signMask = Vector256.Create(0x80000000u);
            Vector256<uint> expMask = Vector256.Create(0x7F800000u);
            Vector256<uint> mantMask = Vector256.Create(0x007FE000u);
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<uint> bits = Vector256.LoadUnsafe(in src, i).AsUInt32();
                Vector256<uint> sign = (bits & signMask) >> 16;
                Vector256<uint> exp = (bits & expMask) - Vector256.Create(0x38000000u);
                Vector256<uint> mant = (bits & mantMask) >> 13;
                Vector256.ConditionalSelect(Vector256.LessThan((bits & expMask).AsInt32(), Vector256.Create(0x38000000)).AsUInt32(), sign, sign | exp | mant).AsUInt16().StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i)
        {
            uint bits = BitConverter.SingleToUInt32Bits(Unsafe.Add(ref src, (nint)i));
            ushort h = (ushort)(((bits >> 16) & 0x8000u) | (((bits & 0x7F800000u) - 0x38000000u) >> 13) | ((bits & 0x007FE000u) >> 13));
            Unsafe.Add(ref dst, (nint)i) = h;
        }
        return result;
    }
}
