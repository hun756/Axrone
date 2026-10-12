namespace Axrone.Physics2D;

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

    /// <summary>Warm-start store carried across steps.</summary>
    public ContactCache Cache { get; internal set; }

    internal Contact(ShapeId shapeA, ShapeId shapeB, BodyId bodyA, BodyId bodyB, in ContactManifold manifold, ContactCache cache)
    {
        ShapeA = shapeA;
        ShapeB = shapeB;
        BodyA = bodyA;
        BodyB = bodyB;
        Manifold = manifold;
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
    private readonly DynamicAabbTree2D<ShapeId, SurfaceAreaHeuristicStrategy2D, NullSpatialMetricsSink> _tree;
    private Contact[] _contacts;
    private Contact[] _previous;
    private int _contactCount;
    private int _previousCount;
    private int _disposed;

    /// <summary>Gravity of the world.</summary>
    public Vec2 Gravity { get; set; }

    /// <summary>Velocity sweeps per step.</summary>
    public int VelocityIterations { get; set; }

    /// <summary>Position sweeps per step.</summary>
    public int PositionIterations { get; set; }

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
        _bodies = new BodyStore();
        _shapes = new ShapeStore();
        _tree = new DynamicAabbTree2D<ShapeId, SurfaceAreaHeuristicStrategy2D, NullSpatialMetricsSink>(definition.BroadphaseOptions);
        _contacts = new Contact[InitialContactCapacity];
        _previous = new Contact[InitialContactCapacity];
        _contactCount = 0;
        _previousCount = 0;
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

    /// <summary>
    /// Advances the world by <paramref name="dt"/>: integrates velocities,
    /// syncs the broadphase, collects pairs into manifolds with warm-start
    /// carry, solves velocities, integrates positions, re-syncs, and solves
    /// positions. New pairs wake sleeping dynamics.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Step(float dt)
    {
        ThrowIfDisposed();

        _bodies.IntegrateVelocities(dt, Gravity);
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

        ContactSolver.SolveVelocity(_bodies, _shapes, _contacts.AsSpan(0, _contactCount), VelocityIterations);

        _bodies.IntegratePositions(dt);
        SyncAwakeBodies();

        ContactSolver.SolvePositions(_bodies, _shapes, _contacts.AsSpan(0, _contactCount), PositionIterations);
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
    /// Destroys a body and every shape attached to it, pulling each shape
    /// from the broadphase and dropping tracked pairs that named them.
    /// Destroying bodies through the raw store orphans tree items; prefer
    /// this path.
    /// </summary>
    public void DestroyBody(BodyId bodyId)
    {
        ThrowIfDisposed();

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

        _bodies.Destroy(bodyId);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _tree.Dispose();
            _contacts = [];
            _previous = [];
            _contactCount = 0;
            _previousCount = 0;
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
    private void AddContact(ShapeId shapeA, ShapeId shapeB, BodyId bodyA, BodyId bodyB, in ContactManifold manifold)
    {
        if (_contactCount >= _contacts.Length)
        {
            Array.Resize(ref _contacts, _contacts.Length * 2);
        }

        _contacts[_contactCount++] = new Contact(shapeA, shapeB, bodyA, bodyB, in manifold, default);
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

            _world.AddContact(shapeA, shapeB, bodyA, bodyB, in manifold);
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

        return Narrowphase2D.EvaluateCapsuleCapsule(in recordA.WorldCapsule, in recordB.WorldCapsule, out manifold);
    }
}
