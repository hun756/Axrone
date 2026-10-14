namespace Axrone.Physics2D;

/// <summary>
/// Decides how a 2D spatial tree scores sibling bounds and which child a newly inserted
/// box descends into. The policy is a static-abstract contract rather than a delegate so
/// the hot insertion loop calls straight into the generic instantiation: no closure,
/// no virtual dispatch, and no allocation per split decision.
/// </summary>
public interface ISpatialPartitionStrategy2D
{
    /// <summary>Cost of grouping the two boxes under one parent, used to compare split candidates.</summary>
    /// <param name="left">The first candidate sibling bounds.</param>
    /// <param name="right">The second candidate sibling bounds.</param>
    /// <returns>A relative cost; smaller is cheaper.</returns>
    static abstract float ComputeCost(in Aabb2D left, in Aabb2D right);

    /// <summary>Selects the child a new box descends into, measured by the growth it forces on each side.</summary>
    /// <param name="leftBox">The bounds of the left child.</param>
    /// <param name="rightBox">The bounds of the right child.</param>
    /// <param name="newBox">The box being inserted.</param>
    /// <returns>Zero for the left child, one for the right child.</returns>
    static abstract int ChooseSubtree(in Aabb2D leftBox, in Aabb2D rightBox, in Aabb2D newBox);
}

/// <summary>
/// The surface-area heuristic in 2D: grouping cost is the summed area of the two boxes,
/// and insertion descends into whichever child grows least. Minimising area keeps
/// the traversal touching the fewest empty regions, which is what makes broad-phase
/// queries cheap over many frames of moving bodies.
/// </summary>
public readonly struct SurfaceAreaHeuristicStrategy2D : ISpatialPartitionStrategy2D
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeCost(in Aabb2D left, in Aabb2D right) =>
        left.Area + right.Area;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int ChooseSubtree(in Aabb2D leftBox, in Aabb2D rightBox, in Aabb2D newBox)
    {
        float leftArea = leftBox.Area;
        float rightArea = rightBox.Area;

        float leftMerged = leftBox.MergedWith(in newBox).Area;
        float rightMerged = rightBox.MergedWith(in newBox).Area;

        float leftGrowth = leftMerged - leftArea;
        float rightGrowth = rightMerged - rightArea;

        return leftGrowth < rightGrowth ? 0 : 1;
    }
}

/// <summary>
/// Callback invoked for every candidate a 2D overlap query reaches. The visitor is a
/// generic type parameter, so a query is closed over the callback type rather than a
/// delegate instance, and <typeparamref name="TContext"/> carries the caller's
/// accumulator down the traversal without a closure or a boxing conversion.
/// </summary>
/// <typeparam name="TUserData">Payload type stored alongside each item identity.</typeparam>
/// <typeparam name="TContext">
/// Traversal state; the anti-constraint lets a <see langword="ref"/> struct be passed
/// through, and a class works as well.
/// </typeparam>
public interface ISpatialVisitor2D<TUserData, TContext>
    where TContext : allows ref struct
{
    /// <summary>Reports one overlapping item.</summary>
    /// <param name="itemId">Identity of the overlapping item.</param>
    /// <param name="userData">Payload the item was inserted with.</param>
    /// <param name="box">Fat bounds stored for the item.</param>
    /// <param name="context">Traversal state the visitor may read and write.</param>
    /// <returns><see langword="true"/> to keep traversing; <see langword="false"/> to stop early.</returns>
    bool OnOverlap(SpatialItemId itemId, TUserData userData, in Aabb2D box, ref TContext context);
}

/// <summary>
/// Callback invoked for every candidate a 2D ray query reaches. The visitor owns the
/// distance, so it can tighten the <c>maxDistance</c> argument to a nearer hit and let
/// the traversal prune everything behind it.
/// </summary>
/// <typeparam name="TUserData">Payload type stored alongside each item identity.</typeparam>
/// <typeparam name="TContext">
/// Traversal state; the anti-constraint lets a <see langword="ref"/> struct be passed
/// through, and a class works as well.
/// </typeparam>
public interface IRayHitVisitor2D<TUserData, TContext>
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
    bool OnHit(SpatialItemId itemId, TUserData userData, in Ray2D ray, ref float maxDistance, ref TContext context);
}

/// <summary>
/// Everything a 2D query needs from a spatial tree, and nothing a mutation needs: reads
/// cannot invalidate an identity, a fat bound, or the tree shape, so a visibility pass
/// and a simulation pass can be handed different contracts and stay independent of one
/// another.
/// </summary>
/// <typeparam name="TUserData">Payload type stored alongside each item identity.</typeparam>
public interface ISpatialReader2D<TUserData>
{
    /// <summary>Number of live items in the tree.</summary>
    int Count { get; }

    /// <summary>Number of items the current node layout can hold before it grows.</summary>
    int Capacity { get; }

    /// <summary>Returns the payload an item was inserted with.</summary>
    /// <param name="itemId">The item to read.</param>
    TUserData GetUserData(SpatialItemId itemId);

    /// <summary>Returns the padded bounds stored for an item, already grown by the fattening margin.</summary>
    /// <param name="itemId">The item to read.</param>
    Aabb2D GetFatAabb(SpatialItemId itemId);

