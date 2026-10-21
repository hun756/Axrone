namespace Axrone.Shapes2D;

/// <summary>Ring of points with an explicit winding.</summary>
public readonly record struct PolygonRing
{
    /// <summary>Ring points.</summary>
    public ImmutableArray<Vec2> Points { get; }

    /// <summary>Ring winding.</summary>
    public PolygonWinding Winding { get; }

    /// <summary>Creates a ring.</summary>
    public PolygonRing(ReadOnlySpan<Vec2> points, PolygonWinding winding)
    {
        if (points.Length < 3)
        {
            ThrowHelper.ThrowShapeValidation("Polygon ring must contain at least 3 points.");
        }

        Points = ImmutableCollectionsMarshal.AsImmutableArray(points.ToArray());
        Winding = winding;
    }
}

/// <summary>Zero-allocation polygon analysis.</summary>
public static class PolygonMath
{
    /// <summary>Signed area (CCW positive).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double SignedArea(ReadOnlySpan<Vec2> points)
    {
        if (points.Length < 3)
        {
            return 0.0;
        }

        double area = 0.0;
        ref Vec2 baseRef = ref MemoryMarshal.GetReference(points);
        nuint length = (nuint)points.Length;

        for (nuint i = 0; i < length; i++)
        {
            nuint next = (i + 1u) % length;
            ref readonly Vec2 p1 = ref Unsafe.Add(ref baseRef, i);
            ref readonly Vec2 p2 = ref Unsafe.Add(ref baseRef, next);
            area += (double)p1.X * p2.Y - (double)p1.Y * p2.X;
        }

        return area * 0.5;
    }

    /// <summary>Absolute area.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double AbsoluteArea(ReadOnlySpan<Vec2> points) => Math.Abs(SignedArea(points));

    /// <summary>Closed perimeter.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float Perimeter(ReadOnlySpan<Vec2> points)
    {
        if (points.Length < 2)
        {
            return 0f;
        }

        float perimeter = 0f;
        ref Vec2 baseRef = ref MemoryMarshal.GetReference(points);
        nuint length = (nuint)points.Length;

        for (nuint i = 0; i < length; i++)
        {
            nuint next = (i + 1u) % length;
            ref readonly Vec2 p1 = ref Unsafe.Add(ref baseRef, i);
            ref readonly Vec2 p2 = ref Unsafe.Add(ref baseRef, next);
            perimeter += (p2 - p1).Length();
        }

        return perimeter;
    }

    /// <summary>Area centroid (falls back to mean on degenerate rings).</summary>
    public static Vec2 Centroid(ReadOnlySpan<Vec2> points)
    {
        if (points.Length == 0)
        {
            return Vec2.Zero;
        }

        if (points.Length < 3)
        {
            return points[0];
        }

        double cx = 0.0;
        double cy = 0.0;
        double signedAreaTimesSix = 0.0;
        ref Vec2 baseRef = ref MemoryMarshal.GetReference(points);
        nuint length = (nuint)points.Length;

        for (nuint i = 0; i < length; i++)
        {
            nuint next = (i + 1u) % length;
            ref readonly Vec2 p0 = ref Unsafe.Add(ref baseRef, i);
            ref readonly Vec2 p1 = ref Unsafe.Add(ref baseRef, next);
            double cross = (double)p0.X * p1.Y - (double)p1.X * p0.Y;
            cx += (p0.X + p1.X) * cross;
            cy += (p0.Y + p1.Y) * cross;
            signedAreaTimesSix += cross;
        }

        if (Math.Abs(signedAreaTimesSix) <= ShapeConstants.DoubleEpsilon)
        {
            double sx = 0.0;
            double sy = 0.0;
            for (nuint i = 0; i < length; i++)
            {
                ref readonly Vec2 p = ref Unsafe.Add(ref baseRef, i);
                sx += p.X;
                sy += p.Y;
            }

            return new Vec2((float)(sx / length), (float)(sy / length));
        }

        return new Vec2((float)(cx / (signedAreaTimesSix * 3.0)), (float)(cy / (signedAreaTimesSix * 3.0)));
    }

    /// <summary>Bounds of points.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ShapeBounds ComputeBounds(ReadOnlySpan<Vec2> points)
    {
        if (points.Length == 0)
        {
            return ShapeBounds.Empty;
        }

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        ref Vec2 baseRef = ref MemoryMarshal.GetReference(points);
        nuint length = (nuint)points.Length;

        for (nuint i = 0; i < length; i++)
        {
            ref readonly Vec2 p = ref Unsafe.Add(ref baseRef, i);
            if (p.X < minX)
            {
                minX = p.X;
            }

            if (p.Y < minY)
            {
                minY = p.Y;
            }

            if (p.X > maxX)
            {
                maxX = p.X;
            }

            if (p.Y > maxY)
            {
                maxY = p.Y;
            }
        }

        return ShapeBounds.Create(minX, minY, maxX, maxY);
    }

