namespace Axrone.Geometry;

/// <summary>
/// Decides how a spatial tree scores sibling bounds and which child a newly inserted
/// box descends into. The policy is a static-abstract contract rather than a delegate so
/// the hot insertion loop calls straight into the generic instantiation: no closure,
/// no virtual dispatch, and no allocation per split decision.
/// </summary>
public interface ISpatialPartitionStrategy
{
    /// <summary>Cost of grouping the two boxes under one parent, used to compare split candidates.</summary>
    /// <param name="left">The first candidate sibling bounds.</param>
    /// <param name="right">The second candidate sibling bounds.</param>
    /// <returns>A relative cost; smaller is cheaper.</returns>
    static abstract float ComputeCost(in Aabb3D left, in Aabb3D right);

    /// <summary>Selects the child a new box descends into, measured by the growth it forces on each side.</summary>
    /// <param name="leftBox">The bounds of the left child.</param>
    /// <param name="rightBox">The bounds of the right child.</param>
    /// <param name="newBox">The box being inserted.</param>
    /// <returns>Zero for the left child, one for the right child.</returns>
    static abstract int ChooseSubtree(in Aabb3D leftBox, in Aabb3D rightBox, in Aabb3D newBox);
}

/// <summary>
/// The surface-area heuristic: grouping cost is the summed surface area of the two boxes,
/// and insertion descends into whichever child grows least. Minimising surface area keeps
/// the traversal touching the fewest empty volumes, which is what makes broad-phase
/// queries cheap over many frames of moving bodies.
/// </summary>
public readonly struct SurfaceAreaHeuristicStrategy : ISpatialPartitionStrategy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeCost(in Aabb3D left, in Aabb3D right) =>
        left.SurfaceArea() + right.SurfaceArea();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int ChooseSubtree(in Aabb3D leftBox, in Aabb3D rightBox, in Aabb3D newBox)
    {
        float leftArea = leftBox.SurfaceArea();
        float rightArea = rightBox.SurfaceArea();

        float leftMerged = Aabb3D.CreateMerged(in leftBox, in newBox).SurfaceArea();
        float rightMerged = Aabb3D.CreateMerged(in rightBox, in newBox).SurfaceArea();

        float leftGrowth = leftMerged - leftArea;
        float rightGrowth = rightMerged - rightArea;

        return leftGrowth < rightGrowth ? 0 : 1;
    }
}

/// <summary>
/// Records what a traversal did: nodes walked, bounds tests run, and mutations applied.
/// The sink is a static-abstract contract so a tree can be instrumented without a
/// delegate, an interface instance, or a counter object per node visit.
/// </summary>
public interface ISpatialMetricsSink
{
    /// <summary>Records that one node was entered.</summary>
    static abstract void OnNodeVisited();

    /// <summary>Records one bounds intersection test and its outcome.</summary>
    /// <param name="hit">Whether the tested bounds were reached.</param>
    static abstract void OnIntersectionTested(bool hit);

    /// <summary>Records that one item was added to the tree.</summary>
    static abstract void OnItemInserted();

    /// <summary>Records that one item was removed from the tree.</summary>
    static abstract void OnItemRemoved();
}

/// <summary>
/// Discards every counter, which is what production builds want: the queries stay
/// allocation-free and branch-free, and the reporting pass swaps this for a real sink.
/// </summary>
public readonly struct NullSpatialMetricsSink : ISpatialMetricsSink
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnNodeVisited()
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnIntersectionTested(bool hit)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnItemInserted()
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnItemRemoved()
    {
    }
}

/// <summary>
/// Callback invoked for every candidate an overlap query reaches. The visitor is a
/// generic type parameter, so a query is closed over the callback type rather than a
/// delegate instance, and <typeparamref name="TContext"/> carries the caller's
/// accumulator down the traversal without a closure or a boxing conversion.
/// </summary>
/// <typeparam name="TUserData">Payload type stored alongside each item identity.</typeparam>
/// <typeparam name="TContext">
/// Traversal state; the anti-constraint lets a <see langword="ref"/> struct be passed
/// through, and a class works as well.
/// </typeparam>
public interface ISpatialVisitor<TUserData, TContext>
    where TContext : allows ref struct
{
    /// <summary>Reports one overlapping item.</summary>
    /// <param name="itemId">Identity of the overlapping item.</param>
    /// <param name="userData">Payload the item was inserted with.</param>
    /// <param name="box">Fat bounds stored for the item.</param>
    /// <param name="context">Traversal state the visitor may read and write.</param>
    /// <returns><see langword="true"/> to keep traversing; <see langword="false"/> to stop early.</returns>
    bool OnOverlap(SpatialItemId itemId, TUserData userData, in Aabb3D box, ref TContext context);
}

