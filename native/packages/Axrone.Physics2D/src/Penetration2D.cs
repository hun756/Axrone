namespace Axrone.Physics2D;

/// <summary>
/// Expanding-polytope penetration queries over any two support-mapped shapes.
/// Runs its own simplex loop until a triangle contains the origin, then
/// expands the ring to the closest face. The reported normal points from the
/// first shape toward the second; moving the first shape along the negated
/// normal by the depth separates the pair.
/// </summary>
public static class Epa2D
{
    private const int MaxSimplexIterations = 32;
    private const int MaxExpandIterations = 32;
    private const int RingCapacity = 40;
    private const float ZeroDirectionEpsilonSquared = 1e-12f;
    private const float TouchDistanceSquared = 1e-12f;
    private const float ImprovementTolerance = 1e-9f;
    private const float ConvergenceTolerance = 1e-6f;
    private const float DegenerateEdgeSquared = 1e-12f;

    /// <summary>
    /// Computes the penetration of two overlapping shapes. Returns
    /// <see langword="true"/> with a unit normal, a non-negative depth and a
    /// witness pair on contact (including touching); disjoint shapes report
    /// <see langword="false"/> with default outputs.
    /// </summary>
    /// <param name="shapeA">The first shape; the normal points away from it.</param>
    /// <param name="shapeB">The second shape; the normal points toward it.</param>
    /// <param name="normal">Unit contact normal, first shape toward second.</param>
    /// <param name="depth">Penetration depth; zero on touching contact.</param>
    /// <param name="pointA">Witness on the first shape.</param>
    /// <param name="pointB">Witness on the second shape.</param>
    /// <typeparam name="TA">First shape type.</typeparam>
    /// <typeparam name="TB">Second shape type.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool ComputePenetration<TA, TB>(
        in TA shapeA,
        in TB shapeB,
        out Vec2 normal,
        out float depth,
        out Vec2 pointA,
        out Vec2 pointB)
        where TA : ISupportShape2D<TA>
        where TB : ISupportShape2D<TB>
    {
        Vec2 centroidA = TA.ComputeCentroid(in shapeA);
        Vec2 centroidB = TB.ComputeCentroid(in shapeB);

        Vec2 direction = centroidB - centroidA;
        if (direction.LengthSquared() < ZeroDirectionEpsilonSquared)
        {
            direction = new Vec2(1.0f, 0.0f);
        }

        Span<SimplexKernel2D.Vertex> ring = stackalloc SimplexKernel2D.Vertex[RingCapacity];
        int count = 0;

        Vec2 firstA = TA.Support(in shapeA, direction);
        Vec2 firstB = TB.Support(in shapeB, direction * -1.0f);
        ring[count++] = new SimplexKernel2D.Vertex(firstA - firstB, firstA, firstB);

        float bestSepSq = float.PositiveInfinity;

        for (int iteration = 0; iteration < MaxSimplexIterations; iteration++)
        {
            Vec2 closest;
            if (count == 1)
            {
                closest = ring[0].Difference;
            }
            else if (count == 2)
            {
                count = ReduceEdge(ring, out closest);
            }
            else
            {
                SimplexKernel2D.Vertex a = ring[0];
                SimplexKernel2D.Vertex b = ring[1];
                SimplexKernel2D.Vertex c = ring[2];

                if (SimplexKernel2D.IsVertexClosest(a.Difference, b.Difference, c.Difference))
                {
                    ring[0] = a;
                    closest = a.Difference;
                    count = 1;
                }
                else if (SimplexKernel2D.IsVertexClosest(b.Difference, a.Difference, c.Difference))
                {
                    ring[0] = b;
                    closest = b.Difference;
                    count = 1;
                }
                else if (SimplexKernel2D.IsVertexClosest(c.Difference, a.Difference, b.Difference))
                {
                    ring[0] = c;
                    closest = c.Difference;
                    count = 1;
                }
                else if (SimplexKernel2D.OriginInTriangle(a.Difference, b.Difference, c.Difference))
                {
                    OrderRing(ring, ref count);
                    return Expand(in shapeA, in shapeB, ring, ref count, out normal, out depth, out pointA, out pointB);
                }
                else
                {
                    count = ReduceToBestEdge(ring);
                    continue;
                }
            }

            float distSq = closest.LengthSquared();
            if (distSq <= TouchDistanceSquared)
            {
                return ResolveDegenerate(in shapeA, in shapeB, ring, count, out normal, out depth, out pointA, out pointB);
            }

            if (distSq >= bestSepSq - ImprovementTolerance)
            {
                return NoContact(out normal, out depth, out pointA, out pointB);
            }

            bestSepSq = distSq;

            Vec2 search = closest * -1.0f;
            Vec2 supportA = TA.Support(in shapeA, search);
            Vec2 supportB = TB.Support(in shapeB, search * -1.0f);
            ring[count++] = new SimplexKernel2D.Vertex(supportA - supportB, supportA, supportB);
        }

        return NoContact(out normal, out depth, out pointA, out pointB);
    }

    /// <summary>
    /// Resolves the eps-band: the origin sits on or near the simplex boundary,
    /// where the closest feature alone cannot tell touching from overlap.
    /// Probes extremal vertices around the feature and expands on the first
    /// strictly containing triple; otherwise reports a touching contact.
    /// </summary>
    private static bool ResolveDegenerate<TA, TB>(
        in TA shapeA,
        in TB shapeB,
        Span<SimplexKernel2D.Vertex> ring,
        int count,
        out Vec2 normal,
        out float depth,
        out Vec2 pointA,
        out Vec2 pointB)
        where TA : ISupportShape2D<TA>
        where TB : ISupportShape2D<TB>
    {
        Span<SimplexKernel2D.Vertex> pool = stackalloc SimplexKernel2D.Vertex[10];
        int poolCount = 0;
        for (int i = 0; i < count && poolCount < 10; i++)
        {
            pool[poolCount++] = ring[i];
        }

        if (count >= 2)
        {
            Vec2 edge = ring[1].Difference - ring[0].Difference;
            if (edge.LengthSquared() >= DegenerateEdgeSquared)
            {
                float invLength = 1.0f / MathF.Sqrt(edge.LengthSquared());
                Vec2 perpendicular = new(edge.Y * invLength, -edge.X * invLength);
                AddProbe(in shapeA, in shapeB, perpendicular, pool, ref poolCount);
                AddProbe(in shapeA, in shapeB, perpendicular * -1.0f, pool, ref poolCount);
            }
        }

        AddProbe(in shapeA, in shapeB, new Vec2(1.0f, 0.0f), pool, ref poolCount);
        AddProbe(in shapeA, in shapeB, new Vec2(-1.0f, 0.0f), pool, ref poolCount);
        AddProbe(in shapeA, in shapeB, new Vec2(0.0f, 1.0f), pool, ref poolCount);
        AddProbe(in shapeA, in shapeB, new Vec2(0.0f, -1.0f), pool, ref poolCount);

        Span<SimplexKernel2D.Vertex> triangle = stackalloc SimplexKernel2D.Vertex[3];
        for (int i = 0; i < poolCount; i++)
        {
            for (int j = i + 1; j < poolCount; j++)
            {
                for (int k = j + 1; k < poolCount; k++)
                {
                    triangle[0] = pool[i];
                    triangle[1] = pool[j];
                    triangle[2] = pool[k];
                    if (SimplexKernel2D.OriginInTriangle(triangle[0].Difference, triangle[1].Difference, triangle[2].Difference))
                    {
                        triangle.CopyTo(ring);
                        int ordered = 3;
                        OrderRing(ring, ref ordered);
                        return Expand(in shapeA, in shapeB, ring, ref ordered, out normal, out depth, out pointA, out pointB);
                    }
                }
            }
        }

        Vec2 centroidA = TA.ComputeCentroid(in shapeA);
        Vec2 centroidB = TB.ComputeCentroid(in shapeB);
        return TouchingContact(in centroidA, in centroidB, out normal, out depth, out pointA, out pointB);
    }

    /// <summary>Adds one support pair along a probe direction, capped at the pool size.</summary>
    private static void AddProbe<TA, TB>(
        in TA shapeA,
        in TB shapeB,
        Vec2 direction,
        Span<SimplexKernel2D.Vertex> pool,
        ref int poolCount)
        where TA : ISupportShape2D<TA>
        where TB : ISupportShape2D<TB>
    {
        if (poolCount >= pool.Length)
        {
            return;
        }

        Vec2 supportA = TA.Support(in shapeA, direction);
        Vec2 supportB = TB.Support(in shapeB, direction * -1.0f);
        pool[poolCount++] = new SimplexKernel2D.Vertex(supportA - supportB, supportA, supportB);
    }

    /// <summary>Separated shapes: default outputs, no contact.</summary>
    private static bool NoContact(out Vec2 normal, out float depth, out Vec2 pointA, out Vec2 pointB)
    {
        normal = default;
        depth = 0.0f;
        pointA = default;
        pointB = default;
        return false;
    }

    /// <summary>Reduces a 2-vertex ring, keeping both endpoints on an interior closest edge.</summary>
    private static int ReduceEdge(Span<SimplexKernel2D.Vertex> ring, out Vec2 closest)
    {
        SimplexKernel2D.ClosestOnEdge(in ring[0], in ring[1], out closest, out float w1, out float w2);
        if (w1 == 1.0f)
        {
            return 1;
        }

        if (w2 == 1.0f)
        {
            ring[0] = ring[1];
            return 1;
        }

        return 2;
    }

    /// <summary>Reduces a non-containing triangle to its closest edge.</summary>
    private static int ReduceToBestEdge(Span<SimplexKernel2D.Vertex> ring)
    {
        float bestDistSq = float.PositiveInfinity;
        int bestEdge = 0;

        for (int i = 0; i < 3; i++)
        {
            SimplexKernel2D.Vertex u = ring[i];
            SimplexKernel2D.Vertex v = ring[(i + 1) % 3];
            SimplexKernel2D.ClosestOnEdge(in u, in v, out Vec2 closest, out _, out _);
            float distSq = closest.LengthSquared();
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestEdge = i;
            }
        }

        ring[0] = ring[bestEdge];
        ring[1] = ring[(bestEdge + 1) % 3];
        return 2;
    }

    /// <summary>Orders three vertices counter-clockwise around their centroid for ring expansion.</summary>
    private static void OrderRing(Span<SimplexKernel2D.Vertex> ring, ref int count)
    {
        Vec2 center = (ring[0].Difference + ring[1].Difference + ring[2].Difference) * (1.0f / 3.0f);

        float a0 = MathF.Atan2(ring[0].Difference.Y - center.Y, ring[0].Difference.X - center.X);
        float a1 = MathF.Atan2(ring[1].Difference.Y - center.Y, ring[1].Difference.X - center.X);
        float a2 = MathF.Atan2(ring[2].Difference.Y - center.Y, ring[2].Difference.X - center.X);

        if (a0 > a1)
        {
            (ring[0], ring[1]) = (ring[1], ring[0]);
            (a0, a1) = (a1, a0);
        }

        if (a1 > a2)
        {
            (ring[1], ring[2]) = (ring[2], ring[1]);
            (a1, a2) = (a2, a1);
        }

        if (a0 > a1)
        {
            (ring[0], ring[1]) = (ring[1], ring[0]);
        }

        count = 3;
    }

    /// <summary>Expands the ring to the closest face, blending witnesses on it.</summary>
    private static bool Expand<TA, TB>(
        in TA shapeA,
        in TB shapeB,
        Span<SimplexKernel2D.Vertex> ring,
        ref int count,
        out Vec2 normal,
        out float depth,
        out Vec2 pointA,
        out Vec2 pointB)
        where TA : ISupportShape2D<TA>
        where TB : ISupportShape2D<TB>
    {
        Vec2 bestNormal = new(1.0f, 0.0f);
        float bestDistance = 0.0f;
        Vec2 bestA = default;
        Vec2 bestB = default;
        bool hasBest = false;

        for (int iteration = 0; iteration < MaxExpandIterations; iteration++)
        {
            float minDistance = float.PositiveInfinity;
            int minEdge = 0;
            Vec2 minNormal = new(1.0f, 0.0f);

            for (int i = 0; i < count; i++)
            {
                SimplexKernel2D.Vertex u = ring[i];
                SimplexKernel2D.Vertex v = ring[(i + 1) % count];
                Vec2 edge = v.Difference - u.Difference;

                float edgeLengthSq = edge.LengthSquared();
                if (edgeLengthSq < DegenerateEdgeSquared)
                {
                    continue;
                }

                Vec2 candidate = new(edge.Y / MathF.Sqrt(edgeLengthSq), -edge.X / MathF.Sqrt(edgeLengthSq));
                if (Vec2.Dot(candidate, u.Difference) < 0.0f)
                {
                    candidate = candidate * -1.0f;
                }

                float distance = Vec2.Dot(candidate, u.Difference);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    minEdge = i;
                    minNormal = candidate;
                }
            }

            if (minDistance == float.PositiveInfinity)
            {
                break;
            }

            Vec2 supportA = TA.Support(in shapeA, minNormal);
            Vec2 supportB = TB.Support(in shapeB, minNormal * -1.0f);
            Vec2 supportDifference = supportA - supportB;

            float penetration = Vec2.Dot(supportDifference, minNormal) - minDistance;
            if (penetration <= ConvergenceTolerance)
            {
                SimplexKernel2D.Vertex u = ring[minEdge];
                SimplexKernel2D.Vertex v = ring[(minEdge + 1) % count];
                BlendWitnesses(in u, in v, out pointA, out pointB);
                normal = minNormal;
                depth = minDistance;
                return true;
            }

            bestNormal = minNormal;
            bestDistance = minDistance;
            BlendWitnesses(in ring[minEdge], in ring[(minEdge + 1) % count], out bestA, out bestB);
            hasBest = true;

            if (count >= RingCapacity)
            {
                break;
            }

            for (int i = count; i > minEdge + 1; i--)
            {
                ring[i] = ring[i - 1];
            }

            ring[minEdge + 1] = new SimplexKernel2D.Vertex(supportDifference, supportA, supportB);
            count++;
        }

        if (hasBest)
        {
            normal = bestNormal;
            depth = bestDistance;
            pointA = bestA;
            pointB = bestB;
            return true;
        }

        normal = new Vec2(1.0f, 0.0f);
        depth = 0.0f;
        pointA = default;
        pointB = default;
        return true;
    }

    /// <summary>Blends witness pairs along an edge by projection.</summary>
    private static void BlendWitnesses(in SimplexKernel2D.Vertex u, in SimplexKernel2D.Vertex v, out Vec2 pointA, out Vec2 pointB)
    {
        Vec2 edge = v.Difference - u.Difference;
        float denom = Vec2.Dot(edge, edge);
        float t = denom < DegenerateEdgeSquared ? 0.0f : Vec2.Dot(u.Difference * -1.0f, edge) / denom;
        t = Math.Clamp(t, 0.0f, 1.0f);
        pointA = u.PointA + ((v.PointA - u.PointA) * t);
        pointB = u.PointB + ((v.PointB - u.PointB) * t);
    }

    /// <summary>Degenerate touching contact: unit centroid axis, zero depth.</summary>
    private static bool TouchingContact(
        in Vec2 centroidA,
        in Vec2 centroidB,
        out Vec2 normal,
        out float depth,
        out Vec2 pointA,
        out Vec2 pointB)
    {
        Vec2 axis = centroidB - centroidA;
        normal = axis.LengthSquared() < ZeroDirectionEpsilonSquared
            ? new Vec2(1.0f, 0.0f)
            : Vec2.Normalize(axis);
        depth = 0.0f;
        pointA = centroidA;
        pointB = centroidB;
        return true;
    }
}
