namespace Axrone.Physics2D;

/// <summary>
/// Dense slot store of simulated bodies: identities are slot plus one, freed
/// slots recycle through an intrusive stack, and the arrays grow
/// geometrically. All validation lives here; frames, velocities, forces and
/// mass flow through guarded accessors, and <see cref="Integrate"/> advances
/// every awake body with semi-implicit Euler plus Box2D-style damping.
/// </summary>
public sealed class BodyStore
{
    private const int InitialCapacity = 16;

    private BodyState[] _bodies;
    private int[] _freeStack;
    private int _freeCount;
    private int _highWater;
    private int _count;
    private int _capacity;

    /// <summary>Creates a store with room for sixteen bodies.</summary>
    public BodyStore()
    {
        _capacity = InitialCapacity;
        _bodies = new BodyState[_capacity];
        _freeStack = new int[_capacity];
        _freeCount = 0;
        _highWater = 0;
        _count = 0;
    }

    /// <summary>Live bodies.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    /// <summary>Adds a body and returns its identity.</summary>
    /// <param name="definition">Construction parameters; damping must not be negative.</param>
    public BodyId Create(in BodyDef definition)
    {
        if (definition.LinearDamping < 0.0f || float.IsNaN(definition.LinearDamping) ||
            definition.AngularDamping < 0.0f || float.IsNaN(definition.AngularDamping))
        {
            ThrowHelper.ThrowInvalidOperation("Damping must not be negative.");
        }

        if (float.IsNaN(definition.GravityScale) || float.IsInfinity(definition.GravityScale))
        {
            ThrowHelper.ThrowInvalidOperation("Gravity scale must be finite.");
        }

        int slot = AllocateSlot();
        ref BodyState body = ref _bodies[slot];

        body = default;
        body.Live = true;
        body.FirstShape = -1;
        body.Type = definition.Type;
        body.Flags = BodyFlags.None;
        if (definition.Awake)
        {
            body.Flags = (body.Flags | BodyFlags.Awake) & ~BodyFlags.Sleeping;
        }

        if (definition.Bullet)
        {
            body.Flags |= BodyFlags.Bullet;
        }

        if (definition.FixedRotation)
        {
            body.Flags |= BodyFlags.FixedRotation;
        }

        if (definition.AllowSleep)
        {
            body.Flags |= BodyFlags.AutoSleep;
        }

        body.Position = definition.Position;
        body.Cos = MathF.Cos(definition.Angle);
        body.Sin = MathF.Sin(definition.Angle);
        body.LinearVelocity = definition.Type == BodyType.Static ? Vec2.Zero : definition.LinearVelocity;
        body.AngularVelocity = definition is { Type: BodyType.Static } or { FixedRotation: true }
            ? 0.0f
            : definition.AngularVelocity;
        body.LinearDamping = definition.LinearDamping;
        body.AngularDamping = definition.AngularDamping;
        body.GravityScale = definition.GravityScale;

        _count++;
        return BodyId.FromIndex(slot);
    }

    /// <summary>Drops a body; its identity dies with it.</summary>
    public void Destroy(BodyId id)
    {
        int slot = Validate(id);
        _bodies[slot] = default;
        _freeStack[_freeCount++] = slot;
        _count--;
    }

    /// <summary>Whether <paramref name="id"/> names a live body.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid(BodyId id)
    {
        int slot = id.ToIndex();
        return id.IsValid && slot >= 0 && slot < _capacity && _bodies[slot].Live;
    }

    /// <summary>Reads the frame of a body.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Transform2D GetTransform(BodyId id)
    {
        ref readonly BodyState body = ref Slot(id);
        return Transform2D.FromRotation(body.Position, body.Cos, body.Sin);
    }

    /// <summary>Moves a body without touching its velocities; wakes it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetTransform(BodyId id, Vec2 position, float angle)
    {
        ref BodyState body = ref Slot(id);
        body.Position = position;
        body.Cos = MathF.Cos(angle);
        body.Sin = MathF.Sin(angle);
        body.Flags = (body.Flags | BodyFlags.Awake) & ~BodyFlags.Sleeping;
    }

    /// <summary>Reads the linear velocity of a body.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec2 GetLinearVelocity(BodyId id) => Slot(id).LinearVelocity;

