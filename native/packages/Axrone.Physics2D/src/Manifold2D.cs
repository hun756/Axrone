namespace Axrone.Physics2D;

/// <summary>
/// Stable identity of one contact point across frames: the reference edge it
/// was clipped from, the incident vertex it came from, and which side owned
/// the reference face. Matching by id (not by position) is what lets the
/// solver carry impulses forward without teleporting them between points.
/// </summary>
public readonly record struct ContactId(uint Value)
{
    private const uint FlipMask = 0x8000_0000u;
    private const uint RefEdgeMask = 0x7FFF_0000u;
    private const int RefEdgeShift = 16;
    private const uint IncidentMask = 0x0000_FFFFu;

    /// <summary>Null identity; never matches a real contact.</summary>
    public static ContactId Null => default;

    /// <summary>Builds an id from a reference edge, an incident vertex and the reference side.</summary>
    /// <param name="referenceEdge">Index of the reference face.</param>
    /// <param name="incidentVertex">Index of the incident vertex the point came from.</param>
    /// <param name="flipped">Whether the reference face belonged to the second shape.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ContactId Create(int referenceEdge, int incidentVertex, bool flipped) =>
        new(((flipped ? FlipMask : 0u))
            | (((uint)referenceEdge << RefEdgeShift) & RefEdgeMask)
            | ((uint)incidentVertex & IncidentMask));

    /// <summary>Index of the reference face.</summary>
    public int ReferenceEdge
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (int)((Value & RefEdgeMask) >> RefEdgeShift);
    }

    /// <summary>Index of the incident vertex the point came from.</summary>
    public int IncidentVertex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (int)(Value & IncidentMask);
    }

    /// <summary>Whether the reference face belonged to the second shape.</summary>
    public bool Flipped
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Value & FlipMask) != 0u;
    }

    /// <summary>Returns the same contact with the reference side toggled.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ContactId FlippedId() => new(Value ^ FlipMask);
}

/// <summary>
/// One contact of a manifold: the witness on each shape, the signed
/// separation (negative while overlapping, zero while touching), the stable
/// id, and the solver-owned accumulated impulses (zero until the solver
/// runs; carried across frames by id).
/// </summary>
public struct ContactPoint : IEquatable<ContactPoint>
{
    /// <summary>Witness on the first shape.</summary>
    public Vec2 PositionA { get; }

    /// <summary>Witness on the second shape.</summary>
    public Vec2 PositionB { get; }

    /// <summary>Signed separation; negative on overlap.</summary>
    public float Separation { get; }

    /// <summary>Stable identity for warm starting.</summary>
    public ContactId Id { get; }

    /// <summary>Accumulated normal impulse; solver-owned.</summary>
    public float NormalImpulse { get; set; }

    /// <summary>Accumulated tangent impulse; solver-owned.</summary>
    public float TangentImpulse { get; set; }

    /// <summary>Creates a contact with zeroed impulses.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ContactPoint(Vec2 positionA, Vec2 positionB, float separation, ContactId id)
    {
        PositionA = positionA;
        PositionB = positionB;
        Separation = separation;
        Id = id;
        NormalImpulse = 0.0f;
        TangentImpulse = 0.0f;
    }

    /// <summary>Returns the same contact with the shape sides swapped.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ContactPoint Flipped() =>
        new(PositionB, PositionA, Separation, Id.FlippedId())
        {
            NormalImpulse = NormalImpulse,
            TangentImpulse = TangentImpulse,
        };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ContactPoint other) =>
        PositionA == other.PositionA &&
        PositionB == other.PositionB &&
        Separation == other.Separation &&
        Id == other.Id;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ContactPoint other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(PositionA, PositionB, Separation, Id);

    /// <summary>Determines whether two contacts match in witnesses, separation and id.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(ContactPoint left, ContactPoint right) => left.Equals(right);

    /// <summary>Determines whether two contacts differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(ContactPoint left, ContactPoint right) => !left.Equals(right);
}

/// <summary>
/// Contact set of one shape pair: the shared unit normal (first shape toward
/// second) plus up to two points. Zero points means no contact. Fixed fields
/// instead of an array so the hot path stays allocation-free; the solver
/// reaches points by reference to accumulate impulses in place.
/// </summary>
public struct ContactManifold : IEquatable<ContactManifold>
{
    private Vec2 _normal;
    private int _count;
    private ContactPoint _point0;
    private ContactPoint _point1;

