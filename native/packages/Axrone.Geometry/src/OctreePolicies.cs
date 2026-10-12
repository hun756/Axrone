namespace Axrone.Geometry;

/// <summary>
/// Decides when an octree node splits and when an emptied branch collapses.
/// The policy is a static-abstract contract so the hot paths call straight
/// into the generic instantiation: no closure, no virtual dispatch.
/// </summary>
/// <typeparam name="TSelf">The policy type itself.</typeparam>
public interface IPartitionPolicy<TSelf>
    where TSelf : struct, IPartitionPolicy<TSelf>
{
    /// <summary>Items a leaf holds before it becomes a split candidate.</summary>
    static abstract ushort MaxItemsPerNode { get; }

    /// <summary>Deepest level a split may produce.</summary>
    static abstract TreeDepth MaxDepth { get; }

    /// <summary>Whether a node with this fill at this depth splits.</summary>
    static abstract bool ShouldSplit(ushort currentItemCount, TreeDepth currentDepth);

    /// <summary>Whether a branch with this many live items collapses.</summary>
    static abstract bool ShouldPrune(ushort activeItemsInSubtree, TreeDepth currentDepth);
}

/// <summary>Default split policy: sixteen items per leaf, twelve levels deep, collapse on empty.</summary>
public readonly struct DefaultPartitionPolicy : IPartitionPolicy<DefaultPartitionPolicy>
{
    /// <inheritdoc/>
    public static ushort MaxItemsPerNode => 16;

    /// <inheritdoc/>
    public static TreeDepth MaxDepth => new(12);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldSplit(ushort currentItemCount, TreeDepth currentDepth) =>
        currentItemCount >= MaxItemsPerNode && currentDepth < MaxDepth;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldPrune(ushort activeItemsInSubtree, TreeDepth currentDepth) =>
        activeItemsInSubtree == 0;
}

/// <summary>
/// Records what an octree did: splits, merges, inserts, removes and pool
/// growth. Static-abstract so the tree stays instrumented without a delegate
/// or an instance per event.
/// </summary>
/// <typeparam name="TSelf">The sink type itself.</typeparam>
public interface IOctreeMetrics<TSelf>
    where TSelf : struct, IOctreeMetrics<TSelf>
{
    /// <summary>Records that a node split at this depth.</summary>
    static abstract void OnNodeSplit(TreeDepth depth);

    /// <summary>Records that a branch merged at this depth.</summary>
    static abstract void OnNodeMerged(TreeDepth depth);

    /// <summary>Records an insert and the live count after it.</summary>
    static abstract void OnItemInserted(uint activeCount);

    /// <summary>Records a remove and the live count after it.</summary>
    static abstract void OnItemRemoved(uint activeCount);

    /// <summary>Records a pool growth and the bytes committed.</summary>
    static abstract void OnMemoryResized(nuint bytesAllocated);
}

/// <summary>Discards every counter, which is what production builds want.</summary>
public readonly struct NullOctreeMetrics : IOctreeMetrics<NullOctreeMetrics>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnNodeSplit(TreeDepth depth)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnNodeMerged(TreeDepth depth)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnItemInserted(uint activeCount)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnItemRemoved(uint activeCount)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnMemoryResized(nuint bytesAllocated)
    {
    }
}
