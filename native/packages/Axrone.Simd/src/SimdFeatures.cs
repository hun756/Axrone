namespace Axrone.Simd;

/// <summary>Processor architecture families with SIMD personalities.</summary>
public enum SimdArchitecture : byte
{
    /// <summary>Unrecognized architecture; only scalar paths are safe.</summary>
    Unknown = 0,

    /// <summary>32-bit x86.</summary>
    X86 = 1,

    /// <summary>64-bit x86.</summary>
    X64 = 2,

    /// <summary>32-bit ARM.</summary>
    Arm = 3,

    /// <summary>64-bit ARM.</summary>
    Arm64 = 4,

    /// <summary>WebAssembly.</summary>
    Wasm = 5,

    /// <summary>LoongArch 64-bit.</summary>
    LoongArch64 = 6,

    /// <summary>RISC-V 64-bit.</summary>
    RiscV64 = 7,
}

/// <summary>ISA baseline levels for dispatch policy.</summary>
public enum SimdIsaLevel : byte
{
    /// <summary>No recognized baseline; scalar only.</summary>
    Generic = 0,

    /// <summary>x86-64 baseline (SSE/SSE2).</summary>
    X86_64_V1 = 1,

    /// <summary>x86-64-v2 (SSSE3/SSE4/POPCNT).</summary>
    X86_64_V2 = 2,

    /// <summary>x86-64-v3 (AVX/AVX2/BMI/FMA).</summary>
    X86_64_V3 = 3,

    /// <summary>x86-64-v4 (AVX-512F/BW/CD/DQ/VL).</summary>
    X86_64_V4 = 4,

    /// <summary>x86-64 with AVX10.1.</summary>
    X86_64_Avx10_1 = 5,

    /// <summary>x86-64 with AVX10.2.</summary>
    X86_64_Avx10_2 = 6,

    /// <summary>ARMv8.0 with AdvSIMD.</summary>
    Arm64_V8_0 = 10,

    /// <summary>ARMv8.2 with AdvSIMD, dot-product and CRC32.</summary>
    Arm64_V8_2 = 11,

    /// <summary>ARMv9.0 with SVE2.</summary>
    Arm64_V9_0 = 12,
}

/// <summary>Individual SIMD capabilities. Values below 64 live in partition 0 (x86),
/// 64..127 in partition 1 (ARM), 128..191 in partition 2 (other ISAs), and 192..255 in
/// partition 3 (portable <c>System.Numerics</c> acceleration flags).</summary>
public enum SimdFeature : byte
{
    /// <summary>Streaming SIMD Extensions.</summary>
    Sse = 0,

    /// <summary>Streaming SIMD Extensions 2.</summary>
    Sse2 = 1,

    /// <summary>Streaming SIMD Extensions 3.</summary>
    Sse3 = 2,

    /// <summary>Supplemental SSE3.</summary>
    Ssse3 = 3,

    /// <summary>SSE 4.1.</summary>
    Sse41 = 4,

    /// <summary>SSE 4.2.</summary>
    Sse42 = 5,

    /// <summary>Advanced Vector Extensions.</summary>
    Avx = 6,

    /// <summary>Advanced Vector Extensions 2.</summary>
    Avx2 = 7,

    /// <summary>Fused multiply-add.</summary>
    Fma = 8,

    /// <summary>Bit Manipulation 1.</summary>
    Bmi1 = 9,

    /// <summary>Bit Manipulation 2.</summary>
    Bmi2 = 10,

    /// <summary>Population count.</summary>
    Popcnt = 11,

    /// <summary>Leading zero count.</summary>
    Lzcnt = 12,

    /// <summary>AES instructions.</summary>
    Aes = 13,

    /// <summary>Carry-less multiplication.</summary>
    Pclmulqdq = 14,

    /// <summary>AVX-512 foundation.</summary>
    Avx512F = 15,

    /// <summary>AVX-512 byte and word.</summary>
    Avx512BW = 16,

    /// <summary>AVX-512 conflict detection.</summary>
    Avx512CD = 17,

    /// <summary>AVX-512 doubleword and quadword.</summary>
    Avx512DQ = 18,

    /// <summary>AVX-512 vector length extensions.</summary>
    Avx512VL = 19,

    /// <summary>AVX-512 vector byte manipulation.</summary>
    Avx512Vbmi = 20,

