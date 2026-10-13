namespace Axrone.Physics2D;

using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Construction parameters of a world: gravity and sleep tuning. Guards live
/// in the properties, so an invalid combination is rejected at assignment.
/// </summary>
public readonly struct WorldDef
{
    /// <summary>Creates default parameters: earth-like gravity, sleeping on.</summary>
    public WorldDef()
    {
    }

    /// <summary>Gravitational acceleration; components must be finite.</summary>
    public Vec2 Gravity
    {
        get => field;
        init
        {
            if (float.IsNaN(value.X) || float.IsNaN(value.Y) || float.IsInfinity(value.X) || float.IsInfinity(value.Y))
            {
                ThrowHelper.ThrowInvalidOperation("Gravity components must be finite.");
            }

            field = value;
        }
    } = new Vec2(0.0f, -10.0f);

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

    /// <summary>Quiet seconds before an island sleeps; strictly positive.</summary>
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
    } = 0.25f;

    /// <summary>CCD substep cap per step; at least one.</summary>
    public int MaxCcdSubsteps
    {
        get => field;
        init
        {
            if (value < 1)
            {
                ThrowHelper.ThrowInvalidOperation("Max CCD substeps must be at least one.");
            }

            field = value;
        }
    } = 8;
}

/// <summary>
/// One tracked pair: the two shapes in stable id order, their bodies, the
/// current manifold, and the warm-start cache carried across steps by contact
/// id. Mutable storage owned by <see cref="World"/>; the solver reads and
/// writes it through the world.
/// </summary>
public struct Contact : IEquatable<Contact>
{
    /// <summary>First shape; the smaller id.</summary>
    public ShapeId ShapeA { get; }

    /// <summary>Second shape; the larger id.</summary>
    public ShapeId ShapeB { get; }

    /// <summary>Body owning <see cref="ShapeA"/>.</summary>
    public BodyId BodyA { get; }

    /// <summary>Body owning <see cref="ShapeB"/>.</summary>
    public BodyId BodyB { get; }

    /// <summary>Current manifold.</summary>
    public ContactManifold Manifold { get; internal set; }

    /// <summary>Whether either shape is a trigger-only sensor.</summary>
    public bool IsSensor { get; internal set; }

    /// <summary>Warm-start store carried across steps.</summary>
    public ContactCache Cache { get; internal set; }

    internal Contact(ShapeId shapeA, ShapeId shapeB, BodyId bodyA, BodyId bodyB, in ContactManifold manifold, ContactCache cache, bool isSensor)
    {
        ShapeA = shapeA;
        ShapeB = shapeB;
        BodyA = bodyA;
        BodyB = bodyB;
        Manifold = manifold;
        IsSensor = isSensor;
        Cache = cache;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Contact other) =>
        ShapeA == other.ShapeA && ShapeB == other.ShapeB && Manifold.Equals(other.Manifold);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Contact other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(ShapeA, ShapeB, Manifold);

    /// <summary>Determines whether two contacts track the same pair with the same manifold.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Contact left, Contact right) => left.Equals(right);

    /// <summary>Determines whether two contacts differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Contact left, Contact right) => !left.Equals(right);
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
/// manifolds with filtering and warm-start carry. Contacts persist across
/// steps by shape-pair identity; separated pairs drop out. Disposable: tears
/// down the broadphase tree.
/// </summary>
public sealed class World : IDisposable
{
    private const int InitialContactCapacity = 16;

    private readonly BodyStore _bodies;
    private readonly ShapeStore _shapes;
    private readonly JointStore _joints;
    private readonly DynamicAabbTree2D<ShapeId, SurfaceAreaHeuristicStrategy2D, NullSpatialMetricsSink> _tree;
    private readonly IslandSolver<DefaultSleepPolicy> _islands = new();
    private Contact[] _contacts;
    private Contact[] _previous;
    private int _contactCount;
    private int _previousCount;
    private ContactEvent[] _events;
    private int _eventCount;
    private PairKey[] _lastPairs;
    private PairKey[] _currentPairs;
    private int _lastPairCount;
    private int _disposed;

    /// <summary>Gravity of the world.</summary>
    public Vec2 Gravity { get; set; }

