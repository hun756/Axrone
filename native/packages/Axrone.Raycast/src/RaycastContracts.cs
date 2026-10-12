namespace Axrone.Raycast;

/// <summary>
/// Single-ray query surface over one primitive soup: nearest hit, occlusion
/// probe, and a lazy hit walk. Backed by the BVH pipeline.
/// </summary>
/// <typeparam name="TPrimitive">Primitive type stored in the hierarchy.</typeparam>
public interface IRaycaster<TPrimitive>
    where TPrimitive : unmanaged, IPrimitiveGeometry<TPrimitive>
{
    /// <summary>Casts for the nearest contact; a miss value when nothing is reached.</summary>
    RayQueryResult Raycast(in Ray ray);

    /// <summary>Probes whether anything blocks the ray; stops at the first contact.</summary>
    bool RaycastAny(in Ray ray);

    /// <summary>Lazily walks every contact within the ray window, nearest first.</summary>
    RayHitEnumerator<TPrimitive> EnumerateHits(in Ray ray);
}

/// <summary>Batch ray query surface: one result slot per input ray.</summary>
/// <typeparam name="TPrimitive">Primitive type stored in the hierarchy.</typeparam>
public interface IBatchRaycaster<TPrimitive>
    where TPrimitive : unmanaged, IPrimitiveGeometry<TPrimitive>
{
    /// <summary>Casts every ray; <paramref name="results"/> must hold one slot per ray.</summary>
    void RaycastBatch(ReadOnlySpan<Ray> rays, Span<RayQueryResult> results);

    /// <summary>Probes every ray; <paramref name="results"/> must hold one slot per ray.</summary>
    void RaycastBatchAny(ReadOnlySpan<Ray> rays, Span<bool> results);
}

/// <summary>Live counters of a raycast hierarchy: totals, load and shape.</summary>
public interface IHierarchyMetrics
{
    /// <summary>Traversals started.</summary>
    long TotalRays { get; }

    /// <summary>Contacts confirmed.</summary>
    long TotalHits { get; }

    /// <summary>Traversals currently inside the engine.</summary>
    int ActiveQueries { get; }

    /// <summary>Whether the pipeline accepts traversals.</summary>
    bool IsOperational { get; }

    /// <summary>Nodes in the built hierarchy.</summary>
    int NodeCount { get; }

    /// <summary>Primitives in the built hierarchy.</summary>
    int PrimitiveCount { get; }
}
