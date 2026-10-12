namespace Axrone.Physics2D;

/// <summary>
/// Gilbert-Johnson-Keerthi distance queries over any two support-mapped
/// shapes. The traversal is generic over the shape types, so the compiler
/// monomorphizes one solver per pair with no virtual dispatch; witness points
/// travel alongside every simplex vertex, so the answer carries both the
/// distance and where it is attained.
/// </summary>
public static class Gjk2D
{
    private const int MaxIterations = 24;
    private const float ZeroDirectionEpsilon = 1e-12f;
    private const float OverlapDistanceSquared = 1e-12f;
    private const float ImprovementTolerance = 1e-9f;
    private const float DuplicateDistanceSquared = 1e-12f;

    /// <summary>One Minkowski-difference vertex with its witness pair.</summary>
    private readonly struct MPoint
    {
        public readonly Vec2 Difference;
        public readonly Vec2 PointA;
        public readonly Vec2 PointB;

        public MPoint(Vec2 difference, Vec2 pointA, Vec2 pointB)
        {
            Difference = difference;
            PointA = pointA;
            PointB = pointB;
        }
    }

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
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool ComputeDistance<TA, TB>(in TA shapeA, in TB shapeB, out float distance, out Vec2 pointA, out Vec2 pointB)
        where TA : ISupportShape2D<TA>
        where TB : ISupportShape2D<TB>
    {
        Vec2 centroidA = TA.ComputeCentroid(in shapeA);
        Vec2 centroidB = TB.ComputeCentroid(in shapeB);

        Vec2 direction = centroidB - centroidA;
        if (direction.LengthSquared() < ZeroDirectionEpsilon)
        {
            direction = new Vec2(1.0f, 0.0f);
        }

        Span<MPoint> simplex = stackalloc MPoint[3];
        int count = 0;

        Vec2 firstA = TA.Support(in shapeA, direction);
        Vec2 firstB = TB.Support(in shapeB, direction * -1.0f);
        simplex[count++] = new MPoint(firstA - firstB, firstA, firstB);

        float bestDistSq = float.PositiveInfinity;
        Vec2 bestA = firstA;
        Vec2 bestB = firstB;

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            Solve(simplex, ref count, out Vec2 closest, out Vec2 search, out float w1, out float w2);

            float distSq = closest.LengthSquared();
            if (distSq <= OverlapDistanceSquared)
            {
                distance = 0.0f;
                pointA = bestA;
                pointB = bestB;
                return false;
            }

            if (distSq >= bestDistSq - ImprovementTolerance)
            {
                break;
            }

            bestDistSq = distSq;
            MPoint first = simplex[0];
            if (count == 1)
            {
                bestA = first.PointA;
                bestB = first.PointB;
            }
            else
            {
                MPoint second = simplex[1];
                bestA = (first.PointA * w1) + (second.PointA * w2);
                bestB = (first.PointB * w1) + (second.PointB * w2);
            }

            Vec2 supportA = TA.Support(in shapeA, search);
            Vec2 supportB = TB.Support(in shapeB, search * -1.0f);
            MPoint candidate = new(supportA - supportB, supportA, supportB);

            bool duplicate = false;
            for (int i = 0; i < count; i++)
            {
                if ((candidate.Difference - simplex[i].Difference).LengthSquared() < DuplicateDistanceSquared)
                {
                    duplicate = true;
                    break;
                }
            }

            if (duplicate)
            {
                break;
            }

            simplex[count++] = candidate;
        }

