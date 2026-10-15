namespace Axrone.Physics3D;

/// <summary>
/// Backward-compatible zero-allocation static facade matching original Axrone.Physics3D API semantics.
/// The traversal itself lives in <see cref="GjkEngine3D"/>; this entry point
/// binds the standard policy with diagnostics elided, and exposes tetrahedron
/// capture for EPA expansion.
/// </summary>
public static class Gjk3D
{
    /// <summary>
    /// Computes the distance between two shapes plus the attaining witness
    /// points. Returns <see langword="true"/> when the shapes are disjoint;
    /// touching or overlapping shapes report distance zero and
    /// <see langword="false"/>.
    /// </summary>
    /// <param name="shapeA">The first shape.</param>
    /// <param name="shapeB">The second shape.</param>
    /// <param name="distance">Non-negative separation; zero on contact or overlap.</param>
    /// <param name="pointA">Witness on the first shape.</param>
    /// <param name="pointB">Witness on the second shape.</param>
    /// <typeparam name="TA">First shape type.</typeparam>
    /// <typeparam name="TB">Second shape type.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool ComputeDistance<TA, TB>(
        in TA shapeA,
        in TB shapeB,
        out float distance,
        out Vec3 pointA,
        out Vec3 pointB)
        where TA : ISupportShape3D<TA>, allows ref struct
        where TB : ISupportShape3D<TB>, allows ref struct
    {
        GjkResult result = GjkEngine3D.ComputeDistance<TA, TB, StandardGjkPolicy, NullGjkObserver>(
            shapeA, shapeB);
        distance = result.Distance.Value.Value;
        pointA = result.WitnessPointA;
        pointB = result.WitnessPointB;
        return result.IsDisjoint;
    }

    /// <summary>
    /// Runs the traversal capturing the enclosing tetrahedron on overlap for
    /// EPA expansion. Returns <see langword="true"/> with four simplex
    /// vertices when the shapes overlap.
    /// </summary>
    internal static bool TryCaptureOverlap<TA, TB>(
        in TA shapeA,
        in TB shapeB,
        Span<SimplexVertex> simplex,
        out int count)
        where TA : ISupportShape3D<TA>, allows ref struct
        where TB : ISupportShape3D<TB>, allows ref struct
    {
        return GjkEngine3D.TryCaptureTetrahedron<TA, TB, StandardGjkPolicy>(
            shapeA, shapeB, simplex, out count);
    }
}
