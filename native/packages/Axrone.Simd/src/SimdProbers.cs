namespace Axrone.Simd;

using System.Runtime.Intrinsics.X86;

/// <summary>x86-64 probing: BCL intrinsics first, raw CPUID leaves for the rest.</summary>
internal static class X8664Prober
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 Probe()
    {
        ulong p0 = 0;

        if (X86Base.IsSupported)
        {
            p0 |= 1UL << (byte)SimdFeature.X86Base;
            if (Sse.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Sse;
            }

            if (Sse2.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Sse2;
            }

            if (Sse3.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Sse3;
            }

            if (Ssse3.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Ssse3;
            }

            if (Sse41.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Sse41;
            }

            if (Sse42.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Sse42;
            }

            if (Avx.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Avx;
            }

            if (Avx2.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Avx2;
            }

            if (Fma.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Fma;
            }

            if (Bmi1.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Bmi1;
            }

            if (Bmi2.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Bmi2;
            }

            if (Popcnt.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Popcnt;
            }

            if (Lzcnt.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Lzcnt;
            }

            if (System.Runtime.Intrinsics.X86.Aes.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Aes;
            }

            if (Pclmulqdq.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Pclmulqdq;
            }

            if (Avx512F.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Avx512F;
            }

            if (Avx512BW.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Avx512BW;
            }

            if (Avx512CD.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Avx512CD;
            }

            if (Avx512DQ.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Avx512DQ;
            }

            if (Avx512Vbmi.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Avx512Vbmi;
            }

            if (Avx512Vbmi2.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Avx512Vbmi2;
            }

            if (AvxVnni.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.AvxVnni;
            }

            if (X86Serialize.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.X86Serialize;
            }

            if (Avx10v1.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Avx10v1;
            }

            if (Avx10v2.IsSupported)
            {
                p0 |= 1UL << (byte)SimdFeature.Avx10v2;
            }

            // x86 instructions without BCL coverage come straight from CPUID leaves.
            (int maxBasicLeaf, _, _, _) = X86Base.CpuId(0, 0);
            if (maxBasicLeaf >= 7)
            {
                (int eax70, int ebx70, int ecx70, int edx70) = X86Base.CpuId(7, 0);

                // Leaf 7.0 EBX
                if ((ebx70 & (1 << 21)) != 0)
                {
                    p0 |= 1UL << (byte)SimdFeature.AvxIfma;
                }

                if ((ebx70 & (1 << 31)) != 0)
                {
                    p0 |= 1UL << (byte)SimdFeature.Avx512VL;
                }

                // Leaf 7.0 ECX
                if ((ecx70 & (1 << 8)) != 0)
                {
                    p0 |= 1UL << (byte)SimdFeature.Gfni;
                }

                if ((ecx70 & (1 << 10)) != 0)
                {
                    p0 |= 1UL << (byte)SimdFeature.Vpclmulqdq;
                }

                if ((ecx70 & (1 << 11)) != 0)
                {
                    p0 |= 1UL << (byte)SimdFeature.Avx512Vnni;
                }

                if ((ecx70 & (1 << 12)) != 0)
                {
                    p0 |= 1UL << (byte)SimdFeature.Avx512Bitalg;
                }

                if ((ecx70 & (1 << 14)) != 0)
                {
                    p0 |= 1UL << (byte)SimdFeature.Avx512Vpopcntdq;
                }

                // Leaf 7.0 EDX
                if ((edx70 & (1 << 8)) != 0)
                {
                    p0 |= 1UL << (byte)SimdFeature.Avx512Vp2Intersect;
                }

                if ((edx70 & (1 << 23)) != 0)
                {
                    p0 |= 1UL << (byte)SimdFeature.Avx512Fp16;
                }

                // Leaf 7.1 is only valid when eax70 >= 1.
                if (eax70 >= 1)
                {
                    (int eax71, _, _, int edx71) = X86Base.CpuId(7, 1);
                    if ((eax71 & (1 << 5)) != 0)
                    {
                        p0 |= 1UL << (byte)SimdFeature.Avx512Bf16;
                    }

                    if ((edx71 & (1 << 4)) != 0)
                    {
                        p0 |= 1UL << (byte)SimdFeature.AvxVnniInt8;
                    }

                    if ((edx71 & (1 << 10)) != 0)
                    {
                        p0 |= 1UL << (byte)SimdFeature.AvxVnniInt16;
                    }
                }
            }
        }

        return new FeatureBitmask256(p0, 0UL, 0UL, 0UL);
    }
}
