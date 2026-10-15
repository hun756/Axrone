namespace Axrone.Physics3D;

/// <summary>
/// Gilbert-Johnson-Keerthi distance queries over any two support-mapped
/// shapes. The traversal is generic over the shape types, so the compiler
/// monomorphizes one solver per pair with no virtual dispatch; witness points
/// travel alongside every simplex vertex, so the answer carries both the
/// distance and where it is attained. Simplex reduction runs on the shared
/// <see cref="SimplexKernel3D"/> primitives.
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
        Span<SimplexKernel3D.Vertex> captured = stackalloc SimplexKernel3D.Vertex[4];
        return Core(in shapeA, in shapeB, false, captured, out _, out distance, out pointA, out pointB);
    }

    /// <summary>
    /// Runs the traversal capturing the enclosing tetrahedron on overlap for
    /// EPA expansion. Returns <see langword="true"/> with four simplex
    /// vertices when the shapes overlap.
    /// </summary>
    internal static bool TryCaptureOverlap<TA, TB>(
        in TA shapeA, in TB shapeB, Span<SimplexKernel3D.Vertex> simplex, out int count)
        where TA : ISupportShape3D<TA>
        where TB : ISupportShape3D<TB>
    {
        bool separated = Core(in shapeA, in shapeB, true, simplex, out count, out _, out _, out _);
        if (!separated && count == 4)
        {
            return true;
        }

        if (separated || !ExpandToTetrahedron(in shapeA, in shapeB, simplex, ref count))
        {
            return false;
        }

        return count == 4
            && SimplexKernel3D.TetrahedronContainsOrigin(
                simplex[0].Difference, simplex[1].Difference, simplex[2].Difference, simplex[3].Difference);
    }

    /// <summary>
    /// Expands a boundary simplex to a tetrahedron with face-normal supports,
    /// mirroring the reference EPA bootstrap for flat-faced shapes whose
    /// supports repeat before enclosure.
    /// </summary>
    private static bool ExpandToTetrahedron<TA, TB>(
        in TA shapeA, in TB shapeB, Span<SimplexKernel3D.Vertex> simplex, ref int count)
        where TA : ISupportShape3D<TA>
        where TB : ISupportShape3D<TB>
    {
        if (count >= 4 || simplex.Length < 4)
        {
            return count == 4;
        }

        if (count == 0)
        {
            return false;
        }

        if (count == 1)
        {
            Vec3 d = simplex[0].Difference;
            Vec3 axis = d.LengthSquared() > 1e-12f ? Vec3.Normalize(d) : new Vec3(1.0f, 0.0f, 0.0f);
            Vec3 perp = MathF.Abs(axis.Y) < 0.99f ? new Vec3(0.0f, 1.0f, 0.0f) : new Vec3(1.0f, 0.0f, 0.0f);
            Vec3 side = Vec3.Normalize(Vec3.Cross(axis, perp));
            AddSupport(in shapeA, in shapeB, axis, simplex, ref count);
            AddSupport(in shapeA, in shapeB, axis * -1.0f, simplex, ref count);
            AddSupport(in shapeA, in shapeB, side, simplex, ref count);
            return count == 4;
        }

        if (count == 2)
        {
            Vec3 edge = simplex[1].Difference - simplex[0].Difference;
            if (edge.LengthSquared() <= 1e-24f)
            {
                return false;
            }

            Vec3 reference = MathF.Abs(edge.Y) < 0.99f ? new Vec3(0.0f, 1.0f, 0.0f) : new Vec3(1.0f, 0.0f, 0.0f);
            Vec3 n1 = Vec3.Normalize(Vec3.Cross(edge, reference));
            Vec3 n2 = Vec3.Cross(edge, n1);
            AddSupport(in shapeA, in shapeB, n1, simplex, ref count);
            AddSupport(in shapeA, in shapeB, n2, simplex, ref count);
            return count == 4;
        }

        Vec3 ab = simplex[1].Difference - simplex[0].Difference;
        Vec3 ac = simplex[2].Difference - simplex[0].Difference;
        Vec3 normal = Vec3.Cross(ab, ac);
        if (normal.LengthSquared() <= 1e-24f)
        {
            return false;
        }

        normal = Vec3.Normalize(normal);
        AddSupport(in shapeA, in shapeB, normal, simplex, ref count);
        if (count < 4)
        {
            AddSupport(in shapeA, in shapeB, normal * -1.0f, simplex, ref count);
        }

        return count == 4;
    }

    /// <summary>Adds a Minkowski support vertex along a direction.</summary>
    private static void AddSupport<TA, TB>(
        in TA shapeA, in TB shapeB, Vec3 direction,
        Span<SimplexKernel3D.Vertex> simplex, ref int count)
        where TA : ISupportShape3D<TA>
        where TB : ISupportShape3D<TB>
    {
        if (count >= simplex.Length)
        {
            return;
        }

        Vec3 supportA = TA.Support(in shapeA, direction);
        Vec3 supportB = TB.Support(in shapeB, direction * -1.0f);
        simplex[count++] = new SimplexKernel3D.Vertex(supportA - supportB, supportA, supportB);
    }

    /// <summary>Shared traversal: distance by default, tetrahedron capture for EPA on demand.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool Core<TA, TB>(
        in TA shapeA, in TB shapeB, bool capture, Span<SimplexKernel3D.Vertex> captured, out int capturedCount,
        out float distance, out Vec3 pointA, out Vec3 pointB)
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

        Span<SimplexKernel3D.Vertex> simplex = stackalloc SimplexKernel3D.Vertex[4];
        Span<float> weights = stackalloc float[3];
        int count = 0;

        Vec3 firstA = TA.Support(in shapeA, direction);
        Vec3 firstB = TB.Support(in shapeB, direction * -1.0f);
        simplex[count++] = new SimplexKernel3D.Vertex(firstA - firstB, firstA, firstB);

        float bestDistSq = float.PositiveInfinity;
        Vec3 bestA = firstA;
        Vec3 bestB = firstB;

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            if (Reduce(simplex, ref count, weights, out Vec3 closest))
            {
                capturedCount = count;
                if (capture)
                {
                    for (int i = 0; i < count && i < captured.Length; i++)
                    {
                        captured[i] = simplex[i];
                    }
                }

                distance = 0.0f;
                pointA = bestA;
                pointB = bestB;
                return false;
            }

            float distSq = closest.LengthSquared();
            if (distSq <= OverlapDistanceSquared && !capture)
            {
                capturedCount = count;
                distance = 0.0f;
                pointA = bestA;
                pointB = bestB;
                return false;
            }

            if (distSq >= bestDistSq - ImprovementTolerance)
            {
                FinishCapture(simplex, count, captured, out capturedCount);
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
            var candidate = new SimplexKernel3D.Vertex(supportA - supportB, supportA, supportB);

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
                FinishCapture(simplex, count, captured, out capturedCount);
                break;
            }

            simplex[count++] = candidate;
        }

        if (!capture)
        {
            capturedCount = 0;
        }
        else
        {
            FinishCapture(simplex, count, captured, out capturedCount);
        }

        distance = MathF.Sqrt(bestDistSq);
        pointA = bestA;
        pointB = bestB;
        return bestDistSq > OverlapDistanceSquared;
    }

    /// <summary>Copies the boundary simplex for EPA bootstrap on capture runs.</summary>
    private static void FinishCapture(
        Span<SimplexKernel3D.Vertex> simplex, int count,
        Span<SimplexKernel3D.Vertex> captured, out int capturedCount)
    {
        capturedCount = 0;
        int copy = Math.Min(count, captured.Length);
        for (int i = 0; i < copy; i++)
        {
            captured[i] = simplex[i];
        }

        capturedCount = copy;
    }

    /// <summary>Reduces the simplex to its closest feature; returns true on containment (overlap).</summary>
    /// <returns><see langword="true"/> when the origin is enclosed; the caller reports overlap.</returns>
    private static bool Reduce(Span<SimplexKernel3D.Vertex> simplex, ref int count, Span<float> weights, out Vec3 closest)
    {
        if (count == 1)
        {
            closest = simplex[0].Difference;
            weights[0] = 1.0f;
            return false;
        }

        if (count == 2)
        {
            SimplexKernel3D.ClosestOnEdge(in simplex[0], in simplex[1], out closest, out float w1, out float w2);
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
            SimplexKernel3D.ClosestOnTriangle(
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

        if (SimplexKernel3D.TetrahedronContainsOrigin(a, b, c, d))
        {
            closest = Vec3.Zero;
            return true;
        }

        if (SimplexKernel3D.OutsideFace(a, b, c, d))
        {
            count = 3;
        }
        else if (SimplexKernel3D.OutsideFace(a, c, d, b))
        {
            simplex[1] = simplex[2];
            simplex[2] = simplex[3];
            count = 3;
        }
        else if (SimplexKernel3D.OutsideFace(a, d, b, c))
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

        SimplexKernel3D.ClosestOnTriangle(
            simplex[0].Difference, simplex[1].Difference, simplex[2].Difference,
            out closest, out float ta, out float tb, out float tc);
        weights[0] = ta;
        weights[1] = tb;
        weights[2] = tc;
        CompactTriangle(simplex, ref count, ta, tb, tc);
        return false;
    }

    /// <summary>Drops zero-weight vertices of a triangle reduction so the simplex stays minimal.</summary>
    private static void CompactTriangle(Span<SimplexKernel3D.Vertex> simplex, ref int count, float wa, float wb, float wc)
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
