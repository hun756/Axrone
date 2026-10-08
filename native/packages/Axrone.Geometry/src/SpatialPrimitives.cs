namespace Axrone.Geometry;

using System.Globalization;

/// <summary>Relationship between a query volume and a spatial item.</summary>
public enum ContainmentType : byte
{
    /// <summary>No point of the item lies inside the volume.</summary>
    Disjoint = 0,

    /// <summary>The volume lies wholly inside the item.</summary>
    Contains = 1,

    /// <summary>The shapes share at least one point.</summary>
    Intersects = 2,
}

/// <summary>Coordinate axis selector, ordered to match spatial-tree child slots.</summary>
public enum Axis : byte
{
    /// <summary>The X axis.</summary>
    X = 0,

    /// <summary>The Y axis.</summary>
    Y = 1,

    /// <summary>The Z axis.</summary>
    Z = 2,
}

/// <summary>Outcome of a ray or segment query against a single candidate.</summary>
public enum IntersectionKind : byte
{
    /// <summary>The ray misses the candidate.</summary>
    None = 0,

    /// <summary>The ray hits the candidate.</summary>
    Hit = 1,
}

/// <summary>Outcome of a swept-volume query against a single candidate.</summary>
public enum SweepKind : byte
{
    /// <summary>The swept volume misses the candidate.</summary>
    Miss = 0,

    /// <summary>The swept volume hits the candidate.</summary>
    Hit = 1,
}

/// <summary>
/// 4-byte identity of a spatial item registered in a tree. The zero value is the
/// invalid sentinel, so a default-initialized slot can never name a live item.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SpatialItemId(uint Value) : IComparable<SpatialItemId>
{
    /// <summary>Invalid sentinel; never a registered item.</summary>
    public static SpatialItemId Invalid => default;

    /// <summary>Whether this identity names a registered item.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value != 0;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(SpatialItemId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => IsValid ? string.Create(CultureInfo.InvariantCulture, $"Item({Value})") : "Item(Invalid)";

    /// <summary>Unwraps the underlying identifier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(SpatialItemId id) => id.Value;

    /// <summary>Wraps a raw identifier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator SpatialItemId(uint value) => new(value);

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SpatialItemId left, SpatialItemId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SpatialItemId left, SpatialItemId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SpatialItemId left, SpatialItemId right) => left.Value <= right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SpatialItemId left, SpatialItemId right) => left.Value >= right.Value;
}

/// <summary>
/// 4-byte slot index into a spatial-tree node pool. Negative values are null slots and
/// never address a live node.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct NodeIndex(int Value) : IComparable<NodeIndex>
{
    /// <summary>Null sentinel; never a live node slot.</summary>
    public static NodeIndex Null => new(-1);

    /// <summary>Whether this index addresses no node.</summary>
    public bool IsNull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value < 0;
    }

    /// <summary>Whether this index addresses a live node slot.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value >= 0;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(NodeIndex other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => IsValid ? string.Create(CultureInfo.InvariantCulture, $"Node({Value})") : "Node(Null)";

    /// <summary>Unwraps the underlying slot index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(NodeIndex index) => index.Value;

    /// <summary>Wraps a raw slot index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator NodeIndex(int value) => new(value);

    /// <summary>Determines whether the left index sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(NodeIndex left, NodeIndex right) => left.Value < right.Value;

    /// <summary>Determines whether the left index sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(NodeIndex left, NodeIndex right) => left.Value > right.Value;

    /// <summary>Determines whether the left index sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(NodeIndex left, NodeIndex right) => left.Value <= right.Value;

    /// <summary>Determines whether the left index sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(NodeIndex left, NodeIndex right) => left.Value >= right.Value;
}

/// <summary>
/// Node capacity of a spatial tree, floored at <see cref="MinimumCapacity"/> so the
/// root fan-out and split thresholds always have room to work. The guard lives in the
/// constructor, so an out-of-range capacity cannot enter the engine.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct TreeCapacity : IComparable<TreeCapacity>
{
    /// <summary>Smallest capacity a spatial tree accepts.</summary>
    public const int MinimumCapacity = 16;

    /// <summary>Creates a capacity, rejecting anything below <see cref="MinimumCapacity"/>.</summary>
    public TreeCapacity(int value)
    {
        if (value < MinimumCapacity)
        {
            ThrowHelper.ThrowInvalidInitialCapacity();
        }

        Value = value;
    }

    /// <summary>Node capacity.</summary>
    public int Value { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(TreeCapacity other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"TreeCapacity({Value})");

    /// <summary>Unwraps the underlying capacity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(TreeCapacity capacity) => capacity.Value;

    /// <summary>Wraps a raw capacity, applying the minimum-capacity guard.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator TreeCapacity(int value) => new(value);

    /// <summary>Determines whether the left capacity is below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(TreeCapacity left, TreeCapacity right) => left.Value < right.Value;

    /// <summary>Determines whether the left capacity exceeds the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(TreeCapacity left, TreeCapacity right) => left.Value > right.Value;

    /// <summary>Determines whether the left capacity is at or below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(TreeCapacity left, TreeCapacity right) => left.Value <= right.Value;

    /// <summary>Determines whether the left capacity is at or above the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(TreeCapacity left, TreeCapacity right) => left.Value >= right.Value;
}

/// <summary>
/// Squared distance between two points. Keeping the squared form avoids a square root
/// on every reject, which is the dominant cost of a traversal.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct DistanceSquared(float Value) : IComparable<DistanceSquared>
{
    /// <summary>An unreachable distance; used as a "no candidate" result.</summary>
    public static DistanceSquared PositiveInfinity => new(float.PositiveInfinity);

    /// <summary>Coincident points.</summary>
    public static DistanceSquared Zero => new(0F);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(DistanceSquared other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"DistanceSquared({Value})");

    /// <summary>Unwraps the underlying squared distance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float(DistanceSquared distance) => distance.Value;

    /// <summary>Wraps a raw squared distance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator DistanceSquared(float value) => new(value);

    /// <summary>Determines whether the left distance is below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(DistanceSquared left, DistanceSquared right) => left.Value < right.Value;

    /// <summary>Determines whether the left distance exceeds the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(DistanceSquared left, DistanceSquared right) => left.Value > right.Value;

    /// <summary>Determines whether the left distance is at or below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(DistanceSquared left, DistanceSquared right) => left.Value <= right.Value;

    /// <summary>Determines whether the left distance is at or above the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(DistanceSquared left, DistanceSquared right) => left.Value >= right.Value;
}