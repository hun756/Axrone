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
        ThrowHelper.ValidateDestinationSpan(destination, indices);
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

    private const double Log2E = 1.4426950408889634;
    private const double Ln2Hi = 0.6931471803691238166;  // high part of ln2: n * Ln2Hi is exact
    private const double Ln2Lo = 1.9082149292705877e-10; // ln2 - Ln2Hi
    private const double ExpClamp = 709.0;               // keeps 2^n in normal range at the lower end

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<double> ExpKernel512(Vector512<double> x)
    {
        Vector512<double> clamped = Vector512.Min(Vector512.Max(x, Vector512.Create(-ExpClamp)), Vector512.Create(ExpClamp));
        Vector512<double> t = clamped * Vector512.Create(Log2E);
        Vector512<long> n = Vector512.ConvertToInt64(Vector512.Round(t));
        Vector512<double> nf = Vector512.ConvertToDouble(n);
        Vector512<double> r = (clamped - nf * Vector512.Create(Ln2Hi)) - nf * Vector512.Create(Ln2Lo);
        Vector512<double> p = Vector512.Create(1.0 / 87178291200.0);
        p = p * r + Vector512.Create(1.0 / 479001600.0);
        p = p * r + Vector512.Create(1.0 / 39916800.0);
        p = p * r + Vector512.Create(1.0 / 3628800.0);
        p = p * r + Vector512.Create(1.0 / 362880.0);
        p = p * r + Vector512.Create(1.0 / 40320.0);
        p = p * r + Vector512.Create(1.0 / 5040.0);
        p = p * r + Vector512.Create(1.0 / 720.0);
        p = p * r + Vector512.Create(1.0 / 120.0);
        p = p * r + Vector512.Create(1.0 / 24.0);
        p = p * r + Vector512.Create(1.0 / 6.0);
        p = p * r + Vector512.Create(0.5);
        p = p * r + Vector512.Create(1.0);
        p = p * r + Vector512.Create(1.0);
        // 2^n via exponent-field write; biased <= 0 means the result is subnormal (musl-style split)
        Vector512<long> biased = n + Vector512.Create(1023L);
        Vector512<long> subMask = Vector512.LessThanOrEqual(biased, Vector512<long>.Zero);
        Vector512<long> expVal = Vector512.ConditionalSelect(subMask, n + Vector512.Create(1077L), biased);
        Vector512<double> pow2 = Vector512.ShiftLeft(expVal, 52).AsDouble()
            * Vector512.ConditionalSelect(subMask.AsDouble(), Vector512.Create(5.551115123125783e-17), Vector512<double>.One);
        return pow2 * p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> ExpKernel256(Vector256<double> x)
    {
        Vector256<double> clamped = Vector256.Min(Vector256.Max(x, Vector256.Create(-ExpClamp)), Vector256.Create(ExpClamp));
        Vector256<double> t = clamped * Vector256.Create(Log2E);
        Vector256<long> n = Vector256.ConvertToInt64(Vector256.Round(t));
        Vector256<double> nf = Vector256.ConvertToDouble(n);
        Vector256<double> r = (clamped - nf * Vector256.Create(Ln2Hi)) - nf * Vector256.Create(Ln2Lo);
        Vector256<double> p = Vector256.Create(1.0 / 87178291200.0);
        p = p * r + Vector256.Create(1.0 / 479001600.0);
        p = p * r + Vector256.Create(1.0 / 39916800.0);
        p = p * r + Vector256.Create(1.0 / 3628800.0);
        p = p * r + Vector256.Create(1.0 / 362880.0);
        p = p * r + Vector256.Create(1.0 / 40320.0);
        p = p * r + Vector256.Create(1.0 / 5040.0);
        p = p * r + Vector256.Create(1.0 / 720.0);
        p = p * r + Vector256.Create(1.0 / 120.0);
        p = p * r + Vector256.Create(1.0 / 24.0);
        p = p * r + Vector256.Create(1.0 / 6.0);
        p = p * r + Vector256.Create(0.5);
        p = p * r + Vector256.Create(1.0);
        p = p * r + Vector256.Create(1.0);
        Vector256<long> biased = n + Vector256.Create(1023L);
        Vector256<long> subMask = Vector256.LessThanOrEqual(biased, Vector256<long>.Zero);
        Vector256<long> expVal = Vector256.ConditionalSelect(subMask, n + Vector256.Create(1077L), biased);
        Vector256<double> pow2 = Vector256.ShiftLeft(expVal, 52).AsDouble()
            * Vector256.ConditionalSelect(subMask.AsDouble(), Vector256.Create(5.551115123125783e-17), Vector256<double>.One);
        return pow2 * p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> ExpKernel128(Vector128<double> x)
    {
        Vector128<double> clamped = Vector128.Min(Vector128.Max(x, Vector128.Create(-ExpClamp)), Vector128.Create(ExpClamp));
        Vector128<double> t = clamped * Vector128.Create(Log2E);
        Vector128<long> n = Vector128.ConvertToInt64(Vector128.Round(t));
        Vector128<double> nf = Vector128.ConvertToDouble(n);
        Vector128<double> r = (clamped - nf * Vector128.Create(Ln2Hi)) - nf * Vector128.Create(Ln2Lo);
        Vector128<double> p = Vector128.Create(1.0 / 87178291200.0);
        p = p * r + Vector128.Create(1.0 / 479001600.0);
        p = p * r + Vector128.Create(1.0 / 39916800.0);
        p = p * r + Vector128.Create(1.0 / 3628800.0);
        p = p * r + Vector128.Create(1.0 / 362880.0);
        p = p * r + Vector128.Create(1.0 / 40320.0);
        p = p * r + Vector128.Create(1.0 / 5040.0);
        p = p * r + Vector128.Create(1.0 / 720.0);
        p = p * r + Vector128.Create(1.0 / 120.0);
        p = p * r + Vector128.Create(1.0 / 24.0);
        p = p * r + Vector128.Create(1.0 / 6.0);
        p = p * r + Vector128.Create(0.5);
        p = p * r + Vector128.Create(1.0);
        p = p * r + Vector128.Create(1.0);
        Vector128<long> biased = n + Vector128.Create(1023L);
        Vector128<long> subMask = Vector128.LessThanOrEqual(biased, Vector128<long>.Zero);
        Vector128<long> expVal = Vector128.ConditionalSelect(subMask, n + Vector128.Create(1077L), biased);
        Vector128<double> pow2 = Vector128.ShiftLeft(expVal, 52).AsDouble()
            * Vector128.ConditionalSelect(subMask.AsDouble(), Vector128.Create(5.551115123125783e-17), Vector128<double>.One);
        return pow2 * p;
    }

    private static double ExpScalar(double x)
    {
        x = Math.Max(-ExpClamp, Math.Min(ExpClamp, x));
        double t = x * Log2E;
        long n = (long)Math.Round(t);
        double r = (x - n * Ln2Hi) - n * Ln2Lo;
        double p = 1.0 / 87178291200.0;
        p = p * r + 1.0 / 479001600.0;
        p = p * r + 1.0 / 39916800.0;
        p = p * r + 1.0 / 3628800.0;
        p = p * r + 1.0 / 362880.0;
        p = p * r + 1.0 / 40320.0;
        p = p * r + 1.0 / 5040.0;
        p = p * r + 1.0 / 720.0;
        p = p * r + 1.0 / 120.0;
        p = p * r + 1.0 / 24.0;
        p = p * r + 1.0 / 6.0;
        p = p * r + 0.5;
        p = p * r + 1.0;
        p = p * r + 1.0;
        long biased = n + 1023;
        double pow2 = biased > 0
            ? BitConverter.Int64BitsToDouble(biased << 52)
            : BitConverter.Int64BitsToDouble((n + 1077) << 52) * 5.551115123125783e-17;
        return pow2 * p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorExp(ReadOnlySpan<double> source, Span<double> destination)
    {
        ThrowHelper.ValidateDestinationSpan(destination, source);
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
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) ExpKernel256(Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) ExpKernel128(Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = ExpScalar(Unsafe.Add(ref src, (nint)i));
    }

    private const double Sqrt2 = 1.4142135623730951;
    private const double DenormScale = 18014398509481984.0; // 2^54: maps any double denormal to a normal
    private const long ExpMask = 0x7FF0000000000000L;
    private const long MantMask = 0x000FFFFFFFFFFFFFL;
    private const long ExpBias = 0x3FF0000000000000L;

    // ln(x) = e*ln2 + ln(1+f), m in [sqrt(2)/2, sqrt(2)), f = m-1, s = f/(2+f), ln(1+f) = 2s*(1 + z*(1/3 + z*(1/5 + ...)))
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<double> LogKernel512(Vector512<double> x)
    {
        Vector512<long> bits = x.AsInt64();
        Vector512<long> expField = bits & Vector512.Create(ExpMask);
        Vector512<long> subMask = Vector512.Equals(expField, Vector512<long>.Zero);
        Vector512<double> xs = Vector512.ConditionalSelect(subMask.AsDouble(), x * Vector512.Create(DenormScale), x);
        Vector512<long> bits2 = xs.AsInt64();
        Vector512<long> e = ((bits2 & Vector512.Create(ExpMask)) >> 52) - Vector512.Create(1023L)
            - Vector512.ConditionalSelect(subMask, Vector512.Create(54L), Vector512<long>.Zero);
        Vector512<double> m = (bits2 & Vector512.Create(MantMask) | Vector512.Create(ExpBias)).AsDouble();
        Vector512<double> ge = Vector512.GreaterThanOrEqual(m, Vector512.Create(Sqrt2));
        m = Vector512.ConditionalSelect(ge, m * Vector512.Create(0.5), m);
        e = Vector512.ConditionalSelect(ge.AsInt64(), e + Vector512.Create(1L), e);
        Vector512<double> f = m - Vector512.Create(1.0);
        Vector512<double> s = f / (f + Vector512.Create(2.0));
        Vector512<double> z = s * s;
        Vector512<double> p = Vector512.Create(1.0 / 33.0);
        p = p * z + Vector512.Create(1.0 / 31.0);
        p = p * z + Vector512.Create(1.0 / 29.0);
        p = p * z + Vector512.Create(1.0 / 27.0);
        p = p * z + Vector512.Create(1.0 / 25.0);
        p = p * z + Vector512.Create(1.0 / 23.0);
        p = p * z + Vector512.Create(1.0 / 21.0);
        p = p * z + Vector512.Create(1.0 / 19.0);
        p = p * z + Vector512.Create(1.0 / 17.0);
        p = p * z + Vector512.Create(1.0 / 15.0);
        p = p * z + Vector512.Create(1.0 / 13.0);
        p = p * z + Vector512.Create(1.0 / 11.0);
        p = p * z + Vector512.Create(1.0 / 9.0);
        p = p * z + Vector512.Create(1.0 / 7.0);
        p = p * z + Vector512.Create(1.0 / 5.0);
        p = p * z + Vector512.Create(1.0 / 3.0);
        Vector512<double> lnF = (Vector512.Create(2.0) * s) * (p * z + Vector512.Create(1.0));
        Vector512<double> result = Vector512.ConvertToDouble(e) * Vector512.Create(0.6931471805599453) + lnF;
        result = Vector512.ConditionalSelect(Vector512.LessThanOrEqual(x, Vector512<double>.Zero), Vector512.Create(double.NegativeInfinity), result);
        result = Vector512.ConditionalSelect(Vector512.Equals(expField, Vector512.Create(ExpMask)).AsDouble(), x, result);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> LogKernel256(Vector256<double> x)
    {
        Vector256<long> bits = x.AsInt64();
        Vector256<long> expField = bits & Vector256.Create(ExpMask);
        Vector256<long> subMask = Vector256.Equals(expField, Vector256<long>.Zero);
        Vector256<double> xs = Vector256.ConditionalSelect(subMask.AsDouble(), x * Vector256.Create(DenormScale), x);
        Vector256<long> bits2 = xs.AsInt64();
        Vector256<long> e = ((bits2 & Vector256.Create(ExpMask)) >> 52) - Vector256.Create(1023L)
            - Vector256.ConditionalSelect(subMask, Vector256.Create(54L), Vector256<long>.Zero);
        Vector256<double> m = (bits2 & Vector256.Create(MantMask) | Vector256.Create(ExpBias)).AsDouble();
        Vector256<double> ge = Vector256.GreaterThanOrEqual(m, Vector256.Create(Sqrt2));
        m = Vector256.ConditionalSelect(ge, m * Vector256.Create(0.5), m);
        e = Vector256.ConditionalSelect(ge.AsInt64(), e + Vector256.Create(1L), e);
        Vector256<double> f = m - Vector256.Create(1.0);
        Vector256<double> s = f / (f + Vector256.Create(2.0));
        Vector256<double> z = s * s;
        Vector256<double> p = Vector256.Create(1.0 / 33.0);
        p = p * z + Vector256.Create(1.0 / 31.0);
        p = p * z + Vector256.Create(1.0 / 29.0);
        p = p * z + Vector256.Create(1.0 / 27.0);
        p = p * z + Vector256.Create(1.0 / 25.0);
        p = p * z + Vector256.Create(1.0 / 23.0);
        p = p * z + Vector256.Create(1.0 / 21.0);
        p = p * z + Vector256.Create(1.0 / 19.0);
        p = p * z + Vector256.Create(1.0 / 17.0);
        p = p * z + Vector256.Create(1.0 / 15.0);
        p = p * z + Vector256.Create(1.0 / 13.0);
        p = p * z + Vector256.Create(1.0 / 11.0);
        p = p * z + Vector256.Create(1.0 / 9.0);
        p = p * z + Vector256.Create(1.0 / 7.0);
        p = p * z + Vector256.Create(1.0 / 5.0);
        p = p * z + Vector256.Create(1.0 / 3.0);
        Vector256<double> lnF = (Vector256.Create(2.0) * s) * (p * z + Vector256.Create(1.0));
        Vector256<double> result = Vector256.ConvertToDouble(e) * Vector256.Create(0.6931471805599453) + lnF;
        result = Vector256.ConditionalSelect(Vector256.LessThanOrEqual(x, Vector256<double>.Zero), Vector256.Create(double.NegativeInfinity), result);
        result = Vector256.ConditionalSelect(Vector256.Equals(expField, Vector256.Create(ExpMask)).AsDouble(), x, result);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorLog(ReadOnlySpan<double> source, Span<double> destination)
    {
        ThrowHelper.ValidateDestinationSpan(destination, source);
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
            for (; i < limit; i += step) LogKernel256(Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = LogScalar(Unsafe.Add(ref src, (nint)i));
    }

    private static double LogScalar(double x)
    {
        if (x <= 0.0) return double.NegativeInfinity;
        long bits = BitConverter.DoubleToInt64Bits(x);
        if ((bits & ExpMask) == ExpMask) return x;
        long e = ((bits & ExpMask) >> 52) - 1023;
        if ((bits & ExpMask) == 0) { x *= DenormScale; bits = BitConverter.DoubleToInt64Bits(x); e = ((bits & ExpMask) >> 52) - 1023 - 54; }
        double m = BitConverter.Int64BitsToDouble((bits & MantMask) | ExpBias);
        if (m >= Sqrt2) { m *= 0.5; e += 1; }
        double f = m - 1.0;
        double s = f / (f + 2.0);
        double z = s * s;
        double p = 1.0 / 33.0;
        p = p * z + 1.0 / 31.0;
        p = p * z + 1.0 / 29.0;
        p = p * z + 1.0 / 27.0;
        p = p * z + 1.0 / 25.0;
        p = p * z + 1.0 / 23.0;
        p = p * z + 1.0 / 21.0;
        p = p * z + 1.0 / 19.0;
        p = p * z + 1.0 / 17.0;
        p = p * z + 1.0 / 15.0;
        p = p * z + 1.0 / 13.0;
        p = p * z + 1.0 / 11.0;
        p = p * z + 1.0 / 9.0;
        p = p * z + 1.0 / 7.0;
        p = p * z + 1.0 / 5.0;
        p = p * z + 1.0 / 3.0;
        return e * 0.6931471805599453 + (2.0 * s) * (p * z + 1.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorSigmoid(ReadOnlySpan<double> source, Span<double> destination)
    {
        ThrowHelper.ValidateDestinationSpan(destination, source);
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
                (Vector256.Create(1.0) / (Vector256.Create(1.0) + ExpKernel256(Vector256.Create(0.0) - Vector256.LoadUnsafe(in src, i)))).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dst, (nint)i) = 1.0 / (1.0 + Math.Exp(-Unsafe.Add(ref src, (nint)i)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorTanh(ReadOnlySpan<double> source, Span<double> destination)
    {
        ThrowHelper.ValidateDestinationSpan(destination, source);
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
                Vector256<double> p = ExpKernel256(Vector256.LoadUnsafe(in src, i) * Vector256.Create(2.0));
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
        ThrowHelper.ValidateDestinationSpan(destination, source);
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
        ThrowHelper.ValidateDestinationSpan(destination, source);
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
        ThrowHelper.ValidateDestinationSpan(destination, source);
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
        ThrowHelper.ValidateDestinationSpan(destination, indices);
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
