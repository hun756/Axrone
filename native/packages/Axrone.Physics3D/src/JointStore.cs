namespace Axrone.Physics3D;

/// <summary>Rigid rod state: bodies, body-frame anchors, rest length and the accumulated impulse.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct DistanceJointData
{
    /// <summary>First body.</summary>
    public BodyId BodyA;

    /// <summary>Second body.</summary>
    public BodyId BodyB;

    /// <summary>Anchor in the first body frame.</summary>
    public Vec3 LocalAnchorA;

    /// <summary>Anchor in the second body frame.</summary>
    public Vec3 LocalAnchorB;

    /// <summary>Rest separation; never negative.</summary>
    public float Length;

    /// <summary>Accumulated impulse.</summary>
    public float Impulse;

    /// <summary>Whether the slot holds a live joint.</summary>
    public bool Live;
}

/// <summary>Point-to-point state: bodies, body-frame anchors and the accumulated impulse vector.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct BallSocketJointData
{
    /// <summary>First body.</summary>
    public BodyId BodyA;

    /// <summary>Second body.</summary>
    public BodyId BodyB;

    /// <summary>Anchor in the first body frame.</summary>
    public Vec3 LocalAnchorA;

    /// <summary>Anchor in the second body frame.</summary>
    public Vec3 LocalAnchorB;

    /// <summary>Accumulated impulse.</summary>
    public Vec3 Impulse;

    /// <summary>Whether the slot holds a live joint.</summary>
    public bool Live;
}

/// <summary>Full rigid-lock state: point coincidence plus the reference relative rotation and both impulses.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct WeldJointData
{
    /// <summary>First body.</summary>
    public BodyId BodyA;

    /// <summary>Second body.</summary>
    public BodyId BodyB;

    /// <summary>Anchor in the first body frame.</summary>
    public Vec3 LocalAnchorA;

    /// <summary>Anchor in the second body frame.</summary>
    public Vec3 LocalAnchorB;

    /// <summary>Relative orientation of B in A's frame at creation.</summary>
    public Quat ReferenceRotation;

    /// <summary>Accumulated linear impulse.</summary>
    public Vec3 LinearImpulse;

    /// <summary>Accumulated angular impulse.</summary>
    public Vec3 AngularImpulse;

    /// <summary>Whether the slot holds a live joint.</summary>
    public bool Live;
}

/// <summary>Hinge state: point coincidence, axis alignment, stops and a speed motor.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct HingeJointData
{
    /// <summary>First body.</summary>
    public BodyId BodyA;

    /// <summary>Second body.</summary>
    public BodyId BodyB;

    /// <summary>Anchor in the first body frame.</summary>
    public Vec3 LocalAnchorA;

    /// <summary>Anchor in the second body frame.</summary>
    public Vec3 LocalAnchorB;

    /// <summary>Hinge axis in the first body frame.</summary>
    public Vec3 LocalAxisA;

    /// <summary>Hinge axis in the second body frame.</summary>
    public Vec3 LocalAxisB;

    /// <summary>Reference perpendicular in the first body frame for angle measurement.</summary>
    public Vec3 LocalPerpA;

    /// <summary>Reference perpendicular in the second body frame.</summary>
    public Vec3 LocalPerpB;

    /// <summary>Lower angular stop in radians.</summary>
    public float LowerLimit;

    /// <summary>Upper angular stop in radians.</summary>
    public float UpperLimit;

    /// <summary>Whether stops are enforced.</summary>
    public bool EnableLimit;

    /// <summary>Motor target speed in radians per second.</summary>
    public float MotorSpeed;

    /// <summary>Motor torque cap.</summary>
    public float MaxMotorTorque;

    /// <summary>Whether the motor drives.</summary>
    public bool EnableMotor;

    /// <summary>Accumulated point impulse.</summary>
    public Vec3 PointImpulse;

    /// <summary>Accumulated axis-alignment impulse (two rows).</summary>
    public Vec3 AxisImpulse;

    /// <summary>Accumulated limit impulse.</summary>
    public float LimitImpulse;

    /// <summary>Accumulated motor impulse.</summary>
    public float MotorImpulse;

    /// <summary>Whether the slot holds a live joint.</summary>
    public bool Live;
}

