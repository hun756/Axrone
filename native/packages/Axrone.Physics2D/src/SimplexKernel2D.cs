namespace Axrone.Physics2D;

/// <summary>
/// Closest-feature primitives over Minkowski-difference simplices in 2D,
/// shared by the EPA ring. Internal because the witness layouts are an
/// implementation detail; the public surface is <see cref="Gjk2D"/> and the
/// penetration queries.
/// </summary>
internal static class SimplexKernel2D
{
    /// <summary>One Minkowski-difference vertex with its witness pair.</summary>
    internal readonly struct Vertex
    {
        public readonly Vec2 Difference;
        public readonly Vec2 PointA;
        public readonly Vec2 PointB;

        public Vertex(Vec2 difference, Vec2 pointA, Vec2 pointB)
        {
            Difference = difference;
            PointA = pointA;
            PointB = pointB;
        }
    }

    /// <summary>Closest point on edge (u, v) to the origin with barycentric weights.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void ClosestOnEdge(in Vertex u, in Vertex v, out Vec2 closest, out float w1, out float w2)
    {
        Vec2 edge = v.Difference - u.Difference;
        float denom = Vec2.Dot(edge, edge);
        if (denom < 1e-12f)
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

    /// <summary>Whether the origin's closest simplex point is the vertex itself.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsVertexClosest(Vec2 vertex, Vec2 neighborA, Vec2 neighborB) =>
        Vec2.Dot(vertex, vertex - neighborA) <= 0.0f && Vec2.Dot(vertex, vertex - neighborB) <= 0.0f;

    /// <summary>Whether the origin lies strictly inside the triangle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool OriginInTriangle(Vec2 a, Vec2 b, Vec2 c)
    {
        float s1 = ((b.X - a.X) * -a.Y) - ((b.Y - a.Y) * -a.X);
        float s2 = ((c.X - b.X) * -b.Y) - ((c.Y - b.Y) * -b.X);
        float s3 = ((a.X - c.X) * -c.Y) - ((a.Y - c.Y) * -c.X);
        return (s1 > 0.0f && s2 > 0.0f && s3 > 0.0f) || (s1 < 0.0f && s2 < 0.0f && s3 < 0.0f);
    }
}