    /// <summary>Writes the linear velocity of a body; statics ignore it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetLinearVelocity(BodyId id, Vec2 velocity)
    {
        ref BodyState body = ref Slot(id);
        if (body.Type != BodyType.Static)
        {
            body.LinearVelocity = velocity;
            body.Flags = (body.Flags | BodyFlags.Awake) & ~BodyFlags.Sleeping;
        }
    }

    /// <summary>Reads the angular velocity of a body.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetAngularVelocity(BodyId id) => Slot(id).AngularVelocity;

    /// <summary>Writes the angular velocity of a body; statics and locked bodies ignore it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetAngularVelocity(BodyId id, float velocity)
    {
        ref BodyState body = ref Slot(id);
        if (body.Type != BodyType.Static && (body.Flags & BodyFlags.FixedRotation) == 0)
        {
            body.AngularVelocity = velocity;
            body.Flags = (body.Flags | BodyFlags.Awake) & ~BodyFlags.Sleeping;
        }
    }

    /// <summary>Reads the type of a body.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BodyType GetType(BodyId id) => Slot(id).Type;

    /// <summary>Whether the body is awake.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAwake(BodyId id) => (Slot(id).Flags & BodyFlags.Awake) != 0;

    /// <summary>Whether the body rests with the sleeping bit set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSleeping(BodyId id) => (Slot(id).Flags & BodyFlags.Sleeping) != 0;

    /// <summary>Puts a body to sleep or wakes it; sleeping zeroes velocities and forces.</summary>
    public void SetAwake(BodyId id, bool awake)
    {
        ref BodyState body = ref Slot(id);
        if (awake)
        {
            body.Flags = (body.Flags | BodyFlags.Awake) & ~BodyFlags.Sleeping;
            body.SleepTime = 0.0f;
            return;
        }

        body.Flags = (body.Flags | BodyFlags.Sleeping) & ~BodyFlags.Awake;
        body.LinearVelocity = Vec2.Zero;
        body.AngularVelocity = 0.0f;
        body.Force = Vec2.Zero;
        body.Torque = 0.0f;
    }

    /// <summary>Accumulates a world force; wakes the body.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyForce(BodyId id, Vec2 force)
    {
        ref BodyState body = ref Slot(id);
        if (body.Type != BodyType.Dynamic)
        {
            return;
        }

        body.Force += force;
        body.Flags = (body.Flags | BodyFlags.Awake) & ~BodyFlags.Sleeping;
    }

    /// <summary>Accumulates a torque; wakes the body.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyTorque(BodyId id, float torque)
    {
        ref BodyState body = ref Slot(id);
        if (body.Type != BodyType.Dynamic)
        {
            return;
        }

        body.Torque += torque;
        body.Flags = (body.Flags | BodyFlags.Awake) & ~BodyFlags.Sleeping;
    }

    /// <summary>Applies an instantaneous linear impulse; wakes the body.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyLinearImpulse(BodyId id, Vec2 impulse)
    {
        ref BodyState body = ref Slot(id);
        if (body.Type != BodyType.Dynamic)
        {
            return;
        }

        body.LinearVelocity += impulse * body.InverseMass;
        body.Flags = (body.Flags | BodyFlags.Awake) & ~BodyFlags.Sleeping;
    }

    /// <summary>Applies an instantaneous angular impulse; wakes the body.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyAngularImpulse(BodyId id, float impulse)
    {
        ref BodyState body = ref Slot(id);
        if (body.Type != BodyType.Dynamic || (body.Flags & BodyFlags.FixedRotation) != 0)
        {
            return;
        }

        body.AngularVelocity += impulse * body.InverseInertia;
        body.Flags = (body.Flags | BodyFlags.Awake) & ~BodyFlags.Sleeping;
    }

    /// <summary>Overwrites the mass properties; zero mass means infinite (static-like response).</summary>
    public void SetMassData(BodyId id, float mass, float inertia)
    {
        if (mass < 0.0f || float.IsNaN(mass) || inertia < 0.0f || float.IsNaN(inertia))
        {
            ThrowHelper.ThrowInvalidOperation("Mass and inertia must not be negative.");
        }

        ref BodyState body = ref Slot(id);
        bool infinite = body.Type != BodyType.Dynamic;
        body.Mass = mass;
        body.InverseMass = !infinite && mass > 0.0f ? 1.0f / mass : 0.0f;
        body.Inertia = inertia;
        body.InverseInertia = !infinite && inertia > 0.0f && (body.Flags & BodyFlags.FixedRotation) == 0 ? 1.0f / inertia : 0.0f;
    }

