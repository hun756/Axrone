namespace Axrone.Simd;

public static unsafe partial class SimdFloat64
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Fill(Span<double> destination, double value)
        => SimdFloatingPointOps<double>.Fill(destination, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void FillLinear(Span<double> destination, double start, double step)
        => SimdFloatingPointOps<double>.FillLinear(destination, start, step);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Gather(ReadOnlySpan<double> source, ReadOnlySpan<int> indices, Span<double> destination)
        => SimdFloatingPointOps<double>.Gather(source, indices, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Scatter(ReadOnlySpan<double> source, ReadOnlySpan<int> indices, Span<double> destination)
        => SimdFloatingPointOps<double>.Scatter(source, indices, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorReLU(ReadOnlySpan<double> source, Span<double> destination)
        => SimdFloatingPointOps<double>.VectorReLU(source, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorRSqrt(ReadOnlySpan<double> source, Span<double> destination)
        => SimdFloatingPointOps<double>.VectorRSqrt(source, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Softmax(ReadOnlySpan<double> source, Span<double> destination)
    {
        ThrowHelper.ValidateDestinationSpan(destination, source);
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);

        double maxVal = Unsafe.Add(ref src, 0);
        for (nuint k = 1; k < length; ++k)
        {
            double v = Unsafe.Add(ref src, (nint)k);
            if (v > maxVal) maxVal = v;
        }

        double sum = 0;
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<double> e = ExpKernel512(Vector512.LoadUnsafe(in src, i) - Vector512.Create(maxVal));
                e.StoreUnsafe(ref dst, i);
                sum += Vector512.Sum(e);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<double> e = ExpKernel256(Vector256.LoadUnsafe(in src, i) - Vector256.Create(maxVal));
                e.StoreUnsafe(ref dst, i);
                sum += Vector256.Sum(e);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<double> e = ExpKernel128(Vector128.LoadUnsafe(in src, i) - Vector128.Create(maxVal));
                e.StoreUnsafe(ref dst, i);
                sum += Vector128.Sum(e);
            }
        }
        for (; i < length; ++i)
        {
            double e = ExpScalar(Unsafe.Add(ref src, (nint)i) - maxVal);
            Unsafe.Add(ref dst, (nint)i) = e;
            sum += e;
        }

        double invSum = 1.0 / sum;
        nuint i2 = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vInv = Vector512.Create(invSum);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i2 < limit; i2 += step)
                (Vector512.LoadUnsafe(in dst, i2) * vInv).StoreUnsafe(ref dst, i2);
        }
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vInv = Vector256.Create(invSum);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i2 < limit; i2 += step)
                (Vector256.LoadUnsafe(in dst, i2) * vInv).StoreUnsafe(ref dst, i2);
        }
        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vInv = Vector128.Create(invSum);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i2 < limit; i2 += step)
                (Vector128.LoadUnsafe(in dst, i2) * vInv).StoreUnsafe(ref dst, i2);
        }
        for (; i2 < length; ++i2)
            Unsafe.Add(ref dst, (nint)i2) = Unsafe.Add(ref dst, (nint)i2) * invSum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Mat4x4Multiply(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloatingPointOps<double>.Mat4x4Multiply(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CrossProduct(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloatingPointOps<double>.CrossProduct(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void QuaternionMultiply(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloatingPointOps<double>.QuaternionMultiply(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void QuaternionSlerp(ReadOnlySpan<double> from, ReadOnlySpan<double> to, double t, Span<double> destination)
        => SimdFloatingPointOps<double>.QuaternionSlerp(from, to, t, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Swizzle(ReadOnlySpan<double> source, ReadOnlySpan<int> indices, Span<double> destination)
        => SimdFloatingPointOps<double>.Swizzle(source, indices, destination);

    // ── Type-specific: VectorExp ────────────────────────────────────────

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
        Vector512<double> result = pow2 * p;
        result = Vector512.ConditionalSelect(Vector512.Equals(x, Vector512.Create(double.NegativeInfinity)), Vector512<double>.Zero, result);
        return Vector512.ConditionalSelect(Vector512.Equals(x, x), result, x);
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
        Vector256<double> result = pow2 * p;
        result = Vector256.ConditionalSelect(Vector256.Equals(x, Vector256.Create(double.NegativeInfinity)), Vector256<double>.Zero, result);
        return Vector256.ConditionalSelect(Vector256.Equals(x, x), result, x);
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
        Vector128<double> result = pow2 * p;
        result = Vector128.ConditionalSelect(Vector128.Equals(x, Vector128.Create(double.NegativeInfinity)), Vector128<double>.Zero, result);
        return Vector128.ConditionalSelect(Vector128.Equals(x, x), result, x);
    }

    private static double ExpScalar(double x)
    {
        if (double.IsNaN(x)) return x;
        if (double.IsNegativeInfinity(x)) return 0.0;
        if (double.IsPositiveInfinity(x)) return x;
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

    // ── Type-specific: VectorLog ────────────────────────────────────────

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
        result = Vector512.ConditionalSelect(Vector512.LessThan(x, Vector512<double>.Zero), Vector512.Create(double.NaN), result);
        result = Vector512.ConditionalSelect(Vector512.Equals(x, Vector512<double>.Zero), Vector512.Create(double.NegativeInfinity), result);
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
        result = Vector256.ConditionalSelect(Vector256.LessThan(x, Vector256<double>.Zero), Vector256.Create(double.NaN), result);
        result = Vector256.ConditionalSelect(Vector256.Equals(x, Vector256<double>.Zero), Vector256.Create(double.NegativeInfinity), result);
        result = Vector256.ConditionalSelect(Vector256.Equals(expField, Vector256.Create(ExpMask)).AsDouble(), x, result);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> LogKernel128(Vector128<double> x)
    {
        Vector128<long> bits = x.AsInt64();
        Vector128<long> expField = bits & Vector128.Create(ExpMask);
        Vector128<long> subMask = Vector128.Equals(expField, Vector128.Create<long>(0));
        Vector128<double> xs = Vector128.ConditionalSelect(subMask.AsDouble(), x * Vector128.Create(DenormScale), x);
        Vector128<long> bits2 = xs.AsInt64();
        Vector128<long> e = ((bits2 & Vector128.Create(ExpMask)) >> 52) - Vector128.Create(1023L)
            - Vector128.ConditionalSelect(subMask, Vector128.Create(54L), Vector128<long>.Zero);
        Vector128<double> m = (bits2 & Vector128.Create(MantMask) | Vector128.Create(ExpBias)).AsDouble();
        Vector128<double> ge = Vector128.GreaterThanOrEqual(m, Vector128.Create(Sqrt2));
        m = Vector128.ConditionalSelect(ge, m * Vector128.Create(0.5), m);
        e = Vector128.ConditionalSelect(ge.AsInt64(), e + Vector128.Create(1L), e);
        Vector128<double> f = m - Vector128.Create(1.0);
        Vector128<double> s = f / (f + Vector128.Create(2.0));
        Vector128<double> z = s * s;
        Vector128<double> p = Vector128.Create(1.0 / 33.0);
        p = p * z + Vector128.Create(1.0 / 31.0);
        p = p * z + Vector128.Create(1.0 / 29.0);
        p = p * z + Vector128.Create(1.0 / 27.0);
        p = p * z + Vector128.Create(1.0 / 25.0);
        p = p * z + Vector128.Create(1.0 / 23.0);
        p = p * z + Vector128.Create(1.0 / 21.0);
        p = p * z + Vector128.Create(1.0 / 19.0);
        p = p * z + Vector128.Create(1.0 / 17.0);
        p = p * z + Vector128.Create(1.0 / 15.0);
        p = p * z + Vector128.Create(1.0 / 13.0);
        p = p * z + Vector128.Create(1.0 / 11.0);
        p = p * z + Vector128.Create(1.0 / 9.0);
        p = p * z + Vector128.Create(1.0 / 7.0);
        p = p * z + Vector128.Create(1.0 / 5.0);
        p = p * z + Vector128.Create(1.0 / 3.0);
        Vector128<double> lnF = (Vector128.Create(2.0) * s) * (p * z + Vector128.Create(1.0));
        Vector128<double> result = Vector128.ConvertToDouble(e) * Vector128.Create(0.6931471805599453) + lnF;
        result = Vector128.ConditionalSelect(Vector128.LessThan(x, Vector128<double>.Zero), Vector128.Create(double.NaN), result);
        result = Vector128.ConditionalSelect(Vector128.Equals(x, Vector128<double>.Zero), Vector128.Create(double.NegativeInfinity), result);
        result = Vector128.ConditionalSelect(Vector128.Equals(expField, Vector128.Create(ExpMask)).AsDouble(), x, result);
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
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) LogKernel128(Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = LogScalar(Unsafe.Add(ref src, (nint)i));
    }

    private static double LogScalar(double x)
    {
        if (x < 0.0) return double.NaN;
        if (x == 0.0) return double.NegativeInfinity;
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

    // ── Type-specific: VectorSigmoid ────────────────────────────────────

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
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector128.Create(1.0) / (Vector128.Create(1.0) + ExpKernel128(Vector128.Create(0.0) - Vector128.LoadUnsafe(in src, i)))).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dst, (nint)i) = 1.0 / (1.0 + Math.Exp(-Unsafe.Add(ref src, (nint)i)));
    }

    // ── Type-specific: VectorTanh ───────────────────────────────────────

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
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<double> x = Vector128.Max(Vector128.Min(Vector128.LoadUnsafe(in src, i), Vector128.Create(19.0)), Vector128.Create(-19.0));
                Vector128<double> p = ExpKernel128(x * Vector128.Create(2.0));
                ((p - Vector128.Create(1.0)) / (p + Vector128.Create(1.0))).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i)
        {
            double x = Unsafe.Add(ref src, (nint)i);
            double e2x = Math.Exp(2.0 * Math.Max(-19.0, Math.Min(19.0, x)));
            Unsafe.Add(ref dst, (nint)i) = (e2x - 1.0) / (e2x + 1.0);
        }
    }

    // ── Type-specific: VectorPow ────────────────────────────────────────

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
            {
                Vector512<double> b = Vector512.LoadUnsafe(in bRef, i);
                Vector512<double> e = Vector512.LoadUnsafe(in eRef, i);
                Vector512<double> result = ExpKernel512(e * LogKernel512(b));
                Vector512<double> negBase = Vector512.LessThan(b, Vector512<double>.Zero);
                if (negBase != Vector512<double>.Zero)
                {
                    for (nuint j = 0; j < step; j++)
                    {
                        if (Unsafe.Add(ref Unsafe.As<Vector512<double>, double>(ref negBase), (nint)j) != 0.0)
                            Unsafe.Add(ref Unsafe.As<Vector512<double>, double>(ref result), (nint)j) =
                                Math.Pow(Unsafe.Add(ref Unsafe.As<Vector512<double>, double>(ref b), (nint)j),
                                         Unsafe.Add(ref Unsafe.As<Vector512<double>, double>(ref e), (nint)j));
                    }
                }
                result.StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<double> b = Vector256.LoadUnsafe(in bRef, i);
                Vector256<double> e = Vector256.LoadUnsafe(in eRef, i);
                Vector256<double> result = ExpKernel256(e * LogKernel256(b));
                Vector256<double> negBase = Vector256.LessThan(b, Vector256<double>.Zero);
                if (negBase != Vector256<double>.Zero)
                {
                    for (nuint j = 0; j < step; j++)
                    {
                        if (Unsafe.Add(ref Unsafe.As<Vector256<double>, double>(ref negBase), (nint)j) != 0.0)
                            Unsafe.Add(ref Unsafe.As<Vector256<double>, double>(ref result), (nint)j) =
                                Math.Pow(Unsafe.Add(ref Unsafe.As<Vector256<double>, double>(ref b), (nint)j),
                                         Unsafe.Add(ref Unsafe.As<Vector256<double>, double>(ref e), (nint)j));
                    }
                }
                result.StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<double> b = Vector128.LoadUnsafe(in bRef, i);
                Vector128<double> e = Vector128.LoadUnsafe(in eRef, i);
                Vector128<double> result = ExpKernel128(e * LogKernel128(b));
                Vector128<double> negBase = Vector128.LessThan(b, Vector128<double>.Zero);
                if (negBase != Vector128<double>.Zero)
                {
                    for (nuint j = 0; j < step; j++)
                    {
                        if (Unsafe.Add(ref Unsafe.As<Vector128<double>, double>(ref negBase), (nint)j) != 0.0)
                            Unsafe.Add(ref Unsafe.As<Vector128<double>, double>(ref result), (nint)j) =
                                Math.Pow(Unsafe.Add(ref Unsafe.As<Vector128<double>, double>(ref b), (nint)j),
                                         Unsafe.Add(ref Unsafe.As<Vector128<double>, double>(ref e), (nint)j));
                    }
                }
                result.StoreUnsafe(ref dRef, i);
            }
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Math.Pow(Unsafe.Add(ref bRef, (nint)i), Unsafe.Add(ref eRef, (nint)i));
    }
}
