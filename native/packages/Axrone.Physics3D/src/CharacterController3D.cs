namespace Axrone.Physics3D;

/// <summary>Where a character controller touched the world during a move.</summary>
[Flags]
public enum CollisionFlags3D : byte
{
    /// <summary>No contact.</summary>
    None = 0,

    /// <summary>Blocked on the sides.</summary>
    Sides = 1 << 0,

    /// <summary>Head bump.</summary>
    Above = 1 << 1,

    /// <summary>Grounded.</summary>
    Below = 1 << 2,
}

/// <summary>Tuning of a character controller: slope climbing, step handling and skin.</summary>
public readonly struct CharacterControllerDef
{
    /// <summary>Creates default tuning.</summary>
    public CharacterControllerDef()
    {
    }

    /// <summary>Steepest climbable slope in degrees.</summary>
    public float SlopeLimit { get; init; } = 45.0f;

    /// <summary>Tallest step auto-climbed.</summary>
    public float StepOffset { get; init; } = 0.3f;

    /// <summary>Contact skin keeping the capsule off surfaces.</summary>
    public float SkinWidth { get; init; } = 0.08f;

    /// <summary>Motions shorter than this are dropped.</summary>
    public float MinMoveDistance { get; init; } = 0.0f;
}

/// <summary>
/// Kinematic capsule character: axis-separated sweep-and-resolve moves with
/// slope limiting, step climbing and grounding probes. The controller owns a
/// kinematic body plus a capsule shape in the world; moves never tunnel
/// through static geometry at sane speeds because each axis resolves
/// against fresh overlaps before the next axis runs.
/// </summary>
public sealed class CharacterController3D
{
    private const int QueryBufferSize = 64;

    private readonly World _world;
    private readonly BodyId _body;
    private readonly ShapeId _shape;
    private readonly float _radius;
    private readonly float _height;

    /// <summary>Slope limit in degrees.</summary>
    public float SlopeLimit { get; set; }

    /// <summary>Step height climbed automatically.</summary>
    public float StepOffset { get; set; }

    /// <summary>Contact skin.</summary>
    public float SkinWidth { get; set; }

    /// <summary>Motions below this length are dropped.</summary>
    public float MinMoveDistance { get; set; }

    /// <summary>Whether the last move found ground.</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>Contacts of the last move.</summary>
    public CollisionFlags3D CollisionFlags { get; private set; }

    /// <summary>Ground velocity underfoot; ride it by adding it to moves.</summary>
    public Vec3 GroundVelocity { get; private set; }

    /// <summary>Creates a controller capsule in the world.</summary>
    /// <param name="world">The simulated world.</param>
    /// <param name="position">Feet position.</param>
    /// <param name="radius">Capsule radius.</param>
    /// <param name="height">Total capsule height including caps.</param>
    /// <param name="definition">Tuning; defaults when null.</param>
    public CharacterController3D(World world, Vec3 position, float radius, float height, in CharacterControllerDef? definition = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (radius <= 0.0f || height <= 2.0f * radius)
        {
            ThrowHelper.ThrowInvalidOperation("Character capsule requires positive radius and height above twice the radius.");
        }

        CharacterControllerDef def = definition ?? new CharacterControllerDef();
        _world = world;
        _radius = radius;
        _height = height;
        SlopeLimit = def.SlopeLimit;
        StepOffset = def.StepOffset;
        SkinWidth = def.SkinWidth;
        MinMoveDistance = def.MinMoveDistance;

        _body = world.Bodies.Create(new BodyDef
        {
            Type = BodyType.Kinematic,
            Position = position + new Vec3(0.0f, height * 0.5f, 0.0f),
            FixedRotation = true,
            AllowSleep = false,
        });
        float halfSegment = (height * 0.5f) - radius;
        _shape = world.Shapes.AttachCapsule(
            world.Bodies, world.Broadphase, _body,
            new Vec3(0.0f, -halfSegment, 0.0f), new Vec3(0.0f, halfSegment, 0.0f), radius);
        world.Shapes.SyncBody(world.Bodies, world.Broadphase, _body);
    }

    /// <summary>Controlled body.</summary>
    public BodyId Body => _body;

    /// <summary>Capsule radius.</summary>
    public float Radius => _radius;

    /// <summary>Total capsule height including caps.</summary>
    public float Height => _height;