    /// <summary>Ray casting point-in-polygon.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool PointInPolygon(ReadOnlySpan<Vec2> points, Vec2 point)
    {
        if (points.Length < 3)
        {
            return false;
        }

        bool inside = false;
        nuint length = (nuint)points.Length;
        nuint j = length - 1u;

        ref Vec2 baseRef = ref MemoryMarshal.GetReference(points);
        for (nuint i = 0; i < length; i++)
        {
            ref readonly Vec2 pi = ref Unsafe.Add(ref baseRef, i);
            ref readonly Vec2 pj = ref Unsafe.Add(ref baseRef, j);

            if ((pi.Y > point.Y) != (pj.Y > point.Y) &&
                point.X < ((pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y == 0f ? ShapeConstants.Epsilon : pj.Y - pi.Y)) + pi.X)
            {
                inside = !inside;
            }

            j = i;
        }

        return inside;
    }

    /// <summary>Point-in-polygon with holes.</summary>
    public static bool PointInPolygonWithHoles(ReadOnlySpan<Vec2> outer, ReadOnlySpan<PolygonRing> holes, Vec2 point)
    {
        if (!PointInPolygon(outer, point))
        {
            return false;
        }

        nuint holeLen = (nuint)holes.Length;
        ref PolygonRing baseHoles = ref MemoryMarshal.GetReference(holes);
        for (nuint i = 0; i < holeLen; i++)
        {
            if (PointInPolygon(Unsafe.Add(ref baseHoles, i).Points.AsSpan(), point))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Convex-only point test.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool PointInConvexPolygon(ReadOnlySpan<Vec2> points, Vec2 point)
    {
        if (points.Length < 3)
        {
            return false;
        }

        float winding = SignedArea(points) >= 0.0 ? 1f : -1f;

        ref Vec2 baseRef = ref MemoryMarshal.GetReference(points);
        nuint length = (nuint)points.Length;
        for (nuint i = 0; i < length; i++)
        {
            nuint next = (i + 1u) % length;
            ref readonly Vec2 cur = ref Unsafe.Add(ref baseRef, i);
            ref readonly Vec2 nxt = ref Unsafe.Add(ref baseRef, next);

            float edgeX = nxt.X - cur.X;
            float edgeY = nxt.Y - cur.Y;
            float pointX = point.X - cur.X;
            float pointY = point.Y - cur.Y;
            float cross = edgeX * pointY - edgeY * pointX;

            if (cross * winding < -ShapeConstants.Epsilon)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether the ring is convex.</summary>
    public static bool IsConvexPolygon(ReadOnlySpan<Vec2> points)
    {
        if (points.Length < 3)
        {
            return false;
        }

        nuint length = (nuint)points.Length;
        int sign = 0;

        ref Vec2 baseRef = ref MemoryMarshal.GetReference(points);
        for (nuint i = 0; i < length; i++)
        {
            nuint bIdx = (i + 1u) % length;
            nuint cIdx = (i + 2u) % length;
            ref readonly Vec2 a = ref Unsafe.Add(ref baseRef, i);
            ref readonly Vec2 b = ref Unsafe.Add(ref baseRef, bIdx);
            ref readonly Vec2 c = ref Unsafe.Add(ref baseRef, cIdx);

            float cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            if (MathF.Abs(cross) <= ShapeConstants.Epsilon)
            {
                continue;
            }

            int currentSign = cross > 0f ? 1 : -1;
            if (sign == 0)
            {
                sign = currentSign;
            }
            else if (sign != currentSign)
            {
                return false;
            }
        }

        return sign != 0;
    }

    /// <summary>Whether the ring is simple (no self-intersection).</summary>
    public static bool IsSimplePolygon(ReadOnlySpan<Vec2> points)
    {
        int length = points.Length;
        if (length < 3)
        {
            return false;
        }

        ref Vec2 baseRef = ref MemoryMarshal.GetReference(points);
        for (int i = 0; i < length; i++)
        {
            ref readonly Vec2 pa1 = ref Unsafe.Add(ref baseRef, i);
            ref readonly Vec2 pa2 = ref Unsafe.Add(ref baseRef, (i + 1) % length);

            for (int j = i + 1; j < length; j++)
            {
                if (i == 0 && j == length - 1)
                {
                    continue;
                }

                if (j == i + 1)
                {
                    continue;
                }

                ref readonly Vec2 pb1 = ref Unsafe.Add(ref baseRef, j);
                ref readonly Vec2 pb2 = ref Unsafe.Add(ref baseRef, (j + 1) % length);

                if (SegmentsIntersect(pa1, pa2, pb1, pb2))
                {
                    return false;
                }
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SegmentsIntersect(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        float r1 = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        float r2 = (b.X - a.X) * (d.Y - a.Y) - (b.Y - a.Y) * (d.X - a.X);
        float r3 = (d.X - c.X) * (a.Y - c.Y) - (d.Y - c.Y) * (a.X - c.X);
        float r4 = (d.X - c.X) * (b.Y - c.Y) - (d.Y - c.Y) * (b.X - c.X);

        if (((r1 > 0f && r2 < 0f) || (r1 < 0f && r2 > 0f)) &&
            ((r3 > 0f && r4 < 0f) || (r3 < 0f && r4 > 0f)))
        {
            return true;
        }

        if (MathF.Abs(r1) <= ShapeConstants.Epsilon && OnSegment(a, c, b))
        {
            return true;
        }

        if (MathF.Abs(r2) <= ShapeConstants.Epsilon && OnSegment(a, d, b))
        {
            return true;
        }

        if (MathF.Abs(r3) <= ShapeConstants.Epsilon && OnSegment(c, a, d))
        {
            return true;
        }

        if (MathF.Abs(r4) <= ShapeConstants.Epsilon && OnSegment(c, b, d))
        {
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool OnSegment(Vec2 p, Vec2 q, Vec2 r) =>
        q.X <= MathF.Max(p.X, r.X) + ShapeConstants.Epsilon &&
        q.X >= MathF.Min(p.X, r.X) - ShapeConstants.Epsilon &&
        q.Y <= MathF.Max(p.Y, r.Y) + ShapeConstants.Epsilon &&
        q.Y >= MathF.Min(p.Y, r.Y) - ShapeConstants.Epsilon;

    /// <summary>Distance from a point to the ring edge.</summary>
    public static float PointToPolygonEdgeDistance(ReadOnlySpan<Vec2> points, Vec2 point)
    {
        if (points.Length < 2)
        {
            return float.PositiveInfinity;
        }

        float minDistanceSq = float.PositiveInfinity;
        ref Vec2 baseRef = ref MemoryMarshal.GetReference(points);
        nuint length = (nuint)points.Length;

        for (nuint i = 0; i < length; i++)
        {
            nuint next = (i + 1u) % length;
            ref readonly Vec2 a = ref Unsafe.Add(ref baseRef, i);
            ref readonly Vec2 b = ref Unsafe.Add(ref baseRef, next);

            float d = DistanceToSegmentSquared(point, a, b);
            if (d < minDistanceSq)
            {
                minDistanceSq = d;
            }
        }

        return MathF.Sqrt(minDistanceSq);
    }

    /// <summary>Squared distance from a point to a segment.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceToSegmentSquared(Vec2 p, Vec2 a, Vec2 b)
    {
        float abx = b.X - a.X;
        float aby = b.Y - a.Y;
        float abLenSq = abx * abx + aby * aby;
        if (abLenSq <= ShapeConstants.Epsilon)
        {
            float dx = p.X - a.X;
            float dy = p.Y - a.Y;
            return dx * dx + dy * dy;
        }

        float t = Math.Clamp(((p.X - a.X) * abx + (p.Y - a.Y) * aby) / abLenSq, 0f, 1f);
        float cx = a.X + abx * t;
        float cy = a.Y + aby * t;
        float diffX = p.X - cx;
        float diffY = p.Y - cy;
        return diffX * diffX + diffY * diffY;
    }

    /// <summary>Removes collinear vertices.</summary>
    public static Vec2[] RemoveCollinearVertices(ReadOnlySpan<Vec2> points)
    {
        int count = points.Length;
        if (count < 3)
        {
            return points.ToArray();
        }

        var result = new List<Vec2>(count);
        for (int i = 0; i < count; i++)
        {
            ref readonly Vec2 a = ref points[i];
            ref readonly Vec2 b = ref points[(i + 1) % count];
            ref readonly Vec2 c = ref points[(i + 2) % count];

            float cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            if (MathF.Abs(cross) > ShapeConstants.Epsilon)
            {
                result.Add(b);
            }
        }

        return result.ToArray();
    }

    /// <summary>Deduplicates consecutive vertices.</summary>
    public static Vec2[] DeduplicateVertices(ReadOnlySpan<Vec2> points, float epsilon = ShapeConstants.Epsilon)
    {
        int count = points.Length;
        if (count < 2)
        {
            return points.ToArray();
        }

        float epsSq = epsilon * epsilon;
        var result = new List<Vec2>(count) { points[0] };

        for (int i = 1; i < count; i++)
        {
            ref readonly Vec2 cur = ref points[i];
            Vec2 last = result[^1];
            float dx = cur.X - last.X;
            float dy = cur.Y - last.Y;
            if (dx * dx + dy * dy > epsSq)
            {
                result.Add(cur);
            }
        }

        if (result.Count > 1)
        {
            Vec2 first = result[0];
            Vec2 last = result[^1];
            float dx = first.X - last.X;
            float dy = first.Y - last.Y;
            if (dx * dx + dy * dy <= epsSq)
            {
                result.RemoveAt(result.Count - 1);
            }
        }

        return result.ToArray();
    }
}
