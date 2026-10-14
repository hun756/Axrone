namespace Axrone.Physics3D;

using System.Buffers;
using System.Threading;

/// <summary>
/// Construction parameters of a world: gravity, solver sweeps, sleep tuning
/// and CCD subdivision. Guards live in the properties, so an invalid
/// combination is rejected at assignment.
/// </summary>
public readonly struct WorldDef
{
    /// <summary>Creates default parameters: earth-like gravity, sleeping on.</summary>
    public WorldDef()
    {
    }

    /// <summary>Gravitational acceleration; components must be finite.</summary>
    public Vec3 Gravity
    {
        get => field;
        init
        {
            if (float.IsNaN(value.X + value.Y + value.Z) || float.IsInfinity(value.X + value.Y + value.Z))
            {
                ThrowHelper.ThrowInvalidOperation("Gravity components must be finite.");
            }

            field = value;
        }
    } = new Vec3(0.0f, -10.0f, 0.0f);

    /// <summary>Tuning of the broadphase tree.</summary>
    public TreeOptions BroadphaseOptions { get; init; } = new TreeOptions();

    /// <summary>Velocity sweeps per step; zero skips the velocity pass.</summary>
    public int VelocityIterations
    {
        get => field;
        init
        {
            if (value < 0)
            {
                ThrowHelper.ThrowInvalidOperation("Velocity iterations must not be negative.");
            }

            field = value;
        }
    } = 8;

    /// <summary>Position sweeps per step; zero skips the position pass.</summary>
    public int PositionIterations
    {
        get => field;
        init
        {
            if (value < 0)
            {
                ThrowHelper.ThrowInvalidOperation("Position iterations must not be negative.");
            }

            field = value;
        }
    } = 3;

    /// <summary>Quiet seconds before a body sleeps; strictly positive.</summary>
    public float TimeToSleep
    {
        get => field;
        init
        {
            if (!(value > 0.0f) || float.IsNaN(value))
            {
                ThrowHelper.ThrowInvalidOperation("Time to sleep must be positive.");
            }

            field = value;
        }
    } = 0.5f;

    /// <summary>Linear speed below which a body counts as quiet; never negative.</summary>
    public float LinearSleepTolerance
    {
        get => field;
        init
        {
            if (value < 0.0f || float.IsNaN(value))
            {
                ThrowHelper.ThrowInvalidOperation("Linear sleep tolerance must not be negative.");
            }

            field = value;
        }
    } = 0.01f;

    /// <summary>Angular speed below which a body counts as quiet; never negative.</summary>
    public float AngularSleepTolerance
    {
        get => field;
        init
        {
            if (value < 0.0f || float.IsNaN(value))
            {
                ThrowHelper.ThrowInvalidOperation("Angular sleep tolerance must not be negative.");
            }

            field = value;
        }
    } = 0.0349f;

    /// <summary>Bullet translation covered per CCD substep; strictly positive.</summary>
    public float CcdSweepStep
    {
        get => field;
        init
        {
            if (!(value > 0.0f) || float.IsNaN(value) || float.IsInfinity(value))
            {
                ThrowHelper.ThrowInvalidOperation("CCD sweep step must be positive.");
            }

            field = value;
        }
    } = 0.5f;

    /// <summary>CCD substep cap per step; at least one.</summary>
    public int MaxCcdSubsteps
    {
        get => field;
        init
        {
            if (value < 1)
            {
                ThrowHelper.ThrowInvalidOperation("CCD substep cap must be at least one.");
            }

            field = value;
        }
    } = 8;
}

/// <summary>
/// Pair lifecycle notice: a shape pair started or stopped overlapping. Events
/// accumulate across the step and clear at the start of the next one; read
/// them after <see cref="World.Step"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ContactEvent(ShapeId ShapeA, ShapeId ShapeB, BodyId BodyA, BodyId BodyB, bool Began);