    /// <summary>Velocity sweeps per step.</summary>
    public int VelocityIterations { get; set; }

    /// <summary>Position sweeps per step.</summary>
    public int PositionIterations { get; set; }

    /// <summary>Quiet seconds before an island sleeps.</summary>
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
    /// <remarks>
    /// There is no optional-parameter overload taking <c>default</c>: a
    /// zeroed definition would skip the field initializers (notably
    /// gravity), so the default tuning only comes from the parameterless
    /// constructor above.
    /// </remarks>
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
        _tree = new DynamicAabbTree2D<ShapeId, SurfaceAreaHeuristicStrategy2D, NullSpatialMetricsSink>(definition.BroadphaseOptions);
        _contacts = new Contact[InitialContactCapacity];
        _previous = new Contact[InitialContactCapacity];
        _contactCount = 0;
        _previousCount = 0;
        _events = new ContactEvent[InitialContactCapacity];
        _eventCount = 0;
        _lastPairs = new PairKey[InitialContactCapacity];
        _currentPairs = new PairKey[InitialContactCapacity];
        _lastPairCount = 0;
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
    public IslandSolverMetrics IslandMetrics
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _islands.Metrics;
    }

    /// <summary>Broadphase tree; the world keeps it in sync every step.</summary>
    public DynamicAabbTree2D<ShapeId, SurfaceAreaHeuristicStrategy2D, NullSpatialMetricsSink> Broadphase
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tree;
    }

    /// <summary>Tracked pairs of the last step.</summary>
    public int ContactCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _contactCount;
    }

    /// <summary>Reads the tracked pair at <paramref name="index"/>.</summary>
    public Contact GetContact(int index)
    {
        if ((uint)index >= (uint)_contactCount)
        {
            ThrowHelper.ThrowInvalidOperation("Contact index is out of range.");
        }

        return _contacts[index];
    }

    /// <summary>Pair notices raised by the last step; cleared on the next one.</summary>
    public int ContactEventCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _eventCount;
    }

    /// <summary>Reads the pair notice at <paramref name="index"/>.</summary>
    public ContactEvent GetContactEvent(int index)
    {
        if ((uint)index >= (uint)_eventCount)
        {
            ThrowHelper.ThrowInvalidOperation("Contact event index is out of range.");
        }

        return _events[index];
    }

    /// <summary>
    /// Casts <paramref name="ray"/> against every shape and writes the hits
    /// into <paramref name="hits"/> in deterministic order (distance, then
    /// shape id). Sensors report like solids; the broadphase only prunes, the
    /// narrow phase decides.
    /// </summary>
    /// <param name="ray">The query ray; its direction is unit length by construction.</param>
    /// <param name="maxDistance">Travel limit; must be positive and finite.</param>
    /// <param name="hits">Destination span; excess hits are dropped once it is full.</param>
    /// <returns>Number of hits written to <paramref name="hits"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int Raycast(in Ray2D ray, float maxDistance, Span<RaycastHit2D> hits)
    {
        ThrowIfDisposed();

        if (!(maxDistance > 0.0f) || float.IsNaN(maxDistance) || float.IsInfinity(maxDistance))
        {
            ThrowHelper.ThrowInvalidOperation("Raycast distance must be positive and finite.");
        }

        if (hits.IsEmpty || _tree.Count == 0)
        {
            return 0;
        }

        int budget = _tree.Count;
        RayHit[] rentedCandidates = ArrayPool<RayHit>.Shared.Rent(budget);
        RaycastHit2D[] rentedHits = ArrayPool<RaycastHit2D>.Shared.Rent(budget);
        try
        {
            int candidateCount = _tree.RayCast(in ray, rentedCandidates.AsSpan(0, budget), maxDistance);

            int hitCount = 0;
            for (int i = 0; i < candidateCount; i++)
            {
                ShapeId shapeId = _tree.GetUserData(rentedCandidates[i].ItemId);
                if (TryRaycastShape(shapeId, in ray, maxDistance, out RaycastHit2D hit))
                {
                    rentedHits[hitCount++] = hit;
                }
            }

            SortHits(rentedHits.AsSpan(0, hitCount));

            int written = Math.Min(hitCount, hits.Length);
            rentedHits.AsSpan(0, written).CopyTo(hits);
            return written;
        }
        finally
        {
            ArrayPool<RayHit>.Shared.Return(rentedCandidates);
            ArrayPool<RaycastHit2D>.Shared.Return(rentedHits);
        }
    }

    /// <summary>
    /// Collects the shapes whose bounds overlap <paramref name="box"/> into
    /// <paramref name="results"/> in deterministic shape-id order. Sensors
    /// report like solids.
    /// </summary>
    /// <param name="box">The query bounds.</param>
    /// <param name="results">Destination span; excess shapes are dropped once it is full.</param>
    /// <returns>Number of shapes written to <paramref name="results"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int OverlapAabb(in Aabb2D box, Span<ShapeId> results)
    {
        ThrowIfDisposed();

        if (results.IsEmpty || _tree.Count == 0)
        {
            return 0;
        }

        int budget = _tree.Count;
        SpatialItemId[] rented = ArrayPool<SpatialItemId>.Shared.Rent(budget);
        ShapeId[] staged = ArrayPool<ShapeId>.Shared.Rent(budget);
        try
        {
            int candidateCount = _tree.QueryOverlaps(in box, rented.AsSpan(0, budget));

            for (int i = 0; i < candidateCount; i++)
            {
                staged[i] = _tree.GetUserData(rented[i]);
            }

            Span<ShapeId> found = staged.AsSpan(0, candidateCount);
            for (int i = 1; i < found.Length; i++)
            {
                ShapeId key = found[i];
                int j = i - 1;
                while (j >= 0 && found[j].Value > key.Value)
                {
                    found[j + 1] = found[j];
                    j--;
                }

                found[j + 1] = key;
            }

            int written = Math.Min(candidateCount, results.Length);
            found[..written].CopyTo(results);
            return written;
        }
        finally
        {
            ArrayPool<SpatialItemId>.Shared.Return(rented);
            ArrayPool<ShapeId>.Shared.Return(staged);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool TryRaycastShape(ShapeId shapeId, in Ray2D ray, float maxDistance, out RaycastHit2D hit)
    {
        ref ShapeRecord record = ref _shapes.Slot(shapeId);

        switch (record.Kind)
        {
            case ColliderKind.Circle:
                if (ShapeRaycaster.RaycastCircle(
                    in ray, record.WorldCircle.Center, record.WorldCircle.Radius, maxDistance,
                    out float circleDistance, out Vec2 circleNormal, out Vec2 circlePoint))
                {
                    hit = new RaycastHit2D(shapeId, record.Body, circlePoint, circleNormal, circleDistance);
                    return true;
                }

                break;

            case ColliderKind.Capsule:
                if (ShapeRaycaster.RaycastCapsule(
                    in ray, record.WorldCapsule.P0, record.WorldCapsule.P1, record.WorldCapsule.Radius, maxDistance,
                    out float capsuleDistance, out Vec2 capsuleNormal, out Vec2 capsulePoint))
                {
                    hit = new RaycastHit2D(shapeId, record.Body, capsulePoint, capsuleNormal, capsuleDistance);
                    return true;
                }

                break;

            case ColliderKind.Edge:
                if (ShapeRaycaster.RaycastEdge(
                    in ray, record.WorldEdge.P0, record.WorldEdge.P1, maxDistance,
                    out float edgeDistance, out Vec2 edgeNormal, out Vec2 edgePoint))
                {
                    hit = new RaycastHit2D(shapeId, record.Body, edgePoint, edgeNormal, edgeDistance);
                    return true;
                }

                break;

            default:
                if (ShapeRaycaster.RaycastPolygon(
                    in ray, record.WorldPolygon.Vertices, record.WorldPolygon.Normals, maxDistance,
                    out float polygonDistance, out Vec2 polygonNormal, out Vec2 polygonPoint))
                {
                    hit = new RaycastHit2D(shapeId, record.Body, polygonPoint, polygonNormal, polygonDistance);
                    return true;
                }

                break;
        }

        hit = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void SortHits(Span<RaycastHit2D> hits)
    {
        for (int i = 1; i < hits.Length; i++)
        {
            RaycastHit2D key = hits[i];
            int j = i - 1;
            while (j >= 0 && (hits[j].Distance > key.Distance ||
                (hits[j].Distance == key.Distance && hits[j].Shape.Value > key.Shape.Value)))
            {
                hits[j + 1] = hits[j];
                j--;
            }

            hits[j + 1] = key;
        }
    }

    /// <summary>
    /// Advances the world by <paramref name="dt"/>: fast bullets transparently
    /// split the step into sweep-bounded slices so no slice moves a bullet
    /// past a thin wall; every slice integrates velocities, syncs the
    /// broadphase, collects pairs into manifolds with warm-start carry,
    /// solves velocities, integrates positions, re-syncs, and solves
    /// positions. New pairs wake sleeping dynamics. Sleep updates once per
    /// step from the final velocities.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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

        for (int slice = 0; slice < slices; slice++)
        {
            StepSlice(h);
        }

        BodyStoreAdapter adapter = new(_bodies);
        SimulationParameters parameters = new(dt, TimeToSleep, LinearSleepTolerance, AngularSleepTolerance);

        ContactPair[] rentedContacts = _contactCount > 0
            ? ArrayPool<ContactPair>.Shared.Rent(_contactCount)
            : [];
        for (int i = 0; i < _contactCount; i++)
        {
            rentedContacts[i] = new ContactPair(_contacts[i].BodyA, _contacts[i].BodyB);
        }

        int jointBudget = _joints.Count;
        JointPair[] rentedJoints = jointBudget > 0
            ? ArrayPool<JointPair>.Shared.Rent(jointBudget)
            : [];
        int jointPairs = _joints.CopyJointPairs(rentedJoints.AsSpan(0, jointBudget));

        try
        {
            _islands.UpdateSleep(
                ref adapter,
                rentedContacts.AsSpan(0, _contactCount),
                rentedJoints.AsSpan(0, jointPairs),
                in parameters);
        }
        finally
        {
            if (rentedContacts.Length > 0)
            {
                ArrayPool<ContactPair>.Shared.Return(rentedContacts);
            }

            if (rentedJoints.Length > 0)
            {
                ArrayPool<JointPair>.Shared.Return(rentedJoints);
            }
        }

        PublishContactEvents();
    }

    /// <summary>
    /// Counts the slices so no awake bullet moves past
    /// <see cref="CcdSweepStep"/> in one slice. Translation uses the body
    /// velocity, spin uses angular velocity times the furthest shape extent,
    /// and two opposing bullets use their closing speed, so bullet-vs-bullet
    /// head-ons split like bullet-vs-wall.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private int CcdSliceCount(float dt)
    {
        float furthest = 0.0f;
        float fastest = 0.0f;
        float second = 0.0f;

        for (int slot = 0; slot < _bodies.SlotCapacity; slot++)
        {
            if (!_bodies.IsSlotLive(slot))
            {
                continue;
            }

            BodyId bodyId = new((uint)slot + 1u);
            ref readonly BodyState body = ref _bodies.Slot(bodyId);
            if (body.Type != BodyType.Dynamic || (body.Flags & BodyFlags.Awake) == 0 ||
                (body.Flags & BodyFlags.Bullet) == 0)
            {
                continue;
            }

            float travel = MathF.Sqrt(body.LinearVelocity.LengthSquared()) * dt;
            if (travel > furthest)
            {
                furthest = travel;
            }

            if (travel >= fastest)
            {
                second = fastest;
                fastest = travel;
            }
            else if (travel > second)
            {
                second = travel;
            }

            float spin = MathF.Abs(body.AngularVelocity) * BulletRadius(in body) * dt;
            if (spin > furthest)
            {
                furthest = spin;
            }
        }

        float closing = fastest + second;
        if (closing > furthest)
        {
            furthest = closing;
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
            ShapeId shapeId = new((uint)current + 1u);
            if (!_shapes.IsValid(shapeId))
            {
                break;
            }

            Aabb2D box = _shapes.GetWorldAabb(shapeId);
            Vec2 extents = box.Extents;
            float halfDiagonal = MathF.Sqrt(extents.LengthSquared());
            float offSq = (box.Center - body.Position).LengthSquared();
            float far = halfDiagonal + MathF.Sqrt(offSq);
            if (far > radius)
            {
                radius = far;
            }

            current = _shapes.Slot(shapeId).NextShape;
        }

        return radius;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void StepSlice(float h)
    {
        _bodies.IntegrateVelocities(h, Gravity);
        SyncAwakeBodies();

        Contact[] built = _previous;
        _previous = _contacts;
        _previousCount = _contactCount;
        _contacts = built;
        _contactCount = 0;

        var collector = new PairCollector(this);
        World self = this;
        _tree.FindPairs(ref collector, ref self);

        for (int i = 0; i < _contactCount; i++)
        {
            ContactCache carried = default;
            bool matched = false;
            for (int j = 0; j < _previousCount; j++)
            {
                if (_previous[j].ShapeA == _contacts[i].ShapeA && _previous[j].ShapeB == _contacts[i].ShapeB)
                {
                    carried = _previous[j].Cache;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                WakePair(_contacts[i].BodyA, _contacts[i].BodyB);
            }

            Contact updated = _contacts[i];
            ContactManifold manifold = updated.Manifold;
            carried.WarmStart(ref manifold);
            updated.Manifold = manifold;
            carried.Store(in manifold);
            updated.Cache = carried;
            _contacts[i] = updated;
        }

        ContactSolver.InitVelocityConstraints(_bodies, _shapes, _contacts.AsSpan(0, _contactCount));
        JointSolver.InitVelocity(_bodies, _joints, LinearSleepTolerance, h);

        for (int iteration = 0; iteration < VelocityIterations; iteration++)
        {
            ContactSolver.SolveVelocityPass(_bodies, _shapes, _contacts.AsSpan(0, _contactCount), 1);
            JointSolver.SolveVelocityPass(_bodies, _joints, 1);
        }

        _bodies.IntegratePositions(h);
        SyncAwakeBodies();

        for (int iteration = 0; iteration < PositionIterations; iteration++)
        {
            ContactSolver.SolvePositionPass(_bodies, _shapes, _contacts.AsSpan(0, _contactCount));
            JointSolver.SolvePositionPass(_bodies, _joints, 1);
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

            BodyId bodyId = new((uint)slot + 1u);
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

    /// <summary>
    /// Destroys a body, its shapes and its joints, pulling each shape from
    /// the broadphase and dropping tracked pairs that named them. Gears
    /// touching the body through either geared joint die with it.
    /// Destroying bodies through the raw store orphans tree items and joint
    /// links; prefer this path.
    /// </summary>
    public void DestroyBody(BodyId bodyId)
    {
        ThrowIfDisposed();

        _joints.DestroyJointsOfBody(bodyId);

        ref BodyState body = ref _bodies.Slot(bodyId);
        while (body.FirstShape != -1)
        {
            ShapeId shapeId = new((uint)body.FirstShape + 1u);
            _shapes.Detach(_bodies, _tree, shapeId);
            body = ref _bodies.Slot(bodyId);
        }

        for (int i = _contactCount - 1; i >= 0; i--)
        {
            if (_contacts[i].BodyA == bodyId || _contacts[i].BodyB == bodyId)
            {
                _contacts[i] = _contacts[--_contactCount];
            }
        }

        for (int i = _previousCount - 1; i >= 0; i--)
        {
            if (_previous[i].BodyA == bodyId || _previous[i].BodyB == bodyId)
            {
                _previous[i] = _previous[--_previousCount];
            }
        }

        // _lastPairs is intentionally kept: the next PublishContactEvents
        // diffs it against the rebuilt contacts and emits Began:false for
        // the vanished pairs, so destroy is observable as end events.

        _bodies.Destroy(bodyId);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _tree.Dispose();
            _islands.Dispose();
            _contacts = [];
            _previous = [];
            _contactCount = 0;
            _previousCount = 0;
            _events = [];
            _eventCount = 0;
            _lastPairs = [];
            _currentPairs = [];
            _lastPairCount = 0;
        }

        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed != 0)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(World));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddContact(ShapeId shapeA, ShapeId shapeB, BodyId bodyA, BodyId bodyB, in ContactManifold manifold, bool isSensor)
    {
        if (_contactCount >= _contacts.Length)
        {
            Array.Resize(ref _contacts, _contacts.Length * 2);
        }

        _contacts[_contactCount++] = new Contact(shapeA, shapeB, bodyA, bodyB, in manifold, default, isSensor);
    }

    /// <summary>Pair identity snapshot for begin/end diffing; shape order matches contacts.</summary>
    private readonly record struct PairKey(ShapeId ShapeA, ShapeId ShapeB, BodyId BodyA, BodyId BodyB);

    /// <summary>
    /// Diffs this step's pairs against the last published set: unseen pairs
    /// raise begin notices, vanished pairs raise end notices, then the
    /// snapshot becomes the new baseline. Sensor pairs report like any other.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void PublishContactEvents()
    {
        if (_currentPairs.Length < _contactCount)
        {
            Array.Resize(ref _currentPairs, Math.Max(_contactCount, 1));
        }

        for (int i = 0; i < _contactCount; i++)
        {
            _currentPairs[i] = new PairKey(_contacts[i].ShapeA, _contacts[i].ShapeB, _contacts[i].BodyA, _contacts[i].BodyB);
        }

        for (int i = 0; i < _contactCount; i++)
        {
            bool known = false;
            for (int j = 0; j < _lastPairCount; j++)
            {
                if (_lastPairs[j].ShapeA == _currentPairs[i].ShapeA && _lastPairs[j].ShapeB == _currentPairs[i].ShapeB)
                {
                    known = true;
                    break;
                }
            }

            if (!known)
            {
                EmitEvent(_currentPairs[i], began: true);
            }
        }

        for (int j = 0; j < _lastPairCount; j++)
        {
            bool alive = false;
            for (int i = 0; i < _contactCount; i++)
            {
                if (_currentPairs[i].ShapeA == _lastPairs[j].ShapeA && _currentPairs[i].ShapeB == _lastPairs[j].ShapeB)
                {
                    alive = true;
                    break;
                }
            }

            if (!alive)
            {
                EmitEvent(_lastPairs[j], began: false);
            }
        }

        PairKey[] swap = _lastPairs;
        _lastPairs = _currentPairs;
        _currentPairs = swap;
        _lastPairCount = _contactCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitEvent(in PairKey pair, bool began)
    {
        if (_eventCount >= _events.Length)
        {
            Array.Resize(ref _events, Math.Max(_events.Length * 2, 1));
        }

        _events[_eventCount++] = new ContactEvent(pair.ShapeA, pair.ShapeB, pair.BodyA, pair.BodyB, began);
    }

    private struct PairCollector : IPairVisitor<ShapeId, World>
    {
        private readonly World _world;

        public PairCollector(World world)
        {
            _world = world;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public bool OnPair(SpatialItemId idA, ShapeId dataA, SpatialItemId idB, ShapeId dataB, ref World context)
        {
            ShapeId shapeA = dataA.Value < dataB.Value ? dataA : dataB;
            ShapeId shapeB = dataA.Value < dataB.Value ? dataB : dataA;

            ref ShapeRecord recordA = ref _world._shapes.Slot(shapeA);
            ref ShapeRecord recordB = ref _world._shapes.Slot(shapeB);

            if (!CollisionFilter.ShouldCollide(in recordA.Filter, in recordB.Filter))
            {
                return true;
            }

            BodyId bodyA = recordA.Body;
            BodyId bodyB = recordB.Body;
            ref readonly BodyState stateA = ref _world._bodies.Slot(bodyA);
            ref readonly BodyState stateB = ref _world._bodies.Slot(bodyB);

            if (stateA.Type == BodyType.Static && stateB.Type == BodyType.Static)
            {
                return true;
            }

            bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
            bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
            if (!awakeA && !awakeB)
            {
                return true;
            }

            if (!_world.EvaluatePair(shapeA, shapeB, out ContactManifold manifold))
            {
                return true;
            }

            _world.AddContact(shapeA, shapeB, bodyA, bodyB, in manifold, recordA.IsSensor || recordB.IsSensor);
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool EvaluatePair(ShapeId shapeA, ShapeId shapeB, out ContactManifold manifold)
    {
        ref readonly ShapeRecord recordA = ref _shapes.Slot(shapeA);
        ref readonly ShapeRecord recordB = ref _shapes.Slot(shapeB);

        ColliderKind kindA = recordA.Kind;
        ColliderKind kindB = recordB.Kind;

        if (kindA == ColliderKind.Circle && kindB == ColliderKind.Circle)
        {
            return Narrowphase2D.EvaluateCircleCircle(in recordA.WorldCircle, in recordB.WorldCircle, out manifold);
        }

        if (kindA == ColliderKind.Circle && kindB == ColliderKind.Polygon)
        {
            return Narrowphase2D.EvaluateCirclePolygon(in recordA.WorldCircle, in recordB.WorldPolygon, out manifold);
        }

        if (kindA == ColliderKind.Polygon && kindB == ColliderKind.Circle)
        {
            return Narrowphase2D.EvaluatePolygonCircle(in recordA.WorldPolygon, in recordB.WorldCircle, out manifold);
        }

        if (kindA == ColliderKind.Polygon && kindB == ColliderKind.Polygon)
        {
            return Narrowphase2D.EvaluatePolygonPolygon(in recordA.WorldPolygon, in recordB.WorldPolygon, out manifold);
        }

        if (kindA == ColliderKind.Capsule && kindB == ColliderKind.Circle)
        {
            return Narrowphase2D.EvaluateCapsuleCircle(in recordA.WorldCapsule, in recordB.WorldCircle, out manifold);
        }

        if (kindA == ColliderKind.Circle && kindB == ColliderKind.Capsule)
        {
            bool hit = Narrowphase2D.EvaluateCapsuleCircle(in recordB.WorldCapsule, in recordA.WorldCircle, out ContactManifold inner);
            manifold = hit ? inner.Flipped() : ContactManifold.Empty;
            return hit;
        }

        if (kindA == ColliderKind.Capsule && kindB == ColliderKind.Polygon)
        {
            return Narrowphase2D.EvaluateCapsulePolygon(in recordA.WorldCapsule, in recordB.WorldPolygon, out manifold);
        }

        if (kindA == ColliderKind.Polygon && kindB == ColliderKind.Capsule)
        {
            bool hit = Narrowphase2D.EvaluateCapsulePolygon(in recordB.WorldCapsule, in recordA.WorldPolygon, out ContactManifold inner);
            manifold = hit ? inner.Flipped() : ContactManifold.Empty;
            return hit;
        }

        if (kindA == ColliderKind.Circle && kindB == ColliderKind.Edge)
        {
            return Narrowphase2D.EvaluateCircleEdge(in recordA.WorldCircle, in recordB.WorldEdge, out manifold);
        }

        if (kindA == ColliderKind.Edge && kindB == ColliderKind.Circle)
        {
            return Narrowphase2D.EvaluateEdgeCircle(in recordA.WorldEdge, in recordB.WorldCircle, out manifold);
        }

        if (kindA == ColliderKind.Capsule && kindB == ColliderKind.Edge)
        {
            return Narrowphase2D.EvaluateCapsuleEdge(in recordA.WorldCapsule, in recordB.WorldEdge, out manifold);
        }

        if (kindA == ColliderKind.Edge && kindB == ColliderKind.Capsule)
        {
            return Narrowphase2D.EvaluateEdgeCapsule(in recordA.WorldEdge, in recordB.WorldCapsule, out manifold);
        }

        if (kindA == ColliderKind.Polygon && kindB == ColliderKind.Edge)
        {
            return Narrowphase2D.EvaluatePolygonEdge(in recordA.WorldPolygon, in recordB.WorldEdge, out manifold);
        }

        if (kindA == ColliderKind.Edge && kindB == ColliderKind.Polygon)
        {
            return Narrowphase2D.EvaluateEdgePolygon(in recordA.WorldEdge, in recordB.WorldPolygon, out manifold);
        }

        if (kindA == ColliderKind.Edge && kindB == ColliderKind.Edge)
        {
            return Narrowphase2D.EvaluateEdgeEdge(in recordA.WorldEdge, in recordB.WorldEdge, out manifold);
        }

        return Narrowphase2D.EvaluateCapsuleCapsule(in recordA.WorldCapsule, in recordB.WorldCapsule, out manifold);
    }
}
