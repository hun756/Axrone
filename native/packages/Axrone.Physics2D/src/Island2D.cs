namespace Axrone.Physics2D;

/// <summary>
/// Island builder plus sleep pass: awake dynamic bodies sharing contacts
/// union into islands, quiet timers accumulate per body, and an island whose
/// every dynamic member allows sleep and outlasted the threshold is put to
/// rest in one go. Static bodies never union; a moving kinematic blocks the
/// sleep of every island it touches. Buffers persist across steps and grow
/// geometrically, so the steady state allocates nothing.
/// </summary>
internal sealed class IslandSolver
{
    private int[] _parent = [];
    private int[] _size = [];
    private float[] _minSleep = [];
    private bool[] _blocked = [];

    /// <summary>
    /// Updates quiet timers from post-step velocities, unions contact-linked
    /// dynamics, and sleeps settled islands. Waking stays outside: new pairs,
    /// forces, teleports and impulses wake explicitly at their own sites.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void UpdateSleep(
        BodyStore bodies,
        JointStore joints,
        Span<Contact> contacts,
        float dt,
        float timeToSleep,
        float linearTolerance,
        float angularTolerance)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(joints);

        int capacity = bodies.SlotCapacity;
        Ensure(capacity);

        float linTolSq = linearTolerance * linearTolerance;
        float angTolSq = angularTolerance * angularTolerance;

        for (int slot = 0; slot < capacity; slot++)
        {
            if (!bodies.IsSlotLive(slot))
            {
                _parent[slot] = -1;
                continue;
            }

            BodyId id = new((uint)slot + 1u);
            ref BodyState body = ref bodies.Slot(id);
            if (body.Type != BodyType.Dynamic)
            {
                _parent[slot] = -1;
                continue;
            }

            _parent[slot] = slot;
            _size[slot] = 1;

            if ((body.Flags & BodyFlags.Awake) == 0)
            {
                continue;
            }

            bool quiet = body.LinearVelocity.LengthSquared() < linTolSq &&
                (body.AngularVelocity * body.AngularVelocity) < angTolSq;
            body.SleepTime = quiet ? body.SleepTime + dt : 0.0f;
        }

        for (int i = 0; i < contacts.Length; i++)
        {
            int slotA = BodyStore.SlotOf(contacts[i].BodyA);
            int slotB = BodyStore.SlotOf(contacts[i].BodyB);
            if (slotA >= 0 && slotB >= 0 && _parent[slotA] >= 0 && _parent[slotB] >= 0)
            {
                Union(slotA, slotB);
            }
        }

        for (int slot = 0; slot < joints.SlotCapacity; slot++)
        {
            if (!joints.IsSlotLive(slot))
            {
                continue;
            }

            ref readonly JointRecord joint = ref joints.SlotByIndex(slot);
            int slotA = BodyStore.SlotOf(joint.BodyA);
            int slotB = BodyStore.SlotOf(joint.BodyB);
            if (slotA >= 0 && slotB >= 0 && slotA < _parent.Length && slotB < _parent.Length &&
                _parent[slotA] >= 0 && _parent[slotB] >= 0)
            {
                Union(slotA, slotB);
            }

            if (joint.Kind == JointKind.Gear)
            {
                UnionGearBodies(joints, joint.GearJointA);
                UnionGearBodies(joints, joint.GearJointB);
            }
        }

        for (int slot = 0; slot < capacity; slot++)
        {
            if (_parent[slot] < 0)
            {
                continue;
            }

            int root = Find(slot);
            _minSleep[root] = float.PositiveInfinity;
            _blocked[root] = false;
        }

        for (int slot = 0; slot < capacity; slot++)
        {
            if (_parent[slot] < 0)
            {
                continue;
            }

            BodyId id = new((uint)slot + 1u);
            ref readonly BodyState body = ref bodies.Slot(id);
            int root = Find(slot);

            if ((body.Flags & BodyFlags.AllowSleep) == 0)
            {
                _blocked[root] = true;
            }

            if (body.SleepTime < _minSleep[root])
            {
                _minSleep[root] = body.SleepTime;
            }
        }

        for (int i = 0; i < contacts.Length; i++)
        {
            BlockOnMovingKinematic(bodies, contacts[i].BodyA, contacts[i].BodyB, linTolSq, angTolSq);
        }

        for (int slot = 0; slot < capacity; slot++)
        {
            if (_parent[slot] < 0)
            {
                continue;
            }

            int root = Find(slot);
            if (!_blocked[root] && _minSleep[root] >= timeToSleep)
            {
                BodyId id = new((uint)slot + 1u);
                ref readonly BodyState body = ref bodies.Slot(id);
                if ((body.Flags & BodyFlags.Awake) != 0)
                {
                    bodies.SetAwake(id, false);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BlockOnMovingKinematic(BodyStore bodies, BodyId bodyA, BodyId bodyB, float linTolSq, float angTolSq)
    {
        int slotA = BodyStore.SlotOf(bodyA);
        int slotB = BodyStore.SlotOf(bodyB);
        BlockSide(bodies, slotA, slotB, linTolSq, angTolSq);
        BlockSide(bodies, slotB, slotA, linTolSq, angTolSq);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BlockSide(BodyStore bodies, int kinematicSlot, int otherSlot, float linTolSq, float angTolSq)
    {
        if ((uint)kinematicSlot >= (uint)_parent.Length || _parent[kinematicSlot] >= 0)
        {
            return;
        }

        BodyId id = new((uint)kinematicSlot + 1u);
        ref readonly BodyState body = ref bodies.Slot(id);
        if (body.Type != BodyType.Kinematic)
        {
            return;
        }

        bool moving = body.LinearVelocity.LengthSquared() >= linTolSq ||
            (body.AngularVelocity * body.AngularVelocity) >= angTolSq;
        if (moving && (uint)otherSlot < (uint)_parent.Length && _parent[otherSlot] >= 0)
        {
            _blocked[Find(otherSlot)] = true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UnionGearBodies(JointStore joints, JointId gearedId)
    {
        if (!joints.IsValid(gearedId))
        {
            return;
        }

        ref readonly JointRecord geared = ref joints.Slot(gearedId);
        int gearedA = BodyStore.SlotOf(geared.BodyA);
        int gearedB = BodyStore.SlotOf(geared.BodyB);
        if (gearedA >= 0 && gearedB >= 0 && gearedA < _parent.Length && gearedB < _parent.Length &&
            _parent[gearedA] >= 0 && _parent[gearedB] >= 0)
        {
            Union(gearedA, gearedB);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Union(int a, int b)
    {
        int rootA = Find(a);
        int rootB = Find(b);
        if (rootA == rootB)
        {
            return;
        }

        if (_size[rootA] < _size[rootB])
        {
            (rootA, rootB) = (rootB, rootA);
        }

        _parent[rootB] = rootA;
        _size[rootA] += _size[rootB];
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private int Find(int slot)
    {
        int root = slot;
        while (_parent[root] != root)
        {
            root = _parent[root];
        }

        while (_parent[slot] != root)
        {
            int next = _parent[slot];
            _parent[slot] = root;
            slot = next;
        }

        return root;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Ensure(int capacity)
    {
        if (_parent.Length >= capacity)
        {
            return;
        }

        int grown = Math.Max(capacity, _parent.Length == 0 ? 16 : _parent.Length * 2);
        Array.Resize(ref _parent, grown);
        Array.Resize(ref _size, grown);
        Array.Resize(ref _minSleep, grown);
        Array.Resize(ref _blocked, grown);
    }
}