    /// <summary>Empty manifold: no contact.</summary>
    public static ContactManifold Empty => default;

    /// <summary>Shared unit normal, first shape toward second.</summary>
    public Vec2 Normal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _normal;
    }

    /// <summary>Contact count: 0, 1 or 2.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    /// <summary>Creates a one-point manifold.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ContactManifold(Vec2 normal, in ContactPoint point)
    {
        _normal = normal;
        _count = 1;
        _point0 = point;
        _point1 = default;
    }

    /// <summary>Creates a two-point manifold.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ContactManifold(Vec2 normal, in ContactPoint point0, in ContactPoint point1)
    {
        _normal = normal;
        _count = 2;
        _point0 = point0;
        _point1 = point1;
    }

    /// <summary>First contact (by value).</summary>
    public ContactPoint Point0
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _point0;
    }

    /// <summary>Second contact (by value); meaningless unless <see cref="Count"/> is 2.</summary>
    public ContactPoint Point1
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _point1;
    }

    /// <summary>Writes solver impulses into the contact at <paramref name="index"/> in place.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetImpulses(int index, float normalImpulse, float tangentImpulse)
    {
        if (index == 0)
        {
            _point0.NormalImpulse = normalImpulse;
            _point0.TangentImpulse = tangentImpulse;
            return;
        }

        _point1.NormalImpulse = normalImpulse;
        _point1.TangentImpulse = tangentImpulse;
    }

    /// <summary>Reads the contact at <paramref name="index"/> by value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ContactPoint GetPointRO(int index) => index == 0 ? _point0 : _point1;

    /// <summary>Returns the same manifold with the shape sides swapped.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ContactManifold Flipped()
    {
        ContactManifold flipped = new()
        {
            _normal = _normal * -1.0f,
            _count = _count,
            _point0 = _point0.Flipped(),
            _point1 = _point1.Flipped(),
        };
        return flipped;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ContactManifold other) =>
        _normal == other._normal &&
        _count == other._count &&
        _point0.Equals(other._point0) &&
        (_count < 2 || _point1.Equals(other._point1));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ContactManifold other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_normal, _count, _point0, _point1);

    /// <summary>Determines whether two manifolds match in normal, count and contacts.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(ContactManifold left, ContactManifold right) => left.Equals(right);

    /// <summary>Determines whether two manifolds differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(ContactManifold left, ContactManifold right) => !left.Equals(right);
}

/// <summary>
/// Analytical narrowphase over 2D shape pairs: exact contact sets for the
/// flat pairs (circle-circle, circle-polygon, polygon-polygon) plus a single
/// EPA witness point for anything involving a capsule. Every evaluator
/// returns <see langword="false"/> with an empty manifold while separated and
/// <see langword="true"/> on touching or overlap; nothing allocates.
/// </summary>
public static class Narrowphase2D
{
    private const float SeparationTolerance = 1e-9f;
    private const float DegenerateLengthSquared = 1e-12f;

    /// <summary>Evaluates two discs.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool EvaluateCircleCircle(in Circle2 a, in Circle2 b, out ContactManifold manifold)
    {
        Vec2 axis = b.Center - a.Center;
        float radiusSum = a.Radius + b.Radius;
        float distSq = axis.LengthSquared();

        if (distSq > radiusSum * radiusSum)
        {
            manifold = ContactManifold.Empty;
            return false;
        }

        Vec2 normal;
        float depth;
        Vec2 pointA;
        Vec2 pointB;

        if (distSq < DegenerateLengthSquared)
        {
            normal = new Vec2(1.0f, 0.0f);
            depth = radiusSum;
            pointA = a.Center + (normal * a.Radius);
            pointB = b.Center - (normal * b.Radius);
        }
        else
        {
            float distance = MathF.Sqrt(distSq);
            normal = axis * (1.0f / distance);
            depth = radiusSum - distance;
            pointA = a.Center + (normal * a.Radius);
            pointB = b.Center - (normal * b.Radius);
        }

        manifold = new ContactManifold(
            normal,
            new ContactPoint(pointA, pointB, -depth, ContactId.Null));
        return true;
    }