/// <summary>
/// Simulation world: owns bodies, shapes and the broadphase tree, and runs
/// the step loop — integrate, sync broadphase, collect pairs, evaluate
/// manifolds with filtering and warm-start carry, solve, sleep. Contacts
/// persist across steps by shape-pair identity; separated pairs drop out.
/// Disposable: tears down the broadphase tree.
/// </summary>
public sealed class World : IDisposable
{
    private const int InitialContactCapacity = 16;

    private readonly BodyStore _bodies;
    private readonly ShapeStore _shapes;
    private readonly JointStore _joints;
    private readonly DynamicAabbTree<ShapeId, SurfaceAreaHeuristicStrategy, NullSpatialMetricsSink> _tree;
    private readonly IslandSolver3D<DefaultSleepPolicy3D> _islands = new();
    private Contact3D[] _contacts;
    private Contact3D[] _previous;
    private int _contactCount;
    private int _previousCount;
    private ContactEvent[] _events;
    private int _eventCount;
    private BroadPair[] _currentPairs;
    private int _currentPairCount;
    private BroadPair[] _stepPairs;
    private int _stepPairCount;
    private int _disposed;

    /// <summary>Gravity of the world.</summary>
    public Vec3 Gravity { get; set; }

    /// <summary>Velocity sweeps per step.</summary>
    public int VelocityIterations { get; set; }

    /// <summary>Position sweeps per step.</summary>
    public int PositionIterations { get; set; }

    /// <summary>Quiet seconds before a body sleeps.</summary>
    public float TimeToSleep { get; set; }

    /// <summary>Linear speed below which a body counts as quiet.</summary>
    public float LinearSleepTolerance { get; set; }

    /// <summary>Angular speed below which a body counts as quiet.</summary>
    public float AngularSleepTolerance { get; set; }

    /// <summary>Bullet translation covered per CCD substep.</summary>
    public float CcdSweepStep { get; set; }

    /// <summary>CCD substep cap per step.</summary>
    public int MaxCcdSubsteps { get; set; }

    /// <summary>Creates a world with default tuning.</summary>
    public World()
        : this(new WorldDef())
    {
    }

    /// <summary>Creates a world with fresh stores and the given tuning.</summary>
    public World(in WorldDef definition)
    {
        Gravity = definition.Gravity;
        VelocityIterations = definition.VelocityIterations;
        PositionIterations = definition.PositionIterations;
        TimeToSleep = definition.TimeToSleep;
        LinearSleepTolerance = definition.LinearSleepTolerance;
        AngularSleepTolerance = definition.AngularSleepTolerance;
        CcdSweepStep = definition.CcdSweepStep;
        MaxCcdSubsteps = definition.MaxCcdSubsteps;
        _bodies = new BodyStore();
        _shapes = new ShapeStore();
        _joints = new JointStore();
        _tree = new DynamicAabbTree<ShapeId, SurfaceAreaHeuristicStrategy, NullSpatialMetricsSink>(definition.BroadphaseOptions);
        _contacts = new Contact3D[InitialContactCapacity];
        _previous = new Contact3D[InitialContactCapacity];
        _contactCount = 0;
        _previousCount = 0;
        _events = new ContactEvent[InitialContactCapacity];
        _eventCount = 0;
        _currentPairs = new BroadPair[InitialContactCapacity];
        _currentPairCount = 0;
        _stepPairs = new BroadPair[InitialContactCapacity];
        _stepPairCount = 0;
        _disposed = 0;
    }

