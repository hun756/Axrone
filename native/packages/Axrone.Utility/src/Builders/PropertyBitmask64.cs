namespace Axrone.Utility.Builders;

/// <summary>
/// Allocation-free 64-bit property bitmask for O(1) required-field tracking.
/// Slots are per-builder constants in range 0..63; each domain defines its own slot table
/// plus a required mask.
/// </summary>
public readonly record struct PropertyBitmask64(ulong Value)
{
    /// <summary>Empty mask.</summary>
    public static PropertyBitmask64 None => new(0UL);

    /// <summary>Returns a copy with the bit set.</summary>
    public PropertyBitmask64 SetBit(int bit) => new(Value | (1UL << bit));

    /// <summary>Returns a copy with the bit cleared.</summary>
    public PropertyBitmask64 ClearBit(int bit) => new(Value & ~(1UL << bit));

    /// <summary>Whether the bit is set.</summary>
    public bool HasBit(int bit) => (Value & (1UL << bit)) != 0UL;

    /// <summary>Whether all required bits are present.</summary>
    public bool ContainsAll(PropertyBitmask64 required) => (Value & required.Value) == required.Value;

    /// <summary>Required bits not yet present.</summary>
    public ulong ComputeMissing(PropertyBitmask64 required) => required.Value & ~Value;

    /// <summary>Number of bits set.</summary>
    public int PopCount() => BitOperations.PopCount(Value);

    /// <inheritdoc/>
    public override string ToString() => $"0x{Value:X16}";
}