    /// <summary>AVX-512 VBMI2.</summary>
    Avx512Vbmi2 = 21,

    /// <summary>AVX-512 vector neural network instructions.</summary>
    Avx512Vnni = 22,

    /// <summary>AVX-512 bit algorithms.</summary>
    Avx512Bitalg = 23,

    /// <summary>AVX-512 population count.</summary>
    Avx512Vpopcntdq = 24,

    /// <summary>AVX-512 half precision.</summary>
    Avx512Fp16 = 25,

    /// <summary>AVX-512 brain float 16.</summary>
    Avx512Bf16 = 26,

    /// <summary>AVX-512 intersect.</summary>
    Avx512Vp2Intersect = 27,

    /// <summary>AVX integer fused multiply-add.</summary>
    AvxIfma = 28,

    /// <summary>AVX vector neural network instructions.</summary>
    AvxVnni = 29,

    /// <summary>AVX-VNNI INT8.</summary>
    AvxVnniInt8 = 30,

    /// <summary>AVX-VNNI INT16.</summary>
    AvxVnniInt16 = 31,

    /// <summary>Galois field instructions.</summary>
    Gfni = 32,

    /// <summary>Vector carry-less multiplication.</summary>
    Vpclmulqdq = 33,

    /// <summary>SERIALIZE instruction.</summary>
    X86Serialize = 34,

    /// <summary>64-bit x86 baseline.</summary>
    X86Base = 35,

    /// <summary>AVX10.1.</summary>
    Avx10v1 = 36,

    /// <summary>AVX10.2.</summary>
    Avx10v2 = 37,

    /// <summary>ARM 64-bit baseline.</summary>
    ArmBase = 64,

    /// <summary>ARM Advanced SIMD (NEON).</summary>
    AdvSimd = 65,

    /// <summary>ARM AES.</summary>
    ArmAes = 66,

    /// <summary>ARM CRC32.</summary>
    ArmCrc32 = 67,

    /// <summary>ARM dot product.</summary>
    ArmDp = 68,

    /// <summary>ARM rounding doubling multiply.</summary>
    ArmRdm = 69,

    /// <summary>ARM SHA1.</summary>
    ArmSha1 = 70,

    /// <summary>ARM SHA256.</summary>
    ArmSha256 = 71,

    /// <summary>ARM large system extensions (atomics).</summary>
    ArmAtomics = 72,

    /// <summary>ARM Scalable Vector Extension.</summary>
    ArmSve = 73,

    /// <summary>ARM SVE2.</summary>
    ArmSve2 = 74,

    /// <summary>ARM SHA512.</summary>
    ArmSha512 = 75,

    /// <summary>ARM SM3.</summary>
    ArmSm3 = 76,

    /// <summary>ARM SM4.</summary>
    ArmSm4 = 77,

    /// <summary>ARM LRCPC.</summary>
    ArmRcpc = 78,

    /// <summary>WebAssembly 128-bit packed SIMD.</summary>
    WasmPackedSimd = 128,

    /// <summary>RISC-V vector extension.</summary>
    RiscVVector = 129,

    /// <summary>LoongArch 128-bit SIMD.</summary>
    LoongArchLsx = 130,

    /// <summary>LoongArch 256-bit SIMD.</summary>
    LoongArchLasx = 131,

    /// <summary>Portable vectors are hardware accelerated.</summary>
    VectorHardwareAccelerated = 192,

    /// <summary>64-bit portable vectors are hardware accelerated.</summary>
    Vector64HardwareAccelerated = 193,

    /// <summary>128-bit portable vectors are hardware accelerated.</summary>
    Vector128HardwareAccelerated = 194,

    /// <summary>256-bit portable vectors are hardware accelerated.</summary>
    Vector256HardwareAccelerated = 195,

    /// <summary>512-bit portable vectors are hardware accelerated.</summary>
    Vector512HardwareAccelerated = 196,
}

/// <summary>Dispatch tiers, widest first. The dispatcher walks down until a policy holds.</summary>
public enum SimdDispatchTier : byte
{
    /// <summary>Plain scalar loop.</summary>
    Scalar = 0,

    /// <summary>128-bit vector path.</summary>
    Vector128 = 1,

    /// <summary>256-bit AVX2 path.</summary>
    Avx2 = 2,

    /// <summary>512-bit AVX-512 path.</summary>
    Avx512 = 3,
}
