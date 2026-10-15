namespace Axrone.Physics3D;

/// <summary>
/// Box with its frame folded into the support map: rotates the query
/// direction into the local frame, evaluates the canonical box support, and
/// maps the witness back to world space. Lets GJK traverse rotated boxes
/// with zero allocation and no virtual dispatch.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct TransformedBox : ISupportShape3D<TransformedBox>, IEquatable<TransformedBox>
{
    /// <summary>Canonical local box.</summary>
    public readonly Box3 Local;

    /// <summary>Frame carrying the box to world space.</summary>
    public readonly RigidTransform3D Frame;

    /// <summary>Binds a local box to its world frame.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TransformedBox(Box3 local, in RigidTransform3D frame)
    {
        Local = local;
        Frame = frame;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 Support(in TransformedBox shape, in Vec3 direction)
    {
        Vec3 localDirection = shape.Frame.Rotation.Conjugate() * direction;
        Vec3 localSupport = Box3.Support(shape.Local, localDirection);
        return shape.Frame.TransformPoint(localSupport);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ComputeCentroid(in TransformedBox shape) => shape.Frame.TransformPoint(shape.Local.Center);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb3D ComputeBounds(in TransformedBox shape) =>
        ShapeStore.RotatedBoxBounds(
            shape.Frame.TransformPoint(shape.Local.Center), shape.Frame.Rotation, shape.Local.HalfExtents);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TransformedBox other) => Local == other.Local && Frame == other.Frame;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TransformedBox other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Local, Frame);

    /// <summary>Determines whether two bindings match.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(TransformedBox left, TransformedBox right) => left.Equals(right);

    /// <summary>Determines whether two bindings differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(TransformedBox left, TransformedBox right) => !left.Equals(right);
}

/// <summary>
/// Penetration queries over world-space shape pairs. Balls and ball-vs-box
/// resolve analytically; box-vs-box runs a 15-axis separating-axis test and
/// clips the incident face against the reference face, keeping at most two
/// points. All normals point from the second shape toward the first.
/// </summary>
public static class Penetration3D
{
    private const float ParallelAxisEpsilon = 1e-6f;
    private const float SeparationSlop = 1e-6f;

    /// <summary>Penetration of two balls; disjoint pairs report no contact.</summary>
    /// <returns><see langword="true"/> with a single-point manifold while overlapping.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool SphereSphere(in Sphere3 a, in Sphere3 b, out Manifold3D manifold)
    {
        Vec3 delta = a.Center - b.Center;
        float distSq = delta.LengthSquared();
        float radiusSum = a.Radius + b.Radius;

        if (distSq >= radiusSum * radiusSum)
        {
            manifold = Manifold3D.Empty;
            return false;
        }

        float dist = MathF.Sqrt(distSq);
        Vec3 normal = dist > 1e-9f ? delta / dist : new Vec3(1.0f, 0.0f, 0.0f);
        float separation = dist - radiusSum;
        var point = new ContactPoint3D(a.Center - (normal * a.Radius), b.Center + (normal * b.Radius), separation, 0u);
        manifold = Manifold3D.One(normal, point);
        return true;
    }

    /// <summary>Penetration of a world ball against a box bound to <paramref name="boxFrame"/>; disjoint pairs report no contact.</summary>
    /// <param name="sphere">The ball in world space.</param>
    /// <param name="box">The box in its local frame.</param>
    /// <param name="boxFrame">The frame carrying the box to world space.</param>
    /// <param name="manifold">The contact manifold while overlapping.</param>
    /// <returns><see langword="true"/> with a single-point manifold while overlapping.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool SphereBox(in Sphere3 sphere, in Box3 box, in RigidTransform3D boxFrame, out Manifold3D manifold)
    {
        Vec3 localCenter = boxFrame.InverseTransformPoint(sphere.Center) - box.Center;
        Vec3 clamped = ClampToBox(localCenter, box.HalfExtents);

        if ((clamped - localCenter).LengthSquared() > SeparationSlop)
        {
            Vec3 worldClosest = boxFrame.TransformPoint(box.Center + clamped);
            Vec3 delta = sphere.Center - worldClosest;
            float distSq = delta.LengthSquared();
            if (distSq >= sphere.Radius * sphere.Radius)
            {
                manifold = Manifold3D.Empty;
                return false;
            }

            float dist = MathF.Sqrt(distSq);
            Vec3 normal = dist > 1e-9f ? delta / dist : new Vec3(1.0f, 0.0f, 0.0f);
            var point = new ContactPoint3D(
                sphere.Center - (normal * sphere.Radius), worldClosest, dist - sphere.Radius, FaceId(clamped, box.HalfExtents));
            manifold = Manifold3D.One(normal, point);
            return true;
        }

        PushOutOfBox(localCenter, box.HalfExtents, out Vec3 pushAxis, out float pushDepth);
        Vec3 worldNormal = boxFrame.Rotation * pushAxis;
        float separation = -(pushDepth + sphere.Radius);
        Vec3 contactB = boxFrame.TransformPoint(box.Center + localCenter);
        var inside = new ContactPoint3D(
            sphere.Center - (worldNormal * sphere.Radius), contactB, separation, 0xFFu);
        manifold = Manifold3D.One(worldNormal, inside);
        return true;
    }

