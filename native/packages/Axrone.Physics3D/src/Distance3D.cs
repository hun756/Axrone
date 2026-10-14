namespace Axrone.Physics3D;

/// <summary>
/// Gilbert-Johnson-Keerthi distance queries over any two support-mapped
/// shapes. The traversal is generic over the shape types, so the compiler
/// monomorphizes one solver per pair with no virtual dispatch; witness points
/// travel alongside every simplex vertex, so the answer carries both the
/// distance and where it is attained. Simplex reduction runs on the shared
/// <see cref="Simplex3D"/> primitives.
/// </summary>
public static class Gjk3D
{
    private const int MaxIterations = 32;
    private const float ZeroDirectionEpsilon = 1e-12f;
    private const float OverlapDistanceSquared = 1e-12f;
    private const float ImprovementTolerance = 1e-9f;
    private const float DuplicateDistanceSquared = 1e-12f;

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
    public static bool ComputeDistance<TA, TB>(in TA shapeA, in TB shapeB, out float distance, out Vec3 pointA, out Vec3 pointB)
        where TA : ISupportShape3D<TA>
        where TB : ISupportShape3D<TB>
    {
        Vec3 centroidA = TA.ComputeCentroid(in shapeA);
        Vec3 centroidB = TB.ComputeCentroid(in shapeB);

        Vec3 direction = centroidB - centroidA;
        if (direction.LengthSquared() < ZeroDirectionEpsilon)
        {
            direction = new Vec3(1.0f, 0.0f, 0.0f);
        }

        Span<Simplex3D.Vertex> simplex = stackalloc Simplex3D.Vertex[4];
        Span<float> weights = stackalloc float[3];
        int count = 0;

        Vec3 firstA = TA.Support(in shapeA, direction);
        Vec3 firstB = TB.Support(in shapeB, direction * -1.0f);
        simplex[count++] = new Simplex3D.Vertex(firstA - firstB, firstA, firstB);

        float bestDistSq = float.PositiveInfinity;
        Vec3 bestA = firstA;
        Vec3 bestB = firstB;

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            if (Reduce(simplex, ref count, weights, out Vec3 closest))
            {
                distance = 0.0f;
                pointA = bestA;
                pointB = bestB;
                return false;
            }

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
            bestA = Vec3.Zero;
            bestB = Vec3.Zero;
            for (int i = 0; i < count; i++)
            {
                bestA += simplex[i].PointA * weights[i];
                bestB += simplex[i].PointB * weights[i];
            }

            Vec3 search = closest * -1.0f;
            Vec3 supportA = TA.Support(in shapeA, search);
            Vec3 supportB = TB.Support(in shapeB, search * -1.0f);
            var candidate = new Simplex3D.Vertex(supportA - supportB, supportA, supportB);

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

    /// <summary>Reduces the simplex to its closest feature; returns true on containment (overlap).</summary>
    /// <returns><see langword="true"/> when the origin is enclosed; the caller reports overlap.</returns>
    private static bool Reduce(Span<Simplex3D.Vertex> simplex, ref int count, Span<float> weights, out Vec3 closest)
    {
        if (count == 1)
        {
            closest = simplex[0].Difference;
            weights[0] = 1.0f;
            return false;
        }

        if (count == 2)
        {
            Simplex3D.ClosestOnEdge(in simplex[0], in simplex[1], out closest, out float w1, out float w2);
            if (w1 == 1.0f)
            {
                count = 1;
            }
            else if (w2 == 1.0f)
            {
                simplex[0] = simplex[1];
                count = 1;
            }

            weights[0] = w1;
            weights[1] = w2;
            return false;
        }

        if (count == 3)
        {
            Simplex3D.ClosestOnTriangle(
                simplex[0].Difference, simplex[1].Difference, simplex[2].Difference,
                out closest, out float wa, out float wb, out float wc);
            weights[0] = wa;
            weights[1] = wb;
            weights[2] = wc;
            CompactTriangle(simplex, ref count, wa, wb, wc);
            return false;
        }

        Vec3 a = simplex[0].Difference;
        Vec3 b = simplex[1].Difference;
        Vec3 c = simplex[2].Difference;
        Vec3 d = simplex[3].Difference;

        if (Simplex3D.TetrahedronContainsOrigin(a, b, c, d))
        {
            closest = Vec3.Zero;
            return true;
        }

        if (Simplex3D.OutsideFace(a, b, c, d))
        {
            count = 3;
        }
        else if (Simplex3D.OutsideFace(a, c, d, b))
        {
            simplex[1] = simplex[2];
            simplex[2] = simplex[3];
            count = 3;
        }
        else if (Simplex3D.OutsideFace(a, d, b, c))
        {
            simplex[2] = simplex[1];
            simplex[1] = simplex[3];
            count = 3;
        }
        else
        {
            simplex[0] = simplex[1];
            simplex[1] = simplex[3];
            count = 3;
        }

        Simplex3D.ClosestOnTriangle(
            simplex[0].Difference, simplex[1].Difference, simplex[2].Difference,
            out closest, out float ta, out float tb, out float tc);
        weights[0] = ta;
        weights[1] = tb;
        weights[2] = tc;
        CompactTriangle(simplex, ref count, ta, tb, tc);
        return false;
    }

    /// <summary>Drops zero-weight vertices of a triangle reduction so the simplex stays minimal.</summary>
    private static void CompactTriangle(Span<Simplex3D.Vertex> simplex, ref int count, float wa, float wb, float wc)
    {
        if (wa == 0.0f)
        {
            simplex[0] = simplex[1];
            simplex[1] = simplex[2];
            count = 2;
        }
        else if (wb == 0.0f)
        {
            simplex[1] = simplex[2];
            count = 2;
        }
        else if (wc == 0.0f)
        {
            count = 2;
        }
    }
}
