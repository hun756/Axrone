namespace Axrone.Geometry;

using System.Globalization;

/// <summary>
/// Slot index into an octree item pool. The zero slot is live storage, so the
/// invalid sentinel is the maximum value rather than a negative.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SlotIndex(uint Value) : IComparable<SlotIndex>
{
    /// <summary>Invalid sentinel; never a live item slot.</summary>
    public static SlotIndex Invalid => new(uint.MaxValue);

    /// <summary>Whether this index addresses a live item slot.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value != uint.MaxValue;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(SlotIndex other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Determines whether the left index sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SlotIndex left, SlotIndex right) => left.Value < right.Value;

    /// <summary>Determines whether the left index sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SlotIndex left, SlotIndex right) => left.Value > right.Value;

    /// <summary>Determines whether the left index sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SlotIndex left, SlotIndex right) => left.Value <= right.Value;

    /// <summary>Determines whether the left index sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SlotIndex left, SlotIndex right) => left.Value >= right.Value;
}

/// <summary>
/// Allocation epoch of an octree item slot, bumped every time the slot is
/// recycled. A handle pairs a slot with the epoch observed at insert, so a
/// handle held across a remove/realloc cycle no longer matches.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Generation(uint Value) : IComparable<Generation>
{
    /// <summary>Unallocated epoch; never observed on a live slot.</summary>
    public static Generation Zero => new(0);

    /// <summary>First epoch assigned to a freshly allocated slot.</summary>
    public static Generation Initial => new(1);

    /// <summary>Advances to the next epoch, wrapping past the maximum back to one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Generation Next() => new(Value == uint.MaxValue ? 1u : Value + 1u);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(Generation other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Determines whether the left epoch sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Generation left, Generation right) => left.Value < right.Value;

    /// <summary>Determines whether the left epoch sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Generation left, Generation right) => left.Value > right.Value;

    /// <summary>Determines whether the left epoch sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Generation left, Generation right) => left.Value <= right.Value;

    /// <summary>Determines whether the left epoch sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Generation left, Generation right) => left.Value >= right.Value;
}

/// <summary>Split depth of an octree node; zero at the root.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct TreeDepth(byte Value) : IComparable<TreeDepth>
{
    /// <summary>Root depth.</summary>
    public static TreeDepth Root => new(0);

    /// <summary>Returns the next deeper level.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TreeDepth Increment() => new((byte)(Value + 1));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(TreeDepth other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Determines whether the left depth is shallower than the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(TreeDepth left, TreeDepth right) => left.Value < right.Value;

    /// <summary>Determines whether the left depth is deeper than the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(TreeDepth left, TreeDepth right) => left.Value > right.Value;

    /// <summary>Determines whether the left depth is at or above the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(TreeDepth left, TreeDepth right) => left.Value <= right.Value;

    /// <summary>Determines whether the left depth is at or below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(TreeDepth left, TreeDepth right) => left.Value >= right.Value;
}

/// <summary>
/// Node pool slots requested at construction. Zero means "use the default";
/// the tree rounds the grant up to a multiple of the eight-slot child block.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct NodeCapacity(uint Value) : IComparable<NodeCapacity>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(NodeCapacity other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Determines whether the left capacity is below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(NodeCapacity left, NodeCapacity right) => left.Value < right.Value;

    /// <summary>Determines whether the left capacity exceeds the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(NodeCapacity left, NodeCapacity right) => left.Value > right.Value;

    /// <summary>Determines whether the left capacity is at or below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(NodeCapacity left, NodeCapacity right) => left.Value <= right.Value;

    /// <summary>Determines whether the left capacity is at or above the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(NodeCapacity left, NodeCapacity right) => left.Value >= right.Value;
}

/// <summary>
/// Item pool slots requested at construction. Zero means "use the default".
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ItemCapacity(uint Value) : IComparable<ItemCapacity>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(ItemCapacity other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Determines whether the left capacity is below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(ItemCapacity left, ItemCapacity right) => left.Value < right.Value;

    /// <summary>Determines whether the left capacity exceeds the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(ItemCapacity left, ItemCapacity right) => left.Value > right.Value;

    /// <summary>Determines whether the left capacity is at or below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(ItemCapacity left, ItemCapacity right) => left.Value <= right.Value;

    /// <summary>Determines whether the left capacity is at or above the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(ItemCapacity left, ItemCapacity right) => left.Value >= right.Value;
}
