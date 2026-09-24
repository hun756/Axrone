namespace Axrone.Simd;

/// <summary>SIMD register width. Only real hardware widths (and the zero sentinel) are valid.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 2)]
public readonly record struct SimdRegisterWidth : IComparable<SimdRegisterWidth>
{
    /// <summary>Width in bits.</summary>
    public readonly ushort BitWidth;

    /// <summary>Creates a width; rejects anything but 0, 64, 128, 256, or 512.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public SimdRegisterWidth(ushort bitWidth)
    {
        if (bitWidth is not (0 or 64 or 128 or 256 or 512))
        {
            SimdThrowHelper.ThrowInvalidRegisterWidth(bitWidth);
        }

        BitWidth = bitWidth;
    }

    /// <summary>No vector registers.</summary>
    public static SimdRegisterWidth None => default;

    /// <summary>64-bit registers.</summary>
    public static SimdRegisterWidth Bits64 => new(64);

    /// <summary>128-bit registers.</summary>
    public static SimdRegisterWidth Bits128 => new(128);

    /// <summary>256-bit registers.</summary>
    public static SimdRegisterWidth Bits256 => new(256);

    /// <summary>512-bit registers.</summary>
    public static SimdRegisterWidth Bits512 => new(512);

    /// <summary>Width in bytes.</summary>
    public byte ByteWidth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => (byte)(BitWidth >>> 3);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(SimdRegisterWidth other) => BitWidth.CompareTo(other.BitWidth);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(SimdRegisterWidth left, SimdRegisterWidth right) => left.BitWidth < right.BitWidth;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(SimdRegisterWidth left, SimdRegisterWidth right) => left.BitWidth <= right.BitWidth;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(SimdRegisterWidth left, SimdRegisterWidth right) => left.BitWidth > right.BitWidth;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(SimdRegisterWidth left, SimdRegisterWidth right) => left.BitWidth >= right.BitWidth;
}

/// <summary>Preferred memory alignment boundary for vector loads and stores.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 2)]
public readonly record struct SimdAlignment
{
    /// <summary>Boundary in bytes; always a power of two.</summary>
    public readonly ushort BoundaryBytes;

    /// <summary>Creates an alignment; rejects zero and non-powers of two.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public SimdAlignment(ushort boundaryBytes)
    {
        if (boundaryBytes == 0 || !BitOperations.IsPow2(boundaryBytes))
        {
            SimdThrowHelper.ThrowInvalidAlignment(boundaryBytes);
        }

        BoundaryBytes = boundaryBytes;
    }

    /// <summary>No alignment requirement (one byte).</summary>
    public static SimdAlignment None => new(1);

    /// <summary>16-byte alignment for 128-bit vectors.</summary>
    public static SimdAlignment Byte16 => new(16);

    /// <summary>32-byte alignment for 256-bit vectors.</summary>
    public static SimdAlignment Byte32 => new(32);

    /// <summary>64-byte alignment for 512-bit vectors.</summary>
    public static SimdAlignment Byte64 => new(64);

    /// <summary>Checks an address against the boundary.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsAligned(nuint address) => (address & (nuint)(BoundaryBytes - 1)) == 0;

    /// <summary>Checks a pointer against the boundary.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool IsAligned(void* address) => IsAligned((nuint)address);
}
