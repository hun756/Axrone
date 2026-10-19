namespace Axrone.Utility.Slots;

/// <summary>
/// Strongly-typed slot index into a pooled table. Zero is live storage, so
/// the invalid sentinel is the maximum value rather than a negative.
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