    /// <summary>Evaluates a disc against a convex polygon (circle first).</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool EvaluateCirclePolygon(in Circle2 circle, in Polygon2D polygon, out ContactManifold manifold)
    {
        ReadOnlySpan<Vec2> vertices = polygon.Vertices;
        ReadOnlySpan<Vec2> normals = polygon.Normals;
        int count = vertices.Length;

        float bestSep = float.NegativeInfinity;
        int bestEdge = 0;
        for (int i = 0; i < count; i++)
        {
            float sep = Vec2.Dot(normals[i], circle.Center - vertices[i]);
            if (sep > bestSep)
            {
                bestSep = sep;
                bestEdge = i;
            }
        }

        if (bestSep > circle.Radius + SeparationTolerance)
        {
            manifold = ContactManifold.Empty;
            return false;
        }

        Vec2 v0 = vertices[bestEdge];
        Vec2 v1 = vertices[(bestEdge + 1) % count];

        if (bestSep <= 0.0f)
        {
            Vec2 outward = normals[bestEdge];
            float faceDistance = -bestSep;
            Vec2 normal = outward * -1.0f;
            float depth = circle.Radius + faceDistance;
            Vec2 pointB = circle.Center - (outward * bestSep);
            Vec2 pointA = circle.Center + (normal * circle.Radius);
            manifold = new ContactManifold(
                normal,
                new ContactPoint(pointA, pointB, -depth, ContactId.Create(bestEdge, 0, flipped: false)));
            return true;
        }

        Vec2 edge = v1 - v0;
        float edgeLengthSq = edge.LengthSquared();
        float t = edgeLengthSq < DegenerateLengthSquared
            ? 0.0f
            : Vec2.Dot(circle.Center - v0, edge) / edgeLengthSq;
        t = Math.Clamp(t, 0.0f, 1.0f);
        Vec2 closest = v0 + (edge * t);
        Vec2 toClosest = closest - circle.Center;
        float distSq = toClosest.LengthSquared();
        float dist = MathF.Sqrt(distSq);

        if (dist > circle.Radius + SeparationTolerance)
        {
            manifold = ContactManifold.Empty;
            return false;
        }

        Vec2 hitNormal;
        Vec2 hitB;
        if (distSq < DegenerateLengthSquared)
        {
            hitNormal = normals[bestEdge] * -1.0f;
            hitB = closest;
        }
        else
        {
            hitNormal = toClosest * (1.0f / dist);
            hitB = closest;
        }

        float hitDepth = circle.Radius - dist;
        Vec2 hitA = circle.Center + (hitNormal * circle.Radius);
        manifold = new ContactManifold(
            hitNormal,
            new ContactPoint(hitA, hitB, -hitDepth, ContactId.Create(bestEdge, 0, flipped: false)));
        return true;
    }

    /// <summary>Evaluates a convex polygon against a disc (polygon first) via the circle-first order.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool EvaluatePolygonCircle(in Polygon2D polygon, in Circle2 circle, out ContactManifold manifold)
    {
        bool hit = EvaluateCirclePolygon(in circle, in polygon, out ContactManifold inner);
        manifold = hit ? inner.Flipped() : ContactManifold.Empty;
        return hit;
    }

    /// <summary>Evaluates two convex polygons with SAT plus incident-edge clipping (up to two points).</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool EvaluatePolygonPolygon(in Polygon2D a, in Polygon2D b, out ContactManifold manifold)
    {
        float sepA = MaxSeparation(in a, in b, out int edgeA);
        float sepB = MaxSeparation(in b, in a, out int edgeB);

        if (sepA > SeparationTolerance || sepB > SeparationTolerance)
        {
            manifold = ContactManifold.Empty;
            return false;
        }

        bool flipped = sepB > sepA;
        if (!flipped)
        {
            return ClipPair(in a, edgeA, in b, flipped: false, out manifold);
        }

        bool hit = ClipPair(in b, edgeB, in a, flipped: true, out ContactManifold inner);
        manifold = hit ? inner.Flipped() : ContactManifold.Empty;
        return hit;
    }

    /// <summary>Evaluates a capsule against a disc through one EPA witness point.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool EvaluateCapsuleCircle(in Capsule2D capsule, in Circle2 circle, out ContactManifold manifold) =>
        EvaluateEpaFallback(in capsule, in circle, out manifold);

    /// <summary>Evaluates a capsule against a convex polygon through one EPA witness point.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool EvaluateCapsulePolygon(in Capsule2D capsule, in Polygon2D polygon, out ContactManifold manifold) =>
        EvaluateEpaFallback(in capsule, in polygon, out manifold);

