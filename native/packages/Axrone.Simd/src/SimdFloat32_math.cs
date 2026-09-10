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

    private const float Log2Ef = 1.4426950408889634f;
    private const float Ln2Hif = 0.693359375f;      // 355/512, exactly representable: n * Ln2Hif is exact
    private const float Ln2Lof = -2.12194440e-4f;   // ln2 - Ln2Hif
    private const float ExpClampf = 87f;            // keeps 2^n in normal range, results never need denormals

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

    private const float Sqrt2f = 1.41421354f;
    private const float DenormScaleF = 33554432f;   // 2^25: maps any float denormal to a normal
    private const int ExpMaskF = 0x7F800000;
    private const int MantMaskF = 0x007FFFFF;
    private const int ExpBiasF = 0x3F800000;

    // ln(x) = e*ln2 + ln(1+f), m in [sqrt(2)/2, sqrt(2)), f = m-1, s = f/(2+f), ln(1+f) = 2s*(1 + z*(1/3 + z*(1/5 + ...)))
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
                (ExpKernel256(Vector256.LoadUnsafe(in eRef, i) * LogKernel256(Vector256.LoadUnsafe(in bRef, i)))).StoreUnsafe(ref dRef, i);
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
