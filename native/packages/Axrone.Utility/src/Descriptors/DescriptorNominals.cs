namespace Axrone.Utility.Descriptors;

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
