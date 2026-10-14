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
    public static Vec3 Support(in TransformedBox shape, Vec3 direction)
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
