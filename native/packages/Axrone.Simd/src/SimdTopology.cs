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

/// <summary>256-bit feature set: four 64-bit partitions keyed by <see cref="SimdFeature"/> value.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly struct FeatureBitmask256 : IEquatable<FeatureBitmask256>
{
    /// <summary>x86 features (values 0..63).</summary>
    public readonly ulong Partition0;

    /// <summary>ARM features (values 64..127).</summary>
    public readonly ulong Partition1;

    /// <summary>Other ISAs (values 128..191).</summary>
    public readonly ulong Partition2;

    /// <summary>Portable acceleration flags (values 192..255).</summary>
    public readonly ulong Partition3;

    /// <summary>Empty set.</summary>
    public static FeatureBitmask256 Empty => default;

    /// <summary>Creates a mask from raw partitions.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public FeatureBitmask256(ulong p0, ulong p1, ulong p2, ulong p3)
    {
        Partition0 = p0;
        Partition1 = p1;
        Partition2 = p2;
        Partition3 = p3;
    }

    /// <summary>Creates a single-feature mask.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 Create(SimdFeature feature)
    {
        byte index = (byte)feature;
        nuint partition = (nuint)(index >>> 6);
        ulong mask = 1UL << (index & 63);

        return partition switch
        {
            0 => new FeatureBitmask256(mask, 0UL, 0UL, 0UL),
            1 => new FeatureBitmask256(0UL, mask, 0UL, 0UL),
            2 => new FeatureBitmask256(0UL, 0UL, mask, 0UL),
            3 => new FeatureBitmask256(0UL, 0UL, 0UL, mask),
            _ => default,
        };
    }

    /// <summary>Returns a copy with the feature set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public FeatureBitmask256 Set(SimdFeature feature)
    {
        byte index = (byte)feature;
        nuint partition = (nuint)(index >>> 6);
        ulong mask = 1UL << (index & 63);

        return partition switch
        {
            0 => new FeatureBitmask256(Partition0 | mask, Partition1, Partition2, Partition3),
            1 => new FeatureBitmask256(Partition0, Partition1 | mask, Partition2, Partition3),
            2 => new FeatureBitmask256(Partition0, Partition1, Partition2 | mask, Partition3),
            3 => new FeatureBitmask256(Partition0, Partition1, Partition2, Partition3 | mask),
            _ => this,
        };
    }

    /// <summary>Tests a single feature.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Contains(SimdFeature feature)
    {
        byte index = (byte)feature;
        nuint partition = (nuint)(index >>> 6);
        ulong mask = 1UL << (index & 63);

        ulong value = partition switch
        {
            0 => Partition0,
            1 => Partition1,
            2 => Partition2,
            3 => Partition3,
            _ => 0UL,
        };

        return (value & mask) != 0UL;
    }

    /// <summary>Tests that every masked feature is present.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool ContainsAll(FeatureBitmask256 mask)
    {
        return ((Partition0 & mask.Partition0) == mask.Partition0) &&
               ((Partition1 & mask.Partition1) == mask.Partition1) &&
               ((Partition2 & mask.Partition2) == mask.Partition2) &&
               ((Partition3 & mask.Partition3) == mask.Partition3);
    }

    /// <summary>Tests that at least one masked feature is present.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool ContainsAny(FeatureBitmask256 mask)
    {
        return (((Partition0 & mask.Partition0) |
                 (Partition1 & mask.Partition1) |
                 (Partition2 & mask.Partition2) |
                 (Partition3 & mask.Partition3)) != 0UL);
    }

    /// <summary>Counts set features.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int PopCount()
    {
        return BitOperations.PopCount(Partition0) +
               BitOperations.PopCount(Partition1) +
               BitOperations.PopCount(Partition2) +
               BitOperations.PopCount(Partition3);
    }

    /// <summary>Whether no feature is set.</summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => (Partition0 | Partition1 | Partition2 | Partition3) == 0UL;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(FeatureBitmask256 other)
    {
        return Partition0 == other.Partition0 &&
               Partition1 == other.Partition1 &&
               Partition2 == other.Partition2 &&
               Partition3 == other.Partition3;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is FeatureBitmask256 other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override int GetHashCode() => HashCode.Combine(Partition0, Partition1, Partition2, Partition3);

    /// <summary>Union of two masks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 BitwiseOr(FeatureBitmask256 left, FeatureBitmask256 right) => left | right;

    /// <summary>Intersection of two masks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 BitwiseAnd(FeatureBitmask256 left, FeatureBitmask256 right) => left & right;

    /// <summary>Symmetric difference of two masks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 Xor(FeatureBitmask256 left, FeatureBitmask256 right) => left ^ right;

    /// <summary>Complement of a mask.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 OnesComplement(FeatureBitmask256 mask) => ~mask;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(FeatureBitmask256 left, FeatureBitmask256 right) => left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(FeatureBitmask256 left, FeatureBitmask256 right) => !left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 operator |(FeatureBitmask256 left, FeatureBitmask256 right) =>
        new(left.Partition0 | right.Partition0,
            left.Partition1 | right.Partition1,
            left.Partition2 | right.Partition2,
            left.Partition3 | right.Partition3);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 operator &(FeatureBitmask256 left, FeatureBitmask256 right) =>
        new(left.Partition0 & right.Partition0,
            left.Partition1 & right.Partition1,
            left.Partition2 & right.Partition2,
            left.Partition3 & right.Partition3);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 operator ^(FeatureBitmask256 left, FeatureBitmask256 right) =>
        new(left.Partition0 ^ right.Partition0,
            left.Partition1 ^ right.Partition1,
            left.Partition2 ^ right.Partition2,
            left.Partition3 ^ right.Partition3);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static FeatureBitmask256 operator ~(FeatureBitmask256 mask) =>
        new(~mask.Partition0, ~mask.Partition1, ~mask.Partition2, ~mask.Partition3);
}

/// <summary>Feature queries over a probed capability set.</summary>
public interface IFeatureQuery
{
    /// <summary>Tests a single feature.</summary>
    bool HasFeature(SimdFeature feature);

    /// <summary>Tests that every masked feature is present.</summary>
    bool Satisfies(FeatureBitmask256 mask);

    /// <summary>Tests a feature list without allocating a mask.</summary>
    bool HasAll(params ReadOnlySpan<SimdFeature> features);
}

/// <summary>Topology description: architecture, ISA level, register width, alignment.</summary>
public interface ITopologyDescriptor
{
    /// <summary>Processor architecture.</summary>
    SimdArchitecture Architecture { get; }

    /// <summary>ISA baseline level.</summary>
    SimdIsaLevel IsaLevel { get; }

    /// <summary>Widest available register.</summary>
    SimdRegisterWidth MaxRegisterWidth { get; }

    /// <summary>Preferred load/store alignment.</summary>
    SimdAlignment PreferredAlignment { get; }
}

/// <summary>Compile-time dispatch policy: required features plus minimum register width.</summary>
public interface ISimdPolicy
{
    /// <summary>Features the tier needs.</summary>
    static abstract FeatureBitmask256 RequiredFeatures { get; }

    /// <summary>Narrowest register the tier tolerates.</summary>
    static abstract SimdRegisterWidth MinimumWidth { get; }

    /// <summary>Stable policy name for diagnostics.</summary>
    static abstract string PolicyIdentifier { get; }
}