    /// <summary>Body registry; create and drive bodies through it.</summary>
    public BodyStore Bodies
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bodies;
    }

    /// <summary>Shape registry; attach colliders through it.</summary>
    public ShapeStore Shapes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _shapes;
    }

    /// <summary>Joint registry; constrain bodies through it.</summary>
    public JointStore Joints
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _joints;
    }

    /// <summary>Island solver diagnostics: completed passes and rested bodies.</summary>
    public IslandMetrics3D IslandMetrics
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _islands.Metrics;
    }

    /// <summary>Broadphase tree; the world keeps it in sync every step.</summary>
    public DynamicAabbTree<ShapeId, SurfaceAreaHeuristicStrategy, NullSpatialMetricsSink> Broadphase
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tree;
    }

    /// <summary>Live contacts after the last step.</summary>
    public int ContactCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _contactCount;
    }

    /// <summary>Reads a tracked contact by index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Contact3D GetContact(int index) => _contacts[index];

    /// <summary>Pair events raised by the last step.</summary>
    public int ContactEventCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _eventCount;
    }

    /// <summary>Reads a pair event by index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ContactEvent GetContactEvent(int index) => _events[index];

    /// <summary>Casts a ray against all shapes; hits sort by ascending distance.</summary>
    /// <param name="ray">The ray.</param>
    /// <param name="maxDistance">The furthest hit kept.</param>
    /// <param name="hits">The destination; truncated when full.</param>
    /// <returns>The hits written.</returns>
    public int Raycast(in Ray3D ray, float maxDistance, Span<RaycastHit3D> hits)
    {
        ThrowIfDisposed();

        Span<RayHit> candidates = stackalloc RayHit[128];
        int candidateCount = Math.Min(_tree.RayCast(in ray, candidates, maxDistance), candidates.Length);

        int written = 0;
        for (int i = 0; i < candidateCount && written < hits.Length; i++)
        {
            ShapeId shapeId = _tree.GetUserData(candidates[i].ItemId);
            if (!_shapes.IsValid(shapeId))
            {
                continue;
            }

            ref ShapeRecord record = ref _shapes.Record(shapeId);
            bool struck;
            float distance;
            Vec3 normal;
            if (record.Kind == ColliderKind.Sphere)
            {
                Sphere3 world = _shapes.GetWorldSphere(shapeId);
                struck = ShapeRaycast3D.RaySphere(in ray, maxDistance, in world, out distance, out normal);
            }
            else
            {
                Box3 local = record.LocalBox;
                RigidTransform3D frame = _shapes.GetWorldFrame(shapeId);
                var world = new Box3(frame.TransformPoint(local.Center), local.HalfExtents);
                struck = ShapeRaycast3D.RayBox(in ray, maxDistance, in world, frame, out distance, out normal);
            }

            if (!struck)
            {
                continue;
            }

            var hit = new RaycastHit3D(shapeId, record.Body, ray.Origin + (ray.Direction * distance), normal, distance);
            int slot = written;
            while (slot > 0 && hits[slot - 1].Distance > distance)
            {
                hits[slot] = hits[slot - 1];
                slot--;
            }

            hits[slot] = hit;
            written++;
        }

        return written;
    }

    /// <summary>Collects the shapes overlapping a volume; ids sort ascending.</summary>
    /// <param name="box">The query volume.</param>
    /// <param name="results">The destination; truncated when full.</param>
    /// <returns>The shapes written.</returns>
    public int OverlapAabb(in Aabb3D box, Span<ShapeId> results)
    {
        ThrowIfDisposed();

        Span<SpatialItemId> candidates = stackalloc SpatialItemId[128];
        int candidateCount = Math.Min(_tree.QueryOverlaps(in box, candidates), candidates.Length);

        int written = 0;
        for (int i = 0; i < candidateCount && written < results.Length; i++)
        {
            ShapeId shapeId = _tree.GetUserData(candidates[i]);
            if (!_shapes.IsValid(shapeId))
            {
                continue;
            }

            int slot = written;
            while (slot > 0 && results[slot - 1] > shapeId)
            {
                results[slot] = results[slot - 1];
                slot--;
            }

            results[slot] = shapeId;
            written++;
        }

        return written;
    }

    /// <summary>Advances the simulation by <paramref name="dt"/> seconds.</summary>
    public void Step(float dt)
    {
        ThrowIfDisposed();

        if (!(dt > 0.0f) || float.IsNaN(dt))
        {
            ThrowHelper.ThrowInvalidOperation("Time step must be positive.");
        }

        int slices = CcdSliceCount(dt);
        float h = dt / slices;

        _eventCount = 0;
        SnapshotStepPairs();

        for (int slice = 0; slice < slices; slice++)
        {
            StepSlice(h);
        }

        UpdateIslands(dt);
        PublishContactEvents();
    }

    /// <summary>
    /// Destroys a body and its shapes, pulling each shape from the
    /// broadphase and dropping tracked pairs that named them. Destroying
    /// bodies through the raw store orphans tree items; prefer this path.
    /// </summary>
    public void DestroyBody(BodyId bodyId)
    {
        ThrowIfDisposed();

        _joints.DestroyJointsOfBody(bodyId);

        ref BodyState body = ref _bodies.Slot(bodyId);
        int current = body.FirstShape;
        while (current != -1)
        {
            ShapeId shapeId = ShapeId.FromIndex(current);
            int next = _shapes.Record(shapeId).NextShape;
            _shapes.Detach(_bodies, _tree, shapeId);
            current = next;
        }

        int kept = 0;
        for (int i = 0; i < _contactCount; i++)
        {
            if (_contacts[i].BodyA != bodyId && _contacts[i].BodyB != bodyId)
            {
                _contacts[kept++] = _contacts[i];
            }
        }

        _contactCount = kept;
        _bodies.Destroy(bodyId);
    }

    /// <summary>Tears down the broadphase tree; the world is unusable afterwards.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _tree.Dispose();
    }

    /// <summary>Canonical shape pair, ordered by raw identity.</summary>
    private readonly record struct BroadPair(ShapeId First, ShapeId Second);

    /// <summary>Collects broadphase pairs into the world's pair buffer.</summary>
    private struct PairCollector : IPairVisitor<ShapeId, World>
    {
        public bool OnPair(SpatialItemId idA, ShapeId dataA, SpatialItemId idB, ShapeId dataB, ref World context)
        {
            ShapeId first = dataA.RawValue <= dataB.RawValue ? dataA : dataB;
            ShapeId second = dataA.RawValue <= dataB.RawValue ? dataB : dataA;

            if (context._currentPairCount >= context._currentPairs.Length)
            {
                Array.Resize(ref context._currentPairs, context._currentPairs.Length * 2);
            }

            context._currentPairs[context._currentPairCount++] = new BroadPair(first, second);
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void StepSlice(float h)
    {
        _bodies.IntegrateVelocities(h, Gravity);
        SyncAwakeBodies();

        Contact3D[] built = _previous;
        _previous = _contacts;
        _previousCount = _contactCount;
        _contacts = built;
        _contactCount = 0;
        _currentPairCount = 0;

        var collector = new PairCollector();
        World self = this;
        _tree.FindPairs(ref collector, ref self);

        for (int i = 0; i < _currentPairCount; i++)
        {
            BroadPair pair = _currentPairs[i];
            if (TryBuildContact(pair.First, pair.Second, out Contact3D contact))
            {
                if (_contactCount >= _contacts.Length)
                {
                    Array.Resize(ref _contacts, _contacts.Length * 2);
                }

                _contacts[_contactCount++] = contact;
            }
        }

        for (int i = 0; i < _contactCount; i++)
        {
            bool matched = false;
            for (int j = 0; j < _previousCount; j++)
            {
                if (_previous[j].ShapeA == _contacts[i].ShapeA && _previous[j].ShapeB == _contacts[i].ShapeB)
                {
                    WarmStart(ref _contacts[i], in _previous[j]);
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                WakePair(_contacts[i].BodyA, _contacts[i].BodyB);
            }
        }

        ContactSolver.SolveVelocity(_bodies, _shapes, _contacts.AsSpan(0, _contactCount), VelocityIterations);

        for (int iteration = 0; iteration < VelocityIterations; iteration++)
        {
            JointSolver.SolveVelocityPass(_bodies, _joints, h);
        }

        _bodies.IntegratePositions(h);
        SyncAwakeBodies();

        ContactSolver.SolvePositions(_bodies, _contacts.AsSpan(0, _contactCount), PositionIterations);
        JointSolver.SolvePositionPass(_bodies, _joints);
    }

    /// <summary>Carries previous impulses into matching points by feature id.</summary>
    private static void WarmStart(ref Contact3D current, in Contact3D previous)
    {
        Manifold3D manifold = current.Manifold;
        Manifold3D old = previous.Manifold;
        for (int p = 0; p < manifold.Count; p++)
        {
            ContactPoint3D point = manifold[p];
            for (int q = 0; q < old.Count; q++)
            {
                ContactPoint3D carried = old[q];
                if (carried.Id == point.Id)
                {
                    ContactPoint3D updated = point;
                    updated.NormalImpulse = carried.NormalImpulse;
                    updated.TangentImpulse1 = carried.TangentImpulse1;
                    updated.TangentImpulse2 = carried.TangentImpulse2;
                    manifold.SetPoint(p, updated);
                    break;
                }
            }
        }

        current.Manifold = manifold;
    }

    /// <summary>Builds a contact from a broadphase pair; filters and narrowphase run here.</summary>
    private bool TryBuildContact(ShapeId shapeA, ShapeId shapeB, out Contact3D contact)
    {
        contact = default;
        if (!_shapes.IsValid(shapeA) || !_shapes.IsValid(shapeB))
        {
            return false;
        }

        ref ShapeRecord recordA = ref _shapes.Record(shapeA);
        ref ShapeRecord recordB = ref _shapes.Record(shapeB);
        if (recordA.Body == recordB.Body)
        {
            return false;
        }

        ref BodyState stateA = ref _bodies.Slot(recordA.Body);
        ref BodyState stateB = ref _bodies.Slot(recordB.Body);
        if (stateA.Type == BodyType.Static && stateB.Type == BodyType.Static)
        {
            return false;
        }

        if (!CollisionFilter.ShouldCollide(in recordA.Filter, in recordB.Filter))
        {
            return false;
        }

        if (!EvaluateNarrowphase(shapeA, in recordA, shapeB, in recordB, out Manifold3D manifold))
        {
            return false;
        }

        contact = new Contact3D
        {
            BodyA = recordA.Body,
            BodyB = recordB.Body,
            ShapeA = shapeA,
            ShapeB = shapeB,
            IsSensor = recordA.IsSensor || recordB.IsSensor,
            Manifold = manifold,
        };
        return true;
    }

    /// <summary>Dispatches the pair to the typed narrowphase by collider kinds.</summary>
    private bool EvaluateNarrowphase(
        ShapeId shapeA, in ShapeRecord recordA,
        ShapeId shapeB, in ShapeRecord recordB,
        out Manifold3D manifold)
    {
        if (recordA.Kind == ColliderKind.Sphere && recordB.Kind == ColliderKind.Sphere)
        {
            Sphere3 worldA = _shapes.GetWorldSphere(shapeA);
            Sphere3 worldB = _shapes.GetWorldSphere(shapeB);
            return Penetration3D.SphereSphere(in worldA, in worldB, out manifold);
        }

        if (recordA.Kind == ColliderKind.Sphere)
        {
            Sphere3 worldSphere = _shapes.GetWorldSphere(shapeA);
            Box3 localBox = recordB.LocalBox;
            RigidTransform3D boxFrame = _shapes.GetWorldFrame(shapeB);
            return Penetration3D.SphereBox(in worldSphere, in localBox, boxFrame, out manifold);
        }

        if (recordB.Kind == ColliderKind.Sphere)
        {
            Sphere3 worldSphere = _shapes.GetWorldSphere(shapeB);
            Box3 localBox = recordA.LocalBox;
            RigidTransform3D boxFrame = _shapes.GetWorldFrame(shapeA);
            bool hit = Penetration3D.SphereBox(in worldSphere, in localBox, boxFrame, out manifold);
            if (hit)
            {
                FlipManifold(ref manifold);
            }

            return hit;
        }

        Box3 localA = recordA.LocalBox;
        RigidTransform3D frameA = _shapes.GetWorldFrame(shapeA);
        Box3 localB = recordB.LocalBox;
        RigidTransform3D frameB = _shapes.GetWorldFrame(shapeB);
        return Penetration3D.BoxBox(in localA, frameA, in localB, frameB, out manifold);
    }

    /// <summary>Swaps a manifold so the normal points from B toward A after a flipped dispatch.</summary>
    private static void FlipManifold(ref Manifold3D manifold)
    {
        manifold.Normal = manifold.Normal * -1.0f;
        for (int p = 0; p < manifold.Count; p++)
        {
            ContactPoint3D point = manifold[p];
            (point.PositionA, point.PositionB) = (point.PositionB, point.PositionA);
            manifold.SetPoint(p, point);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SyncAwakeBodies()
    {
        for (int slot = 0; slot < _bodies.SlotCapacity; slot++)
        {
            if (!_bodies.IsSlotLive(slot))
            {
                continue;
            }

            BodyId bodyId = BodyId.FromIndex(slot);
            ref BodyState body = ref _bodies.Slot(bodyId);
            if (body.Type == BodyType.Static || (body.Flags & BodyFlags.Awake) == 0)
            {
                continue;
            }

            _shapes.SyncBody(_bodies, _tree, bodyId);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WakePair(BodyId bodyA, BodyId bodyB)
    {
        ref BodyState stateA = ref _bodies.Slot(bodyA);
        ref BodyState stateB = ref _bodies.Slot(bodyB);

        if (stateA.Type == BodyType.Dynamic && (stateA.Flags & BodyFlags.Awake) == 0)
        {
            _bodies.SetAwake(bodyA, true);
        }

        if (stateB.Type == BodyType.Dynamic && (stateB.Flags & BodyFlags.Awake) == 0)
        {
            _bodies.SetAwake(bodyB, true);
        }
    }

    /// <summary>Counts the slices so no awake bullet outruns <see cref="CcdSweepStep"/> in one slice.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private int CcdSliceCount(float dt)
    {
        float furthest = 0.0f;

        for (int slot = 0; slot < _bodies.SlotCapacity; slot++)
        {
            if (!_bodies.IsSlotLive(slot))
            {
                continue;
            }

            BodyId bodyId = BodyId.FromIndex(slot);
            ref readonly BodyState body = ref _bodies.Slot(bodyId);
            if (body.Type != BodyType.Dynamic || (body.Flags & BodyFlags.Awake) == 0 ||
                (body.Flags & BodyFlags.Bullet) == 0)
            {
                continue;
            }

            float travel = body.LinearVelocity.Length() * dt;
            if (travel > furthest)
            {
                furthest = travel;
            }

            float spin = body.AngularVelocity.Length() * BulletRadius(in body) * dt;
            if (spin > furthest)
            {
                furthest = spin;
            }
        }

        if (furthest <= CcdSweepStep)
        {
            return 1;
        }

        int slices = (int)MathF.Ceiling(furthest / CcdSweepStep);
        return Math.Clamp(slices, 1, MaxCcdSubsteps);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float BulletRadius(in BodyState body)
    {
        float radius = 0.0f;
        int current = body.FirstShape;
        while (current != -1)
        {
            ShapeId shapeId = ShapeId.FromIndex(current);
            if (!_shapes.IsValid(shapeId))
            {
                break;
            }

            Aabb3D box = _shapes.GetWorldAabb(shapeId);
            Vec3 extents = box.Extents;
            float halfDiagonal = extents.Length();
            float off = (box.Center - body.Position).Length();
            float far = halfDiagonal + off;
            if (far > radius)
            {
                radius = far;
            }

            current = _shapes.Record(shapeId).NextShape;
        }

        return radius;
    }

    /// <summary>Snapshots the step-open contact pairs for event diffing.</summary>
    private void SnapshotStepPairs()
    {
        if (_stepPairs.Length < _contactCount)
        {
            Array.Resize(ref _stepPairs, _contactCount * 2);
        }

        for (int i = 0; i < _contactCount; i++)
        {
            _stepPairs[i] = new BroadPair(_contacts[i].ShapeA, _contacts[i].ShapeB);
        }

        _stepPairCount = _contactCount;
    }

    /// <summary>Runs the island sleep pass over the contact and joint graph.</summary>
    private void UpdateIslands(float dt)
    {
        var parameters = new SimulationParameters3D(dt, TimeToSleep, LinearSleepTolerance, AngularSleepTolerance);

        ContactPair3D[] rentedContacts = _contactCount > 0
            ? ArrayPool<ContactPair3D>.Shared.Rent(_contactCount)
            : [];
        for (int i = 0; i < _contactCount; i++)
        {
            rentedContacts[i] = new ContactPair3D(_contacts[i].BodyA, _contacts[i].BodyB);
        }

        int jointBudget = _joints.Count;
        JointPair3D[] rentedJoints = jointBudget > 0
            ? ArrayPool<JointPair3D>.Shared.Rent(jointBudget)
            : [];
        int jointPairs = _joints.CopyJointPairs(rentedJoints.AsSpan(0, jointBudget));

        try
        {
            _islands.UpdateSleep(
                _bodies,
                rentedContacts.AsSpan(0, _contactCount),
                rentedJoints.AsSpan(0, jointPairs),
                in parameters);
        }
        finally
        {
            if (rentedContacts.Length > 0)
            {
                ArrayPool<ContactPair3D>.Shared.Return(rentedContacts);
            }

            if (rentedJoints.Length > 0)
            {
                ArrayPool<JointPair3D>.Shared.Return(rentedJoints);
            }
        }
    }

    /// <summary>Diffs the confirmed contact sets into begin/end events.</summary>
    private void PublishContactEvents()
    {
        for (int i = 0; i < _contactCount; i++)
        {
            if (!ContainsPair(_stepPairs, _stepPairCount, _contacts[i].ShapeA, _contacts[i].ShapeB))
            {
                PushEvent(_contacts[i].ShapeA, _contacts[i].ShapeB, began: true);
            }
        }

        for (int j = 0; j < _stepPairCount; j++)
        {
            if (!ContainsPair(_contacts, _contactCount, _stepPairs[j].First, _stepPairs[j].Second))
            {
                PushEvent(_stepPairs[j].First, _stepPairs[j].Second, began: false);
            }
        }
    }

    /// <summary>Whether a shape pair names a contact in the set.</summary>
    private static bool ContainsPair(Contact3D[] set, int count, ShapeId shapeA, ShapeId shapeB)
    {
        for (int i = 0; i < count; i++)
        {
            if (set[i].ShapeA == shapeA && set[i].ShapeB == shapeB)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a shape pair names a pair in the set.</summary>
    private static bool ContainsPair(BroadPair[] set, int count, ShapeId shapeA, ShapeId shapeB)
    {
        for (int i = 0; i < count; i++)
        {
            if (set[i].First == shapeA && set[i].Second == shapeB)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Appends a pair event, growing the buffer on demand.</summary>
    private void PushEvent(ShapeId shapeA, ShapeId shapeB, bool began)
    {
        if (!_shapes.IsValid(shapeA) || !_shapes.IsValid(shapeB))
        {
            return;
        }

        if (_eventCount >= _events.Length)
        {
            Array.Resize(ref _events, _events.Length * 2);
        }

        BodyId bodyA = _shapes.GetBody(shapeA);
        BodyId bodyB = _shapes.GetBody(shapeB);
        _events[_eventCount++] = new ContactEvent(shapeA, shapeB, bodyA, bodyB, began);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, nameof(World));
    }
}
