namespace Axrone.Physics2D;

/// <summary>
/// Backward-compatible zero-allocation static facade matching original Axrone.Physics2D API semantics.
/// The traversal itself lives in <see cref="GjkEngine2D"/>; this entry point
/// binds the standard policy with diagnostics elided.
/// </summary>
public static class Gjk2D
{
    /// <summary>
    /// Computes the distance between two shapes plus the attaining witness points.
    /// Returns <see langword="true"/> when the shapes are disjoint;
    /// touching or overlapping shapes report distance zero and <see langword="false"/>.
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
        out Vec2 pointA,
        out Vec2 pointB)
        where TA : ISupportShape2D<TA>, allows ref struct
        where TB : ISupportShape2D<TB>, allows ref struct
    {
        GjkResult result = GjkEngine2D.ComputeDistance<TA, TB, StandardGjkPolicy, NullGjkObserver>(shapeA, shapeB);
        distance = result.Distance.Value.Value;
        pointA = result.WitnessPointA;
        pointB = result.WitnessPointB;
        return result.IsDisjoint;
    }
}