    /// <summary>Capsule center in world space.</summary>
    public Vec3 Center => _world.Bodies.GetTransform(_body).Position;

    /// <summary>Moves by velocity over a step, resolving per axis.</summary>
    /// <param name="velocity">Desired velocity.</param>
    /// <param name="dt">Step length.</param>
    /// <returns>Contacts of the move.</returns>
    public CollisionFlags3D Move(Vec3 velocity, float dt)
    {
        Vec3 displacement = velocity * dt;
        if (displacement.LengthSquared() < MinMoveDistance * MinMoveDistance)
        {
            ProbeGround();
            return CollisionFlags;
        }

        CollisionFlags = CollisionFlags3D.None;
        MoveAxis(new Vec3(displacement.X, 0.0f, 0.0f), CollisionFlags3D.Sides);
        MoveAxis(new Vec3(0.0f, 0.0f, displacement.Z), CollisionFlags3D.Sides);
        MoveAxis(new Vec3(0.0f, displacement.Y, 0.0f), displacement.Y <= 0.0f ? CollisionFlags3D.Below : CollisionFlags3D.Above);
        ProbeGround();
        return CollisionFlags;
    }

    /// <summary>Moves with gravity applied; equivalent to the web simpleMove.</summary>
    /// <param name="velocity">Desired horizontal velocity; vertical accumulates gravity.</param>
    /// <param name="dt">Step length.</param>
    /// <param name="gravity">Gravitational acceleration.</param>
    /// <returns>Contacts of the move.</returns>
    public CollisionFlags3D SimpleMove(Vec3 velocity, float dt, Vec3 gravity)
    {
        GroundVelocity = IsGrounded ? gravity * dt : GroundVelocity + (gravity * dt);
        return Move(new Vec3(velocity.X, GroundVelocity.Y, velocity.Z), dt);
    }

    /// <summary>Sweeps one axis, climbing steps and sliding out of penetration.</summary>
    private void MoveAxis(Vec3 delta, CollisionFlags3D flag)
    {
        if (delta.LengthSquared() <= 1e-16f)
        {
            return;
        }

        Vec3 position = _world.Bodies.GetTransform(_body).Position;
        _world.Bodies.SetTransform(_body, position + delta, Quat.Identity);
        _world.Shapes.SyncBody(_world.Bodies, _world.Broadphase, _body);

        Span<ShapeId> overlaps = stackalloc ShapeId[QueryBufferSize];
        Aabb3D bounds = _world.Shapes.GetWorldAabb(_shape);
        int count = _world.OverlapAabb(bounds, overlaps);
        Vec3 moveDirection = delta.LengthSquared() > 1e-16f ? delta / delta.Length() : delta;
        for (int i = 0; i < count; i++)
        {
            if (overlaps[i] == _shape)
            {
                continue;
            }

            if (!ResolveOverlap(overlaps[i], delta, moveDirection, flag))
            {
                break;
            }
        }
    }

    /// <summary>Pushes out of one overlap, stepping up when the blocker is wall-like.</summary>
    private bool ResolveOverlap(ShapeId other, Vec3 delta, Vec3 moveDirection, CollisionFlags3D flag)
    {
        if (EvaluateOverlap(other, out Vec3 normal, out float separation) && separation >= 0.0f)
        {
            return true;
        }

        if (MathF.Abs(Vec3.Dot(normal, moveDirection)) < 0.5f)
        {
            return true;
        }

        if (flag == CollisionFlags3D.Sides && MathF.Abs(normal.Y) < 0.5f && TryStepUp(delta))
        {
            return true;
        }

        Vec3 push = normal * separation;
        Vec3 position = _world.Bodies.GetTransform(_body).Position;
        _world.Bodies.SetTransform(_body, position - push, Quat.Identity);
        _world.Shapes.SyncBody(_world.Bodies, _world.Broadphase, _body);
        CollisionFlags |= flag;
        return false;
    }