    /// <summary>Penetration of two framed boxes; disjoint pairs report no contact.</summary>
    /// <param name="boxA">The first box in its local frame.</param>
    /// <param name="frameA">The frame carrying the first box to world space.</param>
    /// <param name="boxB">The second box in its local frame.</param>
    /// <param name="frameB">The frame carrying the second box to world space.</param>
    /// <param name="manifold">The contact manifold while overlapping.</param>
    /// <returns><see langword="true"/> with a one- or two-point manifold while overlapping.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool BoxBox(
        in Box3 boxA, in RigidTransform3D frameA,
        in Box3 boxB, in RigidTransform3D frameB,
        out Manifold3D manifold)
    {
        Box3 localA = boxA;
        RigidTransform3D localFrameA = frameA;
        Box3 localB = boxB;
        RigidTransform3D localFrameB = frameB;

        Vec3 centerA = localFrameA.TransformPoint(localA.Center);
        Vec3 centerB = localFrameB.TransformPoint(localB.Center);
        Vec3 axisA0 = localFrameA.Rotation * new Vec3(1.0f, 0.0f, 0.0f);
        Vec3 axisA1 = localFrameA.Rotation * new Vec3(0.0f, 1.0f, 0.0f);
        Vec3 axisA2 = localFrameA.Rotation * new Vec3(0.0f, 0.0f, 1.0f);
        Vec3 axisB0 = localFrameB.Rotation * new Vec3(1.0f, 0.0f, 0.0f);
        Vec3 axisB1 = localFrameB.Rotation * new Vec3(0.0f, 1.0f, 0.0f);
        Vec3 axisB2 = localFrameB.Rotation * new Vec3(0.0f, 0.0f, 1.0f);

        float bestGap = float.NegativeInfinity;
        Vec3 bestAxis = new(1.0f, 0.0f, 0.0f);
        int bestCode = 0;

        TestFaceAxis(axisA0, 0);
        TestFaceAxis(axisA1, 1);
        TestFaceAxis(axisA2, 2);
        TestFaceAxis(axisB0, 3);
        TestFaceAxis(axisB1, 4);
        TestFaceAxis(axisB2, 5);
        TestEdgeAxis(axisA0, axisB0);
        TestEdgeAxis(axisA0, axisB1);
        TestEdgeAxis(axisA0, axisB2);
        TestEdgeAxis(axisA1, axisB0);
        TestEdgeAxis(axisA1, axisB1);
        TestEdgeAxis(axisA1, axisB2);
        TestEdgeAxis(axisA2, axisB0);
        TestEdgeAxis(axisA2, axisB1);
        TestEdgeAxis(axisA2, axisB2);

        if (bestGap > 0.0f)
        {
            manifold = Manifold3D.Empty;
            return false;
        }

        float bestDepth = -bestGap;

        Vec3 normal = OrientAxis(bestAxis, centerA - centerB);

        if (bestCode <= 2)
        {
            manifold = ClipFacePair(
                normal, localA, localFrameA, centerA, bestCode,
                localB, localFrameB, centerB, referenceIsA: true);
        }
        else if (bestCode <= 5)
        {
            manifold = ClipFacePair(
                normal, localB, localFrameB, centerB, bestCode - 3,
                localA, localFrameA, centerA, referenceIsA: false);
        }
        else
        {
            Vec3 pointA = SupportPoint(localA, localFrameA, normal * -1.0f);
            Vec3 pointB = SupportPoint(localB, localFrameB, normal);
            var single = new ContactPoint3D(pointA, pointB, -bestDepth, (uint)bestCode);
            manifold = Manifold3D.One(normal, single);
        }

        return true;

        void TestFaceAxis(Vec3 axis, int code)
        {
            float spanA = ProjectedSpan(localA.HalfExtents, localFrameA.Rotation, axis);
            float spanB = ProjectedSpan(localB.HalfExtents, localFrameB.Rotation, axis);
            float gap = MathF.Abs(Vec3.Dot(centerB - centerA, axis)) - (spanA + spanB);
            if (gap > bestGap)
            {
                bestGap = gap;
                bestAxis = axis;
                bestCode = code;
            }
        }

        void TestEdgeAxis(Vec3 edgeA, Vec3 edgeB)
        {
            Vec3 axis = Vec3.Cross(edgeA, edgeB);
            if (axis.LengthSquared() < ParallelAxisEpsilon)
            {
                return;
            }

            axis = Vec3.Normalize(axis);
            float spanA = ProjectedSpan(localA.HalfExtents, localFrameA.Rotation, axis);
            float spanB = ProjectedSpan(localB.HalfExtents, localFrameB.Rotation, axis);
            float gap = MathF.Abs(Vec3.Dot(centerB - centerA, axis)) - (spanA + spanB);
            if (gap > bestGap)
            {
                bestGap = gap;
                bestAxis = axis;
                bestCode = 6;
            }
        }
    }

    /// <summary>Half-extent projection of a rotated box onto an axis.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float ProjectedSpan(Vec3 halfExtents, Quat rotation, Vec3 axis) =>
        MathF.Abs(Vec3.Dot(rotation * new Vec3(halfExtents.X, 0.0f, 0.0f), axis)) +
        MathF.Abs(Vec3.Dot(rotation * new Vec3(0.0f, halfExtents.Y, 0.0f), axis)) +
        MathF.Abs(Vec3.Dot(rotation * new Vec3(0.0f, 0.0f, halfExtents.Z), axis));

    /// <summary>Orients an axis to point from B toward A.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vec3 OrientAxis(Vec3 axis, Vec3 aToB) =>
        Vec3.Dot(axis, aToB) >= 0.0f ? axis : axis * -1.0f;

    /// <summary>Support of a framed box along a direction.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vec3 SupportPoint(Box3 box, in RigidTransform3D frame, Vec3 direction) =>
        frame.TransformPoint(Box3.Support(box, frame.Rotation.Conjugate() * direction));

    /// <summary>Penetration of two capsules; disjoint pairs report no contact.</summary>
    /// <param name="capsuleA">The first capsule in world space.</param>
    /// <param name="capsuleB">The second capsule in world space.</param>
    /// <param name="manifold">The contact manifold while overlapping.</param>
    /// <returns><see langword="true"/> with a single-point manifold while overlapping.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool CapsuleCapsule(in Capsule3 capsuleA, in Capsule3 capsuleB, out Manifold3D manifold)
    {
        SegmentSegmentClosest(
            capsuleA.P0, capsuleA.P1, capsuleB.P0, capsuleB.P1,
            out Vec3 closestA, out Vec3 closestB);

        Vec3 delta = closestA - closestB;
        float distSq = delta.LengthSquared();
        float radiusSum = capsuleA.Radius + capsuleB.Radius;
        if (distSq >= radiusSum * radiusSum)
        {
            manifold = Manifold3D.Empty;
            return false;
        }

        float dist = MathF.Sqrt(distSq);
        Vec3 normal = dist > 1e-9f ? delta / dist : new Vec3(1.0f, 0.0f, 0.0f);
        var point = new ContactPoint3D(
            closestA - (normal * capsuleA.Radius), closestB + (normal * capsuleB.Radius),
            dist - radiusSum, 0u);
        manifold = Manifold3D.One(normal, point);
        return true;
    }

    /// <summary>Penetration of a capsule against a ball; disjoint pairs report no contact.</summary>
    /// <param name="capsule">The capsule in world space.</param>
    /// <param name="sphere">The ball in world space.</param>
    /// <param name="manifold">The contact manifold while overlapping.</param>
    /// <returns><see langword="true"/> with a single-point manifold while overlapping.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool CapsuleSphere(in Capsule3 capsule, in Sphere3 sphere, out Manifold3D manifold)
    {
        Vec3 closest = ClosestPointOnSegment(sphere.Center, capsule.P0, capsule.P1);
        Vec3 delta = closest - sphere.Center;
        float distSq = delta.LengthSquared();
        float radiusSum = capsule.Radius + sphere.Radius;
        if (distSq >= radiusSum * radiusSum)
        {
            manifold = Manifold3D.Empty;
            return false;
        }

        float dist = MathF.Sqrt(distSq);
        Vec3 normal = dist > 1e-9f ? delta / dist : new Vec3(1.0f, 0.0f, 0.0f);
        var point = new ContactPoint3D(
            closest - (normal * capsule.Radius), sphere.Center + (normal * sphere.Radius),
            dist - radiusSum, 0u);
        manifold = Manifold3D.One(normal, point);
        return true;
    }

    /// <summary>Penetration of a capsule against a framed box; disjoint pairs report no contact.</summary>
    /// <param name="capsule">The capsule in world space.</param>
    /// <param name="box">The box in its local frame.</param>
    /// <param name="boxFrame">The frame carrying the box to world space.</param>
    /// <param name="manifold">The contact manifold while overlapping.</param>
    /// <returns><see langword="true"/> with a single-point manifold while overlapping.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool CapsuleBox(in Capsule3 capsule, in Box3 box, in RigidTransform3D boxFrame, out Manifold3D manifold)
    {
        Vec3 localP0 = boxFrame.InverseTransformPoint(capsule.P0) - box.Center;
        Vec3 localP1 = boxFrame.InverseTransformPoint(capsule.P1) - box.Center;

        ClosestSegmentBox(localP0, localP1, box.HalfExtents, out Vec3 segPoint, out Vec3 boxPoint, out bool intersecting);
        if (!intersecting)
        {
            Vec3 worldSeg = boxFrame.TransformPoint(box.Center + segPoint);
            Vec3 worldBox = boxFrame.TransformPoint(box.Center + boxPoint);
            Vec3 delta = worldSeg - worldBox;
            float distSq = delta.LengthSquared();
            if (distSq >= capsule.Radius * capsule.Radius)
            {
                manifold = Manifold3D.Empty;
                return false;
            }

            float dist = MathF.Sqrt(distSq);
            Vec3 normal = dist > 1e-9f ? delta / dist : boxFrame.Rotation * new Vec3(1.0f, 0.0f, 0.0f);
            var point = new ContactPoint3D(
                worldSeg - (normal * capsule.Radius), worldBox, dist - capsule.Radius, FaceId(boxPoint, box.HalfExtents));
            manifold = Manifold3D.One(normal, point);
            return true;
        }

        PushOutOfBox(segPoint, box.HalfExtents, out Vec3 pushAxis, out float pushDepth);
        Vec3 worldNormal = boxFrame.Rotation * pushAxis;
        float separation = -(pushDepth + capsule.Radius);
        Vec3 contactB = boxFrame.TransformPoint(box.Center + segPoint);
        Vec3 contactA = boxFrame.TransformPoint(box.Center + segPoint) - (worldNormal * capsule.Radius);
        var inside = new ContactPoint3D(contactA, contactB, separation, 0xFFu);
        manifold = Manifold3D.One(worldNormal, inside);
        return true;
    }

    /// <summary>Closest point on a segment to a query point.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static Vec3 ClosestPointOnSegment(Vec3 point, Vec3 a, Vec3 b)
    {
        Vec3 ab = b - a;
        float denom = Vec3.Dot(ab, ab);
        if (denom <= 1e-24f)
        {
            return a;
        }

        float t = Math.Clamp(Vec3.Dot(point - a, ab) / denom, 0.0f, 1.0f);
        return a + (ab * t);
    }

    /// <summary>Closest points between two segments (Ericson 5.1.9).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void SegmentSegmentClosest(
        Vec3 p1, Vec3 q1, Vec3 p2, Vec3 q2, out Vec3 closest1, out Vec3 closest2)
    {
        Vec3 d1 = q1 - p1;
        Vec3 d2 = q2 - p2;
        Vec3 r = p1 - p2;
        float a = Vec3.Dot(d1, d1);
        float e = Vec3.Dot(d2, d2);
        float f = Vec3.Dot(d2, r);

        float s;
        float t;
        if (a <= 1e-12f && e <= 1e-12f)
        {
            s = 0.0f;
            t = 0.0f;
        }
        else if (a <= 1e-12f)
        {
            s = 0.0f;
            t = Math.Clamp(f / e, 0.0f, 1.0f);
        }
        else
        {
            float c = Vec3.Dot(d1, r);
            if (e <= 1e-12f)
            {
                t = 0.0f;
                s = Math.Clamp(-c / a, 0.0f, 1.0f);
            }
            else
            {
                float b = Vec3.Dot(d1, d2);
                float denom = (a * e) - (b * b);
                s = denom != 0.0f ? Math.Clamp(((b * f) - (c * e)) / denom, 0.0f, 1.0f) : 0.0f;
                t = ((b * s) + f) / e;
                if (t < 0.0f)
                {
                    t = 0.0f;
                    s = Math.Clamp(-c / a, 0.0f, 1.0f);
                }
                else if (t > 1.0f)
                {
                    t = 1.0f;
                    s = Math.Clamp((b - c) / a, 0.0f, 1.0f);
                }
            }
        }

        closest1 = p1 + (d1 * s);
        closest2 = p2 + (d2 * t);
    }

    /// <summary>
    /// Closest points between a segment and a box surface. Endpoints inside
    /// the box report intersection with the deepest endpoint; otherwise the
    /// minimum of endpoint clamps and edge spans wins.
    /// </summary>
    internal static void ClosestSegmentBox(
        Vec3 p0, Vec3 p1, Vec3 halfExtents,
        out Vec3 segPoint, out Vec3 boxPoint, out bool intersecting)
    {
        bool inside0 = IsInsideBox(p0, halfExtents);
        bool inside1 = IsInsideBox(p1, halfExtents);
        if (inside0 || inside1)
        {
            PushOutOfBox(p0, halfExtents, out _, out float depth0);
            PushOutOfBox(p1, halfExtents, out _, out float depth1);
            segPoint = depth0 >= depth1 ? p0 : p1;
            boxPoint = segPoint;
            intersecting = true;
            return;
        }

        float bestDistSq = float.PositiveInfinity;
        segPoint = p0;
        boxPoint = ClampToBox(p0, halfExtents);

        TrySegmentBoxCandidate(p0, ClampToBox(p0, halfExtents), ref bestDistSq, ref segPoint, ref boxPoint);
        TrySegmentBoxCandidate(p1, ClampToBox(p1, halfExtents), ref bestDistSq, ref segPoint, ref boxPoint);

        Span<Vec3> corners = stackalloc Vec3[8];
        BoxCorners(halfExtents, corners);
        ReadOnlySpan<int> edges = stackalloc int[]
        {
            0, 1, 1, 3, 3, 2, 2, 0,
            4, 5, 5, 7, 7, 6, 6, 4,
            0, 4, 1, 5, 3, 7, 2, 6,
        };
        for (int e = 0; e < 12; e++)
        {
            SegmentSegmentClosest(p0, p1, corners[edges[e * 2]], corners[edges[e * 2 + 1]], out Vec3 s, out Vec3 b);
            TrySegmentBoxCandidate(s, b, ref bestDistSq, ref segPoint, ref boxPoint);
        }

        intersecting = false;
    }

    /// <summary>Keeps the closest segment-box candidate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TrySegmentBoxCandidate(
        Vec3 s, Vec3 b, ref float bestDistSq, ref Vec3 segPoint, ref Vec3 boxPoint)
    {
        float distSq = (s - b).LengthSquared();
        if (distSq < bestDistSq)
        {
            bestDistSq = distSq;
            segPoint = s;
            boxPoint = b;
        }
    }

    /// <summary>Eight corners of a box centered at the origin.</summary>
    private static void BoxCorners(Vec3 halfExtents, Span<Vec3> corners)
    {
        corners[0] = new Vec3(-halfExtents.X, -halfExtents.Y, -halfExtents.Z);
        corners[1] = new Vec3(halfExtents.X, -halfExtents.Y, -halfExtents.Z);
        corners[2] = new Vec3(-halfExtents.X, halfExtents.Y, -halfExtents.Z);
        corners[3] = new Vec3(halfExtents.X, halfExtents.Y, -halfExtents.Z);
        corners[4] = new Vec3(-halfExtents.X, -halfExtents.Y, halfExtents.Z);
        corners[5] = new Vec3(halfExtents.X, -halfExtents.Y, halfExtents.Z);
        corners[6] = new Vec3(-halfExtents.X, halfExtents.Y, halfExtents.Z);
        corners[7] = new Vec3(halfExtents.X, halfExtents.Y, halfExtents.Z);
    }

    /// <summary>Whether a point lies inside a centered box.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInsideBox(Vec3 point, Vec3 halfExtents) =>
        MathF.Abs(point.X) <= halfExtents.X &&
        MathF.Abs(point.Y) <= halfExtents.Y &&
        MathF.Abs(point.Z) <= halfExtents.Z;

    /// <summary>Clamps a point to the box volume.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vec3 ClampToBox(Vec3 point, Vec3 halfExtents) => new(
        Math.Clamp(point.X, -halfExtents.X, halfExtents.X),
        Math.Clamp(point.Y, -halfExtents.Y, halfExtents.Y),
        Math.Clamp(point.Z, -halfExtents.Z, halfExtents.Z));

    /// <summary>Small feature code of the clamped face for contact stability.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint FaceId(Vec3 clamped, Vec3 halfExtents)
    {
        if (MathF.Abs(MathF.Abs(clamped.X) - halfExtents.X) < 1e-6f)
        {
            return 1u;
        }

        if (MathF.Abs(MathF.Abs(clamped.Y) - halfExtents.Y) < 1e-6f)
        {
            return 2u;
        }

        return 3u;
    }

    /// <summary>Pushes an interior point out along the least-penetrating face axis.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PushOutOfBox(Vec3 point, Vec3 halfExtents, out Vec3 axis, out float depth)
    {
        float dx = halfExtents.X - MathF.Abs(point.X);
        float dy = halfExtents.Y - MathF.Abs(point.Y);
        float dz = halfExtents.Z - MathF.Abs(point.Z);

        if (dx <= dy && dx <= dz)
        {
            axis = new Vec3(MathF.Sign(point.X), 0.0f, 0.0f);
            depth = dx;
        }
        else if (dy <= dz)
        {
            axis = new Vec3(0.0f, MathF.Sign(point.Y), 0.0f);
            depth = dy;
        }
        else
        {
            axis = new Vec3(0.0f, 0.0f, MathF.Sign(point.Z));
            depth = dz;
        }
    }

    /// <summary>Clips the incident face against the reference face side planes, keeping at most two points.</summary>
    private static Manifold3D ClipFacePair(
        Vec3 normal,
        Box3 refBox, in RigidTransform3D refFrame, Vec3 refCenter, int refAxis,
        Box3 incBox, in RigidTransform3D incFrame, Vec3 incCenter, bool referenceIsA)
    {
        Vec3 outward = referenceIsA ? normal * -1.0f : normal;
        Vec3 refNormal = AxisOf(refFrame.Rotation, refAxis);
        if (Vec3.Dot(refNormal, outward) < 0.0f)
        {
            refNormal = refNormal * -1.0f;
        }

        int incAxis = IncidentAxis(incFrame.Rotation, refNormal);
        Span<Vec3> quad = stackalloc Vec3[4];
        IncidentQuad(incBox, incFrame, incAxis, refNormal, quad);

        float refExtent = refAxis == 0 ? refBox.HalfExtents.X : refAxis == 1 ? refBox.HalfExtents.Y : refBox.HalfExtents.Z;
        Vec3 tangentU = AxisOf(refFrame.Rotation, (refAxis + 1) % 3);
        Vec3 tangentV = AxisOf(refFrame.Rotation, (refAxis + 2) % 3);
        float extentU = ((refAxis + 1) % 3) == 0 ? refBox.HalfExtents.X : ((refAxis + 1) % 3) == 1 ? refBox.HalfExtents.Y : refBox.HalfExtents.Z;
        float extentV = ((refAxis + 2) % 3) == 0 ? refBox.HalfExtents.X : ((refAxis + 2) % 3) == 1 ? refBox.HalfExtents.Y : refBox.HalfExtents.Z;

        Vec3 refFaceCenter = refCenter + (refNormal * refExtent);
        float planeOffset = Vec3.Dot(refNormal, refFaceCenter);

        Span<Vec3> kept = stackalloc Vec3[4];
        Span<float> keptSep = stackalloc float[4];
        Span<int> keptCorner = stackalloc int[4];
        int keptCount = 0;

        for (int i = 0; i < 4; i++)
        {
            Vec3 candidate = quad[i];
            float alongU = Vec3.Dot(candidate - refFaceCenter, tangentU);
            float alongV = Vec3.Dot(candidate - refFaceCenter, tangentV);
            if (MathF.Abs(alongU) > extentU + 1e-4f || MathF.Abs(alongV) > extentV + 1e-4f)
            {
                continue;
            }

            float separation = Vec3.Dot(candidate, refNormal) - planeOffset;
            if (separation > 1e-4f)
            {
                continue;
            }

            kept[keptCount] = candidate;
            keptSep[keptCount] = separation;
            keptCorner[keptCount] = i;
            keptCount++;
        }

        if (keptCount == 0)
        {
            Vec3 fallbackA = referenceIsA ? refFaceCenter : SupportPoint(incBox, incFrame, normal * -1.0f);
            Vec3 fallbackB = referenceIsA ? SupportPoint(incBox, incFrame, normal) : refFaceCenter;
            var single = new ContactPoint3D(fallbackA, fallbackB, 0.0f, (uint)(0x10 + refAxis));
            return Manifold3D.One(normal, single);
        }

        int first = 0;
        for (int i = 1; i < keptCount; i++)
        {
            if (keptSep[i] < keptSep[first])
            {
                first = i;
            }
        }

        int second = -1;
        if (keptCount >= 3)
        {
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < keptCount; i++)
            {
                if (i == first)
                {
                    continue;
                }

                float spread = (kept[i] - kept[first]).LengthSquared();
                float depthPenalty = (keptSep[i] - keptSep[first]) * 4.0f;
                float score = spread - (depthPenalty * depthPenalty);
                if (score > bestScore)
                {
                    bestScore = score;
                    second = i;
                }
            }
        }
        else
        {
            for (int i = 0; i < keptCount; i++)
            {
                if (i != first && (second == -1 || keptSep[i] < keptSep[second]))
                {
                    second = i;
                }
            }
        }

        ContactPoint3D MakePoint(Vec3 incidentPoint, float separation, int corner)
        {
            Vec3 onReference = incidentPoint - (refNormal * separation);
            Vec3 pointA = referenceIsA ? onReference : incidentPoint;
            Vec3 pointB = referenceIsA ? incidentPoint : onReference;
            uint id = (uint)(0x10 + (refAxis * 16) + (incAxis * 4) + corner);
            return new ContactPoint3D(pointA, pointB, separation, id);
        }

        if (second == -1)
        {
            return Manifold3D.One(normal, MakePoint(kept[first], keptSep[first], keptCorner[first]));
        }

        return Manifold3D.Pair(
            normal,
            MakePoint(kept[first], keptSep[first], keptCorner[first]),
            MakePoint(kept[second], keptSep[second], keptCorner[second]));
    }

    /// <summary>World axis of a frame by index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vec3 AxisOf(Quat rotation, int axis) => axis switch
    {
        0 => rotation * new Vec3(1.0f, 0.0f, 0.0f),
        1 => rotation * new Vec3(0.0f, 1.0f, 0.0f),
        _ => rotation * new Vec3(0.0f, 0.0f, 1.0f),
    };

    /// <summary>Incident face axis most anti-parallel to the reference normal.</summary>
    private static int IncidentAxis(Quat rotation, Vec3 refNormal)
    {
        int best = 0;
        float bestDot = float.NegativeInfinity;
        for (int axis = 0; axis < 3; axis++)
        {
            float dot = MathF.Abs(Vec3.Dot(AxisOf(rotation, axis), refNormal));
            if (dot > bestDot)
            {
                bestDot = dot;
                best = axis;
            }
        }

        return best;
    }

    /// <summary>Four world corners of the incident face.</summary>
    private static void IncidentQuad(Box3 box, in RigidTransform3D frame, int axis, Vec3 refNormal, Span<Vec3> quad)
    {
        Vec3 faceAxis = AxisOf(frame.Rotation, axis);
        if (Vec3.Dot(faceAxis, refNormal) > 0.0f)
        {
            faceAxis = faceAxis * -1.0f;
        }

        Vec3 center = frame.TransformPoint(box.Center);
        float extent = axis == 0 ? box.HalfExtents.X : axis == 1 ? box.HalfExtents.Y : box.HalfExtents.Z;
        Vec3 faceCenter = center + (faceAxis * extent);
        Vec3 tangentU = AxisOf(frame.Rotation, (axis + 1) % 3);
        Vec3 tangentV = AxisOf(frame.Rotation, (axis + 2) % 3);
        float extentU = ((axis + 1) % 3) == 0 ? box.HalfExtents.X : ((axis + 1) % 3) == 1 ? box.HalfExtents.Y : box.HalfExtents.Z;
        float extentV = ((axis + 2) % 3) == 0 ? box.HalfExtents.X : ((axis + 2) % 3) == 1 ? box.HalfExtents.Y : box.HalfExtents.Z;

        quad[0] = (faceCenter + (tangentU * extentU)) + (tangentV * extentV);
        quad[1] = (faceCenter - (tangentU * extentU)) + (tangentV * extentV);
        quad[2] = (faceCenter - (tangentU * extentU)) - (tangentV * extentV);
        quad[3] = (faceCenter + (tangentU * extentU)) - (tangentV * extentV);
    }
}

