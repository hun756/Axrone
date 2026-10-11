namespace Axrone.Geometry;

using System.Globalization;

/// <summary>
/// Child slot of a quad node, ordered to match the split order: west children
/// first, then east, south rows after north rows.
/// </summary>
public enum Quadrant : byte
{
    /// <summary>Upper-left child.</summary>
    NorthWest = 0,

    /// <summary>Upper-right child.</summary>
    NorthEast = 1,

    /// <summary>Lower-left child.</summary>
    SouthWest = 2,

    /// <summary>Lower-right child.</summary>
    SouthEast = 3,
}

/// <summary>Outcome of a nearest-neighbour query.</summary>
public enum NearestResultKind : byte
{
    /// <summary>The tree holds nothing to report.</summary>
    Empty = 0,

    /// <summary>A nearest element was found.</summary>
    Found = 1,
}

/// <summary>
/// Slot index into a quad-tree element pool. The zero slot is live storage
/// (unlike node slots), so the invalid sentinel is the maximum value rather
/// than a negative.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ElementIndex(uint Value) : IComparable<ElementIndex>
{
    /// <summary>Zero slot; live storage once allocated.</summary>
    public static ElementIndex Zero => new(0);

    /// <summary>Invalid sentinel; never a live element slot.</summary>
    public static ElementIndex Invalid => new(uint.MaxValue);

    /// <summary>Whether this index addresses a live element slot.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value != uint.MaxValue;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(ElementIndex other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => IsValid
        ? Value.ToString(CultureInfo.InvariantCulture)
        : "Invalid";

    /// <summary>Determines whether the left index sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(ElementIndex left, ElementIndex right) => left.Value < right.Value;

    /// <summary>Determines whether the left index sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(ElementIndex left, ElementIndex right) => left.Value > right.Value;

    /// <summary>Determines whether the left index sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(ElementIndex left, ElementIndex right) => left.Value <= right.Value;

    /// <summary>Determines whether the left index sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(ElementIndex left, ElementIndex right) => left.Value >= right.Value;
}

/// <summary>One point payload stored in a quad tree: the position plus its value.</summary>
/// <typeparam name="T">Payload type carried alongside the point.</typeparam>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SpatialElement<T>(Vec2 Point, T Value) where T : unmanaged;

/// <summary>Nearest-neighbour answer: the closest point, its value and the squared distance.</summary>
/// <typeparam name="T">Payload type carried alongside the point.</typeparam>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct NearestQueryResult<T> where T : unmanaged
{
    /// <summary>Whether anything was found.</summary>
    public NearestResultKind Kind { get; }

    /// <summary>Closest point; meaningless on an empty answer.</summary>
    public Vec2 Point { get; }

    /// <summary>Payload of the closest point.</summary>
    public T Value { get; }

    /// <summary>Squared distance to the closest point; infinity on an empty answer.</summary>
    public float DistanceSquared { get; }

    /// <summary>Whether a nearest element was found.</summary>
    public bool IsFound
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Kind == NearestResultKind.Found;
    }

    /// <summary>An empty answer: the tree holds nothing to report.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NearestQueryResult<T> None() => new(NearestResultKind.Empty, default, default, float.PositiveInfinity);

    /// <summary>Builds a found answer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NearestQueryResult<T> Success(in Vec2 point, in T value, float distanceSquared) =>
        new(NearestResultKind.Found, point, value, distanceSquared);

    private NearestQueryResult(NearestResultKind kind, in Vec2 point, in T value, float distanceSquared)
    {
        Kind = kind;
        Point = point;
        Value = value;
        DistanceSquared = distanceSquared;
    }
}
