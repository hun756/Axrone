namespace Axrone.Geometry;

/// <summary>
/// Contact of a traversal ray with one primitive: the world-space point, the
/// ray-facing unit normal, the distance in the ray's own units, and which
/// primitive was reached. Item-level broad-phase answers stay on
/// <see cref="RayHit"/>; this record is the narrow-phase primitive answer.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
public readonly struct PrimitiveHit : IEquatable<PrimitiveHit>
{
    private readonly Vec3 _point;
    private readonly Vec3 _normal;
    private readonly Distance _distance;
    private readonly PrimitiveId _primitiveId;

    /// <summary>Builds a contact record.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PrimitiveHit(Vec3 point, Vec3 normal, Distance distance, PrimitiveId primitiveId)
    {
        _point = point;
        _normal = normal;
        _distance = distance;
        _primitiveId = primitiveId;
    }

    /// <summary>A miss: no primitive was reached, so the payload fields are meaningless.</summary>
    public static PrimitiveHit None
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(Vec3.Zero, Vec3.Zero, Distance.PositiveInfinity, PrimitiveId.None);
    }

    /// <summary>World-space contact point.</summary>
    public Vec3 Point
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _point;
    }

    /// <summary>Ray-facing unit surface normal at the contact.</summary>
    public Vec3 Normal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _normal;
    }

    /// <summary>Distance from the ray origin, in the ray's own units.</summary>
    public Distance Distance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _distance;
    }

    /// <summary>Identity of the reached primitive; <see cref="PrimitiveId.None"/> on a miss.</summary>
    public PrimitiveId PrimitiveId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _primitiveId;
    }

    /// <summary>Whether a primitive was reached.</summary>
    public bool IsHit
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _primitiveId.IsValid;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(PrimitiveHit other) =>
        _point == other._point &&
        _normal == other._normal &&
        _distance == other._distance &&
        _primitiveId == other._primitiveId;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PrimitiveHit other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_point, _normal, _distance, _primitiveId);

    /// <summary>Determines whether two contacts match in point, normal, distance and primitive.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in PrimitiveHit left, in PrimitiveHit right) => left.Equals(right);

    /// <summary>Determines whether two contacts differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in PrimitiveHit left, in PrimitiveHit right) => !left.Equals(right);
}

/// <summary>Outcome of a narrow-phase ray query.</summary>
public enum RayQueryResultKind : byte
{
    /// <summary>The ray reached nothing.</summary>
    Miss = 0,

    /// <summary>The ray reached a primitive; the nearest contact is reported.</summary>
    Hit = 1,

    /// <summary>The ray is blocked; the occluding contact is reported without a nearest search.</summary>
    Occluded = 2,
}

/// <summary>
/// Discriminated verdict of a narrow-phase ray query: miss, nearest hit, or
/// early-out occlusion. A miss is a value, not a null, so traversal writes
/// results in place and never allocates. Broad-phase item answers stay on
/// <see cref="RaycastResult{T}"/>; this union is the primitive-level answer.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 36)]
public readonly struct RayQueryResult : IEquatable<RayQueryResult>
{
    [FieldOffset(0)]
    private readonly RayQueryResultKind _kind;

    [FieldOffset(4)]
    private readonly PrimitiveHit _hit;

    /// <summary>Builds a verdict from a kind and its contact.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RayQueryResult(RayQueryResultKind kind, in PrimitiveHit hit)
    {
        _kind = kind;
        _hit = hit;
    }

    /// <summary>Whether the ray reached a primitive.</summary>
    public RayQueryResultKind Kind
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind;
    }

    /// <summary>Whether the verdict reports a nearest contact.</summary>
    public bool IsHit
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == RayQueryResultKind.Hit;
    }

    /// <summary>Whether the verdict reports nothing reached.</summary>
    public bool IsMiss
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == RayQueryResultKind.Miss;
    }

    /// <summary>Whether the verdict reports an early-out occlusion.</summary>
    public bool IsOccluded
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == RayQueryResultKind.Occluded;
    }

    /// <summary>The reported contact; rejected on a miss.</summary>
    public PrimitiveHit Hit
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_kind == RayQueryResultKind.Miss)
            {
                ThrowHelper.ThrowInvalidOperation("Payload cannot be accessed on an empty raycast result.");
            }

            return _hit;
        }
    }

    /// <summary>A miss: the ray reached nothing.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RayQueryResult Miss() => new(RayQueryResultKind.Miss, PrimitiveHit.None);

    /// <summary>Builds a nearest-contact verdict.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RayQueryResult HitResult(in PrimitiveHit hit) => new(RayQueryResultKind.Hit, hit);

    /// <summary>Builds an early-out occlusion verdict.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RayQueryResult Occluded(in PrimitiveHit hit) => new(RayQueryResultKind.Occluded, hit);

    /// <summary>Folds the verdict: one branch per kind, no type test at the call site.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult Match<TResult, TContext>(
        TContext context,
        Func<TContext, PrimitiveHit, TResult> onHit,
        Func<TContext, TResult> onMiss,
        Func<TContext, PrimitiveHit, TResult> onOccluded)
    {
        ArgumentNullException.ThrowIfNull(onHit);
        ArgumentNullException.ThrowIfNull(onMiss);
        ArgumentNullException.ThrowIfNull(onOccluded);

        return _kind switch
        {
            RayQueryResultKind.Hit => onHit(context, _hit),
            RayQueryResultKind.Miss => onMiss(context),
            RayQueryResultKind.Occluded => onOccluded(context, _hit),
            _ => ThrowHelper.ThrowInvalidOperation<TResult>("Unrecognized discriminated union discriminant.")
        };
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(RayQueryResult other) => _kind == other._kind && _hit.Equals(other._hit);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is RayQueryResult other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine((byte)_kind, _hit);

    /// <summary>Determines whether two verdicts share a kind and a contact.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in RayQueryResult left, in RayQueryResult right) => left.Equals(right);

    /// <summary>Determines whether two verdicts differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in RayQueryResult left, in RayQueryResult right) => !left.Equals(right);
}