    /// <summary>Reads the mass of a body.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetMass(BodyId id) => Slot(id).Mass;

    /// <summary>Reads the inverse mass of a body.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetInverseMass(BodyId id) => Slot(id).InverseMass;

    /// <summary>Reads the inverse inertia of a body.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetInverseInertia(BodyId id) => Slot(id).InverseInertia;

    /// <summary>
    /// Advances every awake body by <paramref name="dt"/> under
    /// <paramref name="gravity"/>: forces integrate into velocities, damping
    /// scales them, velocities integrate into frames, and forces clear.
    /// Static bodies never move; kinematic bodies translate on velocity
    /// alone; locked rotation pins the angular channel.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Integrate(float dt, Vec2 gravity)
    {
        IntegrateVelocities(dt, gravity);
        IntegratePositions(dt);
    }

    /// <summary>
    /// Force half of the step: gravity and accumulated forces integrate into
    /// velocities with damping applied, then forces clear. Positions untouched.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void IntegrateVelocities(float dt, Vec2 gravity)
    {
        if (!(dt > 0.0f))
        {
            ThrowHelper.ThrowInvalidOperation("Time step must be positive.");
        }

        Span<BodyState> bodies = _bodies.AsSpan(0, _capacity);
        for (int i = 0; i < bodies.Length; i++)
        {
            ref BodyState body = ref bodies[i];
            if ((body.Flags & BodyFlags.Awake) == 0)
            {
                continue;
            }

            if (body.Type != BodyType.Dynamic)
            {
                continue;
            }

            body.LinearVelocity += dt * ((gravity * body.GravityScale) + (body.Force * body.InverseMass));
            body.AngularVelocity += dt * body.Torque * body.InverseInertia;

            body.LinearVelocity *= 1.0f / (1.0f + (dt * body.LinearDamping));
            body.AngularVelocity *= 1.0f / (1.0f + (dt * body.AngularDamping));

            if ((body.Flags & BodyFlags.FixedRotation) != 0)
            {
                body.AngularVelocity = 0.0f;
            }

            body.Force = Vec2.Zero;
            body.Torque = 0.0f;
        }
    }

    /// <summary>
    /// Position half of the step: velocities integrate into frames. Static and
    /// sleeping bodies never move; kinematic bodies translate on velocity
    /// alone.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void IntegratePositions(float dt)
    {
        if (!(dt > 0.0f))
        {
            ThrowHelper.ThrowInvalidOperation("Time step must be positive.");
        }

        Span<BodyState> bodies = _bodies.AsSpan(0, _capacity);
        for (int i = 0; i < bodies.Length; i++)
        {
            ref BodyState body = ref bodies[i];
            if ((body.Flags & BodyFlags.Awake) == 0)
            {
                continue;
            }

            if (body.Type == BodyType.Static)
            {
                continue;
            }

            body.Position += body.LinearVelocity * dt;
            float newAngle = MathF.Atan2(body.Sin, body.Cos) + (body.AngularVelocity * dt);
            body.Cos = MathF.Cos(newAngle);
            body.Sin = MathF.Sin(newAngle);
        }
    }

    /// <summary>Internal slot access for the world and the solver; validated.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref BodyState Slot(BodyId id) => ref _bodies[Validate(id)];

    /// <summary>Raw slot of an identity without validation; the caller holds the invariant.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int SlotOf(BodyId id) => id.ToIndex();

    /// <summary>Slot capacity for internal iteration.</summary>
    internal int SlotCapacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _capacity;
    }

    /// <summary>Whether a raw slot holds a live body; for internal iteration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsSlotLive(int slot) => slot >= 0 && slot < _capacity && _bodies[slot].Live;

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
        Array.Resize(ref _bodies, newCapacity);
        Array.Resize(ref _freeStack, newCapacity);
        _capacity = newCapacity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Validate(BodyId id)
    {
        int slot = id.ToIndex();
        if (!id.IsValid || slot < 0 || slot >= _capacity || !_bodies[slot].Live)
        {
            ThrowHelper.ThrowBodyNotFound(id);
        }

        return slot;
    }
}
