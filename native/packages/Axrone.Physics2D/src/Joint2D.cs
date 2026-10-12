namespace Axrone.Physics2D;

/// <summary>Stored joint kind; selects the active constraint of a joint record.</summary>
public enum JointKind : byte
{
    /// <summary>Rigid rod between two anchors.</summary>
    Distance = 0,

    /// <summary>Pinned hinge with optional limits and motor.</summary>
    Revolute = 1,

    /// <summary>Locked relative pose; bodies move as one.</summary>
    Weld = 2,

    /// <summary>Slider along an axis with optional stops and motor.</summary>
    Prismatic = 3,
}

/// <summary>Angular limit state of a revolute joint, resolved every step.</summary>
public enum LimitState : byte
{
    /// <summary>Limits disabled or angle strictly inside.</summary>
    Inactive = 0,

    /// <summary>At the lower stop; only opening impulses apply.</summary>
    AtLower = 1,

    /// <summary>At the upper stop; only closing impulses apply.</summary>
    AtUpper = 2,

    /// <summary>Stops coincide; the angle is locked.</summary>
    Equal = 3,
}

/// <summary>
/// 4-byte identity of a joint. The zero value is the invalid sentinel, so a
/// default-initialized slot can never name a live joint.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct JointId(uint Value) : IComparable<JointId>
{
    /// <summary>Invalid sentinel; never a registered joint.</summary>
    public static JointId Invalid => default;

    /// <summary>Whether this identity names a registered joint.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value != 0;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(JointId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => IsValid ? $"Joint({Value})" : "Joint(Invalid)";

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(JointId left, JointId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(JointId left, JointId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(JointId left, JointId right) => left.Value <= right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(JointId left, JointId right) => left.Value >= right.Value;

    /// <summary>Unwraps the underlying identifier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(JointId id) => id.Value;

    /// <summary>Wraps a raw identifier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator JointId(uint value) => new(value);
}

/// <summary>
/// One distance constraint: two bodies pinned through body-frame anchors at
/// a fixed rest length. The accumulated impulse persists across steps for
/// warm starting. Mutable storage owned by <see cref="JointStore"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct JointRecord
{
    /// <summary>Whether the slot holds a live joint.</summary>
    public bool Live;

    /// <summary>Active constraint variant.</summary>
    public JointKind Kind;

    /// <summary>First body.</summary>
    public BodyId BodyA;

    /// <summary>Second body.</summary>
    public BodyId BodyB;

    /// <summary>Anchor in the first body frame.</summary>
    public Vec2 LocalAnchorA;

    /// <summary>Anchor in the second body frame.</summary>
    public Vec2 LocalAnchorB;

    /// <summary>Rest separation of the anchors; never negative.</summary>
    public float Length;

    /// <summary>Accumulated impulse; warm-started every step.</summary>
    public float Impulse;

    /// <summary>Reference angle (second minus first) at creation.</summary>
    public float ReferenceAngle;

    /// <summary>Lower angular stop in radians.</summary>
    public float LowerAngle;

    /// <summary>Upper angular stop in radians.</summary>
    public float UpperAngle;

    /// <summary>Whether angular stops apply.</summary>
    public bool EnableLimit;

    /// <summary>Target relative angular velocity.</summary>
    public float MotorSpeed;

    /// <summary>Peak motor torque; never negative.</summary>
    public float MaxMotorTorque;

    /// <summary>Whether the motor drives.</summary>
    public bool EnableMotor;

    /// <summary>Accumulated point impulse; warm-started every step.</summary>
    public Vec2 PointImpulse;

    /// <summary>Accumulated motor impulse; warm-started every step.</summary>
    public float MotorImpulse;

    /// <summary>Accumulated limit impulse; warm-started every step.</summary>
    public float LimitImpulse;

    /// <summary>Motor impulse cap of the current step (torque times dt).</summary>
    public float MotorMaxImpulse;

    /// <summary>Resolved limit state of the current step.</summary>
    public LimitState LimitState;

    /// <summary>Slider axis in the first body frame; live for prismatic joints.</summary>
    public Vec2 LocalAxisA;

    /// <summary>Along-axis separation at creation; the prismatic zero.</summary>
    public float ReferenceTranslation;

    /// <summary>Lower slide stop along the axis.</summary>
    public float LowerTranslation;

    /// <summary>Upper slide stop along the axis.</summary>
    public float UpperTranslation;

    /// <summary>Whether slide stops apply.</summary>
    public bool EnablePrismaticLimit;

    /// <summary>Target slide speed along the axis.</summary>
    public float PrismaticMotorSpeed;

    /// <summary>Peak slide force; never negative.</summary>
    public float MaxPrismaticForce;

    /// <summary>Whether the slide motor drives.</summary>
    public bool EnablePrismaticMotor;

    /// <summary>Accumulated weld angle impulse; warm-started every step.</summary>
    public float WeldAngleImpulse;

    /// <summary>Accumulated perp impulse (.Y); the slide axis stays free except limit/motor.</summary>
    public Vec2 PrismaticImpulse;

    /// <summary>Accumulated slide motor impulse; warm-started every step.</summary>
    public float PrismaticMotorImpulse;

    /// <summary>Accumulated slide limit impulse; warm-started every step.</summary>
    public float PrismaticLimitImpulse;

    /// <summary>Slide motor impulse cap of the current step (force times dt).</summary>
    public float PrismaticMotorMaxImpulse;

    /// <summary>Resolved slide limit state of the current step.</summary>
    public LimitState PrismaticLimitState;
}

/// <summary>
/// Construction parameters of a revolute joint: two bodies pinned through a
/// shared world anchor, with optional angular stops and a speed motor.
/// Local anchors and the reference angle derive from the world anchor at
/// creation, so the caller never converts frames by hand.
/// </summary>
public readonly struct RevoluteDef
{
    /// <summary>Creates default parameters.</summary>
    public RevoluteDef()
    {
    }

    /// <summary>First body.</summary>
    public BodyId BodyA { get; init; } = BodyId.Invalid;

    /// <summary>Second body.</summary>
    public BodyId BodyB { get; init; } = BodyId.Invalid;

    /// <summary>Shared world anchor.</summary>
    public Vec2 WorldAnchor { get; init; } = default;

    /// <summary>Lower angular stop in radians.</summary>
    public float LowerAngle { get; init; } = 0.0f;

    /// <summary>Upper angular stop in radians.</summary>
    public float UpperAngle { get; init; } = 0.0f;

    /// <summary>Whether angular stops apply.</summary>
    public bool EnableLimit { get; init; } = false;

    /// <summary>Target relative angular velocity.</summary>
    public float MotorSpeed { get; init; } = 0.0f;

    /// <summary>Peak motor torque; never negative.</summary>
    public float MaxMotorTorque { get; init; } = 0.0f;

    /// <summary>Whether the motor drives.</summary>
    public bool EnableMotor { get; init; } = false;
}

/// <summary>
/// Construction parameters of a weld joint: two bodies locked through a
/// shared world anchor. Local anchors and the reference angle derive from
/// the world anchor at creation.
/// </summary>
public readonly struct WeldDef
{
    /// <summary>Creates default parameters.</summary>
    public WeldDef()
    {
    }

    /// <summary>First body.</summary>
    public BodyId BodyA { get; init; } = BodyId.Invalid;

    /// <summary>Second body.</summary>
    public BodyId BodyB { get; init; } = BodyId.Invalid;

    /// <summary>Shared world anchor.</summary>
    public Vec2 WorldAnchor { get; init; } = default;
}

/// <summary>
/// Construction parameters of a prismatic joint: two bodies sliding along a
/// shared world axis through a world anchor, with optional slide stops and
/// a slide motor. Local anchors, axis and reference translation derive from
/// the world frame at creation.
/// </summary>
public readonly struct PrismaticDef
{
    /// <summary>Creates default parameters.</summary>
    public PrismaticDef()
    {
    }

    /// <summary>First body.</summary>
    public BodyId BodyA { get; init; } = BodyId.Invalid;

    /// <summary>Second body.</summary>
    public BodyId BodyB { get; init; } = BodyId.Invalid;

    /// <summary>Shared world anchor.</summary>
    public Vec2 WorldAnchor { get; init; } = default;

    /// <summary>Slide axis in world coordinates; normalized at creation.</summary>
    public Vec2 WorldAxis { get; init; } = new Vec2(1, 0);

    /// <summary>Lower slide stop along the axis.</summary>
    public float LowerTranslation { get; init; } = 0.0f;

    /// <summary>Upper slide stop along the axis.</summary>
    public float UpperTranslation { get; init; } = 0.0f;

    /// <summary>Whether slide stops apply.</summary>
    public bool EnableLimit { get; init; } = false;

    /// <summary>Target slide speed along the axis.</summary>
    public float MotorSpeed { get; init; } = 0.0f;

    /// <summary>Peak slide force; never negative.</summary>
    public float MaxMotorForce { get; init; } = 0.0f;

    /// <summary>Whether the slide motor drives.</summary>
    public bool EnableMotor { get; init; } = false;
}

/// <summary>
/// Dense slot store of joints: identities are slot plus one, freed slots
/// recycle, arrays grow geometrically. Creation validates bodies and wakes
/// sleeping dynamics; destruction is a pure unlink.
/// </summary>
public sealed class JointStore
{
    private const int InitialCapacity = 16;

    private JointRecord[] _joints;
    private int[] _freeStack;
    private int _freeCount;
    private int _highWater;
    private int _count;
    private int _capacity;

    /// <summary>Creates a store with room for sixteen joints.</summary>
    public JointStore()
    {
        _capacity = InitialCapacity;
        _joints = new JointRecord[_capacity];
        _freeStack = new int[_capacity];
        _freeCount = 0;
        _highWater = 0;
        _count = 0;
    }

    /// <summary>Live joints.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    /// <summary>
    /// Creates a rigid distance joint between two anchor points and wakes
    /// both bodies. The rest length is fixed at creation.
    /// </summary>
    /// <param name="bodies">Body registry; both bodies must be live.</param>
    /// <param name="bodyA">First body.</param>
    /// <param name="bodyB">Second body.</param>
    /// <param name="localAnchorA">Anchor in the first body frame.</param>
    /// <param name="localAnchorB">Anchor in the second body frame.</param>
    /// <param name="length">Rest separation; finite and never negative.</param>
    public JointId CreateDistance(
        BodyStore bodies,
        BodyId bodyA,
        BodyId bodyB,
        Vec2 localAnchorA,
        Vec2 localAnchorB,
        float length)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        bodies.Slot(bodyA);
        bodies.Slot(bodyB);

        if (!(length >= 0.0f) || float.IsNaN(length) || float.IsInfinity(length))
        {
            ThrowHelper.ThrowInvalidOperation("Joint rest length must be finite and non-negative.");
        }

        int slot = AllocateSlot();
        ref JointRecord joint = ref _joints[slot];

        joint = default;
        joint.Live = true;
        joint.Kind = JointKind.Distance;
        joint.BodyA = bodyA;
        joint.BodyB = bodyB;
        joint.LocalAnchorA = localAnchorA;
        joint.LocalAnchorB = localAnchorB;
        joint.Length = length;
        joint.Impulse = 0.0f;

        bodies.SetAwake(bodyA, true);
        bodies.SetAwake(bodyB, true);

        _count++;
        return new JointId((uint)slot + 1u);
    }

    /// <summary>Drops a joint; its identity dies with it.</summary>
    public void Destroy(JointId id)
    {
        int slot = Validate(id);
        _joints[slot] = default;
        _freeStack[_freeCount++] = slot;
        _count--;
    }

    /// <summary>
    /// Creates a pinned hinge between two bodies through a shared world
    /// anchor and wakes both bodies. Angular stops and the speed motor are
    /// configured by <paramref name="definition"/>.
    /// </summary>
    /// <param name="bodies">Body registry; both bodies must be live and distinct.</param>
    /// <param name="definition">Pin anchor, stops and motor tuning.</param>
    public JointId CreateRevolute(BodyStore bodies, in RevoluteDef definition)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        if (!definition.BodyA.IsValid || !definition.BodyB.IsValid || definition.BodyA == definition.BodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Revolute joints need two distinct live bodies.");
        }

        if (float.IsNaN(definition.WorldAnchor.X) || float.IsNaN(definition.WorldAnchor.Y) ||
            float.IsInfinity(definition.WorldAnchor.X) || float.IsInfinity(definition.WorldAnchor.Y))
        {
            ThrowHelper.ThrowInvalidOperation("Joint anchor must be finite.");
        }

        if (definition.EnableLimit && definition.LowerAngle > definition.UpperAngle)
        {
            ThrowHelper.ThrowInvalidOperation("Lower angular stop must not exceed the upper stop.");
        }

        if (definition.EnableMotor && (!(definition.MaxMotorTorque >= 0.0f) || float.IsNaN(definition.MaxMotorTorque)))
        {
            ThrowHelper.ThrowInvalidOperation("Motor torque must be finite and non-negative.");
        }

        if (float.IsNaN(definition.MotorSpeed) || float.IsInfinity(definition.MotorSpeed))
        {
            ThrowHelper.ThrowInvalidOperation("Motor speed must be finite.");
        }

        ref BodyState stateA = ref bodies.Slot(definition.BodyA);
        ref BodyState stateB = ref bodies.Slot(definition.BodyB);

        Transform2D transformA = Transform2D.FromRotation(stateA.Position, stateA.Cos, stateA.Sin);
        Transform2D transformB = Transform2D.FromRotation(stateB.Position, stateB.Cos, stateB.Sin);

        int slot = AllocateSlot();
        ref JointRecord joint = ref _joints[slot];

        joint = default;
        joint.Live = true;
        joint.Kind = JointKind.Revolute;
        joint.BodyA = definition.BodyA;
        joint.BodyB = definition.BodyB;
        joint.LocalAnchorA = transformA.InverseTransformPoint(definition.WorldAnchor);
        joint.LocalAnchorB = transformB.InverseTransformPoint(definition.WorldAnchor);
        joint.ReferenceAngle = transformB.Angle - transformA.Angle;
        joint.LowerAngle = definition.LowerAngle;
        joint.UpperAngle = definition.UpperAngle;
        joint.EnableLimit = definition.EnableLimit;
        joint.MotorSpeed = definition.MotorSpeed;
        joint.MaxMotorTorque = definition.MaxMotorTorque;
        joint.EnableMotor = definition.EnableMotor;

        bodies.SetAwake(definition.BodyA, true);
        bodies.SetAwake(definition.BodyB, true);

        _count++;
        return new JointId((uint)slot + 1u);
    }

    /// <summary>
    /// Creates a weld locking two bodies through a shared world anchor and
    /// wakes both bodies. The relative angle and anchor separation freeze at
    /// creation.
    /// </summary>
    /// <param name="bodies">Body registry; both bodies must be live and distinct.</param>
    /// <param name="definition">Pin anchor.</param>
    public JointId CreateWeld(BodyStore bodies, in WeldDef definition)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        if (!definition.BodyA.IsValid || !definition.BodyB.IsValid || definition.BodyA == definition.BodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Weld joints need two distinct live bodies.");
        }

        if (float.IsNaN(definition.WorldAnchor.X) || float.IsNaN(definition.WorldAnchor.Y) ||
            float.IsInfinity(definition.WorldAnchor.X) || float.IsInfinity(definition.WorldAnchor.Y))
        {
            ThrowHelper.ThrowInvalidOperation("Joint anchor must be finite.");
        }

        ref BodyState stateA = ref bodies.Slot(definition.BodyA);
        ref BodyState stateB = ref bodies.Slot(definition.BodyB);

        Transform2D transformA = Transform2D.FromRotation(stateA.Position, stateA.Cos, stateA.Sin);
        Transform2D transformB = Transform2D.FromRotation(stateB.Position, stateB.Cos, stateB.Sin);

        int slot = AllocateSlot();
        ref JointRecord joint = ref _joints[slot];

        joint = default;
        joint.Live = true;
        joint.Kind = JointKind.Weld;
        joint.BodyA = definition.BodyA;
        joint.BodyB = definition.BodyB;
        joint.LocalAnchorA = transformA.InverseTransformPoint(definition.WorldAnchor);
        joint.LocalAnchorB = transformB.InverseTransformPoint(definition.WorldAnchor);
        joint.ReferenceAngle = transformB.Angle - transformA.Angle;

        bodies.SetAwake(definition.BodyA, true);
        bodies.SetAwake(definition.BodyB, true);

        _count++;
        return new JointId((uint)slot + 1u);
    }

    /// <summary>
    /// Creates a slider along a world axis through a world anchor and wakes
    /// both bodies. Slide stops and the slide motor are configured by
    /// <paramref name="definition"/>.
    /// </summary>
    /// <param name="bodies">Body registry; both bodies must be live and distinct.</param>
    /// <param name="definition">Slide anchor, axis, stops and motor tuning.</param>
    public JointId CreatePrismatic(BodyStore bodies, in PrismaticDef definition)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        if (!definition.BodyA.IsValid || !definition.BodyB.IsValid || definition.BodyA == definition.BodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Prismatic joints need two distinct live bodies.");
        }

        if (float.IsNaN(definition.WorldAnchor.X) || float.IsNaN(definition.WorldAnchor.Y) ||
            float.IsInfinity(definition.WorldAnchor.X) || float.IsInfinity(definition.WorldAnchor.Y))
        {
            ThrowHelper.ThrowInvalidOperation("Joint anchor must be finite.");
        }

        if (definition.WorldAxis.LengthSquared() < 1e-12f ||
            float.IsNaN(definition.WorldAxis.X) || float.IsNaN(definition.WorldAxis.Y))
        {
            ThrowHelper.ThrowInvalidOperation("Slide axis must be a finite non-zero vector.");
        }

        if (definition.EnableLimit && definition.LowerTranslation > definition.UpperTranslation)
        {
            ThrowHelper.ThrowInvalidOperation("Lower slide stop must not exceed the upper stop.");
        }

        if (definition.EnableMotor && (!(definition.MaxMotorForce >= 0.0f) || float.IsNaN(definition.MaxMotorForce)))
        {
            ThrowHelper.ThrowInvalidOperation("Slide motor force must be finite and non-negative.");
        }

        if (float.IsNaN(definition.MotorSpeed) || float.IsInfinity(definition.MotorSpeed))
        {
            ThrowHelper.ThrowInvalidOperation("Slide motor speed must be finite.");
        }

        ref BodyState stateA = ref bodies.Slot(definition.BodyA);
        ref BodyState stateB = ref bodies.Slot(definition.BodyB);

        Transform2D transformA = Transform2D.FromRotation(stateA.Position, stateA.Cos, stateA.Sin);
        Transform2D transformB = Transform2D.FromRotation(stateB.Position, stateB.Cos, stateB.Sin);

        Vec2 axis = Vec2.Normalize(definition.WorldAxis);

        int slot = AllocateSlot();
        ref JointRecord joint = ref _joints[slot];

        joint = default;
        joint.Live = true;
        joint.Kind = JointKind.Prismatic;
        joint.BodyA = definition.BodyA;
        joint.BodyB = definition.BodyB;
        joint.LocalAnchorA = transformA.InverseTransformPoint(definition.WorldAnchor);
        joint.LocalAnchorB = transformB.InverseTransformPoint(definition.WorldAnchor);
        joint.LocalAxisA = new Vec2(
            (transformA.Cos * axis.X) + (transformA.Sin * axis.Y),
            (-transformA.Sin * axis.X) + (transformA.Cos * axis.Y));
        joint.ReferenceAngle = transformB.Angle - transformA.Angle;
        joint.ReferenceTranslation = Vec2.Dot(
            transformB.TransformPoint(joint.LocalAnchorB) - transformA.TransformPoint(joint.LocalAnchorA), axis);
        joint.LowerTranslation = definition.LowerTranslation;
        joint.UpperTranslation = definition.UpperTranslation;
        joint.EnablePrismaticLimit = definition.EnableLimit;
        joint.PrismaticMotorSpeed = definition.MotorSpeed;
        joint.MaxPrismaticForce = definition.MaxMotorForce;
        joint.EnablePrismaticMotor = definition.EnableMotor;

        bodies.SetAwake(definition.BodyA, true);
        bodies.SetAwake(definition.BodyB, true);

        _count++;
        return new JointId((uint)slot + 1u);
    }

    /// <summary>Whether <paramref name="id"/> names a live joint.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid(JointId id)
    {
        int slot = (int)id.Value - 1;
        return id.Value != 0 && slot >= 0 && slot < _capacity && _joints[slot].Live;
    }

    /// <summary>Internal slot access for the solver and the world; validated.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref JointRecord Slot(JointId id) => ref _joints[Validate(id)];

    /// <summary>Slot capacity for internal iteration.</summary>
    internal int SlotCapacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _capacity;
    }

    /// <summary>Whether a raw slot holds a live joint; for internal iteration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsSlotLive(int slot) => slot >= 0 && slot < _capacity && _joints[slot].Live;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AllocateSlot()
    {
        if (_freeCount > 0)
        {
            return _freeStack[--_freeCount];
        }

        if (_highWater >= _capacity)
        {
            Grow();
        }

        return _highWater++;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow()
    {
        int newCapacity = _capacity * 2;
        Array.Resize(ref _joints, newCapacity);
        Array.Resize(ref _freeStack, newCapacity);
        _capacity = newCapacity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Validate(JointId id)
    {
        int slot = (int)id.Value - 1;
        if (id.Value == 0 || slot < 0 || slot >= _capacity || !_joints[slot].Live)
        {
            ThrowHelper.ThrowInvalidOperation($"Joint with ID {id.Value} is unallocated or has been destroyed.");
        }

        return slot;
    }

    /// <summary>Unvalidated slot access for internal iteration; check <see cref="IsSlotLive"/> first.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref JointRecord SlotByIndex(int slot) => ref _joints[slot];
}

/// <summary>
/// Sequential-impulse solver for distance joints: init freezes the effective
/// mass and warm-starts the stored impulse (waking sleeping dynamics on real
/// relative motion), then bilateral impulses hold the rest length in velocity
/// and position. Sleeping endpoints read as infinite mass.
/// </summary>
public static class JointSolver
{
    /// <summary>
    /// Per-step init: wakes sleeping dynamics on genuine relative motion and
    /// applies the stored impulse to velocities. Call once per step before
    /// the iteration loop.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(BodyStore bodies, JointStore joints, float wakeTolerance, float dt)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(joints);

        for (int slot = 0; slot < joints.SlotCapacity; slot++)
        {
            if (!joints.IsSlotLive(slot))
            {
                continue;
            }

            ref JointRecord joint = ref joints.SlotByIndex(slot);
            switch (joint.Kind)
            {
                case JointKind.Distance:
                    InitDistance(bodies, ref joint, wakeTolerance);
                    break;
                case JointKind.Weld:
                    InitWeld(bodies, ref joint, wakeTolerance);
                    break;
                case JointKind.Prismatic:
                    InitPrismatic(bodies, ref joint, wakeTolerance, dt);
                    break;
                default:
                    InitRevolute(bodies, ref joint, wakeTolerance, dt);
                    break;
            }
        }
    }

    /// <summary>Runs <paramref name="iterations"/> sweeps of bilateral velocity impulses.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocityPass(BodyStore bodies, JointStore joints, int iterations)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(joints);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int slot = 0; slot < joints.SlotCapacity; slot++)
            {
                if (!joints.IsSlotLive(slot))
                {
                    continue;
                }

                ref JointRecord joint = ref joints.SlotByIndex(slot);
                switch (joint.Kind)
                {
                    case JointKind.Distance:
                        SolveJointVelocity(bodies, ref joint);
                        break;
                    case JointKind.Weld:
                        SolveWeldVelocity(bodies, ref joint);
                        break;
                    case JointKind.Prismatic:
                        SolvePrismaticVelocity(bodies, ref joint);
                        break;
                    default:
                        SolveRevoluteVelocity(bodies, ref joint);
                        break;
                }
            }
        }
    }

    /// <summary>Distance velocity sweep: one bilateral impulse along the rod.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void SolveJointVelocity(BodyStore bodies, ref JointRecord joint)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 rA = Rotate(stateA, joint.LocalAnchorA) - Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = Rotate(stateB, joint.LocalAnchorB) - Rotate(stateB, stateB.LocalCenter);

        Vec2 pA = stateA.Position + Rotate(stateA, joint.LocalAnchorA);
        Vec2 pB = stateB.Position + Rotate(stateB, joint.LocalAnchorB);
        Vec2 axis = pB - pA;
        float lengthSq = axis.LengthSquared();
        if (lengthSq < 1e-12f)
        {
            return;
        }

        Vec2 direction = axis * (1.0f / MathF.Sqrt(lengthSq));

        float rnA = Vec2.Cross(rA, direction);
        float rnB = Vec2.Cross(rB, direction);
        float k = invMassA + invMassB + (invIA * rnA * rnA) + (invIB * rnB * rnB);
        if (k <= 0.0f)
        {
            return;
        }

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * Perp(rB));
        float velocityError = Vec2.Dot(direction, vB - vA);

        float impulse = -velocityError / k;
        joint.Impulse += impulse;

        Vec2 applied = direction * impulse;
        if (awakeA)
        {
            stateA.LinearVelocity -= applied * invMassA;
            stateA.AngularVelocity -= invIA * Vec2.Cross(rA, applied);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += applied * invMassB;
            stateB.AngularVelocity += invIB * Vec2.Cross(rB, applied);
        }
    }

    /// <summary>Runs <paramref name="iterations"/> sweeps of bilateral position correction.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void SolvePositionPass(BodyStore bodies, JointStore joints, int iterations)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(joints);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int slot = 0; slot < joints.SlotCapacity; slot++)
            {
                if (!joints.IsSlotLive(slot))
                {
                    continue;
                }

                ref JointRecord joint = ref joints.SlotByIndex(slot);
                switch (joint.Kind)
                {
                    case JointKind.Distance:
                        SolveJointPosition(bodies, ref joint);
                        break;
                    case JointKind.Weld:
                        SolveWeldPosition(bodies, ref joint);
                        break;
                    case JointKind.Prismatic:
                        SolvePrismaticPosition(bodies, ref joint);
                        break;
                    default:
                        SolveRevolutePosition(bodies, ref joint);
                        break;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void InitDistance(BodyStore bodies, ref JointRecord joint, float wakeTolerance)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);

        Vec2 rA = Rotate(stateA, joint.LocalAnchorA);
        Vec2 rB = Rotate(stateB, joint.LocalAnchorB);

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * Perp(rB));
        if ((vB - vA).LengthSquared() > wakeTolerance * wakeTolerance)
        {
            if (stateA.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(joint.BodyA, true);
            }

            if (stateB.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(joint.BodyB, true);
            }
        }

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        Vec2 pA = stateA.Position + Rotate(stateA, joint.LocalAnchorA);
        Vec2 pB = stateB.Position + Rotate(stateB, joint.LocalAnchorB);
        Vec2 axis = pB - pA;
        Vec2 direction = axis.LengthSquared() > 1e-12f ? axis * (1.0f / MathF.Sqrt(axis.LengthSquared())) : Vec2.Zero;

        Vec2 impulse = direction * joint.Impulse;
        if (awakeA)
        {
            stateA.LinearVelocity -= impulse * stateA.InverseMass;
            stateA.AngularVelocity -= stateA.InverseInertia * Vec2.Cross(rA, impulse);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += impulse * stateB.InverseMass;
            stateB.AngularVelocity += stateB.InverseInertia * Vec2.Cross(rB, impulse);
        }
    }

    /// <summary>Revolute init: resolves the limit state, warm-starts point, motor and limit impulses.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void InitRevolute(BodyStore bodies, ref JointRecord joint, float wakeTolerance, float dt)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);

        float angleA = MathF.Atan2(stateA.Sin, stateA.Cos);
        float angleB = MathF.Atan2(stateB.Sin, stateB.Cos);
        float jointAngle = NormalizeAngle((angleB - angleA) - joint.ReferenceAngle);

        LimitState previous = joint.LimitState;
        joint.LimitState = ResolveLimitState(joint.EnableLimit, joint.LowerAngle, joint.UpperAngle, jointAngle);
        if (joint.LimitState != previous)
        {
            joint.LimitImpulse = 0.0f;
        }

        joint.MotorMaxImpulse = joint.EnableMotor ? joint.MaxMotorTorque * dt : 0.0f;

        Vec2 rA = Rotate(stateA, joint.LocalAnchorA) - Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = Rotate(stateB, joint.LocalAnchorB) - Rotate(stateB, stateB.LocalCenter);

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * Perp(rB));
        if ((vB - vA).LengthSquared() > wakeTolerance * wakeTolerance ||
            MathF.Abs(stateB.AngularVelocity - stateA.AngularVelocity) > wakeTolerance)
        {
            if (stateA.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(joint.BodyA, true);
            }

            if (stateB.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(joint.BodyB, true);
            }
        }

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        Vec2 pointImpulse = joint.PointImpulse;
        if (awakeA)
        {
            stateA.LinearVelocity -= pointImpulse * stateA.InverseMass;
            stateA.AngularVelocity -= stateA.InverseInertia * (Vec2.Cross(rA, pointImpulse) + joint.MotorImpulse + joint.LimitImpulse);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += pointImpulse * stateB.InverseMass;
            stateB.AngularVelocity += stateB.InverseInertia * (Vec2.Cross(rB, pointImpulse) + joint.MotorImpulse + joint.LimitImpulse);
        }
    }

    /// <summary>Resolves the angular limit state from the current joint angle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static LimitState ResolveLimitState(bool enabled, float lower, float upper, float angle)
    {
        if (!enabled)
        {
            return LimitState.Inactive;
        }

        if (upper - lower < 2.0f * SolverTuning.AngularSlop)
        {
            return LimitState.Equal;
        }

        if (angle <= lower)
        {
            return LimitState.AtLower;
        }

        if (angle >= upper)
        {
            return LimitState.AtUpper;
        }

        return LimitState.Inactive;
    }

    /// <summary>Wraps an angle into [-pi, pi].</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float NormalizeAngle(float angle)
    {
        float wrapped = angle - (MathF.Floor((angle + MathF.PI) / (2.0f * MathF.PI)) * 2.0f * MathF.PI);
        return wrapped;
    }

    /// <summary>Revolute velocity sweep: motor, then limit, then the point block.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void SolveRevoluteVelocity(BodyStore bodies, ref JointRecord joint)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        float axialMass = 1.0f / (invIA + invIB);

        if (joint.EnableMotor && (awakeA || awakeB))
        {
            float rate = (stateB.AngularVelocity - stateA.AngularVelocity) - joint.MotorSpeed;
            float impulse = -axialMass * rate;
            float old = joint.MotorImpulse;
            float next = Math.Clamp(old + impulse, -joint.MotorMaxImpulse, joint.MotorMaxImpulse);
            float delta = next - old;
            joint.MotorImpulse = next;

            if (awakeA)
            {
                stateA.AngularVelocity -= invIA * delta;
            }

            if (awakeB)
            {
                stateB.AngularVelocity += invIB * delta;
            }
        }

        if (joint.LimitState != LimitState.Inactive && (awakeA || awakeB))
        {
            float rate = stateB.AngularVelocity - stateA.AngularVelocity;
            float impulse = -axialMass * rate;
            float old = joint.LimitImpulse;
            float next = joint.LimitState switch
            {
                LimitState.AtLower => MathF.Max(old + impulse, 0.0f),
                LimitState.AtUpper => MathF.Min(old + impulse, 0.0f),
                _ => old + impulse,
            };
            float delta = next - old;
            joint.LimitImpulse = next;

            if (awakeA)
            {
                stateA.AngularVelocity -= invIA * delta;
            }

            if (awakeB)
            {
                stateB.AngularVelocity += invIB * delta;
            }
        }

        Vec2 rA = Rotate(stateA, joint.LocalAnchorA) - Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = Rotate(stateB, joint.LocalAnchorB) - Rotate(stateB, stateB.LocalCenter);

        float k11 = invMassA + invMassB + (invIA * rA.Y * rA.Y) + (invIB * rB.Y * rB.Y);
        float k22 = invMassA + invMassB + (invIA * rA.X * rA.X) + (invIB * rB.X * rB.X);
        float k12 = (invIA * rA.X * rA.Y) - (invIB * rB.X * rB.Y);

        float det = (k11 * k22) - (k12 * k12);
        if (det <= 0.0f)
        {
            return;
        }

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * Perp(rB));
        Vec2 accumulated = joint.PointImpulse;

        Vec2 residual = (vB - vA) - new Vec2(
            (k11 * accumulated.X) + (k12 * accumulated.Y),
            (k12 * accumulated.X) + (k22 * accumulated.Y));

        float invDet = 1.0f / det;
        Vec2 solved = new(
            ((k12 * residual.Y) - (k22 * residual.X)) * invDet,
            ((k12 * residual.X) - (k11 * residual.Y)) * invDet);
        Vec2 deltaImpulse = solved - accumulated;
        joint.PointImpulse = solved;

        if (awakeA)
        {
            stateA.LinearVelocity -= deltaImpulse * invMassA;
            stateA.AngularVelocity -= invIA * Vec2.Cross(rA, deltaImpulse);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += deltaImpulse * invMassB;
            stateB.AngularVelocity += invIB * Vec2.Cross(rB, deltaImpulse);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void SolveJointPosition(BodyStore bodies, ref JointRecord joint)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 rA = Rotate(stateA, joint.LocalAnchorA) - Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = Rotate(stateB, joint.LocalAnchorB) - Rotate(stateB, stateB.LocalCenter);

        Vec2 pA = stateA.Position + Rotate(stateA, joint.LocalAnchorA);
        Vec2 pB = stateB.Position + Rotate(stateB, joint.LocalAnchorB);
        Vec2 axis = pB - pA;
        float length = MathF.Sqrt(axis.LengthSquared());
        if (length < 1e-9f)
        {
            return;
        }

        Vec2 direction = axis * (1.0f / length);

        float rnA = Vec2.Cross(rA, direction);
        float rnB = Vec2.Cross(rB, direction);
        float k = invMassA + invMassB + (invIA * rnA * rnA) + (invIB * rnB * rnB);
        if (k <= 0.0f)
        {
            return;
        }

        float error = Math.Clamp(length - joint.Length, -SolverTuning.MaxCorrection, SolverTuning.MaxCorrection);
        Vec2 applied = direction * (-error / k);

        if (awakeA)
        {
            stateA.Position -= applied * invMassA;
            float angleA = MathF.Atan2(stateA.Sin, stateA.Cos) - (invIA * Vec2.Cross(rA, applied));
            stateA.Cos = MathF.Cos(angleA);
            stateA.Sin = MathF.Sin(angleA);
        }

        if (awakeB)
        {
            stateB.Position += applied * invMassB;
            float angleB = MathF.Atan2(stateB.Sin, stateB.Cos) + (invIB * Vec2.Cross(rB, applied));
            stateB.Cos = MathF.Cos(angleB);
            stateB.Sin = MathF.Sin(angleB);
        }
    }

    /// <summary>Revolute position sweep: pin correction plus angular limit correction.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void SolveRevolutePosition(BodyStore bodies, ref JointRecord joint)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 rA = Rotate(stateA, joint.LocalAnchorA) - Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = Rotate(stateB, joint.LocalAnchorB) - Rotate(stateB, stateB.LocalCenter);

        float k11 = invMassA + invMassB + (invIA * rA.Y * rA.Y) + (invIB * rB.Y * rB.Y);
        float k22 = invMassA + invMassB + (invIA * rA.X * rA.X) + (invIB * rB.X * rB.X);
        float k12 = (invIA * rA.X * rA.Y) - (invIB * rB.X * rB.Y);

        float det = (k11 * k22) - (k12 * k12);
        if (det > 0.0f)
        {
            Vec2 pA = stateA.Position + Rotate(stateA, joint.LocalAnchorA);
            Vec2 pB = stateB.Position + Rotate(stateB, joint.LocalAnchorB);
            Vec2 error = pB - pA;

            float invDet = 1.0f / det;
            Vec2 correction = new(
                ((k12 * error.Y) - (k22 * error.X)) * invDet,
                ((k12 * error.X) - (k11 * error.Y)) * invDet);

            if (awakeA)
            {
                stateA.Position -= correction * invMassA;
                float angleA = MathF.Atan2(stateA.Sin, stateA.Cos) - (invIA * Vec2.Cross(rA, correction));
                stateA.Cos = MathF.Cos(angleA);
                stateA.Sin = MathF.Sin(angleA);
            }

            if (awakeB)
            {
                stateB.Position += correction * invMassB;
                float angleB = MathF.Atan2(stateB.Sin, stateB.Cos) + (invIB * Vec2.Cross(rB, correction));
                stateB.Cos = MathF.Cos(angleB);
                stateB.Sin = MathF.Sin(angleB);
            }
        }

        if (joint.LimitState == LimitState.Inactive)
        {
            return;
        }

        float axialMass = 1.0f / (invIA + invIB);
        float angleA2 = MathF.Atan2(stateA.Sin, stateA.Cos);
        float angleB2 = MathF.Atan2(stateB.Sin, stateB.Cos);
        float jointAngle = NormalizeAngle((angleB2 - angleA2) - joint.ReferenceAngle);

        float angularError = joint.LimitState switch
        {
            LimitState.AtLower => jointAngle - joint.LowerAngle + SolverTuning.AngularSlop,
            LimitState.AtUpper => jointAngle - joint.UpperAngle - SolverTuning.AngularSlop,
            _ => jointAngle - joint.LowerAngle,
        };

        bool violates = joint.LimitState switch
        {
            LimitState.AtLower => angularError < 0.0f,
            LimitState.AtUpper => angularError > 0.0f,
            _ => angularError != 0.0f,
        };

        if (!violates)
        {
            return;
        }

        float limitCorrection = -axialMass * angularError;
        if (awakeA)
        {
            float correctedA = MathF.Atan2(stateA.Sin, stateA.Cos) - (invIA * limitCorrection);
            stateA.Cos = MathF.Cos(correctedA);
            stateA.Sin = MathF.Sin(correctedA);
        }

        if (awakeB)
        {
            float correctedB = MathF.Atan2(stateB.Sin, stateB.Cos) + (invIB * limitCorrection);
            stateB.Cos = MathF.Cos(correctedB);
            stateB.Sin = MathF.Sin(correctedB);
        }
    }

    /// <summary>Weld init: warm-starts the pin block and the angle lock.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void InitWeld(BodyStore bodies, ref JointRecord joint, float wakeTolerance)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);

        Vec2 rA = Rotate(stateA, joint.LocalAnchorA) - Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = Rotate(stateB, joint.LocalAnchorB) - Rotate(stateB, stateB.LocalCenter);

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * Perp(rB));
        if ((vB - vA).LengthSquared() > wakeTolerance * wakeTolerance ||
            MathF.Abs(stateB.AngularVelocity - stateA.AngularVelocity) > wakeTolerance)
        {
            if (stateA.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(joint.BodyA, true);
            }

            if (stateB.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(joint.BodyB, true);
            }
        }

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        Vec2 pointImpulse = joint.PointImpulse;
        if (awakeA)
        {
            stateA.LinearVelocity -= pointImpulse * stateA.InverseMass;
            stateA.AngularVelocity -= stateA.InverseInertia * (Vec2.Cross(rA, pointImpulse) + joint.WeldAngleImpulse);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += pointImpulse * stateB.InverseMass;
            stateB.AngularVelocity += stateB.InverseInertia * (Vec2.Cross(rB, pointImpulse) + joint.WeldAngleImpulse);
        }
    }

    /// <summary>Weld velocity sweep: angle lock, then the pin block.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void SolveWeldVelocity(BodyStore bodies, ref JointRecord joint)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        float axialMass = invIA + invIB;
        if (axialMass > 0.0f && (awakeA || awakeB))
        {
            float rate = stateB.AngularVelocity - stateA.AngularVelocity;
            float delta = -rate / axialMass;
            joint.WeldAngleImpulse += delta;

            if (awakeA)
            {
                stateA.AngularVelocity -= invIA * delta;
            }

            if (awakeB)
            {
                stateB.AngularVelocity += invIB * delta;
            }
        }

        SolvePinBlock(bodies, ref joint, invMassA, invMassB, invIA, invIB, awakeA, awakeB);
    }

    /// <summary>Weld position sweep: pin correction plus angle correction to reference.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void SolveWeldPosition(BodyStore bodies, ref JointRecord joint)
    {
        SolvePinCorrection(bodies, ref joint);

        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;
        float axialMass = invIA + invIB;
        if (axialMass <= 0.0f)
        {
            return;
        }

        float angleA = MathF.Atan2(stateA.Sin, stateA.Cos);
        float angleB = MathF.Atan2(stateB.Sin, stateB.Cos);
        float error = NormalizeAngle((angleB - angleA) - joint.ReferenceAngle);
        if (MathF.Abs(error) < SolverTuning.AngularSlop)
        {
            return;
        }

        float correction = -error / axialMass;
        if (awakeA)
        {
            float correctedA = angleA - (invIA * correction);
            stateA.Cos = MathF.Cos(correctedA);
            stateA.Sin = MathF.Sin(correctedA);
        }

        if (awakeB)
        {
            float correctedB = angleB + (invIB * correction);
            stateB.Cos = MathF.Cos(correctedB);
            stateB.Sin = MathF.Sin(correctedB);
        }
    }

    /// <summary>Prismatic init: resolves the slide state, warm-starts perp, motor, limit and angle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void InitPrismatic(BodyStore bodies, ref JointRecord joint, float wakeTolerance, float dt)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);

        Vec2 axis = Rotate(stateA, joint.LocalAxisA);
        float axisLenSq = axis.LengthSquared();
        if (axisLenSq > 1e-12f)
        {
            axis *= 1.0f / MathF.Sqrt(axisLenSq);
        }

        Vec2 pA = stateA.Position + Rotate(stateA, joint.LocalAnchorA);
        Vec2 pB = stateB.Position + Rotate(stateB, joint.LocalAnchorB);
        float translation = Vec2.Dot(pB - pA, axis) - joint.ReferenceTranslation;

        LimitState previous = joint.PrismaticLimitState;
        joint.PrismaticLimitState = ResolveLimitState(joint.EnablePrismaticLimit, joint.LowerTranslation, joint.UpperTranslation, translation);
        if (joint.PrismaticLimitState != previous)
        {
            joint.PrismaticLimitImpulse = 0.0f;
        }

        joint.PrismaticMotorMaxImpulse = joint.EnablePrismaticMotor ? joint.MaxPrismaticForce * dt : 0.0f;

        Vec2 rA = Rotate(stateA, joint.LocalAnchorA) - Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = Rotate(stateB, joint.LocalAnchorB) - Rotate(stateB, stateB.LocalCenter);

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * Perp(rB));
        if ((vB - vA).LengthSquared() > wakeTolerance * wakeTolerance ||
            MathF.Abs(stateB.AngularVelocity - stateA.AngularVelocity) > wakeTolerance)
        {
            if (stateA.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(joint.BodyA, true);
            }

            if (stateB.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(joint.BodyB, true);
            }
        }

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        Vec2 perp = Perp(axis);
        Vec2 impulse = (axis * (joint.PrismaticMotorImpulse + joint.PrismaticLimitImpulse)) + (perp * joint.PrismaticImpulse.Y);
        if (awakeA)
        {
            stateA.LinearVelocity -= impulse * stateA.InverseMass;
            stateA.AngularVelocity -= stateA.InverseInertia * (Vec2.Cross(rA, impulse) + joint.WeldAngleImpulse);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += impulse * stateB.InverseMass;
            stateB.AngularVelocity += stateB.InverseInertia * (Vec2.Cross(rB, impulse) + joint.WeldAngleImpulse);
        }
    }

    /// <summary>Prismatic velocity sweep: motor, then limit, then perp, then angle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void SolvePrismaticVelocity(BodyStore bodies, ref JointRecord joint)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 axis = Rotate(stateA, joint.LocalAxisA);
        if (axis.LengthSquared() > 1e-12f)
        {
            axis *= 1.0f / MathF.Sqrt(axis.LengthSquared());
        }

        Vec2 perp = Perp(axis);
        Vec2 rA = Rotate(stateA, joint.LocalAnchorA) - Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = Rotate(stateB, joint.LocalAnchorB) - Rotate(stateB, stateB.LocalCenter);

        float axisA = Vec2.Cross(rA, axis);
        float axisB = Vec2.Cross(rB, axis);
        float axisMass = invMassA + invMassB + (invIA * axisA * axisA) + (invIB * axisB * axisB);

        if (joint.EnablePrismaticMotor && axisMass > 0.0f && (awakeA || awakeB))
        {
            Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * Perp(rA));
            Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * Perp(rB));
            float rate = Vec2.Dot(vB - vA, axis) - joint.PrismaticMotorSpeed;
            float impulse = -rate / axisMass;
            float old = joint.PrismaticMotorImpulse;
            float next = Math.Clamp(old + impulse, -joint.PrismaticMotorMaxImpulse, joint.PrismaticMotorMaxImpulse);
            Vec2 applied = axis * (next - old);
            joint.PrismaticMotorImpulse = next;

            if (awakeA)
            {
                stateA.LinearVelocity -= applied * invMassA;
                stateA.AngularVelocity -= invIA * Vec2.Cross(rA, applied);
            }

            if (awakeB)
            {
                stateB.LinearVelocity += applied * invMassB;
                stateB.AngularVelocity += invIB * Vec2.Cross(rB, applied);
            }
        }

        if (joint.PrismaticLimitState != LimitState.Inactive && axisMass > 0.0f && (awakeA || awakeB))
        {
            Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * Perp(rA));
            Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * Perp(rB));
            float rate = Vec2.Dot(vB - vA, axis);
            float impulse = -rate / axisMass;
            float old = joint.PrismaticLimitImpulse;
            float next = joint.PrismaticLimitState switch
            {
                LimitState.AtLower => MathF.Max(old + impulse, 0.0f),
                LimitState.AtUpper => MathF.Min(old + impulse, 0.0f),
                _ => old + impulse,
            };
            Vec2 applied = axis * (next - old);
            joint.PrismaticLimitImpulse = next;

            if (awakeA)
            {
                stateA.LinearVelocity -= applied * invMassA;
                stateA.AngularVelocity -= invIA * Vec2.Cross(rA, applied);
            }

            if (awakeB)
            {
                stateB.LinearVelocity += applied * invMassB;
                stateB.AngularVelocity += invIB * Vec2.Cross(rB, applied);
            }
        }

        float perpA = Vec2.Cross(rA, perp);
        float perpB = Vec2.Cross(rB, perp);
        float perpMass = invMassA + invMassB + (invIA * perpA * perpA) + (invIB * perpB * perpB);
        if (perpMass > 0.0f && (awakeA || awakeB))
        {
            Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * Perp(rA));
            Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * Perp(rB));
            float rate = Vec2.Dot(vB - vA, perp);
            float delta = -rate / perpMass;
            joint.PrismaticImpulse = new Vec2(joint.PrismaticImpulse.X, joint.PrismaticImpulse.Y + delta);
            Vec2 applied = perp * delta;

            if (awakeA)
            {
                stateA.LinearVelocity -= applied * invMassA;
                stateA.AngularVelocity -= invIA * Vec2.Cross(rA, applied);
            }

            if (awakeB)
            {
                stateB.LinearVelocity += applied * invMassB;
                stateB.AngularVelocity += invIB * Vec2.Cross(rB, applied);
            }
        }

        float axialMass = invIA + invIB;
        if (axialMass > 0.0f && (awakeA || awakeB))
        {
            float rate = stateB.AngularVelocity - stateA.AngularVelocity;
            float delta = -rate / axialMass;
            joint.WeldAngleImpulse += delta;

            if (awakeA)
            {
                stateA.AngularVelocity -= invIA * delta;
            }

            if (awakeB)
            {
                stateB.AngularVelocity += invIB * delta;
            }
        }
    }

    /// <summary>Prismatic position sweep: perp lock, angle lock, then slide limit.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void SolvePrismaticPosition(BodyStore bodies, ref JointRecord joint)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 axis = Rotate(stateA, joint.LocalAxisA);
        if (axis.LengthSquared() > 1e-12f)
        {
            axis *= 1.0f / MathF.Sqrt(axis.LengthSquared());
        }

        Vec2 perp = Perp(axis);
        Vec2 rA = Rotate(stateA, joint.LocalAnchorA) - Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = Rotate(stateB, joint.LocalAnchorB) - Rotate(stateB, stateB.LocalCenter);
        Vec2 pA = stateA.Position + Rotate(stateA, joint.LocalAnchorA);
        Vec2 pB = stateB.Position + Rotate(stateB, joint.LocalAnchorB);
        Vec2 error = pB - pA;

        float perpA = Vec2.Cross(rA, perp);
        float perpB = Vec2.Cross(rB, perp);
        float perpK = invMassA + invMassB + (invIA * perpA * perpA) + (invIB * perpB * perpB);
        if (perpK > 0.0f)
        {
            float perpError = Math.Clamp(Vec2.Dot(error, perp), -SolverTuning.MaxCorrection, SolverTuning.MaxCorrection);
            Vec2 applied = perp * (-perpError / perpK);
            ApplyPositionCorrection(ref stateA, ref stateB, rA, rB, applied, invMassA, invMassB, invIA, invIB, awakeA, awakeB);
        }

        float axialMass = invIA + invIB;
        if (axialMass > 0.0f)
        {
            float angleA = MathF.Atan2(stateA.Sin, stateA.Cos);
            float angleB = MathF.Atan2(stateB.Sin, stateB.Cos);
            float angleError = NormalizeAngle((angleB - angleA) - joint.ReferenceAngle);
            if (MathF.Abs(angleError) >= SolverTuning.AngularSlop)
            {
                float correction = -angleError / axialMass;
                if (awakeA)
                {
                    float correctedA = angleA - (invIA * correction);
                    stateA.Cos = MathF.Cos(correctedA);
                    stateA.Sin = MathF.Sin(correctedA);
                }

                if (awakeB)
                {
                    float correctedB = angleB + (invIB * correction);
                    stateB.Cos = MathF.Cos(correctedB);
                    stateB.Sin = MathF.Sin(correctedB);
                }
            }
        }

        if (joint.PrismaticLimitState == LimitState.Inactive)
        {
            return;
        }

        float axisA = Vec2.Cross(rA, axis);
        float axisB = Vec2.Cross(rB, axis);
        float axisK = invMassA + invMassB + (invIA * axisA * axisA) + (invIB * axisB * axisB);
        if (axisK <= 0.0f)
        {
            return;
        }

        float translation = Vec2.Dot(error, axis) - joint.ReferenceTranslation;
        float limitError = joint.PrismaticLimitState switch
        {
            LimitState.AtLower => translation - joint.LowerTranslation + SolverTuning.LinearSlop,
            LimitState.AtUpper => translation - joint.UpperTranslation - SolverTuning.LinearSlop,
            _ => translation - joint.LowerTranslation,
        };

        bool violates = joint.PrismaticLimitState switch
        {
            LimitState.AtLower => limitError < 0.0f,
            LimitState.AtUpper => limitError > 0.0f,
            _ => limitError != 0.0f,
        };

        if (!violates)
        {
            return;
        }

        Vec2 limitApplied = axis * (-Math.Clamp(limitError, -SolverTuning.MaxCorrection, SolverTuning.MaxCorrection) / axisK);
        ApplyPositionCorrection(ref stateA, ref stateB, rA, rB, limitApplied, invMassA, invMassB, invIA, invIB, awakeA, awakeB);
    }

    /// <summary>Shared 2x2 pin velocity block used by weld joints.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void SolvePinBlock(
        BodyStore bodies, ref JointRecord joint,
        float invMassA, float invMassB, float invIA, float invIB, bool awakeA, bool awakeB)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);

        Vec2 rA = Rotate(stateA, joint.LocalAnchorA) - Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = Rotate(stateB, joint.LocalAnchorB) - Rotate(stateB, stateB.LocalCenter);

        float k11 = invMassA + invMassB + (invIA * rA.Y * rA.Y) + (invIB * rB.Y * rB.Y);
        float k22 = invMassA + invMassB + (invIA * rA.X * rA.X) + (invIB * rB.X * rB.X);
        float k12 = (invIA * rA.X * rA.Y) - (invIB * rB.X * rB.Y);

        float det = (k11 * k22) - (k12 * k12);
        if (det <= 0.0f)
        {
            return;
        }

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * Perp(rB));
        Vec2 accumulated = joint.PointImpulse;

        Vec2 residual = (vB - vA) - new Vec2(
            (k11 * accumulated.X) + (k12 * accumulated.Y),
            (k12 * accumulated.X) + (k22 * accumulated.Y));

        float invDet = 1.0f / det;
        Vec2 solved = new(
            ((k12 * residual.Y) - (k22 * residual.X)) * invDet,
            ((k12 * residual.X) - (k11 * residual.Y)) * invDet);
        Vec2 deltaImpulse = solved - accumulated;
        joint.PointImpulse = solved;

        if (awakeA)
        {
            stateA.LinearVelocity -= deltaImpulse * invMassA;
            stateA.AngularVelocity -= invIA * Vec2.Cross(rA, deltaImpulse);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += deltaImpulse * invMassB;
            stateB.AngularVelocity += invIB * Vec2.Cross(rB, deltaImpulse);
        }
    }

    /// <summary>Shared pin position correction used by weld joints.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void SolvePinCorrection(BodyStore bodies, ref JointRecord joint)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 rA = Rotate(stateA, joint.LocalAnchorA) - Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = Rotate(stateB, joint.LocalAnchorB) - Rotate(stateB, stateB.LocalCenter);

        float k11 = invMassA + invMassB + (invIA * rA.Y * rA.Y) + (invIB * rB.Y * rB.Y);
        float k22 = invMassA + invMassB + (invIA * rA.X * rA.X) + (invIB * rB.X * rB.X);
        float k12 = (invIA * rA.X * rA.Y) - (invIB * rB.X * rB.Y);

        float det = (k11 * k22) - (k12 * k12);
        if (det <= 0.0f)
        {
            return;
        }

        Vec2 pA = stateA.Position + Rotate(stateA, joint.LocalAnchorA);
        Vec2 pB = stateB.Position + Rotate(stateB, joint.LocalAnchorB);
        Vec2 error = pB - pA;

        float invDet = 1.0f / det;
        Vec2 correction = new(
            ((k12 * error.Y) - (k22 * error.X)) * invDet,
            ((k12 * error.X) - (k11 * error.Y)) * invDet);

        ApplyPositionCorrection(ref stateA, ref stateB, rA, rB, correction, invMassA, invMassB, invIA, invIB, awakeA, awakeB);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyPositionCorrection(
        ref BodyState stateA, ref BodyState stateB, Vec2 rA, Vec2 rB, Vec2 correction,
        float invMassA, float invMassB, float invIA, float invIB, bool awakeA, bool awakeB)
    {
        if (awakeA)
        {
            stateA.Position -= correction * invMassA;
            float angleA = MathF.Atan2(stateA.Sin, stateA.Cos) - (invIA * Vec2.Cross(rA, correction));
            stateA.Cos = MathF.Cos(angleA);
            stateA.Sin = MathF.Sin(angleA);
        }

        if (awakeB)
        {
            stateB.Position += correction * invMassB;
            float angleB = MathF.Atan2(stateB.Sin, stateB.Cos) + (invIB * Vec2.Cross(rB, correction));
            stateB.Cos = MathF.Cos(angleB);
            stateB.Sin = MathF.Sin(angleB);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vec2 Rotate(in BodyState state, Vec2 vector) =>
        new((state.Cos * vector.X) - (state.Sin * vector.Y), (state.Sin * vector.X) + (state.Cos * vector.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vec2 Perp(Vec2 vector) => new(-vector.Y, vector.X);
}
