namespace Axrone.Physics2D;

/// <summary>
/// Support-mapping contract of a 2D collider: the furthest point, centroid and
/// bounds, all reachable without a virtual call so GJK-style traversals close
/// over the shape type. Deliberately free of an <c>unmanaged</c> constraint:
/// array-backed shapes (polygons) implement this alongside flat ones.
/// </summary>
/// <typeparam name="TSelf">The shape type itself.</typeparam>
public interface ISupportShape2D<TSelf>
    where TSelf : ISupportShape2D<TSelf>
{
    /// <summary>Returns the point of the shape furthest along <paramref name="direction"/>.</summary>
    static abstract Vec2 Support(in TSelf shape, Vec2 direction);

    /// <summary>Returns the area centroid of the shape.</summary>
    static abstract Vec2 ComputeCentroid(in TSelf shape);

    /// <summary>Returns the tight bounds of the shape.</summary>
    static abstract Aabb2D ComputeBounds(in TSelf shape);
}

/// <summary>
/// Mass properties of a shape at unit-agnostic density scale: mass, inertia
/// about the centroid, their inverses (zero for static/zero-mass bodies so
/// the solver never divides), and the centroid they are measured from.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct MassData(float Mass, float InverseMass, float Inertia, float InverseInertia, Vec2 Centroid)
{
    /// <summary>Massless data; every field zero.</summary>
    public static MassData Zero => new(0.0f, 0.0f, 0.0f, 0.0f, default);
}

