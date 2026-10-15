namespace Axrone.Physics2D;

/// <summary>
/// Zero-allocation, fully monomorphized Gilbert-Johnson-Keerthi (GJK) 2D distance and collision query.
/// Convergence tolerances, iteration budgets and observability inject through
/// compile-time policies, so each shape pair plus policy mix monomorphizes
/// into its own branch-free traversal with no virtual dispatch.
/// </summary>
public static class Gjk2D
{
    /// <summary>
    /// Executes the monomorphized GJK distance query between any two support-mapped shapes.
    /// </summary>
    /// <param name="shapeA">The first shape.</param>
    /// <param name="shapeB">The second shape.</param>
    /// <param name="iterationLimit">Traversal budget; the policy default when zero.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static GjkResult ComputeDistance<TShapeA, TShapeB, TPolicy, TObserver>(
        TShapeA shapeA,
        TShapeB shapeB,
        IterationLimit iterationLimit = default)
        where TShapeA : ISupportShape2D<TShapeA>, allows ref struct
        where TShapeB : ISupportShape2D<TShapeB>, allows ref struct
        where TPolicy : struct, IGjkConvergencePolicy<TPolicy>
        where TObserver : struct, IGjkObserver<TObserver>
    {
        IterationLimit limit = iterationLimit.Value == 0 ? TPolicy.DefaultIterationLimit : iterationLimit;

        Vec2 centroidA = TShapeA.ComputeCentroid(in shapeA);
        Vec2 centroidB = TShapeB.ComputeCentroid(in shapeB);

        Vec2 initialDirection = centroidB - centroidA;
        if (LengthSquared(initialDirection).Value.Value < TPolicy.ZeroDirectionEpsilon.Value.Value)
        {
            initialDirection = Vec2.UnitX;
        }

        Simplex2D simplex = default;

        Vec2 firstA = TShapeA.Support(in shapeA, initialDirection);
        Vec2 firstB = TShapeB.Support(in shapeB, -initialDirection);
        SimplexVertex initialVertex = SimplexVertex.Create(firstA, firstB);
        simplex.Add(initialVertex);

        DistanceSquared bestDistSq = DistanceSquared.PositiveInfinity;
        Vec2 bestWitnessA = firstA;
        Vec2 bestWitnessB = firstB;
        Vec2 bestClosest = initialVertex.Difference;

        IterationCount currentIteration = IterationCount.Zero;
        GjkStatus exitStatus = GjkStatus.MaxIterationsExceeded;

        while (!limit.HasReached(currentIteration))
        {
            currentIteration = IterationCount.Increment(currentIteration);

            SolveSimplex(ref simplex, out Vec2 closest, out Vec2 witnessA, out Vec2 witnessB);

            DistanceSquared distSq = LengthSquared(closest);
            TObserver.OnIteration(currentIteration, distSq, -closest);

            if (distSq <= TPolicy.OverlapTolerance)
            {
                exitStatus = GjkStatus.TouchingOrOverlapping;
                bestWitnessA = witnessA;
                bestWitnessB = witnessB;
                bestClosest = Vec2.Zero;
                bestDistSq = DistanceSquared.Zero;
                break;
            }

            if (distSq.Value.Value >= bestDistSq.Value.Value - TPolicy.ImprovementTolerance.Value)
            {
                exitStatus = GjkStatus.Disjoint;
                break;
            }

            bestDistSq = distSq;
            bestWitnessA = witnessA;
            bestWitnessB = witnessB;
            bestClosest = closest;

            Vec2 searchDirection = -closest;

            Vec2 suppA = TShapeA.Support(in shapeA, searchDirection);
            Vec2 suppB = TShapeB.Support(in shapeB, -searchDirection);
            SimplexVertex candidate = SimplexVertex.Create(suppA, suppB);

            float candidateProgress = Vec2.Dot(candidate.Difference - closest, searchDirection);
            if (candidateProgress <= 0.0f)
            {
                exitStatus = GjkStatus.Disjoint;
                break;
            }

            if (simplex.ContainsDuplicate(candidate.Difference, TPolicy.DuplicateTolerance))
            {
                exitStatus = GjkStatus.Disjoint;
                break;
            }

            simplex.Add(candidate);
        }

        Distance finalDistance = exitStatus == GjkStatus.TouchingOrOverlapping
            ? Distance.Zero
            : bestDistSq.ToDistance();

        Vec2 separatingAxis = NormalizeOr(bestClosest, Vec2.UnitX);

        TObserver.OnTerminated(exitStatus, currentIteration, finalDistance);

        return new GjkResult(
            exitStatus,
            finalDistance,
            bestWitnessA,
            bestWitnessB,
            separatingAxis,
            currentIteration);
    }

    /// <summary>
    /// Computes the distance between two shapes plus the attaining witness points.
    /// Returns <see langword="true"/> when the shapes are disjoint;
    /// touching or overlapping shapes report distance zero and <see langword="false"/>.
    /// Standard policy with diagnostics elided.
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
        GjkResult result = ComputeDistance<TA, TB, StandardGjkPolicy, NullGjkObserver>(shapeA, shapeB);
        distance = result.Distance.Value.Value;
        pointA = result.WitnessPointA;
        pointB = result.WitnessPointB;
        return result.IsDisjoint;
    }

    /// <summary>
    /// Reduces the 2D simplex to its closest Voronoi sub-feature and computes interpolated witness points.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void SolveSimplex(
        ref Simplex2D simplex,
        out Vec2 closest,
        out Vec2 witnessA,
        out Vec2 witnessB)
    {
        uint count = simplex.Count;
        if (count == 1)
        {
            SimplexVertex v0 = simplex[0];
            closest = v0.Difference;
            witnessA = v0.WitnessA;
            witnessB = v0.WitnessB;
            return;
        }

        if (count == 2)
        {
            SolveSegment(ref simplex, out closest, out witnessA, out witnessB);
            return;
        }

        if (count == 3)
        {
            SolveTriangle(ref simplex, out closest, out witnessA, out witnessB);
            return;
        }

        ThrowHelper.ThrowSimplexInvalidState();
        closest = Vec2.Zero;
        witnessA = Vec2.Zero;
        witnessB = Vec2.Zero;
    }

    /// <summary>
    /// Voronoi segment solver for 2-simplex with Barycentric reduction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void SolveSegment(
        ref Simplex2D simplex,
        out Vec2 closest,
        out Vec2 witnessA,
        out Vec2 witnessB)
    {
        SimplexVertex a = simplex[1];
        SimplexVertex b = simplex[0];

        Vec2 ab = b.Difference - a.Difference;
        Vec2 ao = -a.Difference;

        float denom = Vec2.Dot(ab, ab);
        if (denom <= 1e-18f)
        {
            simplex.Set1(a);
            closest = a.Difference;
            witnessA = a.WitnessA;
            witnessB = a.WitnessB;
            return;
        }

        float t = Vec2.Dot(ao, ab) / denom;
        if (t <= 0.0f)
        {
            simplex.Set1(a);
            closest = a.Difference;
            witnessA = a.WitnessA;
            witnessB = a.WitnessB;
            return;
        }

        if (t >= 1.0f)
        {
            simplex.Set1(b);
            closest = b.Difference;
            witnessA = b.WitnessA;
            witnessB = b.WitnessB;
            return;
        }

        float u = 1.0f - t;
        float v = t;

        simplex.Set2(a, b);
        closest = (a.Difference * u) + (b.Difference * v);
        witnessA = (a.WitnessA * u) + (b.WitnessA * v);
        witnessB = (a.WitnessB * u) + (b.WitnessB * v);
    }

    /// <summary>
    /// Exact 7-region Voronoi solver for 3-simplex (triangle).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void SolveTriangle(
        ref Simplex2D simplex,
        out Vec2 closest,
        out Vec2 witnessA,
        out Vec2 witnessB)
    {
        SimplexVertex a = simplex[2];
        SimplexVertex b = simplex[1];
        SimplexVertex c = simplex[0];

        Vec2 aDiff = a.Difference;
        Vec2 bDiff = b.Difference;
        Vec2 cDiff = c.Difference;

        Vec2 ab = bDiff - aDiff;
        Vec2 ac = cDiff - aDiff;
        Vec2 ap = -aDiff;

        float d1 = Vec2.Dot(ab, ap);
        float d2 = Vec2.Dot(ac, ap);

        if (d1 <= 0.0f && d2 <= 0.0f)
        {
            simplex.Set1(a);
            closest = aDiff;
            witnessA = a.WitnessA;
            witnessB = a.WitnessB;
            return;
        }

        Vec2 bp = -bDiff;
        float d3 = Vec2.Dot(ab, bp);
        float d4 = Vec2.Dot(ac, bp);

        if (d3 >= 0.0f && d4 <= d3)
        {
            simplex.Set1(b);
            closest = bDiff;
            witnessA = b.WitnessA;
            witnessB = b.WitnessB;
            return;
        }

        float vc = (d1 * d4) - (d3 * d2);
        if (vc <= 0.0f && d1 >= 0.0f && d3 <= 0.0f)
        {
            float v = d1 / (d1 - d3);
            float u = 1.0f - v;
            simplex.Set2(a, b);
            closest = (aDiff * u) + (bDiff * v);
            witnessA = (a.WitnessA * u) + (b.WitnessA * v);
            witnessB = (a.WitnessB * u) + (b.WitnessB * v);
            return;
        }

        Vec2 cp = -cDiff;
        float d5 = Vec2.Dot(ab, cp);
        float d6 = Vec2.Dot(ac, cp);

        if (d6 >= 0.0f && d5 <= d6)
        {
            simplex.Set1(c);
            closest = cDiff;
            witnessA = c.WitnessA;
            witnessB = c.WitnessB;
            return;
        }

        float vb = (d5 * d2) - (d1 * d6);
        if (vb <= 0.0f && d2 >= 0.0f && d6 <= 0.0f)
        {
            float w = d2 / (d2 - d6);
            float u = 1.0f - w;
            simplex.Set2(a, c);
            closest = (aDiff * u) + (cDiff * w);
            witnessA = (a.WitnessA * u) + (c.WitnessA * w);
            witnessB = (a.WitnessB * u) + (c.WitnessB * w);
            return;
        }

        float va = (d3 * d6) - (d5 * d4);
        if (va <= 0.0f && (d4 - d3) >= 0.0f && (d5 - d6) >= 0.0f)
        {
            float d4SubD3 = d4 - d3;
            float d5SubD6 = d5 - d6;
            float w = d4SubD3 / (d4SubD3 + d5SubD6);
            float v = 1.0f - w;
            simplex.Set2(b, c);
            closest = (bDiff * v) + (cDiff * w);
            witnessA = (b.WitnessA * v) + (c.WitnessA * w);
            witnessB = (b.WitnessB * v) + (c.WitnessB * w);
            return;
        }

        float denom = 1.0f / (va + vb + vc);
        float vBary = vb * denom;
        float wBary = vc * denom;
        float uBary = 1.0f - vBary - wBary;

        simplex.Set3(a, b, c);
        closest = Vec2.Zero;
        witnessA = (a.WitnessA * uBary) + (b.WitnessA * vBary) + (c.WitnessA * wBary);
        witnessB = (a.WitnessB * uBary) + (b.WitnessB * vBary) + (c.WitnessB * wBary);
    }

    /// <summary>Squared length as a domain nominal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static DistanceSquared LengthSquared(in Vec2 vector) =>
        DistanceSquared.From(Vec2.Dot(vector, vector));

    /// <summary>Unit vector with a fallback for degenerate inputs.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vec2 NormalizeOr(in Vec2 vector, in Vec2 fallback)
    {
        float lengthSq = Vec2.Dot(vector, vector);
        if (lengthSq <= 1e-18f)
        {
            return fallback;
        }

        return vector * (1.0f / MathF.Sqrt(lengthSq));
    }
}
