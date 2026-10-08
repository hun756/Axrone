namespace Axrone.Geometry;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Everything a query needs from a spatial tree, and nothing a mutation needs: reads
/// cannot invalidate an identity, a fat bound, or the tree shape, so a visibility pass
/// and a simulation pass can be handed different contracts and stay independent of one
/// another. Callers that only query stay decoupled from <see cref="ISpatialWriter{TUserData}"/>
/// and <see cref="ISpatialLifecycle"/>.
/// </summary>
/// <typeparam name="TUserData">Payload type stored alongside each item identity.</typeparam>
public interface ISpatialReader<TUserData>
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
    Aabb3D GetFatAabb(SpatialItemId itemId);

    /// <summary>Collects every item whose fat bounds overlap <paramref name="box"/>.</summary>
    /// <param name="box">The query bounds.</param>
    /// <param name="results">Destination span for the identities; too short a span yields a truncated answer.</param>
    /// <returns>Number of identities written to <paramref name="results"/>.</returns>
    int QueryOverlaps(in Aabb3D box, Span<SpatialItemId> results);

    /// <summary>Reports every item whose fat bounds overlap <paramref name="box"/> to a visitor.</summary>
    /// <param name="box">The query bounds.</param>
    /// <param name="visitor">The visitor that receives each overlap.</param>
    /// <param name="context">Traversal state handed to the visitor.</param>
    /// <typeparam name="TVisitor">The visitor type; a struct so the traversal closes over the type.</typeparam>
    /// <typeparam name="TContext">Traversal state type; a <see langword="ref"/> struct may be used.</typeparam>
    /// <returns>Number of overlaps reported.</returns>
    int QueryOverlaps<TVisitor, TContext>(in Aabb3D box, ref TVisitor visitor, ref TContext context)
        where TVisitor : struct, ISpatialVisitor<TUserData, TContext>
        where TContext : allows ref struct;

    /// <summary>Collects the hits a ray reaches, ordered by distance.</summary>
    /// <param name="ray">The query ray; its direction is assumed unit length.</param>
    /// <param name="results">Destination span for the hits; too short a span yields a truncated answer.</param>
    /// <param name="maxDistance">Distance along the ray past which hits are ignored.</param>
    /// <returns>Number of hits written to <paramref name="results"/>.</returns>
    int RayCast(in Ray3D ray, Span<RayHit> results, float maxDistance = float.PositiveInfinity);

    /// <summary>Reports the hits a ray reaches to a visitor, which may tighten the search limit as it runs.</summary>
    /// <param name="ray">The query ray; its direction is assumed unit length.</param>
    /// <param name="maxDistance">Distance along the ray past which hits are ignored; the visitor may shrink it.</param>
    /// <param name="visitor">The visitor that receives each hit.</param>
    /// <param name="context">Traversal state handed to the visitor.</param>
    /// <typeparam name="TVisitor">The visitor type; a struct so the traversal closes over the type.</typeparam>
    /// <typeparam name="TContext">Traversal state type; a <see langword="ref"/> struct may be used.</typeparam>
    void RayCast<TVisitor, TContext>(in Ray3D ray, float maxDistance, ref TVisitor visitor, ref TContext context)
        where TVisitor : struct, IRayHitVisitor<TUserData, TContext>
        where TContext : allows ref struct;

    /// <summary>Reports every item inside the view volume to a visitor.</summary>
    /// <param name="frustum">The view volume to cull against.</param>
    /// <param name="visitor">The visitor that receives each visible item.</param>
    /// <param name="context">Traversal state handed to the visitor.</param>
    /// <typeparam name="TVisitor">The visitor type; a struct so the traversal closes over the type.</typeparam>
    /// <typeparam name="TContext">Traversal state type; a <see langword="ref"/> struct may be used.</typeparam>
    void FrustumCull<TVisitor, TContext>(in Frustum3 frustum, ref TVisitor visitor, ref TContext context)
        where TVisitor : struct, IFrustumVisitor<TUserData, TContext>
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
    int QueryKNearest(Vec3 point, Span<KnnResult<TUserData>> results, float maxDistance = float.PositiveInfinity);
}

/// <summary>
/// The mutating half of the tree, kept apart from reads so a consumer that only queries
/// can never trigger a structural change. Every member hands back the identity or the
/// verdict it produced, so the writer never needs to keep state on the caller's behalf.
/// </summary>
/// <typeparam name="TUserData">Payload type stored alongside each item identity.</typeparam>
public interface ISpatialWriter<TUserData>
{
    /// <summary>Adds an item and returns its identity.</summary>
    /// <param name="box">World bounds of the item; they are grown by the fattening margin.</param>
    /// <param name="userData">Payload to carry alongside the identity.</param>
    SpatialItemId Insert(in Aabb3D box, TUserData userData);

    /// <summary>Replaces the bounds of an item, budgeting for its travel so the margin absorbs the motion.</summary>
    /// <param name="itemId">The item to move.</param>
    /// <param name="box">New world bounds.</param>
    /// <param name="displacement">Motion since the last update; it feeds the fattening budget.</param>
    /// <returns><see langword="true"/> when the item left its fat box and was re-inserted.</returns>
    bool Move(SpatialItemId itemId, in Aabb3D box, in Vec3 displacement);

    /// <summary>Replaces the bounds of an item without a motion budget.</summary>
    /// <param name="itemId">The item to update.</param>
    /// <param name="box">New world bounds.</param>
    void Update(SpatialItemId itemId, in Aabb3D box);

    /// <summary>Drops an item and frees its slot for reuse.</summary>
    /// <param name="itemId">The item to remove.</param>
    void Remove(SpatialItemId itemId);
}

/// <summary>
/// Bulk-drain lifecycle for a tree that updates on a worker thread: <see cref="Complete"/>
/// closes the submission stream and <see cref="DrainAsync"/> awaits the queued work, so a
/// caller never has to expose its own synchronization primitives.
/// </summary>
public interface ISpatialLifecycle
{
    /// <summary>Closes the tree to further work, optionally recording the failure that ended it.</summary>
    /// <param name="error">The failure that ended the stream, or <see langword="null"/> when it ended cleanly.</param>
    void Complete(Exception? error = null);

    /// <summary>Awaits every queued update.</summary>
    /// <param name="cancellationToken">Token that abandons the wait; the queued work still runs.</param>
    ValueTask DrainAsync(CancellationToken cancellationToken = default);
}
