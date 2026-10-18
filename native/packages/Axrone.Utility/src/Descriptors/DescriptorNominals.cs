namespace Axrone.Utility.Descriptors;

/// <summary>
/// Strongly-typed slot index into a descriptor table.
/// Zero-based; <see cref="Invalid"/> is the free-list sentinel.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SlotIndex(uint Value) :
    IComparable<SlotIndex>,
    IComparisonOperators<SlotIndex, SlotIndex, bool>
{
    /// <summary>Free-list sentinel.</summary>
    public static SlotIndex Invalid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(uint.MaxValue);
    }

    /// <summary>Whether the index addresses a live slot.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value != uint.MaxValue;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SlotIndex other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left index sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SlotIndex left, SlotIndex right) => left.Value < right.Value;

    /// <summary>Determines whether the left index sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SlotIndex left, SlotIndex right) => left.Value <= right.Value;

    /// <summary>Determines whether the left index sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SlotIndex left, SlotIndex right) => left.Value > right.Value;

    /// <summary>Determines whether the left index sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SlotIndex left, SlotIndex right) => left.Value >= right.Value;

    /// <summary>Unwraps the index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator uint(SlotIndex index) => index.Value;

    /// <summary>Wraps an index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator SlotIndex(uint value) => new(value);

    /// <inheritdoc/>
    public override string ToString() => Value == uint.MaxValue
        ? "SlotIndex[Invalid]"
        : string.Create(CultureInfo.InvariantCulture, $"SlotIndex[{Value}]");
}

/// <summary>
/// Generational fence against ABA races on slot reuse.
/// Zero is the invalid sentinel; live generations start at one.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Generation(uint Value) :
    IComparable<Generation>,
    IComparisonOperators<Generation, Generation, bool>
{
    /// <summary>Invalid sentinel.</summary>
    public static Generation Zero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(0U);
    }

    /// <summary>First live generation.</summary>
    public static Generation Initial
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(1U);
    }

    /// <summary>Next generation, skipping zero on wrap.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Generation Next()
    {
        uint next = Value + 1U;
        return next == 0U ? new(1U) : new(next);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Generation other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left generation sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Generation left, Generation right) => left.Value < right.Value;

    /// <summary>Determines whether the left generation sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Generation left, Generation right) => left.Value <= right.Value;

    /// <summary>Determines whether the left generation sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Generation left, Generation right) => left.Value > right.Value;

    /// <summary>Determines whether the left generation sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Generation left, Generation right) => left.Value >= right.Value;

    /// <summary>Unwraps the generation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator uint(Generation generation) => generation.Value;

    /// <summary>Wraps a generation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Generation(uint value) => new(value);

    /// <inheritdoc/>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"Gen[{Value}]");
}

/// <summary>
/// Opaque 64-bit descriptor flag mask.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct DescriptorFlags(ulong Value) :
    IComparable<DescriptorFlags>,
    IBitwiseOperators<DescriptorFlags, DescriptorFlags, DescriptorFlags>
{
    /// <summary>No flags.</summary>
    public static DescriptorFlags None
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(0UL);
    }

    /// <summary>Whether all bits of <paramref name="flag"/> are set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasFlag(DescriptorFlags flag) => (Value & flag.Value) == flag.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(DescriptorFlags other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DescriptorFlags operator &(DescriptorFlags left, DescriptorFlags right) => new(left.Value & right.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DescriptorFlags operator |(DescriptorFlags left, DescriptorFlags right) => new(left.Value | right.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DescriptorFlags operator ^(DescriptorFlags left, DescriptorFlags right) => new(left.Value ^ right.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DescriptorFlags operator ~(DescriptorFlags value) => new(~value.Value);

    /// <summary>Unwraps the mask.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ulong(DescriptorFlags flags) => flags.Value;

    /// <summary>Wraps a mask.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator DescriptorFlags(ulong value) => new(value);

    /// <inheritdoc/>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"Flags[0x{Value:X16}]");
}

/// <summary>
/// Descriptor table sizing. Capacity must be a nonzero power of two.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct TableCapacity : IComparable<TableCapacity>
{
    /// <summary>Slot capacity.</summary>
    public uint Value
    {
        get;
        init
        {
            if (value == 0 || !BitOperations.IsPow2(value))
            {
                ThrowHelper.ThrowInvalidCapacity((int)value);
            }

            field = value;
        }
    }

    /// <summary>Index mask for power-of-two capacities.</summary>
    public uint Mask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value - 1U;
    }

    /// <summary>Creates a capacity; rejects non-powers-of-two.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TableCapacity(uint value)
    {
        if (value == 0 || !BitOperations.IsPow2(value))
        {
            ThrowHelper.ThrowInvalidCapacity((int)value);
        }

        Value = value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(TableCapacity other) => Value.CompareTo(other.Value);

    /// <summary>Wraps a capacity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator TableCapacity(uint value) => new(value);

    /// <summary>Unwraps the capacity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(TableCapacity capacity) => capacity.Value;

    /// <inheritdoc/>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"Capacity[{Value}]");
}
