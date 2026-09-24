namespace Axrone.Simd;

using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.Wasm;
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

/// <summary>OS-level ARM feature probes for capabilities the BCL does not surface.</summary>
internal static partial class ArmOsCapabilityProber
{
    [LibraryImport("libc", EntryPoint = "getauxval")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial nuint GetAuxVal(nuint type);

    [LibraryImport("kernel32.dll", EntryPoint = "IsProcessorFeaturePresent")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsProcessorFeaturePresent(uint processorFeature);

    [LibraryImport("libc", EntryPoint = "sysctlbyname", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static unsafe partial int SysCtlByName(string name, void* oldp, nuint* oldlenp, void* newp, nuint newlen);

    private const nuint AT_HWCAP = 16;

    // ARM64 HWCAP flags (Linux uapi asm/hwcap.h)
    private const ulong HWCAP_ATOMICS = 1UL << 8;
    private const ulong HWCAP_SHA512 = 1UL << 21;
    private const ulong HWCAP_SM3 = 1UL << 18;
    private const ulong HWCAP_SM4 = 1UL << 19;
    private const ulong HWCAP_LRCPC = 1UL << 20;

    // ARM32 (ARMv7) HWCAP flags (Linux uapi asm/hwcap.h)
    private const ulong HWCAP_ARM32_NEON = 1UL << 12;

    private const uint PF_ARM_V81_ATOMIC_INSTRUCTIONS_AVAILABLE = 34;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe bool CheckArmFeature(SimdFeature feature)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
        {
            ulong hwcap = (ulong)GetAuxVal(AT_HWCAP);
            return feature switch
            {
                SimdFeature.ArmAtomics => (hwcap & HWCAP_ATOMICS) != 0,
                SimdFeature.ArmSha512 => (hwcap & HWCAP_SHA512) != 0,
                SimdFeature.ArmSm3 => (hwcap & HWCAP_SM3) != 0,
                SimdFeature.ArmSm4 => (hwcap & HWCAP_SM4) != 0,
                SimdFeature.ArmRcpc => (hwcap & HWCAP_LRCPC) != 0,
                _ => false,
            };
        }

        if (OperatingSystem.IsWindows())
        {
            return feature switch
            {
                SimdFeature.ArmAtomics => IsProcessorFeaturePresent(PF_ARM_V81_ATOMIC_INSTRUCTIONS_AVAILABLE),
                _ => false,
            };
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsWatchOS())
        {
            string? mib = feature switch
            {
                SimdFeature.ArmAtomics => "hw.optional.arm.FEAT_LSE",
                SimdFeature.ArmSha512 => "hw.optional.arm.FEAT_SHA512",
                SimdFeature.ArmRcpc => "hw.optional.arm.FEAT_LRCPC",
                _ => null,
            };

            if (mib is null)
            {
                return false;
            }

            // CA1508 cannot see through the native out-param: sysctlbyname writes value
            // on success, so the comparison is live, not dead.
#pragma warning disable CA1508
            int value = 0;
            nuint size = (nuint)sizeof(int);
            if (SysCtlByName(mib, &value, &size, null, 0) == 0)
            {
                return value != 0;
            }
#pragma warning restore CA1508
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool CheckArm32Neon()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
        {
            ulong hwcap = (ulong)GetAuxVal(AT_HWCAP);
            return (hwcap & HWCAP_ARM32_NEON) != 0;
        }

        return false;
    }
}

/// <summary>ARM64 probing: BCL intrinsics plus OS capability fallbacks.</summary>
internal static class Arm64Prober
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 Probe()
    {
        ulong p1 = 0;

        if (ArmBase.IsSupported)
        {
            p1 |= 1UL << ((byte)SimdFeature.ArmBase - 64);
            if (AdvSimd.IsSupported)
            {
                p1 |= 1UL << ((byte)SimdFeature.AdvSimd - 64);
            }

            if (System.Runtime.Intrinsics.Arm.Aes.IsSupported)
            {
                p1 |= 1UL << ((byte)SimdFeature.ArmAes - 64);
            }

            if (Crc32.IsSupported)
            {
                p1 |= 1UL << ((byte)SimdFeature.ArmCrc32 - 64);
            }

            if (Dp.IsSupported)
            {
                p1 |= 1UL << ((byte)SimdFeature.ArmDp - 64);
            }

            if (Rdm.IsSupported)
            {
                p1 |= 1UL << ((byte)SimdFeature.ArmRdm - 64);
            }

            if (Sha1.IsSupported)
            {
                p1 |= 1UL << ((byte)SimdFeature.ArmSha1 - 64);
            }

            if (Sha256.IsSupported)
            {
                p1 |= 1UL << ((byte)SimdFeature.ArmSha256 - 64);
            }

            // SYSLIB5003: Sve/Sve2 stay experimental in .NET 9/10; suppressed under
            // TreatWarningsAsErrors the same way the reference implementation does.
#pragma warning disable SYSLIB5003
            if (Sve.IsSupported)
            {
                p1 |= 1UL << ((byte)SimdFeature.ArmSve - 64);
            }

            if (Sve2.IsSupported)
            {
                p1 |= 1UL << ((byte)SimdFeature.ArmSve2 - 64);
            }
#pragma warning restore SYSLIB5003

            if (ArmOsCapabilityProber.CheckArmFeature(SimdFeature.ArmAtomics))
            {
                p1 |= 1UL << ((byte)SimdFeature.ArmAtomics - 64);
            }

            if (ArmOsCapabilityProber.CheckArmFeature(SimdFeature.ArmSha512))
            {
                p1 |= 1UL << ((byte)SimdFeature.ArmSha512 - 64);
            }

            if (ArmOsCapabilityProber.CheckArmFeature(SimdFeature.ArmSm3))
            {
                p1 |= 1UL << ((byte)SimdFeature.ArmSm3 - 64);
            }

            if (ArmOsCapabilityProber.CheckArmFeature(SimdFeature.ArmSm4))
            {
                p1 |= 1UL << ((byte)SimdFeature.ArmSm4 - 64);
            }

            if (ArmOsCapabilityProber.CheckArmFeature(SimdFeature.ArmRcpc))
            {
                p1 |= 1UL << ((byte)SimdFeature.ArmRcpc - 64);
            }
        }

        return new FeatureBitmask256(0UL, p1, 0UL, 0UL);
    }
}

/// <summary>32-bit ARM probing with an HWCAP fallback for NEON.</summary>
internal static class Arm32Prober
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 Probe()
    {
        ulong p1 = 0;

        if (ArmBase.IsSupported)
        {
            p1 |= 1UL << ((byte)SimdFeature.ArmBase - 64);
            if (AdvSimd.IsSupported || ArmOsCapabilityProber.CheckArm32Neon())
            {
                p1 |= 1UL << ((byte)SimdFeature.AdvSimd - 64);
            }
        }

        return new FeatureBitmask256(0UL, p1, 0UL, 0UL);
    }
}

/// <summary>WebAssembly 128-bit SIMD rides on PackedSimd and Vector128.</summary>
internal static class WasmProber
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 Probe()
    {
        ulong p2 = 0;
        if (PackedSimd.IsSupported || Vector128.IsHardwareAccelerated)
        {
            p2 |= 1UL << ((byte)SimdFeature.WasmPackedSimd - 128);
        }

        return new FeatureBitmask256(0UL, 0UL, p2, 0UL);
    }
}

/// <summary>RISC-V vector support follows portable acceleration.</summary>
internal static class RiscV64Prober
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 Probe()
    {
        ulong p2 = 0;
        if (Vector.IsHardwareAccelerated)
        {
            p2 |= 1UL << ((byte)SimdFeature.RiscVVector - 128);
        }

        return new FeatureBitmask256(0UL, 0UL, p2, 0UL);
    }
}

