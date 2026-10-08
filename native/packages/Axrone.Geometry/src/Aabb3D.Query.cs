namespace Axrone.Geometry;

using System.Globalization;

/// <summary>
/// Query, kinematics and text surface of <see cref="Aabb3D"/>: projection onto a plane,
/// closest-point/distance evaluation, ray and sphere tests, minimum-translation
/// penetration, swept collision, corner enumeration, transform and the
/// <see cref="ISpanFormattable"/>/<see cref="ISpanParsable{T}"/> contract.
/// </summary>
public readonly partial struct Aabb3D : ISpanFormattable, ISpanParsable<Aabb3D>
{
    /// <summary>Flattens the box onto the plane perpendicular to <paramref name="dropAxis"/>.</summary>
    /// <param name="dropAxis">The axis collapsed by the projection; the other two form the 2D box.</param>
    /// <exception cref="ArgumentOutOfRangeException">The axis is not one of the three coordinate axes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Aabb2D ProjectTo2D(Axis dropAxis) =>
        dropAxis switch
        {
            Axis.X => new Aabb2D(Min.Y, Min.Z, Max.Y, Max.Z),
            Axis.Y => new Aabb2D(Min.X, Min.Z, Max.X, Max.Z),
            Axis.Z => new Aabb2D(Min.X, Min.Y, Max.X, Max.Y),
            _ => ThrowHelper.ThrowInvalidAxis<Aabb2D>(),
        };

    /// <summary>
    /// Returns the point of the box nearest <paramref name="point"/>, which is the clamped
    /// point when the point is outside and the point itself when it is inside.
    /// </summary>
    /// <param name="point">The point to snap onto the box.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Vec3 ClosestPoint(in Vec3 point) => Vec3.Clamp(point, Min, Max);

    /// <summary>Squared distance from <paramref name="point"/> to the box; zero when the point is inside.</summary>
    /// <param name="point">The point to measure from.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float DistanceSquared(in Vec3 point)
    {
        Vec3 c = ClosestPoint(in point);
        return Vec3.DistanceSquared(point, c);
    }

    /// <summary>Distance from <paramref name="point"/> to the box; zero when the point is inside.</summary>
    /// <param name="point">The point to measure from.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float Distance(in Vec3 point) => MathF.Sqrt(DistanceSquared(in point));

    /// <summary>
    /// Tests the ray against the box with no distance limit, accepting the first contact in
    /// front of the origin.
    /// </summary>
    /// <param name="ray">The ray to test; its reciprocal direction must be the cached one.</param>
    /// <param name="distance">
    /// The distance to the first contact, or zero when the ray starts inside the box.
    /// </param>
    /// <returns><see langword="true"/> if the ray reaches the box.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Intersects(in Ray3D ray, out float distance) => Intersects(in ray, float.PositiveInfinity, out distance);

    /// <summary>
    /// Tests the ray against the box with the scalar slab method: each axis contributes an
    /// interval, and the ray hits when those intervals overlap within
    /// <paramref name="maxDistance"/>. The reciprocal direction is read from
    /// <see cref="Ray3D.InvDirection"/>, which the ray guards against a zero or
    /// near-parallel component, so no division happens here.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="maxDistance">The furthest distance along the ray that still counts as a hit.</param>
    /// <param name="distance">
    /// The distance to the first contact, or zero when the ray starts inside the box.
    /// </param>
    /// <returns><see langword="true"/> if the ray reaches the box within the limit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(in Ray3D ray, float maxDistance, out float distance)
    {
        float t1x = (Min.X - ray.Origin.X) * ray.InvDirection.X;
        float t2x = (Max.X - ray.Origin.X) * ray.InvDirection.X;
        float t1y = (Min.Y - ray.Origin.Y) * ray.InvDirection.Y;
        float t2y = (Max.Y - ray.Origin.Y) * ray.InvDirection.Y;
        float t1z = (Min.Z - ray.Origin.Z) * ray.InvDirection.Z;
        float t2z = (Max.Z - ray.Origin.Z) * ray.InvDirection.Z;

        float tminx = MathF.Min(t1x, t2x);
        float tmaxx = MathF.Max(t1x, t2x);
        float tminy = MathF.Min(t1y, t2y);
        float tmaxy = MathF.Max(t1y, t2y);
        float tminz = MathF.Min(t1z, t2z);
        float tmaxz = MathF.Max(t1z, t2z);

        float tNear = MathF.Max(tminx, MathF.Max(tminy, tminz));
        float tFar = MathF.Min(tmaxx, MathF.Min(tmaxy, tmaxz));

        if (tFar >= MathF.Max(tNear, 0.0F) && tNear <= maxDistance)
        {
            distance = tNear < 0.0F ? 0.0F : tNear;
            return true;
        }

        distance = 0.0F;
        return false;
    }

    /// <summary>Determines whether the sphere overlaps the box; touching counts as an overlap.</summary>
    /// <param name="sphere">The sphere to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Intersects(in BoundingSphere3 sphere) => DistanceSquared(sphere.Center) <= (sphere.Radius * sphere.Radius);

    /// <summary>
    /// Determines whether the box reaches the plane, using the positive vertex (the corner
    /// furthest along the normal) and the negative vertex. A box whose positive vertex is
    /// already behind the plane is a miss; otherwise the box touches the plane exactly when
    /// its negative vertex is not strictly in front of it.
    /// </summary>
    /// <param name="plane">The plane to test against.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Intersects(in Plane plane)
    {
        float nx = plane.Normal.X;
        float ny = plane.Normal.Y;
        float nz = plane.Normal.Z;
        float d = plane.D;

        float px = nx >= 0.0F ? Max.X : Min.X;
        float py = ny >= 0.0F ? Max.Y : Min.Y;
        float pz = nz >= 0.0F ? Max.Z : Min.Z;

        if ((px * nx) + (py * ny) + (pz * nz) + d < 0.0F)
        {
            return false;
        }

        float qx = nx >= 0.0F ? Min.X : Max.X;
        float qy = ny >= 0.0F ? Min.Y : Max.Y;
        float qz = nz >= 0.0F ? Min.Z : Max.Z;

        return ((qx * nx) + (qy * ny) + (qz * nz) + d) <= 0.0F;
    }

    /// <summary>
    /// Computes the smallest translation that separates this box from
    /// <paramref name="other"/>. Only overlapping boxes can penetrate, so a disjoint pair
    /// reports <see langword="false"/>.
    /// </summary>
    /// <param name="other">The box to resolve against.</param>
    /// <param name="normal">
    /// The unit axis-aligned direction this box moves along to separate; <see cref="Vec3.Zero"/>
    /// on a miss.
    /// </param>
    /// <param name="depth">The distance to move along <paramref name="normal"/>; zero on a miss.</param>
    /// <returns><see langword="true"/> if the boxes overlap.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ComputePenetration(in Aabb3D other, out Vec3 normal, out float depth)
    {
        normal = Vec3.Zero;
        depth = 0.0F;

        if (!Overlaps(in other))
        {
            return false;
        }

        Vec3 delta1 = Max - other.Min;
        Vec3 delta2 = other.Max - Min;

        float px = delta1.X < delta2.X ? delta1.X : delta2.X;
        float py = delta1.Y < delta2.Y ? delta1.Y : delta2.Y;
        float pz = delta1.Z < delta2.Z ? delta1.Z : delta2.Z;

        float ax = delta1.X < delta2.X ? delta1.X : delta2.X;
        float ay = delta1.Y < delta2.Y ? delta1.Y : delta2.Y;
        float az = delta1.Z < delta2.Z ? delta1.Z : delta2.Z;

        if (ax <= ay && ax <= az)
        {
            depth = ax;
            normal = new Vec3((float)MathF.Sign(px), 0.0F, 0.0F);
        }
        else if (ay <= ax && ay <= az)
        {
            depth = ay;
            normal = new Vec3(0.0F, (float)MathF.Sign(py), 0.0F);
        }
        else
        {
            depth = az;
            normal = new Vec3(0.0F, 0.0F, (float)MathF.Sign(pz));
        }

        return true;
    }

    /// <summary>
    /// Sweeps this box along <paramref name="velocity"/> against a static
    /// <paramref name="target"/> and returns the normalized time of impact. The moving box
    /// is collapsed to a ray from its centre and the target is grown by the mover's extents,
    /// which reduces the swept test to the slab test.
    /// </summary>
    /// <param name="target">The static box to sweep against.</param>
    /// <param name="velocity">The world-space travel of this box per unit of time.</param>
    /// <param name="hit">The impact record on a hit; <see langword="default"/> on a miss.</param>
    /// <returns><see langword="true"/> if the swept box reaches the target.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Sweep(in Aabb3D target, in Vec3 velocity, out SweepHit3 hit)
    {
        float speedSq = velocity.LengthSquared();

        if (speedSq <= 0.0F)
        {
            if (Overlaps(in target))
            {
                hit = new SweepHit3(0.0F, Vec3.Zero, Center);
                return true;
            }

            hit = default;
            return false;
        }

        Vec3 extents = Extents;
        Aabb3D expandedTarget = new(target.Min - extents, target.Max + extents);

        Ray3D ray = new(Center, velocity);
        float speed = MathF.Sqrt(speedSq);

        if (!expandedTarget.Intersects(in ray, speed, out float distance))
        {
            hit = default;
            return false;
        }

        float normalizedTime = distance / speed;
        Vec3 hitPoint = ray.At(distance);
        Vec3 hitOffset = hitPoint - target.Center;
        Vec3 targetExtents = target.Extents;

        Vec3 normal = Vec3.Zero;
        if (MathF.Abs(hitOffset.X) <= targetExtents.X)
        {
            normal = new Vec3(0.0F, hitOffset.Y > 0.0F ? -1.0F : 1.0F, 0.0F);
        }
        else if (MathF.Abs(hitOffset.Y) <= targetExtents.Y)
        {
            normal = new Vec3(hitOffset.X > 0.0F ? -1.0F : 1.0F, 0.0F, 0.0F);
        }
        else
        {
            normal = new Vec3(0.0F, 0.0F, hitOffset.Z > 0.0F ? -1.0F : 1.0F);
        }

        hit = new SweepHit3(normalizedTime, normal, hitPoint);
        return true;
    }

    /// <summary>
    /// Writes the eight corners of the box into <paramref name="destination"/>, the lower
    /// corner first.
    /// </summary>
    /// <param name="destination">The span that receives the eight corners.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than eight elements.</exception>
    public void GetCorners(Span<Vec3> destination)
    {
        if (destination.Length < 8)
        {
            ThrowHelper.ThrowDestinationTooSmall();
            return;
        }

        destination[0] = new Vec3(Min.X, Min.Y, Min.Z);
        destination[1] = new Vec3(Max.X, Min.Y, Min.Z);
        destination[2] = new Vec3(Min.X, Max.Y, Min.Z);
        destination[3] = new Vec3(Max.X, Max.Y, Min.Z);
        destination[4] = new Vec3(Min.X, Min.Y, Max.Z);
        destination[5] = new Vec3(Max.X, Min.Y, Max.Z);
        destination[6] = new Vec3(Min.X, Max.Y, Max.Z);
        destination[7] = new Vec3(Max.X, Max.Y, Max.Z);
    }

    /// <summary>
    /// Returns the axis-aligned box that encloses this box after the matrix is applied. The
    /// centre is transformed exactly and the half-sizes are scaled by the absolute column
    /// lengths, which is the tight bound for any linear map.
    /// </summary>
    /// <param name="matrix">The transform to apply, in the row-vector convention of <see cref="Matrix4x4"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Aabb3D Transform(in Matrix4x4 matrix)
    {
        Vec3 center = Center;
        Vec3 extents = Extents;

        Vec3 newCenter = new(
            (matrix.M11 * center.X) + (matrix.M21 * center.Y) + (matrix.M31 * center.Z) + matrix.M41,
            (matrix.M12 * center.X) + (matrix.M22 * center.Y) + (matrix.M32 * center.Z) + matrix.M42,
            (matrix.M13 * center.X) + (matrix.M23 * center.Y) + (matrix.M33 * center.Z) + matrix.M43);

        Vec3 newExtents = new(
            (MathF.Abs(matrix.M11) * extents.X) + (MathF.Abs(matrix.M21) * extents.Y) + (MathF.Abs(matrix.M31) * extents.Z),
            (MathF.Abs(matrix.M12) * extents.X) + (MathF.Abs(matrix.M22) * extents.Y) + (MathF.Abs(matrix.M32) * extents.Z),
            (MathF.Abs(matrix.M13) * extents.X) + (MathF.Abs(matrix.M23) * extents.Y) + (MathF.Abs(matrix.M33) * extents.Z));

        return new Aabb3D(newCenter - newExtents, newCenter + newExtents);
    }

    /// <inheritdoc/>
    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    /// <summary>Formats the corners using the given format and provider.</summary>
    /// <param name="format">The component format, or <see langword="null"/> for the default.</param>
    /// <param name="formatProvider">The culture that formats the components.</param>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        $"Aabb3D(Min={Min.ToString(format, formatProvider)}, Max={Max.ToString(format, formatProvider)})";

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        destination.TryWrite(provider, $"Aabb3D(Min={Min}, Max={Max})", out charsWritten);

    /// <summary>Parses a box from <c>Aabb3D(&lt;x, y, z&gt;;&lt;x, y, z&gt;)</c> using the invariant culture.</summary>
    /// <param name="s">The text to parse.</param>
    /// <exception cref="FormatException">The text is not a well-formed box.</exception>
    public static Aabb3D Parse(string s) => Parse(s, CultureInfo.InvariantCulture);

    /// <summary>Parses a box from <c>Aabb3D(&lt;x, y, z&gt;;&lt;x, y, z&gt;)</c>.</summary>
    /// <param name="s">The text to parse.</param>
    /// <param name="provider">The culture that parses the components.</param>
    /// <exception cref="FormatException">The text is not a well-formed box.</exception>
    public static Aabb3D Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out Aabb3D result))
        {
            return result;
        }

        ThrowHelper.ThrowInvalidFormatException();
        return default;
    }

    /// <summary>Parses a box from <c>Aabb3D(&lt;x, y, z&gt;;&lt;x, y, z&gt;)</c>.</summary>
    /// <param name="s">The text to parse.</param>
    /// <param name="provider">The culture that parses the components.</param>
    /// <exception cref="FormatException">The text is not a well-formed box.</exception>
    public static Aabb3D Parse(string s, IFormatProvider? provider)
    {
        if (TryParse(s, provider, out Aabb3D result))
        {
            return result;
        }

        ThrowHelper.ThrowInvalidFormatException();
        return default;
    }

    /// <summary>Attempts to parse a box from <paramref name="s"/>.</summary>
    /// <param name="s">The text to parse.</param>
    /// <param name="provider">The culture that parses the components.</param>
    /// <param name="result">The parsed box, or <see langword="default"/> on failure.</param>
    /// <returns><see langword="true"/> if the text is a well-formed box.</returns>
    public static bool TryParse(string? s, IFormatProvider? provider, out Aabb3D result) => TryParse(s.AsSpan(), provider, out result);

    /// <summary>Attempts to parse a box from <c>Aabb3D(&lt;x, y, z&gt;;&lt;x, y, z&gt;)</c>.</summary>
    /// <param name="s">The text to parse.</param>
    /// <param name="provider">The culture that parses the components.</param>
    /// <param name="result">The parsed box, or <see langword="default"/> on failure.</param>
    /// <returns><see langword="true"/> if the text is a well-formed box.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Aabb3D result)
    {
        provider ??= CultureInfo.CurrentCulture;
        s = s.Trim();

        if (!s.StartsWith("Aabb3D(", StringComparison.Ordinal) || !s.EndsWith(")", StringComparison.Ordinal) || s.Length < 8)
        {
            result = default;
            return false;
        }

        ReadOnlySpan<char> body = s["Aabb3D(".Length..^1];
        int separator = body.IndexOf(';');

        if (separator < 0 ||
            !TryParseVector3(body[..separator], provider, out Vec3 min) ||
            !TryParseVector3(body[(separator + 1)..], provider, out Vec3 max))
        {
            result = default;
            return false;
        }

        result = new Aabb3D(min, max);
        return true;
    }

    /// <summary>Attempts to parse one corner, delegating the component grammar to <see cref="Vec3"/>.</summary>
    /// <param name="s">The corner text, brackets optional.</param>
    /// <param name="provider">The culture that parses the components.</param>
    /// <param name="value">The parsed corner, or <see langword="default"/> on failure.</param>
    /// <returns><see langword="true"/> if the text is a well-formed corner.</returns>
    private static bool TryParseVector3(ReadOnlySpan<char> s, IFormatProvider? provider, out Vec3 value)
    {
        s = s.Trim();

        if (s.Length >= 2 && s[0] == '<' && s[^1] == '>')
        {
            s = s[1..^1];
        }

        return Vec3.TryParse(s, provider, out value);
    }
}