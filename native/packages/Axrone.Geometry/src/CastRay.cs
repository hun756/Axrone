namespace Axrone.Geometry;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Traversal ray of the BVH raycaster: an origin, a direction used as-is, its
/// guarded reciprocal, and the accepted distance window. Unlike
/// <see cref="Ray3D"/>, which normalizes once so picking distances stay in
/// world units, this ray keeps the caller's direction untouched so a traversal
/// never pays a square root per ray; thresholds and hits then share the ray's
/// own distance units. Normalize before casting when world-unit distances or
/// sphere tests are needed.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Ray : IEquatable<Ray>
{
    /// <summary>Point the ray starts at.</summary>
    public readonly Vec3 Origin;

    /// <summary>Travel direction, used exactly as supplied.</summary>
    public readonly Vec3 Direction;

    /// <summary>Per-component reciprocal of <see cref="Direction"/>, guarded against axis-aligned components.</summary>
    public readonly Vec3 InvDirection;

    /// <summary>Closest accepted contact; self-hits below this are ignored.</summary>
    public readonly Distance MinDistance;

    /// <summary>Furthest accepted contact.</summary>
    public readonly Distance MaxDistance;

    /// <summary>Creates a ray with an explicit distance window.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Ray(Vec3 origin, Vec3 direction, Distance minDistance, Distance maxDistance)
    {
        if (minDistance.Value < 0.0f || minDistance > maxDistance)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(minDistance));
        }

        Origin = origin;
        Direction = direction;
        InvDirection = new Vec3(
            ComputeSafeReciprocal(direction.X),
            ComputeSafeReciprocal(direction.Y),
            ComputeSafeReciprocal(direction.Z));
        MinDistance = minDistance;
        MaxDistance = maxDistance;
    }

    /// <summary>Creates a ray with the default epsilon guard and no far limit.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ray(Vec3 origin, Vec3 direction)
        : this(origin, direction, Distance.DefaultEpsilon, Distance.PositiveInfinity)
    {
    }

    /// <summary>Returns the point reached after travelling <paramref name="distance"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec3 PointAt(float distance) => Origin + (Direction * distance);

    /// <summary>Returns the point reached after travelling <paramref name="distance"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec3 PointAt(Distance distance) => Origin + (Direction * distance.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ComputeSafeReciprocal(float component)
    {
        const float epsilon = 1e-9f;
        if (MathF.Abs(component) < epsilon)
        {
            return component >= 0.0f ? 1.0f / epsilon : -1.0f / epsilon;
        }

        return 1.0f / component;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Ray other) =>
        Origin == other.Origin &&
        Direction == other.Direction &&
        MinDistance == other.MinDistance &&
        MaxDistance == other.MaxDistance;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Ray other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Origin, Direction, MinDistance, MaxDistance);

    /// <summary>Determines whether two rays share an origin, direction and distance window.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in Ray left, in Ray right) => left.Equals(right);

    /// <summary>Determines whether two rays differ in origin, direction or window.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in Ray left, in Ray right) => !left.Equals(right);
}
