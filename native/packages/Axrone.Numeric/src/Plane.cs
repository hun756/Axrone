namespace Axrone.Numeric;

/// <summary>
/// A plane in Hessian normal form: all points <c>p</c> with
/// <c>Dot(Normal, p) + D == 0</c> lie on the plane. Mirrors the
/// System.Numerics semantics so frustum and clipping code ports verbatim.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Plane : IEquatable<Plane>, ISpanFormattable
{
    /// <summary>The unit-length normal of the plane.</summary>
    public readonly Vec3 Normal;

    /// <summary>The signed distance of the plane from the origin along <see cref="Normal"/>.</summary>
    public readonly float D;

    /// <summary>Creates a plane from a normal and a constant term.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Plane(Vec3 normal, float d)
    {
        Normal = normal;
        D = d;
    }

    /// <summary>Creates a plane from its four components.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Plane(float x, float y, float z, float d)
    {
        Normal = new Vec3(x, y, z);
        D = d;
    }

    /// <summary>Creates the plane through three counter-clockwise points.</summary>
    /// <param name="point1">The first point.</param>
    /// <param name="point2">The second point.</param>
    /// <param name="point3">The third point.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Plane CreateFromVertices(Vec3 point1, Vec3 point2, Vec3 point3)
    {
        Vec3 normal = Vec3.Normalize(Vec3.Cross(point2 - point1, point3 - point1));
        return new Plane(normal, -Vec3.Dot(normal, point1));
    }

    /// <summary>
    /// Scales the plane so <see cref="Normal"/> is unit length. A degenerate
    /// plane (zero normal) is returned unchanged.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Plane Normalize(Plane value)
    {
        float lengthSquared = value.Normal.LengthSquared();
        if (lengthSquared > 1e-12f)
        {
            float invLength = 1.0f / MathF.Sqrt(lengthSquared);
            return new Plane(value.Normal * invLength, value.D * invLength);
        }

        return value;
    }

    /// <summary>Computes <c>Dot(Normal, value.Xyz) + D * value.W</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Plane plane, Vec4 value) =>
        Vec3.Dot(plane.Normal, new Vec3(value.X, value.Y, value.Z)) + (value.W * plane.D);

    /// <summary>Computes the signed distance of <paramref name="value"/> from the plane.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DotCoordinate(Plane plane, Vec3 value) => Vec3.Dot(plane.Normal, value) + plane.D;

    /// <summary>Computes the projection of <paramref name="value"/> onto <see cref="Normal"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DotNormal(Plane plane, Vec3 value) => Vec3.Dot(plane.Normal, value);

    /// <summary>
    /// Transforms a plane by a matrix using the inverse-transpose, so points
    /// on the original plane map onto the transformed plane.
    /// </summary>
    /// <param name="plane">The plane to transform.</param>
    /// <param name="matrix">The row-major transform.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Plane Transform(Plane plane, Mat4 matrix)
    {
        matrix.Invert(out Mat4 inverted);

        float x = plane.Normal.X;
        float y = plane.Normal.Y;
        float z = plane.Normal.Z;

        return new Plane(
            (x * inverted.M11) + (y * inverted.M12) + (z * inverted.M13) + (plane.D * inverted.M14),
            (x * inverted.M21) + (y * inverted.M22) + (z * inverted.M23) + (plane.D * inverted.M24),
            (x * inverted.M31) + (y * inverted.M32) + (z * inverted.M33) + (plane.D * inverted.M34),
            (x * inverted.M41) + (y * inverted.M42) + (z * inverted.M43) + (plane.D * inverted.M44));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Plane other) => Normal == other.Normal && D == other.D;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Plane other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Normal, D);

    /// <summary>Determines whether two planes have equal components.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Plane left, Plane right) => left.Equals(right);

    /// <summary>Determines whether two planes differ in any component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Plane left, Plane right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    /// <summary>Formats the normal and constant using the given format and provider.</summary>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        $"Plane(Normal={Normal.ToString(format, formatProvider)}, D={D.ToString(format, formatProvider)})";

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        destination.TryWrite(provider, $"Plane(Normal={Normal}, D={D})", out charsWritten);
}