/// <summary>LoongArch LSX/LASX follow the matching portable widths.</summary>
internal static class LoongArch64Prober
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 Probe()
    {
        ulong p2 = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            p2 |= 1UL << ((byte)SimdFeature.LoongArchLsx - 128);
        }

        if (Vector256.IsHardwareAccelerated)
        {
            p2 |= 1UL << ((byte)SimdFeature.LoongArchLasx - 128);
        }

        return new FeatureBitmask256(0UL, 0UL, p2, 0UL);
    }
}

/// <summary>Portable <c>System.Numerics</c> acceleration flags, valid on every ISA.</summary>
internal static class GenericVectorProber
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 Probe()
    {
        ulong p3 = 0;

        if (Vector.IsHardwareAccelerated)
        {
            p3 |= 1UL << ((byte)SimdFeature.VectorHardwareAccelerated - 192);
        }

        if (Vector64.IsHardwareAccelerated)
        {
            p3 |= 1UL << ((byte)SimdFeature.Vector64HardwareAccelerated - 192);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            p3 |= 1UL << ((byte)SimdFeature.Vector128HardwareAccelerated - 192);
        }

        if (Vector256.IsHardwareAccelerated)
        {
            p3 |= 1UL << ((byte)SimdFeature.Vector256HardwareAccelerated - 192);
        }

        if (Vector512.IsHardwareAccelerated)
        {
            p3 |= 1UL << ((byte)SimdFeature.Vector512HardwareAccelerated - 192);
        }

        return new FeatureBitmask256(0UL, 0UL, 0UL, p3);
    }
}

