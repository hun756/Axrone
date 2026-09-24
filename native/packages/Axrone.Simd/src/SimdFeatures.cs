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
