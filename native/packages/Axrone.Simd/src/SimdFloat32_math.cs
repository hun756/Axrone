namespace Axrone.Simd;

public static unsafe partial class SimdFloat32
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Fill(Span<float> destination, float value)
        => SimdFloatingPointOps<float>.Fill(destination, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void FillLinear(Span<float> destination, float start, float step)
        => SimdFloatingPointOps<float>.FillLinear(destination, start, step);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Gather(ReadOnlySpan<float> source, ReadOnlySpan<int> indices, Span<float> destination)
        => SimdFloatingPointOps<float>.Gather(source, indices, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Scatter(ReadOnlySpan<float> source, ReadOnlySpan<int> indices, Span<float> destination)
        => SimdFloatingPointOps<float>.Scatter(source, indices, destination);

    // ── Type-specific: Exp ──────────────────────────────────────────────

    private const float Log2Ef = 1.4426950408889634f;
    private const float Ln2Hif = 0.693359375f;
    private const float Ln2Lof = -2.12194440e-4f;
    private const float ExpClampf = 87f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> ExpKernel512(Vector512<float> x)
    {
        Vector512<float> clamped = Vector512.Max(Vector512.Min(x, Vector512.Create(ExpClampf)), Vector512.Create(-ExpClampf));
        Vector512<float> t = clamped * Vector512.Create(Log2Ef);
        Vector512<int> n = Vector512.ConvertToInt32(Vector512.Round(t));
        Vector512<float> nf = Vector512.ConvertToSingle(n);
        Vector512<float> r = (clamped - nf * Vector512.Create(Ln2Hif)) - nf * Vector512.Create(Ln2Lof);
        Vector512<float> p = Vector512.Create(1f / 5040f);
        p = p * r + Vector512.Create(1f / 720f);
        p = p * r + Vector512.Create(1f / 120f);
        p = p * r + Vector512.Create(1f / 24f);
        p = p * r + Vector512.Create(1f / 6f);
        p = p * r + Vector512.Create(0.5f);
        p = p * r + Vector512.Create(1f);
        p = p * r + Vector512.Create(1f);
        Vector512<int> biased = Vector512.Min(Vector512.Max(n, Vector512.Create(-126)), Vector512.Create(127)) + Vector512.Create(127);
        return Vector512.ShiftLeft(biased, 23).AsSingle() * p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> ExpKernel256(Vector256<float> x)
    {
        Vector256<float> clamped = Vector256.Max(Vector256.Min(x, Vector256.Create(ExpClampf)), Vector256.Create(-ExpClampf));
        Vector256<float> t = clamped * Vector256.Create(Log2Ef);
        Vector256<int> n = Vector256.ConvertToInt32(Vector256.Round(t));
        Vector256<float> nf = Vector256.ConvertToSingle(n);
        Vector256<float> r = (clamped - nf * Vector256.Create(Ln2Hif)) - nf * Vector256.Create(Ln2Lof);
        Vector256<float> p = Vector256.Create(1f / 5040f);
        p = p * r + Vector256.Create(1f / 720f);
        p = p * r + Vector256.Create(1f / 120f);
        p = p * r + Vector256.Create(1f / 24f);
        p = p * r + Vector256.Create(1f / 6f);
        p = p * r + Vector256.Create(0.5f);
        p = p * r + Vector256.Create(1f);
        p = p * r + Vector256.Create(1f);
        Vector256<int> biased = Vector256.Min(Vector256.Max(n, Vector256.Create(-126)), Vector256.Create(127)) + Vector256.Create(127);
        return Vector256.ShiftLeft(biased, 23).AsSingle() * p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> ExpKernel128(Vector128<float> x)
    {
        Vector128<float> clamped = Vector128.Max(Vector128.Min(x, Vector128.Create(ExpClampf)), Vector128.Create(-ExpClampf));
        Vector128<float> t = clamped * Vector128.Create(Log2Ef);
        Vector128<int> n = Vector128.ConvertToInt32(Vector128.Round(t));
        Vector128<float> nf = Vector128.ConvertToSingle(n);
        Vector128<float> r = (clamped - nf * Vector128.Create(Ln2Hif)) - nf * Vector128.Create(Ln2Lof);
        Vector128<float> p = Vector128.Create(1f / 5040f);
        p = p * r + Vector128.Create(1f / 720f);
        p = p * r + Vector128.Create(1f / 120f);
        p = p * r + Vector128.Create(1f / 24f);
        p = p * r + Vector128.Create(1f / 6f);
        p = p * r + Vector128.Create(0.5f);
        p = p * r + Vector128.Create(1f);
        p = p * r + Vector128.Create(1f);
        Vector128<int> biased = Vector128.Min(Vector128.Max(n, Vector128.Create(-126)), Vector128.Create(127)) + Vector128.Create(127);
        return Vector128.ShiftLeft(biased, 23).AsSingle() * p;
    }

    private static float ExpScalar(float x)
    {
        x = Math.Max(-ExpClampf, Math.Min(ExpClampf, x));
        float t = x * Log2Ef;
        int n = (int)MathF.Round(t);
        float r = (x - n * Ln2Hif) - n * Ln2Lof;
        float p = 1f / 5040f;
        p = p * r + 1f / 720f;
        p = p * r + 1f / 120f;
        p = p * r + 1f / 24f;
        p = p * r + 1f / 6f;
        p = p * r + 0.5f;
        p = p * r + 1f;
        p = p * r + 1f;
        int biased = Math.Clamp(n, -126, 127) + 127;
        return BitConverter.Int32BitsToSingle(biased << 23) * p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorExp(ReadOnlySpan<float> source, Span<float> destination)
    {
        ThrowHelper.ValidateDestinationSpan(destination, source);
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
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) ExpKernel256(Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) ExpKernel128(Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = ExpScalar(Unsafe.Add(ref src, (nint)i));
    }

    // ── Type-specific: Log ──────────────────────────────────────────────

    private const float Sqrt2f = 1.41421354f;
    private const float DenormScaleF = 33554432f;
    private const int ExpMaskF = 0x7F800000;
    private const int MantMaskF = 0x007FFFFF;
    private const int ExpBiasF = 0x3F800000;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> LogKernel512(Vector512<float> x)
    {
        Vector512<int> bits = x.AsInt32();
        Vector512<int> expField = bits & Vector512.Create(ExpMaskF);
        Vector512<int> subMask = Vector512.Equals(expField, Vector512<int>.Zero);
        Vector512<float> xs = Vector512.ConditionalSelect(subMask.AsSingle(), x * Vector512.Create(DenormScaleF), x);
        Vector512<int> bits2 = xs.AsInt32();
        Vector512<int> e = ((bits2 & Vector512.Create(ExpMaskF)) >> 23) - Vector512.Create(127) - Vector512.ConditionalSelect(subMask, Vector512.Create(25), Vector512<int>.Zero);
        Vector512<float> m = (bits2 & Vector512.Create(MantMaskF) | Vector512.Create(ExpBiasF)).AsSingle();
        Vector512<float> ge = Vector512.GreaterThanOrEqual(m, Vector512.Create(Sqrt2f));
        m = Vector512.ConditionalSelect(ge, m * Vector512.Create(0.5f), m);
        e = Vector512.ConditionalSelect(ge.AsInt32(), e + Vector512.Create(1), e);
        Vector512<float> f = m - Vector512.Create(1f);
        Vector512<float> s = f / (f + Vector512.Create(2f));
        Vector512<float> z = s * s;
        Vector512<float> p = Vector512.Create(1f / 15f);
        p = p * z + Vector512.Create(1f / 13f);
        p = p * z + Vector512.Create(1f / 11f);
        p = p * z + Vector512.Create(1f / 9f);
        p = p * z + Vector512.Create(1f / 7f);
        p = p * z + Vector512.Create(1f / 5f);
        p = p * z + Vector512.Create(1f / 3f);
        Vector512<float> lnF = (Vector512.Create(2f) * s) * (p * z + Vector512.Create(1f));
        Vector512<float> result = Vector512.ConvertToSingle(e) * Vector512.Create(0.6931471805599453f) + lnF;
        result = Vector512.ConditionalSelect(Vector512.LessThanOrEqual(x, Vector512<float>.Zero), Vector512.Create(float.NegativeInfinity), result);
        result = Vector512.ConditionalSelect(Vector512.Equals(expField, Vector512.Create(ExpMaskF)).AsSingle(), x, result);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> LogKernel256(Vector256<float> x)
    {
        Vector256<int> bits = x.AsInt32();
        Vector256<int> expField = bits & Vector256.Create(ExpMaskF);
        Vector256<int> subMask = Vector256.Equals(expField, Vector256<int>.Zero);
        Vector256<float> xs = Vector256.ConditionalSelect(subMask.AsSingle(), x * Vector256.Create(DenormScaleF), x);
        Vector256<int> bits2 = xs.AsInt32();
        Vector256<int> e = ((bits2 & Vector256.Create(ExpMaskF)) >> 23) - Vector256.Create(127) - Vector256.ConditionalSelect(subMask, Vector256.Create(25), Vector256<int>.Zero);
        Vector256<float> m = (bits2 & Vector256.Create(MantMaskF) | Vector256.Create(ExpBiasF)).AsSingle();
        Vector256<float> ge = Vector256.GreaterThanOrEqual(m, Vector256.Create(Sqrt2f));
        m = Vector256.ConditionalSelect(ge, m * Vector256.Create(0.5f), m);
        e = Vector256.ConditionalSelect(ge.AsInt32(), e + Vector256.Create(1), e);
        Vector256<float> f = m - Vector256.Create(1f);
        Vector256<float> s = f / (f + Vector256.Create(2f));
        Vector256<float> z = s * s;
        Vector256<float> p = Vector256.Create(1f / 15f);
        p = p * z + Vector256.Create(1f / 13f);
        p = p * z + Vector256.Create(1f / 11f);
        p = p * z + Vector256.Create(1f / 9f);
        p = p * z + Vector256.Create(1f / 7f);
        p = p * z + Vector256.Create(1f / 5f);
        p = p * z + Vector256.Create(1f / 3f);
        Vector256<float> lnF = (Vector256.Create(2f) * s) * (p * z + Vector256.Create(1f));
        Vector256<float> result = Vector256.ConvertToSingle(e) * Vector256.Create(0.6931471805599453f) + lnF;
        result = Vector256.ConditionalSelect(Vector256.LessThanOrEqual(x, Vector256<float>.Zero), Vector256.Create(float.NegativeInfinity), result);
        result = Vector256.ConditionalSelect(Vector256.Equals(expField, Vector256.Create(ExpMaskF)).AsSingle(), x, result);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> LogKernel128(Vector128<float> x)
    {
        Vector128<int> bits = x.AsInt32();
        Vector128<int> expField = bits & Vector128.Create(ExpMaskF);
        Vector128<int> subMask = Vector128.Equals(expField, Vector128<int>.Zero);
        Vector128<float> xs = Vector128.ConditionalSelect(subMask.AsSingle(), x * Vector128.Create(DenormScaleF), x);
        Vector128<int> bits2 = xs.AsInt32();
        Vector128<int> e = ((bits2 & Vector128.Create(ExpMaskF)) >> 23) - Vector128.Create(127) - Vector128.ConditionalSelect(subMask, Vector128.Create(25), Vector128<int>.Zero);
        Vector128<float> m = (bits2 & Vector128.Create(MantMaskF) | Vector128.Create(ExpBiasF)).AsSingle();
        Vector128<float> ge = Vector128.GreaterThanOrEqual(m, Vector128.Create(Sqrt2f));
        m = Vector128.ConditionalSelect(ge, m * Vector128.Create(0.5f), m);
        e = Vector128.ConditionalSelect(ge.AsInt32(), e + Vector128.Create(1), e);
        Vector128<float> f = m - Vector128.Create(1f);
        Vector128<float> s = f / (f + Vector128.Create(2f));
        Vector128<float> z = s * s;
        Vector128<float> p = Vector128.Create(1f / 15f);
        p = p * z + Vector128.Create(1f / 13f);
        p = p * z + Vector128.Create(1f / 11f);
        p = p * z + Vector128.Create(1f / 9f);
        p = p * z + Vector128.Create(1f / 7f);
        p = p * z + Vector128.Create(1f / 5f);
        p = p * z + Vector128.Create(1f / 3f);
        Vector128<float> lnF = (Vector128.Create(2f) * s) * (p * z + Vector128.Create(1f));
        Vector128<float> result = Vector128.ConvertToSingle(e) * Vector128.Create(0.6931471805599453f) + lnF;
        result = Vector128.ConditionalSelect(Vector128.LessThanOrEqual(x, Vector128<float>.Zero), Vector128.Create(float.NegativeInfinity), result);
        result = Vector128.ConditionalSelect(Vector128.Equals(expField, Vector128.Create(ExpMaskF)).AsSingle(), x, result);
        return result;
    }

    private static float LogScalar(float x)
    {
        if (x <= 0f) return float.NegativeInfinity;
        int bits = BitConverter.SingleToInt32Bits(x);
        if ((bits & ExpMaskF) == ExpMaskF) return x;
        int e = ((bits & ExpMaskF) >> 23) - 127;
        if ((bits & ExpMaskF) == 0) { x *= DenormScaleF; bits = BitConverter.SingleToInt32Bits(x); e = ((bits & ExpMaskF) >> 23) - 127 - 25; }
        float m = BitConverter.Int32BitsToSingle((bits & MantMaskF) | ExpBiasF);
        if (m >= Sqrt2f) { m *= 0.5f; e += 1; }
        float f = m - 1f;
        float s = f / (f + 2f);
        float z = s * s;
        float p = 1f / 15f;
        p = p * z + 1f / 13f;
        p = p * z + 1f / 11f;
        p = p * z + 1f / 9f;
        p = p * z + 1f / 7f;
        p = p * z + 1f / 5f;
        p = p * z + 1f / 3f;
        return e * 0.6931471805599453f + (2f * s) * (p * z + 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorLog(ReadOnlySpan<float> source, Span<float> destination)
    {
        ThrowHelper.ValidateDestinationSpan(destination, source);
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
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) LogKernel256(Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) LogKernel128(Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = LogScalar(Unsafe.Add(ref src, (nint)i));
    }

    // ── Type-specific: Sigmoid ──────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorSigmoid(ReadOnlySpan<float> source, Span<float> destination)
    {
        ThrowHelper.ValidateDestinationSpan(destination, source);
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
                (Vector256.Create(1f) / (Vector256.Create(1f) + ExpKernel256(Vector256.Create(0f) - Vector256.LoadUnsafe(in src, i)))).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector128.Create(1f) / (Vector128.Create(1f) + ExpKernel128(Vector128.Create(0f) - Vector128.LoadUnsafe(in src, i)))).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i)
        {
            float x = Unsafe.Add(ref src, (nint)i);
            Unsafe.Add(ref dst, (nint)i) = 1f / (1f + MathF.Exp(-x));
        }
    }

    // ── Type-specific: Tanh ─────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorTanh(ReadOnlySpan<float> source, Span<float> destination)
    {
        ThrowHelper.ValidateDestinationSpan(destination, source);
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
                Vector256<float> p = ExpKernel256(x * Vector256.Create(2f));
                ((p - Vector256.Create(1f)) / (p + Vector256.Create(1f))).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<float> x = Vector128.Max(Vector128.Min(Vector128.LoadUnsafe(in src, i), Vector128.Create(9f)), Vector128.Create(-9f));
                Vector128<float> p = ExpKernel128(x * Vector128.Create(2f));
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

    // ── Delegated math methods ──────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorReLU(ReadOnlySpan<float> source, Span<float> destination)
        => SimdFloatingPointOps<float>.VectorReLU(source, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorRSqrt(ReadOnlySpan<float> source, Span<float> destination)
        => SimdFloatingPointOps<float>.VectorRSqrt(source, destination);

    // ── Type-specific: Pow ──────────────────────────────────────────────

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
                Vector512<float> result = ExpKernel512(e * LogKernel512(b));
                Vector512<float> negBase = Vector512.LessThan(b, Vector512<float>.Zero);
                if (negBase != Vector512<float>.Zero)
                {
                    for (nuint j = 0; j < step; j++)
                    {
                        if (Unsafe.Add(ref Unsafe.As<Vector512<float>, float>(ref negBase), (nint)j) != 0f)
                            Unsafe.Add(ref Unsafe.As<Vector512<float>, float>(ref result), (nint)j) =
                                MathF.Pow(Unsafe.Add(ref Unsafe.As<Vector512<float>, float>(ref b), (nint)j),
                                          Unsafe.Add(ref Unsafe.As<Vector512<float>, float>(ref e), (nint)j));
                    }
                }
                result.StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<float> b = Vector256.LoadUnsafe(in bRef, i);
                Vector256<float> e = Vector256.LoadUnsafe(in eRef, i);
                Vector256<float> result = ExpKernel256(e * LogKernel256(b));
                Vector256<float> negBase = Vector256.LessThan(b, Vector256<float>.Zero);
                if (negBase != Vector256<float>.Zero)
                {
                    for (nuint j = 0; j < step; j++)
                    {
                        if (Unsafe.Add(ref Unsafe.As<Vector256<float>, float>(ref negBase), (nint)j) != 0f)
                            Unsafe.Add(ref Unsafe.As<Vector256<float>, float>(ref result), (nint)j) =
                                MathF.Pow(Unsafe.Add(ref Unsafe.As<Vector256<float>, float>(ref b), (nint)j),
                                          Unsafe.Add(ref Unsafe.As<Vector256<float>, float>(ref e), (nint)j));
                    }
                }
                result.StoreUnsafe(ref dRef, i);
            }
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = MathF.Pow(Unsafe.Add(ref bRef, (nint)i), Unsafe.Add(ref eRef, (nint)i));
    }

    // ── Delegated math methods (continued) ──────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Softmax(ReadOnlySpan<float> source, Span<float> destination)
        => SimdFloatingPointOps<float>.Softmax(source, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Mat4x4Multiply(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloatingPointOps<float>.Mat4x4Multiply(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CrossProduct(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloatingPointOps<float>.CrossProduct(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void QuaternionMultiply(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloatingPointOps<float>.QuaternionMultiply(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void QuaternionSlerp(ReadOnlySpan<float> from, ReadOnlySpan<float> to, float t, Span<float> destination)
        => SimdFloatingPointOps<float>.QuaternionSlerp(from, to, t, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Swizzle(ReadOnlySpan<float> source, ReadOnlySpan<int> indices, Span<float> destination)
        => SimdFloatingPointOps<float>.Swizzle(source, indices, destination);

    // ── Type-specific: Half pack/unpack ─────────────────────────────────

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