/// <summary>
/// Callback invoked for every candidate a ray query reaches. The visitor owns the
/// distance, so it can tighten the <c>maxDistance</c> argument to a nearer hit and let
/// the traversal prune everything behind it.
/// </summary>
/// <typeparam name="TUserData">Payload type stored alongside each item identity.</typeparam>
/// <typeparam name="TContext">
/// Traversal state; the anti-constraint lets a <see langword="ref"/> struct be passed
/// through, and a class works as well.
/// </typeparam>
public interface IRayHitVisitor<TUserData, TContext>
    where TContext : allows ref struct
{
    /// <summary>Reports one item the ray reached.</summary>
    /// <param name="itemId">Identity of the reached item.</param>
    /// <param name="userData">Payload the item was inserted with.</param>
    /// <param name="ray">The query ray, in the space the tree was built in.</param>
    /// <param name="maxDistance">
    /// Current search limit; a nearer hit tightens it so later candidates are pruned.
    /// </param>
    /// <param name="context">Traversal state the visitor may read and write.</param>
    /// <returns><see langword="true"/> to keep traversing; <see langword="false"/> to stop early.</returns>
    bool OnHit(SpatialItemId itemId, TUserData userData, in Ray3D ray, ref float maxDistance, ref TContext context);
}

/// <summary>
/// Callback invoked for every item whose bounds fall inside a view volume. Returning
/// <see langword="false"/> stops the walk, so a visibility pass can bail out as soon as
/// its draw budget is spent.
/// </summary>
/// <typeparam name="TUserData">Payload type stored alongside each item identity.</typeparam>
/// <typeparam name="TContext">
/// Traversal state; the anti-constraint lets a <see langword="ref"/> struct be passed
/// through, and a class works as well.
/// </typeparam>
public interface IFrustumVisitor<TUserData, TContext>
    where TContext : allows ref struct
{
    /// <summary>Reports one visible item.</summary>
    /// <param name="itemId">Identity of the visible item.</param>
    /// <param name="userData">Payload the item was inserted with.</param>
    /// <param name="box">Fat bounds stored for the item.</param>
    /// <param name="context">Traversal state the visitor may read and write.</param>
    /// <returns><see langword="true"/> to keep traversing; <see langword="false"/> to stop early.</returns>
    bool OnVisible(SpatialItemId itemId, TUserData userData, in Aabb3D box, ref TContext context);
}

/// <summary>
/// Callback invoked once per overlapping pair a self-query discovers. Each pair is
/// reported once, with the stable "lower identity first" ordering, so a broad-phase
/// contact pass consumes it without de-duplicating.
/// </summary>
/// <typeparam name="TUserData">Payload type stored alongside each item identity.</typeparam>
/// <typeparam name="TContext">
/// Traversal state; the anti-constraint lets a <see langword="ref"/> struct be passed
/// through, and a class works as well.
/// </typeparam>
public interface IPairVisitor<TUserData, TContext>
    where TContext : allows ref struct
{
    /// <summary>Reports one overlapping pair.</summary>
    /// <param name="idA">Identity of the first item of the pair.</param>
    /// <param name="dataA">Payload of the first item.</param>
    /// <param name="idB">Identity of the second item of the pair.</param>
    /// <param name="dataB">Payload of the second item.</param>
    /// <param name="context">Traversal state the visitor may read and write.</param>
    /// <returns><see langword="true"/> to keep traversing; <see langword="false"/> to stop early.</returns>
    bool OnPair(SpatialItemId idA, TUserData dataA, SpatialItemId idB, TUserData dataB, ref TContext context);
}