    /// <summary>Collects every item whose fat bounds overlap <paramref name="box"/>.</summary>
    /// <param name="box">The query bounds.</param>
    /// <param name="results">Destination span for the identities; too short a span yields a truncated answer.</param>
    /// <returns>Number of identities written to <paramref name="results"/>.</returns>
    int QueryOverlaps(in Aabb2D box, Span<SpatialItemId> results);

    /// <summary>Reports every item whose fat bounds overlap <paramref name="box"/> to a visitor.</summary>
    /// <param name="box">The query bounds.</param>
    /// <param name="visitor">The visitor that receives each overlap.</param>
    /// <param name="context">Traversal state handed to the visitor.</param>
    /// <typeparam name="TVisitor">The visitor type; a struct so the traversal closes over the type.</typeparam>
    /// <typeparam name="TContext">Traversal state type; a <see langword="ref"/> struct may be used.</typeparam>
    /// <returns>Number of overlaps reported.</returns>
    int QueryOverlaps<TVisitor, TContext>(in Aabb2D box, ref TVisitor visitor, ref TContext context)
        where TVisitor : struct, ISpatialVisitor2D<TUserData, TContext>
        where TContext : allows ref struct;

    /// <summary>Collects the hits a ray reaches, ordered by distance.</summary>
    /// <param name="ray">The query ray; its direction is assumed unit length.</param>
    /// <param name="results">Destination span for the hits; too short a span yields a truncated answer.</param>
    /// <param name="maxDistance">Distance along the ray past which hits are ignored.</param>
    /// <returns>Number of hits written to <paramref name="results"/>.</returns>
    int RayCast(in Ray2D ray, Span<RayHit> results, float maxDistance = float.PositiveInfinity);

    /// <summary>Reports the hits a ray reaches to a visitor, which may tighten the search limit as it runs.</summary>
    /// <param name="ray">The query ray; its direction is assumed unit length.</param>
    /// <param name="maxDistance">Distance along the ray past which hits are ignored; the visitor may shrink it.</param>
    /// <param name="visitor">The visitor that receives each hit.</param>
    /// <param name="context">Traversal state handed to the visitor.</param>
    /// <typeparam name="TVisitor">The visitor type; a struct so the traversal closes over the type.</typeparam>
    /// <typeparam name="TContext">Traversal state type; a <see langword="ref"/> struct may be used.</typeparam>
    void RayCast<TVisitor, TContext>(in Ray2D ray, float maxDistance, ref TVisitor visitor, ref TContext context)
        where TVisitor : struct, IRayHitVisitor2D<TUserData, TContext>
        where TContext : allows ref struct;

    /// <summary>Reports each pair of overlapping items once, in stable identity order.</summary>
    /// <param name="visitor">The visitor that receives each pair.</param>
    /// <param name="context">Traversal state handed to the visitor.</param>
    /// <typeparam name="TPairVisitor">The visitor type; a struct so the traversal closes over the type.</typeparam>
    /// <typeparam name="TContext">Traversal state type; a <see langword="ref"/> struct may be used.</typeparam>
    void FindPairs<TPairVisitor, TContext>(ref TPairVisitor visitor, ref TContext context)
        where TPairVisitor : struct, IPairVisitor<TUserData, TContext>
        where TContext : allows ref struct;

    /// <summary>Fills the nearest items to <paramref name="point"/>, ordered by ascending squared distance.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="results">Destination span for the neighbours; too short a span yields the nearest subset.</param>
    /// <param name="maxDistance">Squared distance beyond which neighbours are ignored.</param>
    /// <returns>Number of neighbours written to <paramref name="results"/>.</returns>
    int QueryKNearest(Vec2 point, Span<KnnResult<TUserData>> results, float maxDistance = float.PositiveInfinity);
}

/// <summary>
/// The mutating half of the 2D tree, kept apart from reads so a consumer that only queries
/// can never trigger a structural change. Every member hands back the identity or the
/// verdict it produced, so the writer never needs to keep state on the caller's behalf.
/// </summary>
/// <typeparam name="TUserData">Payload type stored alongside each item identity.</typeparam>
public interface ISpatialWriter2D<TUserData>
{
    /// <summary>Adds an item and returns its identity.</summary>
    /// <param name="box">World bounds of the item; they are grown by the fattening margin.</param>
    /// <param name="userData">Payload to carry alongside the identity.</param>
    SpatialItemId Insert(in Aabb2D box, TUserData userData);

    /// <summary>Replaces the bounds of an item, budgeting for its travel so the margin absorbs the motion.</summary>
    /// <param name="itemId">The item to move.</param>
    /// <param name="box">New world bounds.</param>
    /// <param name="displacement">Motion since the last update; it feeds the fattening budget.</param>
    /// <returns><see langword="true"/> when the item left its fat box and was re-inserted.</returns>
    bool Move(SpatialItemId itemId, in Aabb2D box, in Vec2 displacement);

    /// <summary>Replaces the bounds of an item without a motion budget.</summary>
    /// <param name="itemId">The item to update.</param>
    /// <param name="box">New world bounds.</param>
    void Update(SpatialItemId itemId, in Aabb2D box);

    /// <summary>Drops an item and frees its slot for reuse.</summary>
    /// <param name="itemId">The item to remove.</param>
    void Remove(SpatialItemId itemId);
}