/// <summary>Architecture detection and baseline classification.</summary>
internal static class TopologyClassifier
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static SimdArchitecture DetectArchitecture()
    {
        Architecture arch = RuntimeInformation.ProcessArchitecture;
        return arch switch
        {
            Architecture.X86 => SimdArchitecture.X86,
            Architecture.X64 => SimdArchitecture.X64,
            Architecture.Arm => SimdArchitecture.Arm,
            Architecture.Arm64 => SimdArchitecture.Arm64,
            Architecture.Wasm => SimdArchitecture.Wasm,
            Architecture.LoongArch64 => SimdArchitecture.LoongArch64,
            Architecture.RiscV64 => SimdArchitecture.RiscV64,
            _ => SimdArchitecture.Unknown,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static SimdIsaLevel ClassifyLevel(SimdArchitecture arch, FeatureBitmask256 mask)
    {
        if (arch is SimdArchitecture.X64 or SimdArchitecture.X86)
        {
            if (mask.Contains(SimdFeature.Avx10v2))
            {
                return SimdIsaLevel.X86_64_Avx10_2;
            }

            if (mask.Contains(SimdFeature.Avx10v1))
            {
                return SimdIsaLevel.X86_64_Avx10_1;
            }

            if (mask.Contains(SimdFeature.Avx512F) &&
                mask.Contains(SimdFeature.Avx512BW) &&
                mask.Contains(SimdFeature.Avx512CD) &&
                mask.Contains(SimdFeature.Avx512DQ) &&
                mask.Contains(SimdFeature.Avx512VL))
            {
                return SimdIsaLevel.X86_64_V4;
            }

            if (mask.Contains(SimdFeature.Avx) &&
                mask.Contains(SimdFeature.Avx2) &&
                mask.Contains(SimdFeature.Bmi1) &&
                mask.Contains(SimdFeature.Bmi2) &&
                mask.Contains(SimdFeature.Fma))
            {
                return SimdIsaLevel.X86_64_V3;
            }

            if (mask.Contains(SimdFeature.Sse3) &&
                mask.Contains(SimdFeature.Ssse3) &&
                mask.Contains(SimdFeature.Sse41) &&
                mask.Contains(SimdFeature.Sse42) &&
                mask.Contains(SimdFeature.Popcnt))
            {
                return SimdIsaLevel.X86_64_V2;
            }

            if (mask.Contains(SimdFeature.Sse) && mask.Contains(SimdFeature.Sse2))
            {
                return SimdIsaLevel.X86_64_V1;
            }
        }
        else if (arch is SimdArchitecture.Arm64)
        {
            if (mask.Contains(SimdFeature.ArmSve2))
            {
                return SimdIsaLevel.Arm64_V9_0;
            }

            if (mask.Contains(SimdFeature.AdvSimd) &&
                mask.Contains(SimdFeature.ArmDp) &&
                mask.Contains(SimdFeature.ArmCrc32))
            {
                return SimdIsaLevel.Arm64_V8_2;
            }

            if (mask.Contains(SimdFeature.AdvSimd))
            {
                return SimdIsaLevel.Arm64_V8_0;
            }
        }

        return SimdIsaLevel.Generic;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static SimdRegisterWidth ResolveMaxRegisterWidth(FeatureBitmask256 mask)
    {
        if (mask.Contains(SimdFeature.Vector512HardwareAccelerated) || mask.Contains(SimdFeature.Avx512F))
        {
            return SimdRegisterWidth.Bits512;
        }

        if (mask.Contains(SimdFeature.Vector256HardwareAccelerated) ||
            mask.Contains(SimdFeature.Avx2) ||
            mask.Contains(SimdFeature.LoongArchLasx))
        {
            return SimdRegisterWidth.Bits256;
        }

        if (mask.Contains(SimdFeature.Vector128HardwareAccelerated) ||
            mask.Contains(SimdFeature.AdvSimd) ||
            mask.Contains(SimdFeature.WasmPackedSimd) ||
            mask.Contains(SimdFeature.LoongArchLsx) ||
            mask.Contains(SimdFeature.Sse2))
        {
            return SimdRegisterWidth.Bits128;
        }

        if (mask.Contains(SimdFeature.Vector64HardwareAccelerated))
        {
            return SimdRegisterWidth.Bits64;
        }

        return SimdRegisterWidth.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static SimdAlignment ResolveAlignment(SimdRegisterWidth registerWidth)
    {
        if (registerWidth >= SimdRegisterWidth.Bits512)
        {
            return SimdAlignment.Byte64;
        }

        if (registerWidth >= SimdRegisterWidth.Bits256)
        {
            return SimdAlignment.Byte32;
        }

        if (registerWidth >= SimdRegisterWidth.Bits128)
        {
            return SimdAlignment.Byte16;
        }

        return SimdAlignment.None;
    }
}
