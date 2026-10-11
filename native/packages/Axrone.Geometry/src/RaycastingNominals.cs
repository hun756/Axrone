namespace Axrone.Geometry;

using System.Globalization;

/// <summary>
/// Identity of one raycastable primitive inside a BVH. Negative values are the
/// none sentinel and never address a live primitive.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct PrimitiveId(int Value) : IComparable<PrimitiveId>
{
    /// <summary>No primitive; the miss answer of a primitive query.</summary>
    public static PrimitiveId None => new(-1);

    /// <summary>Smallest representable identity.</summary>
    public static PrimitiveId MinValue => new(int.MinValue);

    /// <summary>Largest representable identity.</summary>
    public static PrimitiveId MaxValue => new(int.MaxValue);

    /// <summary>Whether this identity addresses a live primitive.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value >= 0;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(PrimitiveId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(PrimitiveId left, PrimitiveId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(PrimitiveId left, PrimitiveId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(PrimitiveId left, PrimitiveId right) => left.Value <= right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(PrimitiveId left, PrimitiveId right) => left.Value >= right.Value;
}

/// <summary>
/// Number of primitives grouped under a BVH node. Zero marks an internal node;
/// a positive value marks a leaf and counts its primitive range.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct PrimitiveCount(int Value) : IComparable<PrimitiveCount>
{
    /// <summary>No primitives; an internal node.</summary>
    public static PrimitiveCount Zero => new(0);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(PrimitiveCount other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Determines whether the left count is below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(PrimitiveCount left, PrimitiveCount right) => left.Value < right.Value;

    /// <summary>Determines whether the left count exceeds the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(PrimitiveCount left, PrimitiveCount right) => left.Value > right.Value;

    /// <summary>Determines whether the left count is at or below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(PrimitiveCount left, PrimitiveCount right) => left.Value <= right.Value;

    /// <summary>Determines whether the left count is at or above the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(PrimitiveCount left, PrimitiveCount right) => left.Value >= right.Value;

    /// <summary>Adds a raw delta to a count.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PrimitiveCount operator +(PrimitiveCount left, int count) => new(left.Value + count);
}