/// <summary>
/// Dense segregated store of joints: one packed table per archetype,
/// kind-tagged identities, stable slots recycled through intrusive stacks.
/// Creation validates bodies and wakes sleeping dynamics; destroying a body
/// drops every joint touching it through <see cref="DestroyJointsOfBody"/>.
/// </summary>
public sealed class JointStore
{
    private const int InitialCapacity = 8;

    private DistanceJointData[] _distance;
    private int[] _distanceFree;
    private int _distanceFreeCount;
    private int _distanceHighWater;
    private int _distanceCount;

    private BallSocketJointData[] _ballSocket;
    private int[] _ballSocketFree;
    private int _ballSocketFreeCount;
    private int _ballSocketHighWater;
    private int _ballSocketCount;

    private WeldJointData[] _weld;
    private int[] _weldFree;
    private int _weldFreeCount;
    private int _weldHighWater;
    private int _weldCount;

    private HingeJointData[] _hinge;
    private int[] _hingeFree;
    private int _hingeFreeCount;
    private int _hingeHighWater;
    private int _hingeCount;

    /// <summary>Creates a store with room for eight joints per archetype.</summary>
    public JointStore()
    {
        _distance = new DistanceJointData[InitialCapacity];
        _distanceFree = new int[InitialCapacity];
        _ballSocket = new BallSocketJointData[InitialCapacity];
        _ballSocketFree = new int[InitialCapacity];
        _weld = new WeldJointData[InitialCapacity];
        _weldFree = new int[InitialCapacity];
        _hinge = new HingeJointData[InitialCapacity];
        _hingeFree = new int[InitialCapacity];
    }

