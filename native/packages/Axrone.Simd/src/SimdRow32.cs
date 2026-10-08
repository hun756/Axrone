namespace Axrone.Simd;

public static class SimdRow32
{
    public static bool IsFusedMultiplyAddSupported
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => System.Runtime.Intrinsics.X86.Fma.IsSupported || System.Runtime.Intrinsics.Arm.AdvSimd.Arm64.IsSupported;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector128<float> MultiplyAddRowFused(
        float x, float y, float z, float w,
        Vector128<float> b0, Vector128<float> b1, Vector128<float> b2, Vector128<float> b3)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            return Vector128.FusedMultiplyAdd(Vector128.Create(x), b0,
                Vector128.FusedMultiplyAdd(Vector128.Create(y), b1,
                Vector128.FusedMultiplyAdd(Vector128.Create(z), b2, Vector128.Create(w) * b3)));
        }
        return MultiplyAddRowScalar(x, y, z, w, b0, b1, b2, b3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector128<float> MultiplyAddRowPlain(
        float x, float y, float z, float w,
        Vector128<float> b0, Vector128<float> b1, Vector128<float> b2, Vector128<float> b3)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> bx = Vector128.Create(x);
            Vector128<float> by = Vector128.Create(y);
            Vector128<float> bz = Vector128.Create(z);
            Vector128<float> bw = Vector128.Create(w);
            return Vector128.Add(Vector128.Multiply(bx, b0),
                Vector128.Add(Vector128.Multiply(by, b1),
                Vector128.Add(Vector128.Multiply(bz, b2), Vector128.Multiply(bw, b3))));
        }
        return MultiplyAddRowScalar(x, y, z, w, b0, b1, b2, b3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector128<float> MultiplyAddRowScalar(
        float x, float y, float z, float w,
        Vector128<float> b0, Vector128<float> b1, Vector128<float> b2, Vector128<float> b3)
    {
        Vector128<float> lb0 = b0;
        Vector128<float> lb1 = b1;
        Vector128<float> lb2 = b2;
        Vector128<float> lb3 = b3;
        ref float r0 = ref Unsafe.As<Vector128<float>, float>(ref lb0);
        ref float r1 = ref Unsafe.As<Vector128<float>, float>(ref lb1);
        ref float r2 = ref Unsafe.As<Vector128<float>, float>(ref lb2);
        ref float r3 = ref Unsafe.As<Vector128<float>, float>(ref lb3);
        float f0 = (x * r0) + ((y * r1) + ((z * r2) + (w * r3)));
        float f1 = (x * Unsafe.Add(ref r0, 1)) + ((y * Unsafe.Add(ref r1, 1)) + ((z * Unsafe.Add(ref r2, 1)) + (w * Unsafe.Add(ref r3, 1))));
        float f2 = (x * Unsafe.Add(ref r0, 2)) + ((y * Unsafe.Add(ref r1, 2)) + ((z * Unsafe.Add(ref r2, 2)) + (w * Unsafe.Add(ref r3, 2))));
        float f3 = (x * Unsafe.Add(ref r0, 3)) + ((y * Unsafe.Add(ref r1, 3)) + ((z * Unsafe.Add(ref r2, 3)) + (w * Unsafe.Add(ref r3, 3))));
        return Vector128.Create(f0, f1, f2, f3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector128<float> LerpRowFused(Vector128<float> a, Vector128<float> b, Vector128<float> amount)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            return Vector128.FusedMultiplyAdd(b - a, amount, a);
        }
        return LerpRowScalar(a, b, amount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector128<float> LerpRowPlain(Vector128<float> a, Vector128<float> b, Vector128<float> amount)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            return Vector128.Add(Vector128.Multiply(b - a, amount), a);
        }
        return LerpRowScalar(a, b, amount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector128<float> LerpRowScalar(Vector128<float> a, Vector128<float> b, Vector128<float> amount)
    {
        Vector128<float> la = a;
        Vector128<float> lb = b;
        Vector128<float> lt = amount;
        ref float ra = ref Unsafe.As<Vector128<float>, float>(ref la);
        ref float rb = ref Unsafe.As<Vector128<float>, float>(ref lb);
        ref float rt = ref Unsafe.As<Vector128<float>, float>(ref lt);
        float f0 = ((rb - ra) * rt) + ra;
        float f1 = ((Unsafe.Add(ref rb, 1) - Unsafe.Add(ref ra, 1)) * Unsafe.Add(ref rt, 1)) + Unsafe.Add(ref ra, 1);
        float f2 = ((Unsafe.Add(ref rb, 2) - Unsafe.Add(ref ra, 2)) * Unsafe.Add(ref rt, 2)) + Unsafe.Add(ref ra, 2);
        float f3 = ((Unsafe.Add(ref rb, 3) - Unsafe.Add(ref ra, 3)) * Unsafe.Add(ref rt, 3)) + Unsafe.Add(ref ra, 3);
        return Vector128.Create(f0, f1, f2, f3);
    }
}