    /// <summary>Evaluates two capsules through one EPA witness point.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool EvaluateCapsuleCapsule(in Capsule2D a, in Capsule2D b, out ContactManifold manifold) =>
        EvaluateEpaFallback(in a, in b, out manifold);

    /// <summary>Single-point fallback for pairs without an analytic clip: EPA normal, depth and witnesses.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool EvaluateEpaFallback<TA, TB>(in TA shapeA, in TB shapeB, out ContactManifold manifold)
        where TA : ISupportShape2D<TA>
        where TB : ISupportShape2D<TB>
    {
        if (!Epa2D.ComputePenetration(in shapeA, in shapeB, out Vec2 normal, out float depth, out Vec2 pointA, out Vec2 pointB))
        {
            manifold = ContactManifold.Empty;
            return false;
        }

        manifold = new ContactManifold(
            normal,
            new ContactPoint(pointA, pointB, -depth, ContactId.Null));
        return true;
    }

    /// <summary>Largest separation of <paramref name="reference"/> edges against <paramref name="incident"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float MaxSeparation(in Polygon2D reference, in Polygon2D incident, out int bestEdge)
    {
        ReadOnlySpan<Vec2> refVertices = reference.Vertices;
        ReadOnlySpan<Vec2> refNormals = reference.Normals;

        float best = float.NegativeInfinity;
        bestEdge = 0;

        for (int i = 0; i < refVertices.Length; i++)
        {
            Vec2 support = Polygon2D.Support(in incident, refNormals[i] * -1.0f);
            float separation = Vec2.Dot(refNormals[i], support - refVertices[i]);
            if (separation > best)
            {
                best = separation;
                bestEdge = i;
            }
        }

        return best;
    }

    /// <summary>Clips the most anti-parallel incident edge to the reference side planes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool ClipPair(in Polygon2D reference, int referenceEdge, in Polygon2D incident, bool flipped, out ContactManifold manifold)
    {
        ReadOnlySpan<Vec2> refVertices = reference.Vertices;
        ReadOnlySpan<Vec2> refNormals = reference.Normals;
        ReadOnlySpan<Vec2> incVertices = incident.Vertices;
        ReadOnlySpan<Vec2> incNormals = incident.Normals;

        Vec2 refNormal = refNormals[referenceEdge];
        Vec2 refV0 = refVertices[referenceEdge];
        Vec2 refV1 = refVertices[(referenceEdge + 1) % refVertices.Length];

        int incidentEdge = 0;
        float bestAnti = float.PositiveInfinity;
        for (int i = 0; i < incNormals.Length; i++)
        {
            float alignment = Vec2.Dot(refNormal, incNormals[i]);
            if (alignment < bestAnti)
            {
                bestAnti = alignment;
                incidentEdge = i;
            }
        }

        Vec2 s0 = incVertices[incidentEdge];
        Vec2 s1 = incVertices[(incidentEdge + 1) % incVertices.Length];

        Vec2 tangent = new(-refNormal.Y, refNormal.X);
        float frontOffset = Vec2.Dot(tangent, refV1);
        float backOffset = Vec2.Dot(tangent * -1.0f, refV0);

        Vec2 c0 = s0;
        Vec2 c1 = s1;
        int id0 = incidentEdge;
        int id1 = (incidentEdge + 1) % incVertices.Length;

        if (!ClipToPlane(ref c0, ref c1, ref id0, ref id1, tangent, frontOffset) ||
            !ClipToPlane(ref c0, ref c1, ref id0, ref id1, tangent * -1.0f, backOffset))
        {
            manifold = ContactManifold.Empty;
            return false;
        }

        float sep0 = Vec2.Dot(refNormal, c0 - refV0);
        float sep1 = Vec2.Dot(refNormal, c1 - refV0);

        bool keep0 = sep0 <= SeparationTolerance;
        bool keep1 = sep1 <= SeparationTolerance;

        if (!keep0 && !keep1)
        {
            manifold = ContactManifold.Empty;
            return false;
        }

        if (keep0 && keep1)
        {
            if ((c1 - c0).LengthSquared() < DegenerateLengthSquared)
            {
                Vec2 a0 = c0 - (refNormal * sep0);
                manifold = new ContactManifold(
                    refNormal,
                    new ContactPoint(a0, c0, sep0, ContactId.Create(referenceEdge, id0, flipped)));
                return true;
            }

            Vec2 a0b = c0 - (refNormal * sep0);
            Vec2 a1b = c1 - (refNormal * sep1);
            manifold = new ContactManifold(
                refNormal,
                new ContactPoint(a0b, c0, sep0, ContactId.Create(referenceEdge, id0, flipped)),
                new ContactPoint(a1b, c1, sep1, ContactId.Create(referenceEdge, id1, flipped)));
            return true;
        }

        if (keep0)
        {
            Vec2 a0 = c0 - (refNormal * sep0);
            manifold = new ContactManifold(
                refNormal,
                new ContactPoint(a0, c0, sep0, ContactId.Create(referenceEdge, id0, flipped)));
            return true;
        }

        Vec2 a1Only = c1 - (refNormal * sep1);
        manifold = new ContactManifold(
            refNormal,
            new ContactPoint(a1Only, c1, sep1, ContactId.Create(referenceEdge, id1, flipped)));
        return true;
    }

