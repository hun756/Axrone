namespace Axrone.Geometry;

/// <summary>
/// Six-plane view volume used by the visibility pass. Each plane is stored as a
/// <see cref="Plane"/> with an inward-pointing normal, so a point lies inside the frustum
/// exactly when it satisfies all six half-space tests and a box is classified by probing
/// the two corners furthest along each normal. The planes come from
/// <see cref="Mat4"/> directly, so a frustum is just the extracted
/// plane data with no extra state to keep in sync.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 16)]
public readonly struct Frustum3
{
    /// <summary>Near clipping plane; its inward normal points towards the eye.</summary>
    public readonly Plane Near;

    /// <summary>Far clipping plane.</summary>
    public readonly Plane Far;

    /// <summary>Left clipping plane; its inward normal points to the right.</summary>
    public readonly Plane Left;

    /// <summary>Right clipping plane.</summary>
    public readonly Plane Right;

    /// <summary>Top clipping plane; its inward normal points down.</summary>
    public readonly Plane Top;

    /// <summary>Bottom clipping plane.</summary>
    public readonly Plane Bottom;

    /// <summary>Creates a frustum from six inward-facing planes.</summary>
    /// <param name="near">The near clipping plane.</param>
    /// <param name="far">The far clipping plane.</param>
    /// <param name="left">The left clipping plane.</param>
    /// <param name="right">The right clipping plane.</param>
    /// <param name="top">The top clipping plane.</param>
    /// <param name="bottom">The bottom clipping plane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Frustum3(Plane near, Plane far, Plane left, Plane right, Plane top, Plane bottom)
    {
        Near = near;
        Far = far;
        Left = left;
        Right = right;
        Top = top;
        Bottom = bottom;
    }

    /// <summary>
    /// Extracts the six clipping planes of a view-projection matrix by adding and
    /// subtracting the row combinations of the left-handed clip-space convention, then
    /// normalizing each so the plane distances stay in world units.
    /// </summary>
    /// <param name="m">The combined view-projection matrix; row-vector convention.</param>
    /// <returns>The frustum described by <paramref name="m"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Frustum3 CreateFromMatrix(in Mat4 m)
    {
        Plane left = Plane.Normalize(new Plane(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41));
        Plane right = Plane.Normalize(new Plane(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41));
        Plane top = Plane.Normalize(new Plane(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42));
        Plane bottom = Plane.Normalize(new Plane(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42));
        Plane near = Plane.Normalize(new Plane(m.M13, m.M23, m.M33, m.M43));
        Plane far = Plane.Normalize(new Plane(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43));
        return new Frustum3(near, far, left, right, top, bottom);
    }

    /// <summary>
    /// Classifies a box against the frustum with the two-vertices-per-plane test: if the
    /// corner furthest along a plane normal is already outside, the box is
    /// <see cref="ContainmentType.Disjoint"/>; otherwise a corner behind any plane makes it
    /// <see cref="ContainmentType.Intersects"/>, and a box in front of all six planes is
    /// <see cref="ContainmentType.Contains"/>.
    /// </summary>
    /// <param name="box">The box to classify against the frustum.</param>
    /// <returns>The relationship between <paramref name="box"/> and the frustum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ContainmentType Contains(in Aabb3D box)
    {
        ReadOnlySpan<Plane> planes = [Near, Far, Left, Right, Top, Bottom];
        bool intersects = false;
        Vec3 min = box.Min;
        Vec3 max = box.Max;

        for (int i = 0; i < planes.Length; i++)
        {
            Plane p = planes[i];
            Vec3 normal = p.Normal;

            Vec3 pVertex = new(normal.X >= 0 ? max.X : min.X, normal.Y >= 0 ? max.Y : min.Y, normal.Z >= 0 ? max.Z : min.Z);
            if (Vec3.Dot(normal, pVertex) + p.D < 0)
            {
                return ContainmentType.Disjoint;
            }

            Vec3 nVertex = new(normal.X >= 0 ? min.X : max.X, normal.Y >= 0 ? min.Y : max.Y, normal.Z >= 0 ? min.Z : max.Z);
            if (Vec3.Dot(normal, nVertex) + p.D < 0)
            {
                intersects = true;
            }
        }

        return intersects ? ContainmentType.Intersects : ContainmentType.Contains;
    }
}