    /// <summary>Evaluates the narrowphase against another shape; separated when disjoint.</summary>
    /// <returns><see langword="true"/> when disjoint.</returns>
    private bool EvaluateOverlap(ShapeId other, out Vec3 normal, out float separation)
    {
        Capsule3 capsule = _world.Shapes.GetWorldCapsule(_shape);
        ref ShapeRecord record = ref _world.Shapes.Record(other);

        Manifold3D manifold;
        bool separated;
        if (record.Kind == ColliderKind.Sphere)
        {
            Sphere3 world = _world.Shapes.GetWorldSphere(other);
            separated = !Penetration3D.CapsuleSphere(capsule, world, out manifold);
        }
        else if (record.Kind == ColliderKind.Box)
        {
            Box3 local = record.LocalBox;
            RigidTransform3D frame = _world.Shapes.GetWorldFrame(other);
            separated = !Penetration3D.CapsuleBox(capsule, local, frame, out manifold);
        }
        else if (record.Kind == ColliderKind.Capsule)
        {
            Capsule3 world = _world.Shapes.GetWorldCapsule(other);
            separated = !Penetration3D.CapsuleCapsule(capsule, world, out manifold);
        }
        else
        {
            AnyCollider self = AnyCollider.FromCapsule(
                _world.Shapes.GetCapsule(_shape), _world.Shapes.GetWorldFrame(_shape));
            AnyCollider target = AnyCollider.FromRecord(in record, _world.Shapes.GetWorldFrame(other));
            separated = !Epa3D.ComputePenetration(self, target, out manifold);
        }

        normal = separated ? Vec3.Zero : manifold.Normal;
        separation = separated ? 0.0f : manifold[0].Separation;
        return separated;
    }

    /// <summary>Tries to climb a low blocker instead of stopping.</summary>
    private bool TryStepUp(Vec3 delta)
    {
        if (StepOffset <= 0.0f)
        {
            return false;
        }

        Vec3 position = _world.Bodies.GetTransform(_body).Position;
        Vec3 lifted = position + new Vec3(0.0f, StepOffset + SkinWidth, 0.0f);
        _world.Bodies.SetTransform(_body, lifted + delta, Quat.Identity);
        _world.Shapes.SyncBody(_world.Bodies, _world.Broadphase, _body);

        Span<ShapeId> overlaps = stackalloc ShapeId[QueryBufferSize];
        Aabb3D bounds = _world.Shapes.GetWorldAabb(_shape);
        int count = _world.OverlapAabb(bounds, overlaps);
        for (int i = 0; i < count; i++)
        {
            if (overlaps[i] == _shape)
            {
                continue;
            }

            if (IsOverlapping(overlaps[i]))
            {
                _world.Bodies.SetTransform(_body, position, Quat.Identity);
                _world.Shapes.SyncBody(_world.Bodies, _world.Broadphase, _body);
                return false;
            }
        }

        _world.Bodies.SetTransform(_body, lifted + delta, Quat.Identity);
        _world.Shapes.SyncBody(_world.Bodies, _world.Broadphase, _body);
        CollisionFlags |= CollisionFlags3D.Below;
        return true;
    }

    /// <summary>Whether two shapes truly overlap through the narrowphase.</summary>
    private bool IsOverlapping(ShapeId other) =>
        !EvaluateOverlap(other, out _, out _);

    /// <summary>Casts down to find ground, honoring the slope limit.</summary>
    private void ProbeGround()
    {
        IsGrounded = false;

        Capsule3 capsule = _world.Shapes.GetWorldCapsule(_shape);
        float bottom = MathF.Min(capsule.P0.Y, capsule.P1.Y) - capsule.Radius;
        Vec3 feet = new((capsule.P0.X + capsule.P1.X) * 0.5f, bottom, (capsule.P0.Z + capsule.P1.Z) * 0.5f);
        var ray = new Ray3D(feet + new Vec3(0.0f, SkinWidth, 0.0f), new Vec3(0.0f, -1.0f, 0.0f));

        Span<RaycastHit3D> hits = stackalloc RaycastHit3D[QueryBufferSize];
        int count = _world.Raycast(ray, SkinWidth + StepOffset + 0.05f, hits);
        for (int i = 0; i < count; i++)
        {
            if (hits[i].Shape == _shape)
            {
                continue;
            }

            float slope = MathF.Acos(Math.Clamp(hits[i].Normal.Y, -1.0f, 1.0f)) * (180.0f / MathF.PI);
            if (slope <= SlopeLimit)
            {
                IsGrounded = true;
                CollisionFlags |= CollisionFlags3D.Below;
                if (_world.Bodies.GetType(hits[i].Body) == BodyType.Kinematic)
                {
                    GroundVelocity = _world.Bodies.GetLinearVelocity(hits[i].Body);
                }

                return;
            }
        }
    }
}