/// <summary>Solid disc: a center plus a non-negative radius.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Circle2 : ISupportShape2D<Circle2>, IEquatable<Circle2>
{
    /// <summary>Center of the disc.</summary>
    public readonly Vec2 Center;

    /// <summary>Radius; never negative.</summary>
    public readonly float Radius;

    /// <summary>Creates a disc, rejecting a negative radius.</summary>
    /// <param name="center">The center of the disc.</param>
    /// <param name="radius">The radius; must not be negative.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Circle2(Vec2 center, float radius)
    {
        if (radius < 0.0f || float.IsNaN(radius))
        {
            ThrowHelper.ThrowNegativeRadius();
        }

        Center = center;
        Radius = radius;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec2 Support(in Circle2 shape, Vec2 direction)
    {
        if (direction.LengthSquared() < 1e-12f)
        {
            return shape.Center;
        }

        return shape.Center + (Vec2.Normalize(direction) * shape.Radius);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 ComputeCentroid(in Circle2 shape) => shape.Center;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb2D ComputeBounds(in Circle2 shape)
    {
        Vec2 r = new(shape.Radius, shape.Radius);
        return new Aabb2D(shape.Center - r, shape.Center + r);
    }

    /// <summary>Computes mass properties at <paramref name="density"/>: area pi r squared, half-m-r-squared inertia.</summary>
    /// <param name="density">Mass per unit area.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MassData ComputeMass(float density)
    {
        float mass = MathF.PI * Radius * Radius * density;
        if (mass <= 0.0f)
        {
            return MassData.Zero;
        }

        float inertia = 0.5f * mass * Radius * Radius;
        return new MassData(mass, 1.0f / mass, inertia, 1.0f / inertia, Center);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Circle2 other) => Center == other.Center && Radius == other.Radius;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Circle2 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Center, Radius);

    /// <summary>Determines whether two discs share a center and a radius.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Circle2 left, Circle2 right) => left.Equals(right);

    /// <summary>Determines whether two discs differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Circle2 left, Circle2 right) => !left.Equals(right);
}

/// <summary>
/// Stadium shape: a segment swept by a disc. A zero-length segment degrades
/// gracefully to a disc.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Capsule2D : ISupportShape2D<Capsule2D>, IEquatable<Capsule2D>
{
    /// <summary>First cap center.</summary>
    public readonly Vec2 P0;

    /// <summary>Second cap center.</summary>
    public readonly Vec2 P1;

    /// <summary>Cap radius; never negative.</summary>
    public readonly float Radius;

    /// <summary>Creates a capsule, rejecting a negative radius.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Capsule2D(Vec2 p0, Vec2 p1, float radius)
    {
        if (radius < 0.0f || float.IsNaN(radius))
        {
            ThrowHelper.ThrowNegativeRadius();
        }

        P0 = p0;
        P1 = p1;
        Radius = radius;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec2 Support(in Capsule2D shape, Vec2 direction)
    {
        Vec2 offset = MathF.Abs(direction.X) + MathF.Abs(direction.Y) < 1e-12f
            ? Vec2.Zero
            : Vec2.Normalize(direction) * shape.Radius;

        Vec2 a = shape.P0 + offset;
        Vec2 b = shape.P1 + offset;
        return Vec2.Dot(a, direction) >= Vec2.Dot(b, direction) ? a : b;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 ComputeCentroid(in Capsule2D shape) => (shape.P0 + shape.P1) * 0.5f;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb2D ComputeBounds(in Capsule2D shape)
    {
        Vec2 r = new(shape.Radius, shape.Radius);
        return new Aabb2D(Vec2.Min(shape.P0, shape.P1) - r, Vec2.Max(shape.P0, shape.P1) + r);
    }

    /// <summary>Computes mass properties at <paramref name="density"/>: rectangle plus two semicircles.</summary>
    /// <param name="density">Mass per unit area.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MassData ComputeMass(float density)
    {
        Vec2 axis = P1 - P0;
        float length = MathF.Sqrt(axis.LengthSquared());

        float rectArea = 2.0f * Radius * length;
        float capArea = MathF.PI * Radius * Radius;
        float rectMass = rectArea * density;
        float capMass = capArea * density;
        float mass = rectMass + capMass;
        if (mass <= 0.0f)
        {
            return MassData.Zero;
        }

        float diameter = 2.0f * Radius;
        float inertia = (rectMass * ((length * length) + (diameter * diameter)) / 12.0f)
            + (0.5f * capMass * Radius * Radius);
        Vec2 center = (P0 + P1) * 0.5f;
        return new MassData(mass, 1.0f / mass, inertia, 1.0f / inertia, center);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Capsule2D other) => P0 == other.P0 && P1 == other.P1 && Radius == other.Radius;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Capsule2D other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(P0, P1, Radius);

    /// <summary>Determines whether two capsules share segment and radius.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Capsule2D left, Capsule2D right) => left.Equals(right);

    /// <summary>Determines whether two capsules differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Capsule2D left, Capsule2D right) => !left.Equals(right);
}

/// <summary>
/// Strictly convex counter-clockwise polygon: vertices plus precomputed outward
/// edge normals, area centroid and tight bounds. Factories validate convexity
/// (collinear runs rejected) and wind clockwise input into CCW automatically.
/// Treat the arrays as immutable after creation.
/// </summary>
public readonly struct Polygon2D : ISupportShape2D<Polygon2D>, IEquatable<Polygon2D>
{
    private readonly Vec2[] _vertices;
    private readonly Vec2[] _normals;
    private readonly Vec2 _centroid;
    private readonly Aabb2D _bounds;

    /// <summary>Counter-clockwise vertices.</summary>
    public ReadOnlySpan<Vec2> Vertices
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _vertices;
    }

    /// <summary>Unit outward edge normals, one per edge starting at the same index.</summary>
    public ReadOnlySpan<Vec2> Normals
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _normals;
    }

    /// <summary>Area centroid.</summary>
    public Vec2 Centroid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _centroid;
    }

    /// <summary>Tight bounds.</summary>
    public Aabb2D Bounds
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bounds;
    }

    /// <summary>Vertex count.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _vertices.Length;
    }

    private Polygon2D(Vec2[] vertices, Vec2[] normals, Vec2 centroid, Aabb2D bounds)
    {
        _vertices = vertices;
        _normals = normals;
        _centroid = centroid;
        _bounds = bounds;
    }

    /// <summary>Creates an axis-aligned box polygon centered on the origin.</summary>
    /// <param name="halfExtents">Half size per axis.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Polygon2D CreateBox(Vec2 halfExtents)
    {
        Vec2[] vertices =
        [
            new(-halfExtents.X, -halfExtents.Y),
            new(halfExtents.X, -halfExtents.Y),
            new(halfExtents.X, halfExtents.Y),
            new(-halfExtents.X, halfExtents.Y),
        ];
        return Create(vertices);
    }

    /// <summary>Creates a polygon from vertices, winding clockwise input into CCW automatically.</summary>
    /// <param name="vertices">At least three finite vertices forming a strictly convex loop.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Polygon2D Create(ReadOnlySpan<Vec2> vertices)
    {
        if (vertices.Length < 3)
        {
            ThrowHelper.ThrowTooFewVertices();
        }

        Vec2[] owned = vertices.ToArray();
        for (int i = 0; i < owned.Length; i++)
        {
            if (float.IsNaN(owned[i].X) || float.IsNaN(owned[i].Y))
            {
                ThrowHelper.ThrowInvalidVertices();
            }
        }

        float signedArea = 0.0f;
        for (int i = 0; i < owned.Length; i++)
        {
            Vec2 current = owned[i];
            Vec2 next = owned[(i + 1) % owned.Length];
            signedArea += (current.X * next.Y) - (next.X * current.Y);
        }

        if (signedArea < 0.0f)
        {
            Array.Reverse(owned);
            signedArea = -signedArea;
        }

        if (signedArea <= 1e-9f)
        {
            ThrowHelper.ThrowNonConvexPolygon();
        }

        int count = owned.Length;
        for (int i = 0; i < count; i++)
        {
            Vec2 a = owned[i];
            Vec2 b = owned[(i + 1) % count];
            Vec2 c = owned[(i + 2) % count];
            float cross = ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
            if (cross <= 1e-9f)
            {
                ThrowHelper.ThrowNonConvexPolygon();
            }
        }

        Vec2[] normals = new Vec2[count];
        Vec2 min = owned[0];
        Vec2 max = owned[0];
        for (int i = 0; i < count; i++)
        {
            Vec2 a = owned[i];
            Vec2 b = owned[(i + 1) % count];
            Vec2 edge = b - a;
            float length = MathF.Sqrt(edge.LengthSquared());
            normals[i] = new Vec2(edge.Y / length, -edge.X / length);
            min = Vec2.Min(min, a);
            max = Vec2.Max(max, a);
        }

        Vec2 centroid = Vec2.Zero;
        float area = 0.0f;
        for (int i = 0; i < count; i++)
        {
            Vec2 a = owned[i];
            Vec2 b = owned[(i + 1) % count];
            float cross = (a.X * b.Y) - (b.X * a.Y);
            area += cross;
            centroid += (a + b) * cross;
        }

        area *= 0.5f;
        centroid /= 6.0f * area;

        return new Polygon2D(owned, normals, centroid, new Aabb2D(min, max));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec2 Support(in Polygon2D shape, Vec2 direction)
    {
        ReadOnlySpan<Vec2> vertices = shape.Vertices;
        Vec2 best = vertices[0];
        float bestDot = Vec2.Dot(best, direction);
        for (int i = 1; i < vertices.Length; i++)
        {
            float dot = Vec2.Dot(vertices[i], direction);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = vertices[i];
            }
        }

        return best;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 ComputeCentroid(in Polygon2D shape) => shape.Centroid;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb2D ComputeBounds(in Polygon2D shape) => shape.Bounds;

    /// <summary>Computes mass properties at <paramref name="density"/> via the shoelace area and edge inertia sum.</summary>
    /// <param name="density">Mass per unit area.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MassData ComputeMass(float density)
    {
        ReadOnlySpan<Vec2> vertices = Vertices;
        int count = vertices.Length;

        float area2 = 0.0f;
        Vec2 centroidAccum = Vec2.Zero;
        float inertiaOrigin = 0.0f;

        for (int i = 0; i < count; i++)
        {
            Vec2 a = vertices[i];
            Vec2 b = vertices[(i + 1) % count];
            float cross = (a.X * b.Y) - (b.X * a.Y);
            area2 += cross;
            centroidAccum += (a + b) * cross;
            inertiaOrigin += cross * (Vec2.Dot(a, a) + Vec2.Dot(a, b) + Vec2.Dot(b, b));
        }

        float area = area2 * 0.5f;
        float mass = area * density;
        if (mass <= 0.0f)
        {
            return MassData.Zero;
        }

        Vec2 centroid = centroidAccum / (3.0f * area2);
        float inertia = ((density * inertiaOrigin) / 12.0f) - (mass * Vec2.Dot(centroid, centroid));
        return new MassData(mass, 1.0f / mass, inertia, 1.0f / inertia, centroid);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Polygon2D other)
    {
        if (Vertices.Length != other.Vertices.Length)
        {
            return false;
        }

        for (int i = 0; i < Vertices.Length; i++)
        {
            if (Vertices[i] != other.Vertices[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Polygon2D other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Vertices.Length);
        for (int i = 0; i < Vertices.Length; i++)
        {
            hash.Add(Vertices[i]);
        }

        return hash.ToHashCode();
    }

    /// <summary>Determines whether two polygons share the same vertex loop.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Polygon2D left, Polygon2D right) => left.Equals(right);

    /// <summary>Determines whether two polygons differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Polygon2D left, Polygon2D right) => !left.Equals(right);
}