/// <summary>
/// Tagged support union over every local shape: one monomorphic support
/// map feeding GJK and EPA, so curved prisms and future hulls resolve
/// without a combinatorial explosion of generic instantiations. Orientation
/// rides in <see cref="Frame"/>; geometry stays canonical and local.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct AnyCollider : ISupportShape3D<AnyCollider>, IEquatable<AnyCollider>
{
    /// <summary>Active geometry variant.</summary>
    public readonly ColliderKind Kind;

    /// <summary>Frame carrying the shape to world space.</summary>
    public readonly RigidTransform3D Frame;

    /// <summary>Local geometry variants; exactly one is live per <see cref="Kind"/>.</summary>
    public readonly Sphere3 Sphere;
    /// <summary>Local box variant.</summary>
    public readonly Box3 Box;
    /// <summary>Local capsule variant.</summary>
    public readonly Capsule3 Capsule;
    /// <summary>Local cylinder variant.</summary>
    public readonly Cylinder3 Cylinder;
    /// <summary>Local cone variant.</summary>
    public readonly Cone3 Cone;
    /// <summary>Local hull variant.</summary>
    public readonly Hull3D Hull;
    /// <summary>Local mesh variant.</summary>
    public readonly Mesh3D Mesh;
    /// <summary>Local heightfield variant.</summary>
    public readonly Heightfield3D Heightfield;

    private AnyCollider(
        ColliderKind kind, in RigidTransform3D frame,
        Sphere3 sphere, Box3 box, Capsule3 capsule, Cylinder3 cylinder, Cone3 cone,
        Hull3D hull, Mesh3D mesh, Heightfield3D field)
    {
        Kind = kind;
        Frame = frame;
        Sphere = sphere;
        Box = box;
        Capsule = capsule;
        Cylinder = cylinder;
        Cone = cone;
        Hull = hull;
        Mesh = mesh;
        Heightfield = field;
    }

    /// <summary>Wraps a ball.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AnyCollider FromSphere(Sphere3 sphere, in RigidTransform3D frame) =>
        new(ColliderKind.Sphere, frame, sphere, default, default, default, default, default, default, default);

    /// <summary>Wraps a box.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AnyCollider FromBox(Box3 box, in RigidTransform3D frame) =>
        new(ColliderKind.Box, frame, default, box, default, default, default, default, default, default);

    /// <summary>Wraps a capsule.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AnyCollider FromCapsule(Capsule3 capsule, in RigidTransform3D frame) =>
        new(ColliderKind.Capsule, frame, default, default, capsule, default, default, default, default, default);

    /// <summary>Wraps a cylinder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AnyCollider FromCylinder(Cylinder3 cylinder, in RigidTransform3D frame) =>
        new(ColliderKind.Cylinder, frame, default, default, default, cylinder, default, default, default, default);

    /// <summary>Wraps a cone.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AnyCollider FromCone(Cone3 cone, in RigidTransform3D frame) =>
        new(ColliderKind.Cone, frame, default, default, default, default, cone, default, default, default);

    /// <summary>Wraps a hull.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AnyCollider FromHull(Hull3D hull, in RigidTransform3D frame) =>
        new(ColliderKind.Hull, frame, default, default, default, default, default, hull, default, default);

    /// <summary>Wraps a mesh.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AnyCollider FromMesh(Mesh3D mesh, in RigidTransform3D frame) =>
        new(ColliderKind.Mesh, frame, default, default, default, default, default, default, mesh, default);

    /// <summary>Wraps a heightfield.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AnyCollider FromHeightfield(Heightfield3D field, in RigidTransform3D frame) =>
        new(ColliderKind.Heightfield, frame, default, default, default, default, default, default, default, field);

    /// <summary>Builds the union for a shape record from its local geometry and world frame.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static AnyCollider FromRecord(in ShapeRecord record, in RigidTransform3D frame) => record.Kind switch
    {
        ColliderKind.Sphere => FromSphere(record.LocalSphere, frame),
        ColliderKind.Box => FromBox(record.LocalBox, frame),
        ColliderKind.Capsule => FromCapsule(record.LocalCapsule, frame),
        ColliderKind.Cylinder => FromCylinder(record.LocalCylinder, frame),
        ColliderKind.Cone => FromCone(record.LocalCone, frame),
        ColliderKind.Hull => FromHull(record.LocalHull, frame),
        ColliderKind.Mesh => FromMesh(record.LocalMesh, frame),
        _ => FromHeightfield(record.LocalHeightfield, frame),
    };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 Support(in AnyCollider shape, in Vec3 direction)
    {
        Vec3 localDirection = shape.Frame.Rotation.Conjugate() * direction;
        Vec3 local = shape.Kind switch
        {
            ColliderKind.Sphere => Sphere3.Support(shape.Sphere, localDirection),
            ColliderKind.Box => Box3.Support(shape.Box, localDirection),
            ColliderKind.Capsule => Capsule3.Support(shape.Capsule, localDirection),
            ColliderKind.Cylinder => Cylinder3.Support(shape.Cylinder, localDirection),
            ColliderKind.Cone => Cone3.Support(shape.Cone, localDirection),
            ColliderKind.Hull => Hull3D.Support(shape.Hull, localDirection),
            ColliderKind.Mesh => Mesh3D.Support(shape.Mesh, localDirection),
            _ => Heightfield3D.Support(shape.Heightfield, localDirection),
        };
        return shape.Frame.TransformPoint(local);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ComputeCentroid(in AnyCollider shape)
    {
        Vec3 local = shape.Kind switch
        {
            ColliderKind.Sphere => Sphere3.ComputeCentroid(shape.Sphere),
            ColliderKind.Box => Box3.ComputeCentroid(shape.Box),
            ColliderKind.Capsule => Capsule3.ComputeCentroid(shape.Capsule),
            ColliderKind.Cylinder => Cylinder3.ComputeCentroid(shape.Cylinder),
            ColliderKind.Cone => Cone3.ComputeCentroid(shape.Cone),
            ColliderKind.Hull => Hull3D.ComputeCentroid(shape.Hull),
            ColliderKind.Mesh => Mesh3D.ComputeCentroid(shape.Mesh),
            _ => Heightfield3D.ComputeCentroid(shape.Heightfield),
        };
        return shape.Frame.TransformPoint(local);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb3D ComputeBounds(in AnyCollider shape)
    {
        Aabb3D local = shape.Kind switch
        {
            ColliderKind.Sphere => Sphere3.ComputeBounds(shape.Sphere),
            ColliderKind.Box => Box3.ComputeBounds(shape.Box),
            ColliderKind.Capsule => Capsule3.ComputeBounds(shape.Capsule),
            ColliderKind.Cylinder => Cylinder3.ComputeBounds(shape.Cylinder),
            ColliderKind.Cone => Cone3.ComputeBounds(shape.Cone),
            ColliderKind.Hull => Hull3D.ComputeBounds(shape.Hull),
            ColliderKind.Mesh => Mesh3D.ComputeBounds(shape.Mesh),
            _ => Heightfield3D.ComputeBounds(shape.Heightfield),
        };

        Vec3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vec3 max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vec3(
                (i & 1) == 0 ? local.Min.X : local.Max.X,
                (i & 2) == 0 ? local.Min.Y : local.Max.Y,
                (i & 4) == 0 ? local.Min.Z : local.Max.Z);
            Vec3 world = shape.Frame.TransformPoint(corner);
            min = Vec3.Min(min, world);
            max = Vec3.Max(max, world);
        }

        return new Aabb3D(min, max);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AnyCollider other) =>
        Kind == other.Kind && Frame == other.Frame && Sphere == other.Sphere && Box == other.Box &&
        Capsule == other.Capsule && Cylinder == other.Cylinder && Cone == other.Cone &&
        Hull == other.Hull && Mesh == other.Mesh && Heightfield == other.Heightfield;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is AnyCollider other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine((int)Kind, Frame, Sphere, Box, Capsule, Cylinder, Cone);

    /// <summary>Determines whether two unions match.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(AnyCollider left, AnyCollider right) => left.Equals(right);

    /// <summary>Determines whether two unions differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(AnyCollider left, AnyCollider right) => !left.Equals(right);
}