    /// <summary>Clips a segment to one half-plane, keeping the inside part with stable vertex provenance.</summary>
    /// <returns><see langword="false"/> when nothing survives.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool ClipToPlane(ref Vec2 p0, ref Vec2 p1, ref int id0, ref int id1, Vec2 direction, float offset)
    {
        float d0 = Vec2.Dot(direction, p0) - offset;
        float d1 = Vec2.Dot(direction, p1) - offset;

        if (d0 <= 0.0f && d1 <= 0.0f)
        {
            return true;
        }

        if (d0 > 0.0f && d1 > 0.0f)
        {
            return false;
        }

        float t = d0 / (d0 - d1);
        Vec2 crossing = p0 + ((p1 - p0) * t);

        if (d0 > 0.0f)
        {
            p0 = crossing;
            id0 = id1;
        }
        else
        {
            p1 = crossing;
            id1 = id0;
        }

        return true;
    }
}

/// <summary>
/// Warm-start store for one pair: remembers up to two contact ids with their
/// solver impulses, reapplies them to the next frame's manifold on id match,
/// and forgets everything else. Small fixed fields, no heap, no dictionary.
/// </summary>
public struct ContactCache : IEquatable<ContactCache>
{
    private ContactId _id0;
    private ContactId _id1;
    private float _normal0;
    private float _tangent0;
    private float _normal1;
    private float _tangent1;
    private int _count;

    /// <summary>Contacts remembered.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    /// <summary>Copies stored impulses into matching points of <paramref name="manifold"/> by id.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void WarmStart(ref ContactManifold manifold)
    {
        for (int i = 0; i < manifold.Count; i++)
        {
            ContactPoint point = manifold.GetPointRO(i);
            if (point.Id == _id0 && _count > 0)
            {
                manifold.SetImpulses(i, _normal0, _tangent0);
            }
            else if (_count > 1 && point.Id == _id1)
            {
                manifold.SetImpulses(i, _normal1, _tangent1);
            }
        }
    }

    /// <summary>Remembers the ids and impulses of <paramref name="manifold"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Store(in ContactManifold manifold)
    {
        _count = manifold.Count;
        if (_count > 0)
        {
            ContactPoint p0 = manifold.Point0;
            _id0 = p0.Id;
            _normal0 = p0.NormalImpulse;
            _tangent0 = p0.TangentImpulse;
        }

        if (_count > 1)
        {
            ContactPoint p1 = manifold.Point1;
            _id1 = p1.Id;
            _normal1 = p1.NormalImpulse;
            _tangent1 = p1.TangentImpulse;
        }
    }

    /// <summary>Forgets everything.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _count = 0;
        _id0 = ContactId.Null;
        _id1 = ContactId.Null;
        _normal0 = 0.0f;
        _tangent0 = 0.0f;
        _normal1 = 0.0f;
        _tangent1 = 0.0f;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ContactCache other) =>
        _count == other._count &&
        _id0 == other._id0 &&
        _id1 == other._id1;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ContactCache other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_count, _id0, _id1);

    /// <summary>Determines whether two caches remember the same contacts.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(ContactCache left, ContactCache right) => left.Equals(right);

    /// <summary>Determines whether two caches differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(ContactCache left, ContactCache right) => !left.Equals(right);
}
