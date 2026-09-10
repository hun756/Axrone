namespace Axrone.Simd;

public static unsafe partial class SimdFloat64
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Fill(Span<double> destination, double value)
    {
        nuint length = (nuint)destination.Length;
        if (length == 0) return;
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> v = Vector512.Create(value);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> v = Vector256.Create(value);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> v = Vector128.Create(value);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void FillLinear(Span<double> destination, double start, double step)
    {
        nuint length = (nuint)destination.Length;
        if (length == 0) return;
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vStart = Vector512.Create(start);
            Vector512<double> indices = Vector512.Create(0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0);
            Vector512<double> vStep = Vector512.Create(step);
            nuint vc = (nuint)Vector512<double>.Count;
            Vector512<double> vInc = Vector512.Create((double)vc * step);
            nuint vLimit = (length / vc) * vc;
            for (; i < vLimit; i += vc) { (vStart + indices * vStep).StoreUnsafe(ref dRef, i); vStart += vInc; }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vStart = Vector256.Create(start);
            Vector256<double> indices = Vector256.Create(0.0, 1.0, 2.0, 3.0);
            Vector256<double> vStep = Vector256.Create(step);
            nuint vc = (nuint)Vector256<double>.Count;
            Vector256<double> vInc = Vector256.Create((double)vc * step);
            nuint vLimit = (length / vc) * vc;
            for (; i < vLimit; i += vc) { (vStart + indices * vStep).StoreUnsafe(ref dRef, i); vStart += vInc; }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vStart = Vector128.Create(start);
            Vector128<double> indices = Vector128.Create(0.0, 1.0);
            Vector128<double> vStep = Vector128.Create(step);
            nuint vc = (nuint)Vector128<double>.Count;
            Vector128<double> vInc = Vector128.Create((double)vc * step);
            nuint vLimit = (length / vc) * vc;
            for (; i < vLimit; i += vc) { (vStart + indices * vStep).StoreUnsafe(ref dRef, i); vStart += vInc; }
        }
        double val = start + (double)i * step;
        for (; i < length; ++i) { Unsafe.Add(ref dRef, (nint)i) = val; val += step; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Gather(ReadOnlySpan<double> source, ReadOnlySpan<int> indices, Span<double> destination)
    {
        if (destination.Length < indices.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        for (; i < count; ++i)
            Unsafe.Add(ref dst, (nint)i) = Unsafe.Add(ref src, Unsafe.Add(ref idx, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Scatter(ReadOnlySpan<double> source, ReadOnlySpan<int> indices, Span<double> destination)
    {
        if (source.Length > destination.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        for (; i < count; ++i)
            Unsafe.Add(ref dst, Unsafe.Add(ref idx, (nint)i)) = Unsafe.Add(ref src, (nint)i);
    }

    private static Vector512<double> ExpKernel512(Vector512<double> x)
    {
        const double LOG2E = 1.4426950408889634;
        const double LN2 = 0.6931471805599453;
        Vector512<double> clamped = Vector512.Max(Vector512.Min(x, Vector512.Create(709.0)), Vector512.Create(-709.0));
        Vector512<double> t = clamped * Vector512.Create(LOG2E);
        Vector512<long> k = Vector512.Floor(t).AsInt64() - Vector512.Create(1L);
        Vector512<double> f = t - Vector512.ConvertToDouble(k);
        Vector512<long> biased = k + Vector512.Create(1023L);
        Vector512<double> pow2 = Vector512.ShiftLeft(biased, 52).AsDouble();
        Vector512<double> u = f * Vector512.Create(LN2);
        Vector512<double> poly = Vector512.Create(0.00833333333333333);
        poly = poly * u + Vector512.Create(0.0416666666666667);
        poly = poly * u + Vector512.Create(0.166666666666667);
        poly = poly * u + Vector512.Create(0.5);
        poly = poly * u + Vector512.Create(1.0);
        return pow2 * (poly * u + Vector512.Create(1.0));
    }

    private static double ExpScalar(double x)
    {
        x = Math.Max(-709.0, Math.Min(709.0, x));
        double t = x * 1.4426950408889634;
        long k = (long)Math.Floor(t) - 1;
        double f = t - k;
        double u = f * 0.6931471805599453;
        double poly = ((((0.00833333333333333 * u + 0.0416666666666667) * u + 0.166666666666667) * u + 0.5) * u + 1.0) * u + 1.0;
        return BitConverter.Int64BitsToDouble((Math.Max(-1022L, Math.Min(1023L, k)) + 1023L) << 52) * poly;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorExp(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) ExpKernel512(Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vLog2E = Vector256.Create(1.4426950408889634), vLn2 = Vector256.Create(0.6931471805599453), vClamp = Vector256.Create(709.0);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<double> x = Vector256.Max(Vector256.Min(Vector256.LoadUnsafe(in src, i), vClamp), -vClamp);
                Vector256<double> tt = x * vLog2E;
                Vector256<long> k2 = Vector256.Floor(tt).AsInt64() - Vector256.Create(1L);
                Vector256<double> f2 = tt - Vector256.ConvertToDouble(k2);
                Vector256<double> p2 = Vector256.ShiftLeft(k2 + Vector256.Create(1023L), 52).AsDouble();
                Vector256<double> u2 = f2 * vLn2;
                Vector256<double> q2 = Vector256.Create(0.00833333333333333);
                q2 = q2 * u2 + Vector256.Create(0.0416666666666667); q2 = q2 * u2 + Vector256.Create(0.166666666666667);
                q2 = q2 * u2 + Vector256.Create(0.5); q2 = q2 * u2 + Vector256.Create(1.0);
                (p2 * (q2 * u2 + Vector256.Create(1.0))).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vLog2E = Vector128.Create(1.4426950408889634), vLn2 = Vector128.Create(0.6931471805599453), vClamp = Vector128.Create(709.0);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<double> x = Vector128.Max(Vector128.Min(Vector128.LoadUnsafe(in src, i), vClamp), -vClamp);
                Vector128<double> tt = x * vLog2E;
                Vector128<long> k2 = Vector128.Floor(tt).AsInt64() - Vector128.Create(1L);
                Vector128<double> f2 = tt - Vector128.ConvertToDouble(k2);
                Vector128<double> p2 = Vector128.ShiftLeft(k2 + Vector128.Create(1023L), 52).AsDouble();
                Vector128<double> u2 = f2 * vLn2;
                Vector128<double> q2 = Vector128.Create(0.00833333333333333);
                q2 = q2 * u2 + Vector128.Create(0.0416666666666667); q2 = q2 * u2 + Vector128.Create(0.166666666666667);
                q2 = q2 * u2 + Vector128.Create(0.5); q2 = q2 * u2 + Vector128.Create(1.0);
                (p2 * (q2 * u2 + Vector128.Create(1.0))).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = ExpScalar(Unsafe.Add(ref src, (nint)i));
    }

    private static Vector512<double> LogKernel512(Vector512<double> x)
    {
        Vector512<double> v = x, n = Vector512<double>.Zero;
        Vector512<double> one = Vector512.Create(1.0), two = Vector512.Create(2.0), half = Vector512.Create(0.5);
        for (int iter = 0; iter < 1200; ++iter)
        {
            Vector512<double> g = Vector512.GreaterThanOrEqual(v, two);
            if (Vector512.Equals(g, Vector512<long>.Zero)) break;
            v = Vector512.ConditionalSelect(g, v * half, v);
            n += Vector512.ConditionalSelect(g, one, Vector512<double>.Zero);
        }
        for (int iter = 0; iter < 1200; ++iter)
        {
            Vector512<double> l = Vector512.LessThan(v, one);
            if (Vector512.Equals(l, Vector512<long>.Zero)) break;
            v = Vector512.ConditionalSelect(l, v * two, v);
            n -= Vector512.ConditionalSelect(l, one, Vector512<double>.Zero);
        }
        Vector512<double> u = v - one;
        Vector512<double> poly = Vector512.Create(-0.020835085);
        poly = poly * u + Vector512.Create(0.0277272808); poly = poly * u + Vector512.Create(-0.0397820075);
        poly = poly * u + Vector512.Create(0.0667107478); poly = poly * u + Vector512.Create(-0.117496403);
        poly = poly * u + Vector512.Create(0.333331568);
        return n * Vector512.Create(0.6931471805599453) + (poly * u + one) * u;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorLog(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) LogKernel512(Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<double> v = Vector256.LoadUnsafe(in src, i), nn = Vector256<double>.Zero;
                Vector256<double> one = Vector256.Create(1.0), two = Vector256.Create(2.0), half = Vector256.Create(0.5);
                for (int it = 0; it < 1200; ++it) { var g = Vector256.GreaterThanOrEqual(v, two); if (Vector256.Equals(g, Vector256<long>.Zero)) break; v = Vector256.ConditionalSelect(g, v * half, v); nn += Vector256.ConditionalSelect(g, one, Vector256<double>.Zero); }
                for (int it = 0; it < 1200; ++it) { var l = Vector256.LessThan(v, one); if (Vector256.Equals(l, Vector256<long>.Zero)) break; v = Vector256.ConditionalSelect(l, v * two, v); nn -= Vector256.ConditionalSelect(l, one, Vector256<double>.Zero); }
                Vector256<double> u2 = v - one;
                Vector256<double> q2 = Vector256.Create(-0.020835085);
                q2 = q2 * u2 + Vector256.Create(0.0277272808); q2 = q2 * u2 + Vector256.Create(-0.0397820075);
                q2 = q2 * u2 + Vector256.Create(0.0667107478); q2 = q2 * u2 + Vector256.Create(-0.117496403);
                q2 = q2 * u2 + Vector256.Create(0.333331568);
                (nn * Vector256.Create(0.6931471805599453) + (q2 * u2 + one) * u2).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i)
        {
            double x = Unsafe.Add(ref src, (nint)i);
            if (x <= 0) { Unsafe.Add(ref dst, (nint)i) = double.NegativeInfinity; continue; }
            double v = x, nn = 0;
            for (int it = 0; it < 1200 && v >= 2; ++it) { v *= 0.5; nn += 1; }
            for (int it = 0; it < 1200 && v < 1; ++it) { v *= 2; nn -= 1; }
            double u = v - 1;
            double poly = (((((-0.020835085 * u + 0.0277272808) * u - 0.0397820075) * u + 0.0667107478) * u - 0.117496403) * u + 0.333331568) * u + 1.0;
            Unsafe.Add(ref dst, (nint)i) = nn * 0.6931471805599453 + poly * u;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorSigmoid(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<double> e = ExpKernel512(Vector512.Create(0.0) - Vector512.LoadUnsafe(in src, i));
                (Vector512.Create(1.0) / (Vector512.Create(1.0) + e)).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<double> neg = Vector256.Create(0.0) - Vector256.LoadUnsafe(in src, i);
                Vector256<double> clamped = Vector256.Max(Vector256.Min(neg, Vector256.Create(709.0)), Vector256.Create(-709.0));
                Vector256<double> t = clamped * Vector256.Create(1.4426950408889634);
                Vector256<long> k = Vector256.Floor(t).AsInt64() - Vector256.Create(1L);
                Vector256<double> p2 = Vector256.ShiftLeft(k + Vector256.Create(1023L), 52).AsDouble();
                Vector256<double> u = (t - Vector256.ConvertToDouble(k)) * Vector256.Create(0.6931471805599453);
                Vector256<double> poly = Vector256.Create(0.00833333333333333);
                poly = poly * u + Vector256.Create(0.0416666666666667); poly = poly * u + Vector256.Create(0.166666666666667);
                poly = poly * u + Vector256.Create(0.5); poly = poly * u + Vector256.Create(1.0);
                Vector256<double> e = p2 * (poly * u + Vector256.Create(1.0));
                (Vector256.Create(1.0) / (Vector256.Create(1.0) + e)).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dst, (nint)i) = 1.0 / (1.0 + Math.Exp(-Unsafe.Add(ref src, (nint)i)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorTanh(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<double> x = Vector512.Max(Vector512.Min(Vector512.LoadUnsafe(in src, i), Vector512.Create(19.0)), Vector512.Create(-19.0));
                Vector512<double> p = ExpKernel512(x * Vector512.Create(2.0));
                ((p - Vector512.Create(1.0)) / (p + Vector512.Create(1.0))).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<double> x = Vector256.Max(Vector256.Min(Vector256.LoadUnsafe(in src, i), Vector256.Create(19.0)), Vector256.Create(-19.0));
                Vector256<double> cx = x * Vector256.Create(1.4426950408889634 * 2.0);
                Vector256<long> k = Vector256.Floor(cx).AsInt64() - Vector256.Create(1L);
                Vector256<double> p2 = Vector256.ShiftLeft(k + Vector256.Create(1023L), 52).AsDouble();
                Vector256<double> u = (cx - Vector256.ConvertToDouble(k)) * Vector256.Create(0.6931471805599453);
                Vector256<double> poly = Vector256.Create(0.00833333333333333);
                poly = poly * u + Vector256.Create(0.0416666666666667); poly = poly * u + Vector256.Create(0.166666666666667);
                poly = poly * u + Vector256.Create(0.5); poly = poly * u + Vector256.Create(1.0);
                Vector256<double> p = p2 * (poly * u + Vector256.Create(1.0));
                ((p - Vector256.Create(1.0)) / (p + Vector256.Create(1.0))).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i)
        {
            double x = Unsafe.Add(ref src, (nint)i);
            double e2x = Math.Exp(2.0 * Math.Max(-19.0, Math.Min(19.0, x)));
            Unsafe.Add(ref dst, (nint)i) = (e2x - 1.0) / (e2x + 1.0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorReLU(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> zero = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector512.Max(Vector512.LoadUnsafe(in src, i), zero).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> zero = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector256.Max(Vector256.LoadUnsafe(in src, i), zero).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> zero = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector128.Max(Vector128.LoadUnsafe(in src, i), zero).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) { double v = Unsafe.Add(ref src, (nint)i); Unsafe.Add(ref dst, (nint)i) = v > 0 ? v : 0; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorRSqrt(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector512.Create(1.0) / Vector512.Sqrt(Vector512.LoadUnsafe(in src, i))).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector256.Create(1.0) / Vector256.Sqrt(Vector256.LoadUnsafe(in src, i))).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector128.Create(1.0) / Vector128.Sqrt(Vector128.LoadUnsafe(in src, i))).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = 1.0 / Math.Sqrt(Unsafe.Add(ref src, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorPow(ReadOnlySpan<double> @base, ReadOnlySpan<double> exponent, Span<double> destination)
    {
        if (@base.Length != exponent.Length || destination.Length < @base.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)@base.Length;
        if (length == 0) return;
        ref double bRef = ref MemoryMarshal.GetReference(@base);
        ref double eRef = ref MemoryMarshal.GetReference(exponent);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                ExpKernel512(Vector512.LoadUnsafe(in eRef, i) * LogKernel512(Vector512.LoadUnsafe(in bRef, i))).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Math.Pow(Unsafe.Add(ref bRef, (nint)i), Unsafe.Add(ref eRef, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Softmax(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        double maxVal = double.NegativeInfinity;
        for (nuint j = 0; j < length; ++j) { double v = Unsafe.Add(ref src, (nint)j); if (v > maxVal) maxVal = v; }
        double sum = 0;
        for (nuint j = 0; j < length; ++j) { double e = Math.Exp(Unsafe.Add(ref src, (nint)j) - maxVal); Unsafe.Add(ref dst, (nint)j) = e; sum += e; }
        double inv = 1.0 / sum;
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vInv = Vector512.Create(inv);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector512.LoadUnsafe(in dst, i) * vInv).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vInv = Vector256.Create(inv);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector256.LoadUnsafe(in dst, i) * vInv).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vInv = Vector128.Create(inv);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector128.LoadUnsafe(in dst, i) * vInv).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) *= inv;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Mat4x4Multiply(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length < 16 || right.Length < 16 || destination.Length < 16) ThrowHelper.ThrowMismatchedSpans();
        ref double l = ref MemoryMarshal.GetReference(left);
        ref double r = ref MemoryMarshal.GetReference(right);
        ref double d = ref MemoryMarshal.GetReference(destination);
        if (Vector128.IsHardwareAccelerated)
        {
            for (int col = 0; col < 4; ++col)
            {
                Vector128<double> r0 = Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 0))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4));
                r0 += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 1))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 2));
                r0 += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 2))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 4));
                r0 += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 3))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 6));
                r0.StoreUnsafe(ref d, (nuint)(col * 2));
                Vector128<double> r1 = Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 0))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 1));
                r1 += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 1))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 3));
                r1 += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 2))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 5));
                r1 += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 3))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 7));
                r1.StoreUnsafe(ref d, (nuint)(col * 2 + 1));
            }
        }
        else
        {
            for (int col = 0; col < 4; ++col)
                for (int row = 0; row < 4; ++row)
                {
                    double sum = 0;
                    for (int k = 0; k < 4; ++k) sum += Unsafe.Add(ref l, (nint)(k * 4 + col)) * Unsafe.Add(ref r, (nint)(row + k * 4));
                    Unsafe.Add(ref d, (nint)(row + col * 4)) = sum;
                }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CrossProduct(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length < 3 || right.Length < 3 || destination.Length < 3) ThrowHelper.ThrowMismatchedSpans();
        ref double l = ref MemoryMarshal.GetReference(left);
        ref double r = ref MemoryMarshal.GetReference(right);
        double lx = Unsafe.Add(ref l, 0), ly = Unsafe.Add(ref l, 1), lz = Unsafe.Add(ref l, 2);
        double rx = Unsafe.Add(ref r, 0), ry = Unsafe.Add(ref r, 1), rz = Unsafe.Add(ref r, 2);
        ref double d = ref MemoryMarshal.GetReference(destination);
        Unsafe.Add(ref d, 0) = ly * rz - lz * ry;
        Unsafe.Add(ref d, 1) = lz * rx - lx * rz;
        Unsafe.Add(ref d, 2) = lx * ry - ly * rx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void QuaternionMultiply(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length < 4 || right.Length < 4 || destination.Length < 4) ThrowHelper.ThrowMismatchedSpans();
        ref double l = ref MemoryMarshal.GetReference(left);
        ref double r = ref MemoryMarshal.GetReference(right);
        double lx = Unsafe.Add(ref l, 0), ly = Unsafe.Add(ref l, 1), lz = Unsafe.Add(ref l, 2), lw = Unsafe.Add(ref l, 3);
        double rx = Unsafe.Add(ref r, 0), ry = Unsafe.Add(ref r, 1), rz = Unsafe.Add(ref r, 2), rw = Unsafe.Add(ref r, 3);
        ref double d = ref MemoryMarshal.GetReference(destination);
        Unsafe.Add(ref d, 0) = lw * rx + lx * rw + ly * rz - lz * ry;
        Unsafe.Add(ref d, 1) = lw * ry - lx * rz + ly * rw + lz * rx;
        Unsafe.Add(ref d, 2) = lw * rz + lx * ry - ly * rx + lz * rw;
        Unsafe.Add(ref d, 3) = lw * rw - lx * rx - ly * ry - lz * rz;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void QuaternionSlerp(ReadOnlySpan<double> from, ReadOnlySpan<double> to, double t, Span<double> destination)
    {
        if (from.Length < 4 || to.Length < 4 || destination.Length < 4) ThrowHelper.ThrowMismatchedSpans();
        ref double a = ref MemoryMarshal.GetReference(from);
        ref double b = ref MemoryMarshal.GetReference(to);
        double dot = Unsafe.Add(ref a, 0) * Unsafe.Add(ref b, 0) + Unsafe.Add(ref a, 1) * Unsafe.Add(ref b, 1)
                   + Unsafe.Add(ref a, 2) * Unsafe.Add(ref b, 2) + Unsafe.Add(ref a, 3) * Unsafe.Add(ref b, 3);
        double bx = Unsafe.Add(ref b, 0), by = Unsafe.Add(ref b, 1), bz = Unsafe.Add(ref b, 2), bw = Unsafe.Add(ref b, 3);
        if (dot < 0) { dot = -dot; bx = -bx; by = -by; bz = -bz; bw = -bw; }
        ref double d = ref MemoryMarshal.GetReference(destination);
        if (dot > 0.9995)
        {
            Unsafe.Add(ref d, 0) = Unsafe.Add(ref a, 0) + t * (bx - Unsafe.Add(ref a, 0));
            Unsafe.Add(ref d, 1) = Unsafe.Add(ref a, 1) + t * (by - Unsafe.Add(ref a, 1));
            Unsafe.Add(ref d, 2) = Unsafe.Add(ref a, 2) + t * (bz - Unsafe.Add(ref a, 2));
            Unsafe.Add(ref d, 3) = Unsafe.Add(ref a, 3) + t * (bw - Unsafe.Add(ref a, 3));
            double len = Math.Sqrt(Unsafe.Add(ref d, 0) * Unsafe.Add(ref d, 0) + Unsafe.Add(ref d, 1) * Unsafe.Add(ref d, 1)
                                 + Unsafe.Add(ref d, 2) * Unsafe.Add(ref d, 2) + Unsafe.Add(ref d, 3) * Unsafe.Add(ref d, 3));
            double inv = 1.0 / len;
            Unsafe.Add(ref d, 0) *= inv; Unsafe.Add(ref d, 1) *= inv; Unsafe.Add(ref d, 2) *= inv; Unsafe.Add(ref d, 3) *= inv;
        }
        else
        {
            double theta = Math.Acos(Math.Clamp(dot, -1.0, 1.0));
            double sinTheta = Math.Sin(theta);
            double w0 = Math.Sin((1.0 - t) * theta) / sinTheta;
            double w1 = Math.Sin(t * theta) / sinTheta;
            Unsafe.Add(ref d, 0) = w0 * Unsafe.Add(ref a, 0) + w1 * bx;
            Unsafe.Add(ref d, 1) = w0 * Unsafe.Add(ref a, 1) + w1 * by;
            Unsafe.Add(ref d, 2) = w0 * Unsafe.Add(ref a, 2) + w1 * bz;
            Unsafe.Add(ref d, 3) = w0 * Unsafe.Add(ref a, 3) + w1 * bw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Swizzle(ReadOnlySpan<double> source, ReadOnlySpan<int> indices, Span<double> destination)
    {
        if (destination.Length < indices.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        for (; i < count; ++i)
            Unsafe.Add(ref dst, (nint)i) = Unsafe.Add(ref src, Unsafe.Add(ref idx, (nint)i));
    }
}