    /// <summary>Live joints across all archetypes.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _distanceCount + _ballSocketCount + _weldCount + _hingeCount;
    }

    /// <summary>Whether <paramref name="id"/> names a live joint.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid(JointId id)
    {
        if (!id.IsValid)
        {
            return false;
        }

        int slot = (int)id.SlotIndex;
        return id.Kind switch
        {
            JointKind.Distance => slot < _distance.Length && _distance[slot].Live,
            JointKind.BallSocket => slot < _ballSocket.Length && _ballSocket[slot].Live,
            JointKind.Weld => slot < _weld.Length && _weld[slot].Live,
            JointKind.Hinge => slot < _hinge.Length && _hinge[slot].Live,
            _ => false,
        };
    }

    /// <summary>Creates a rigid rod between two body-frame anchors and wakes both bodies.</summary>
    /// <param name="bodies">Body registry; both bodies must be live.</param>
    /// <param name="bodyA">First body.</param>
    /// <param name="bodyB">Second body.</param>
    /// <param name="localAnchorA">Anchor in the first body frame.</param>
    /// <param name="localAnchorB">Anchor in the second body frame.</param>
    /// <param name="length">Rest separation; finite and never negative.</param>
    public JointId CreateDistance(
        BodyStore bodies, BodyId bodyA, BodyId bodyB, Vec3 localAnchorA, Vec3 localAnchorB, float length)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        bodies.Slot(bodyA);
        bodies.Slot(bodyB);

        if (!(length >= 0.0f) || float.IsNaN(length) || float.IsInfinity(length))
        {
            ThrowHelper.ThrowInvalidOperation("Joint rest length must be finite and non-negative.");
        }

        int slot = AllocateDistance();
        _distance[slot] = new DistanceJointData
        {
            BodyA = bodyA,
            BodyB = bodyB,
            LocalAnchorA = localAnchorA,
            LocalAnchorB = localAnchorB,
            Length = length,
            Impulse = 0.0f,
            Live = true,
        };
        _distanceCount++;
        bodies.SetAwake(bodyA, true);
        bodies.SetAwake(bodyB, true);
        return new JointId(JointKind.Distance, (uint)slot);
    }

    /// <summary>Creates a point-to-point joint between two body-frame anchors and wakes both bodies.</summary>
    /// <param name="bodies">Body registry; both bodies must be live.</param>
    /// <param name="bodyA">First body.</param>
    /// <param name="bodyB">Second body.</param>
    /// <param name="localAnchorA">Anchor in the first body frame.</param>
    /// <param name="localAnchorB">Anchor in the second body frame.</param>
    public JointId CreateBallSocket(BodyStore bodies, BodyId bodyA, BodyId bodyB, Vec3 localAnchorA, Vec3 localAnchorB)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        bodies.Slot(bodyA);
        bodies.Slot(bodyB);

        int slot = AllocateBallSocket();
        _ballSocket[slot] = new BallSocketJointData
        {
            BodyA = bodyA,
            BodyB = bodyB,
            LocalAnchorA = localAnchorA,
            LocalAnchorB = localAnchorB,
            Impulse = Vec3.Zero,
            Live = true,
        };
        _ballSocketCount++;
        bodies.SetAwake(bodyA, true);
        bodies.SetAwake(bodyB, true);
        return new JointId(JointKind.BallSocket, (uint)slot);
    }

    /// <summary>Freezes the current relative placement of two bodies and wakes both.</summary>
    /// <param name="bodies">Body registry; both bodies must be live.</param>
    /// <param name="bodyA">First body.</param>
    /// <param name="bodyB">Second body.</param>
    /// <param name="localAnchorA">Anchor in the first body frame.</param>
    /// <param name="localAnchorB">Anchor in the second body frame.</param>
    public JointId CreateWeld(BodyStore bodies, BodyId bodyA, BodyId bodyB, Vec3 localAnchorA, Vec3 localAnchorB)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ref BodyState stateA = ref bodies.Slot(bodyA);
        ref BodyState stateB = ref bodies.Slot(bodyB);

        int slot = AllocateWeld();
        _weld[slot] = new WeldJointData
        {
            BodyA = bodyA,
            BodyB = bodyB,
            LocalAnchorA = localAnchorA,
            LocalAnchorB = localAnchorB,
            ReferenceRotation = stateA.Rotation.Conjugate() * stateB.Rotation,
            LinearImpulse = Vec3.Zero,
            AngularImpulse = Vec3.Zero,
            Live = true,
        };
        _weldCount++;
        bodies.SetAwake(bodyA, true);
        bodies.SetAwake(bodyB, true);
        return new JointId(JointKind.Weld, (uint)slot);
    }

    /// <summary>Creates a hinge around a body-frame axis with optional stops and motor; wakes both bodies.</summary>
    /// <param name="bodies">Body registry; both bodies must be live.</param>
    /// <param name="bodyA">First body.</param>
    /// <param name="bodyB">Second body.</param>
    /// <param name="localAnchorA">Anchor in the first body frame.</param>
    /// <param name="localAnchorB">Anchor in the second body frame.</param>
    /// <param name="localAxisA">Hinge axis in the first body frame; must be a unit vector.</param>
    /// <param name="localAxisB">Hinge axis in the second body frame; must be a unit vector.</param>
    /// <param name="lowerLimit">Lower angular stop in radians.</param>
    /// <param name="upperLimit">Upper angular stop in radians.</param>
    /// <param name="enableLimit">Whether stops are enforced.</param>
    /// <param name="motorSpeed">Motor target speed in radians per second.</param>
    /// <param name="maxMotorTorque">Motor torque cap.</param>
    /// <param name="enableMotor">Whether the motor drives.</param>
    public JointId CreateHinge(
        BodyStore bodies, BodyId bodyA, BodyId bodyB,
        Vec3 localAnchorA, Vec3 localAnchorB, Vec3 localAxisA, Vec3 localAxisB,
        float lowerLimit = 0.0f, float upperLimit = 0.0f, bool enableLimit = false,
        float motorSpeed = 0.0f, float maxMotorTorque = 0.0f, bool enableMotor = false)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        bodies.Slot(bodyA);
        bodies.Slot(bodyB);

        if (MathF.Abs(localAxisA.LengthSquared() - 1.0f) > 1e-4f || MathF.Abs(localAxisB.LengthSquared() - 1.0f) > 1e-4f)
        {
            ThrowHelper.ThrowInvalidOperation("Hinge axes must be unit vectors.");
        }

        if (enableLimit && lowerLimit > upperLimit)
        {
            ThrowHelper.ThrowInvalidOperation("Hinge lower limit must not exceed the upper limit.");
        }

        if (enableMotor && (!(maxMotorTorque >= 0.0f) || float.IsNaN(maxMotorTorque)))
        {
            ThrowHelper.ThrowInvalidOperation("Hinge motor torque cap must not be negative.");
        }

        int slot = AllocateHinge();
        _hinge[slot] = new HingeJointData
        {
            BodyA = bodyA,
            BodyB = bodyB,
            LocalAnchorA = localAnchorA,
            LocalAnchorB = localAnchorB,
            LocalAxisA = localAxisA,
            LocalAxisB = localAxisB,
            LocalPerpA = Perpendicular(localAxisA),
            LocalPerpB = Perpendicular(localAxisB),
            LowerLimit = lowerLimit,
            UpperLimit = upperLimit,
            EnableLimit = enableLimit,
            MotorSpeed = motorSpeed,
            MaxMotorTorque = maxMotorTorque,
            EnableMotor = enableMotor,
            PointImpulse = Vec3.Zero,
            AxisImpulse = Vec3.Zero,
            LimitImpulse = 0.0f,
            MotorImpulse = 0.0f,
            Live = true,
        };
        _hingeCount++;
        bodies.SetAwake(bodyA, true);
        bodies.SetAwake(bodyB, true);
        return new JointId(JointKind.Hinge, (uint)slot);
    }

    /// <summary>Drops a joint; its identity dies with it.</summary>
    public void Destroy(JointId id)
    {
        int slot = Validate(id);
        switch (id.Kind)
        {
            case JointKind.Distance:
                _distance[slot] = default;
                _distanceFree[_distanceFreeCount++] = slot;
                _distanceCount--;
                break;
            case JointKind.BallSocket:
                _ballSocket[slot] = default;
                _ballSocketFree[_ballSocketFreeCount++] = slot;
                _ballSocketCount--;
                break;
            case JointKind.Weld:
                _weld[slot] = default;
                _weldFree[_weldFreeCount++] = slot;
                _weldCount--;
                break;
            default:
                _hinge[slot] = default;
                _hingeFree[_hingeFreeCount++] = slot;
                _hingeCount--;
                break;
        }
    }

    /// <summary>Drops every joint touching a body; call before destroying the body.</summary>
    public void DestroyJointsOfBody(BodyId bodyId)
    {
        for (int i = 0; i < _distance.Length; i++)
        {
            if (_distance[i].Live && (_distance[i].BodyA == bodyId || _distance[i].BodyB == bodyId))
            {
                Destroy(new JointId(JointKind.Distance, (uint)i));
            }
        }

        for (int i = 0; i < _ballSocket.Length; i++)
        {
            if (_ballSocket[i].Live && (_ballSocket[i].BodyA == bodyId || _ballSocket[i].BodyB == bodyId))
            {
                Destroy(new JointId(JointKind.BallSocket, (uint)i));
            }
        }

        for (int i = 0; i < _weld.Length; i++)
        {
            if (_weld[i].Live && (_weld[i].BodyA == bodyId || _weld[i].BodyB == bodyId))
            {
                Destroy(new JointId(JointKind.Weld, (uint)i));
            }
        }

        for (int i = 0; i < _hinge.Length; i++)
        {
            if (_hinge[i].Live && (_hinge[i].BodyA == bodyId || _hinge[i].BodyB == bodyId))
            {
                Destroy(new JointId(JointKind.Hinge, (uint)i));
            }
        }
    }

    /// <summary>Internal table access for the solver; validated.</summary>
    internal ref DistanceJointData Distance(int slot) => ref _distance[slot];

    /// <summary>Internal table access for the solver; validated.</summary>
    internal ref BallSocketJointData BallSocket(int slot) => ref _ballSocket[slot];

    /// <summary>Internal table access for the solver; validated.</summary>
    internal ref WeldJointData Weld(int slot) => ref _weld[slot];

    /// <summary>Internal table access for the solver; validated.</summary>
    internal ref HingeJointData Hinge(int slot) => ref _hinge[slot];

    /// <summary>Table length for solver iteration.</summary>
    internal int DistanceCapacity => _distance.Length;

    /// <summary>Table length for solver iteration.</summary>
    internal int BallSocketCapacity => _ballSocket.Length;

    /// <summary>Table length for solver iteration.</summary>
    internal int WeldCapacity => _weld.Length;

    /// <summary>Table length for solver iteration.</summary>
    internal int HingeCapacity => _hinge.Length;

    /// <summary>Whether a distance slot holds a live joint; for solver iteration.</summary>
    internal bool IsDistanceLive(int slot) => _distance[slot].Live;

    /// <summary>Whether a ball-socket slot holds a live joint; for solver iteration.</summary>
    internal bool IsBallSocketLive(int slot) => _ballSocket[slot].Live;

    /// <summary>Whether a weld slot holds a live joint; for solver iteration.</summary>
    internal bool IsWeldLive(int slot) => _weld[slot].Live;

    /// <summary>Whether a hinge slot holds a live joint; for solver iteration.</summary>
    internal bool IsHingeLive(int slot) => _hinge[slot].Live;

    private int AllocateDistance()
    {
        if (_distanceFreeCount > 0)
        {
            return _distanceFree[--_distanceFreeCount];
        }

        if (_distanceHighWater >= _distance.Length)
        {
            Array.Resize(ref _distance, _distance.Length * 2);
            Array.Resize(ref _distanceFree, _distanceFree.Length * 2);
        }

        return _distanceHighWater++;
    }


    private int AllocateBallSocket()
    {
        if (_ballSocketFreeCount > 0)
        {
            return _ballSocketFree[--_ballSocketFreeCount];
        }

        if (_ballSocketHighWater >= _ballSocket.Length)
        {
            Array.Resize(ref _ballSocket, _ballSocket.Length * 2);
            Array.Resize(ref _ballSocketFree, _ballSocketFree.Length * 2);
        }

        return _ballSocketHighWater++;
    }


    private int AllocateWeld()
    {
        if (_weldFreeCount > 0)
        {
            return _weldFree[--_weldFreeCount];
        }

        if (_weldHighWater >= _weld.Length)
        {
            Array.Resize(ref _weld, _weld.Length * 2);
            Array.Resize(ref _weldFree, _weldFree.Length * 2);
        }

        return _weldHighWater++;
    }

    private int AllocateHinge()
    {
        if (_hingeFreeCount > 0)
        {
            return _hingeFree[--_hingeFreeCount];
        }

        if (_hingeHighWater >= _hinge.Length)
        {
            Array.Resize(ref _hinge, _hinge.Length * 2);
            Array.Resize(ref _hingeFree, _hingeFree.Length * 2);
        }

        return _hingeHighWater++;
    }

    /// <summary>Builds a unit perpendicular to an axis.</summary>
    private static Vec3 Perpendicular(Vec3 axis)
    {
        Vec3 reference = MathF.Abs(axis.Y) < 0.99f ? new Vec3(0.0f, 1.0f, 0.0f) : new Vec3(1.0f, 0.0f, 0.0f);
        return Vec3.Normalize(Vec3.Cross(axis, reference));
    }


    private int Validate(JointId id)
    {
        if (!IsValid(id))
        {
            ThrowHelper.ThrowInvalidOperation($"Joint is unallocated or has been removed.");
        }

        return (int)id.SlotIndex;
    }
}