        distance = MathF.Sqrt(bestDistSq);
        pointA = bestA;
        pointB = bestB;
        return bestDistSq > OverlapDistanceSquared;
    }

    /// <summary>Reduces the simplex to its closest feature, reporting weights for witness blending.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void Solve(Span<MPoint> simplex, ref int count, out Vec2 closest, out Vec2 search, out float w1, out float w2)
    {
        if (count == 1)
        {
            closest = simplex[0].Difference;
            search = closest * -1.0f;
            w1 = 1.0f;
            w2 = 0.0f;
            return;
        }

        if (count == 2)
        {
            SolveEdge(simplex[0], simplex[1], out closest, out w1, out w2);
            search = closest * -1.0f;
            if (w1 == 1.0f)
            {
                count = 1;
            }
            else if (w2 == 1.0f)
            {
                simplex[0] = simplex[1];
                count = 1;
            }

            return;
        }

        SolveTriangle(simplex, ref count, out closest, out w1, out w2);
        search = closest * -1.0f;
    }

    /// <summary>Closest point on edge (u, v) to the origin with barycentric weights.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void SolveEdge(in MPoint u, in MPoint v, out Vec2 closest, out float w1, out float w2)
    {
        Vec2 edge = v.Difference - u.Difference;
        float denom = Vec2.Dot(edge, edge);
        if (denom < ZeroDirectionEpsilon)
        {
            closest = u.Difference;
            w1 = 1.0f;
            w2 = 0.0f;
            return;
        }

        float t = Vec2.Dot(u.Difference * -1.0f, edge) / denom;
        if (t <= 0.0f)
        {
            closest = u.Difference;
            w1 = 1.0f;
            w2 = 0.0f;
        }
        else if (t >= 1.0f)
        {
            closest = v.Difference;
            w1 = 0.0f;
            w2 = 1.0f;
        }
        else
        {
            closest = u.Difference + (edge * t);
            w1 = 1.0f - t;
            w2 = t;
        }
    }

    /// <summary>Reduces a triangle to its closest vertex or edge; origin inside means overlap.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void SolveTriangle(Span<MPoint> simplex, ref int count, out Vec2 closest, out float w1, out float w2)
    {
        MPoint a = simplex[0];
        MPoint b = simplex[1];
        MPoint c = simplex[2];

        if (IsVertexClosest(a.Difference, b.Difference, c.Difference))
        {
            simplex[0] = a;
            SolveEdge(a, a, out closest, out w1, out w2);
            count = 1;
            return;
        }

        if (IsVertexClosest(b.Difference, a.Difference, c.Difference))
        {
            simplex[0] = b;
            SolveEdge(b, b, out closest, out w1, out w2);
            count = 1;
            return;
        }

        if (IsVertexClosest(c.Difference, a.Difference, b.Difference))
        {
            simplex[0] = c;
            SolveEdge(c, c, out closest, out w1, out w2);
            count = 1;
            return;
        }

        if (OriginInTriangle(a.Difference, b.Difference, c.Difference))
        {
            closest = Vec2.Zero;
            w1 = 1.0f;
            w2 = 0.0f;
            count = 1;
            simplex[0] = a;
            return;
        }

        float bestDistSq = float.PositiveInfinity;
        MPoint bestU = a;
        MPoint bestV = b;
        float bestW1 = 0.5f;
        float bestW2 = 0.5f;

        TryEdge(a, b, ref bestDistSq, ref bestU, ref bestV, ref bestW1, ref bestW2);
        TryEdge(b, c, ref bestDistSq, ref bestU, ref bestV, ref bestW1, ref bestW2);
        TryEdge(c, a, ref bestDistSq, ref bestU, ref bestV, ref bestW1, ref bestW2);

        simplex[0] = bestU;
        simplex[1] = bestV;
        closest = (bestU.Difference * bestW1) + (bestV.Difference * bestW2);
        w1 = bestW1;
        w2 = bestW2;
        count = 2;
    }

    /// <summary>Whether the origin's closest simplex point is the vertex itself.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsVertexClosest(Vec2 vertex, Vec2 neighborA, Vec2 neighborB) =>
        Vec2.Dot(vertex, vertex - neighborA) <= 0.0f && Vec2.Dot(vertex, vertex - neighborB) <= 0.0f;

    /// <summary>Whether the origin lies strictly inside the triangle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool OriginInTriangle(Vec2 a, Vec2 b, Vec2 c)
    {
        float s1 = ((b.X - a.X) * -a.Y) - ((b.Y - a.Y) * -a.X);
        float s2 = ((c.X - b.X) * -b.Y) - ((c.Y - b.Y) * -b.X);
        float s3 = ((a.X - c.X) * -c.Y) - ((a.Y - c.Y) * -c.X);
        return (s1 > 0.0f && s2 > 0.0f && s3 > 0.0f) || (s1 < 0.0f && s2 < 0.0f && s3 < 0.0f);
    }

    /// <summary>Keeps the closest of the candidate edge and the running best.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TryEdge(
        in MPoint u,
        in MPoint v,
        ref float bestDistSq,
        ref MPoint bestU,
        ref MPoint bestV,
        ref float bestW1,
        ref float bestW2)
    {
        SolveEdge(in u, in v, out Vec2 closest, out float w1, out float w2);
        float distSq = closest.LengthSquared();
        if (distSq < bestDistSq)
        {
            bestDistSq = distSq;
            bestU = u;
            bestV = v;
            bestW1 = w1;
            bestW2 = w2;
        }
    }
}
