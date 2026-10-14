namespace Axrone.Physics3D;

/// <summary>Stored collider kind; selects the active geometry of a shape record.</summary>
public enum ColliderKind : byte
{
    /// <summary>Ball collider.</summary>
    Sphere = 0,

    /// <summary>Axis-aligned box collider.</summary>
    Box = 1,
}

/// <summary>
/// One attached collider: the owning body, the local frame it sits in, the
/// local geometry (exactly one variant is live, selected by
/// <see cref="Kind"/>), density/filter/material, the cached world frame plus
/// world bounds (refreshed by sync, never allocated after attach), and the
/// broadphase item. Mutable storage owned by <see cref="ShapeStore"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct ShapeRecord
{
    /// <summary>Whether the slot holds a live shape.</summary>
    public bool Live;

    /// <summary>Active geometry variant.</summary>
    public ColliderKind Kind;

    /// <summary>Owning body.</summary>
    public BodyId Body;

    /// <summary>Shape frame inside the body frame.</summary>
    public RigidTransform3D LocalFrame;

    /// <summary>Mass per unit volume; always positive.</summary>
    public float Density;

    /// <summary>Collision filtering.</summary>
    public CollisionFilter Filter;

    /// <summary>Contact response.</summary>
    public PhysicsMaterial Material;

    /// <summary>Trigger-only collider: overlaps report but never push.</summary>
    public bool IsSensor;

    /// <summary>Local ball; live when <see cref="Kind"/> is <see cref="ColliderKind.Sphere"/>.</summary>
    public Sphere3 LocalSphere;

    /// <summary>Local box; live when <see cref="Kind"/> is <see cref="ColliderKind.Box"/>.</summary>
    public Box3 LocalBox;

    /// <summary>World frame; refreshed on sync.</summary>
    public RigidTransform3D WorldFrame;

    /// <summary>World centroid; refreshed on sync.</summary>
    public Vec3 WorldCentroid;

    /// <summary>World bounds; refreshed on sync.</summary>
    public Aabb3D WorldAabb;

    /// <summary>Broadphase item; invalid until inserted.</summary>
    public SpatialItemId TreeItem;

    /// <summary>Next shape of the owning body; -1 at the tail.</summary>
    public int NextShape;
}

/// <summary>
/// Dense slot store of attached colliders: local-frame geometry per body,
/// cached world frames plus bounds (no allocation after attach), automatic
/// mass accumulation into the owning body with center-of-mass tracking, and
/// broadphase insert/update/remove wired to the 3D tree. Identities are slot
/// plus one; freed slots recycle.
/// </summary>
public sealed class ShapeStore
{
    private const int InitialCapacity = 16;

    private ShapeRecord[] _shapes;
    private int[] _freeStack;
    private int _freeCount;
    private int _highWater;
    private int _count;
    private int _capacity;

    /// <summary>Creates a store with room for sixteen shapes.</summary>
    public ShapeStore()
    {
        _capacity = InitialCapacity;
        _shapes = new ShapeRecord[_capacity];
        _freeStack = new int[_capacity];
        _freeCount = 0;
        _highWater = 0;
        _count = 0;
    }

    /// <summary>Live shapes.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    /// <summary>Attaches a ball to <paramref name="bodyId"/> and inserts it into <paramref name="tree"/>.</summary>
    public ShapeId AttachSphere<TStrategy, TMetrics>(
        BodyStore bodies,
        DynamicAabbTree<ShapeId, TStrategy, TMetrics> tree,
        BodyId bodyId,
        Vec3 center,
        float radius,
        float density = 1.0f,
        CollisionFilter filter = default,
        PhysicsMaterial material = default,
        Vec3 localOffset = default,
        Quat localRotation = default,
        bool isSensor = false)
        where TStrategy : struct, ISpatialPartitionStrategy
        where TMetrics : struct, ISpatialMetricsSink
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(tree);
        var local = new Sphere3(center, radius);
        int slot = InsertRecord(bodies, bodyId, ColliderKind.Sphere, density, filter, material, localOffset, localRotation, isSensor);
        _shapes[slot].LocalSphere = local;
        FinishAttach(bodies, tree, slot);
        return ShapeId.FromIndex(slot);
    }

    /// <summary>Attaches an axis-aligned box to <paramref name="bodyId"/> and inserts it into <paramref name="tree"/>.</summary>
    public ShapeId AttachBox<TStrategy, TMetrics>(
        BodyStore bodies,
        DynamicAabbTree<ShapeId, TStrategy, TMetrics> tree,
        BodyId bodyId,
        Vec3 center,
        Vec3 halfExtents,
        float density = 1.0f,
        CollisionFilter filter = default,
        PhysicsMaterial material = default,
        Vec3 localOffset = default,
        Quat localRotation = default,
        bool isSensor = false)
        where TStrategy : struct, ISpatialPartitionStrategy
        where TMetrics : struct, ISpatialMetricsSink
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(tree);
        var local = new Box3(center, halfExtents);
        int slot = InsertRecord(bodies, bodyId, ColliderKind.Box, density, filter, material, localOffset, localRotation, isSensor);
        _shapes[slot].LocalBox = local;
        FinishAttach(bodies, tree, slot);
        return ShapeId.FromIndex(slot);
    }

    /// <summary>Drops a shape, unlinks it from its body, pulls it from the tree and re-sums the body mass.</summary>
    public void Detach<TStrategy, TMetrics>(
        BodyStore bodies,
        DynamicAabbTree<ShapeId, TStrategy, TMetrics> tree,
        ShapeId id)
        where TStrategy : struct, ISpatialPartitionStrategy
        where TMetrics : struct, ISpatialMetricsSink
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(tree);
        int slot = Validate(id);
        ref ShapeRecord shape = ref _shapes[slot];
        BodyId bodyId = shape.Body;

        tree.Remove(shape.TreeItem);

        ref BodyState body = ref bodies.Slot(bodyId);
        int previous = -1;
        int current = body.FirstShape;
        while (current != -1)
        {
            if (current == slot)
            {
                if (previous == -1)
                {
                    body.FirstShape = shape.NextShape;
                }
                else
                {
                    _shapes[previous].NextShape = shape.NextShape;
                }

                body.ShapeCount--;
                break;
            }

            previous = current;
            current = _shapes[current].NextShape;
        }

        shape = default;
        _freeStack[_freeCount++] = slot;
        _count--;

        RecalculateMass(bodies, bodyId);
    }

    /// <summary>Refreshes the world frame, bounds and tree placement of one shape.</summary>
    public void SyncShape<TStrategy, TMetrics>(
        BodyStore bodies,
        DynamicAabbTree<ShapeId, TStrategy, TMetrics> tree,
        ShapeId id)
        where TStrategy : struct, ISpatialPartitionStrategy
        where TMetrics : struct, ISpatialMetricsSink
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(tree);
        int slot = Validate(id);
        RefreshWorld(bodies, slot);
        ref ShapeRecord shape = ref _shapes[slot];
        tree.Update(shape.TreeItem, in shape.WorldAabb);
    }

    /// <summary>Refreshes every shape of a body; call after the body moved.</summary>
    public void SyncBody<TStrategy, TMetrics>(
        BodyStore bodies,
        DynamicAabbTree<ShapeId, TStrategy, TMetrics> tree,
        BodyId bodyId)
        where TStrategy : struct, ISpatialPartitionStrategy
        where TMetrics : struct, ISpatialMetricsSink
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(tree);
        ref BodyState body = ref bodies.Slot(bodyId);
        int current = body.FirstShape;
        while (current != -1)
        {
            RefreshWorld(bodies, current);
            ref ShapeRecord shape = ref _shapes[current];
            tree.Update(shape.TreeItem, in shape.WorldAabb);
            current = shape.NextShape;
        }
    }

    /// <summary>Whether <paramref name="id"/> names a live shape.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid(ShapeId id)
    {
        int slot = id.ToIndex();
        return id.IsValid && slot >= 0 && slot < _capacity && _shapes[slot].Live;
    }

    /// <summary>Reads the owning body of a shape.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BodyId GetBody(ShapeId id) => _shapes[Validate(id)].Body;

    /// <summary>Reads the collider kind of a shape.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColliderKind GetKind(ShapeId id) => _shapes[Validate(id)].Kind;

    /// <summary>Reads the world frame of a shape.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RigidTransform3D GetWorldFrame(ShapeId id) => _shapes[Validate(id)].WorldFrame;

    /// <summary>Reads the world ball of a sphere shape.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sphere3 GetWorldSphere(ShapeId id)
    {
        ref readonly ShapeRecord shape = ref _shapes[Validate(id)];
        return new Sphere3(shape.WorldFrame.TransformPoint(shape.LocalSphere.Center), shape.LocalSphere.Radius);
    }

    /// <summary>Reads the world box of a box shape: center in world, extents and orientation via <see cref="GetWorldFrame"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Box3 GetWorldBox(ShapeId id)
    {
        ref readonly ShapeRecord shape = ref _shapes[Validate(id)];
        return new Box3(shape.WorldFrame.TransformPoint(shape.LocalBox.Center), shape.LocalBox.HalfExtents);
    }

    /// <summary>Reads the world bounds of a shape.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Aabb3D GetWorldAabb(ShapeId id) => _shapes[Validate(id)].WorldAabb;

    /// <summary>Whether the shape is a trigger-only sensor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSensor(ShapeId id) => _shapes[Validate(id)].IsSensor;

    /// <summary>Marks a shape as a trigger-only sensor or a solid collider.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetSensor(ShapeId id, bool isSensor)
    {
        _shapes[Validate(id)].IsSensor = isSensor;
    }

    /// <summary>Internal record access for the world and the solver; validated.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref ShapeRecord Record(ShapeId id) => ref _shapes[Validate(id)];

    private void FinishAttach<TStrategy, TMetrics>(
        BodyStore bodies,
        DynamicAabbTree<ShapeId, TStrategy, TMetrics> tree,
        int slot)
        where TStrategy : struct, ISpatialPartitionStrategy
        where TMetrics : struct, ISpatialMetricsSink
    {
        RefreshWorld(bodies, slot);
        ref ShapeRecord shape = ref _shapes[slot];
        shape.TreeItem = tree.Insert(in shape.WorldAabb, ShapeId.FromIndex(slot));

        ref BodyState body = ref bodies.Slot(shape.Body);
        shape.NextShape = body.FirstShape;
        body.FirstShape = slot;
        body.ShapeCount++;
        _count++;

        RecalculateMass(bodies, shape.Body);
    }

    private int InsertRecord(
        BodyStore bodies,
        BodyId bodyId,
        ColliderKind kind,
        float density,
        CollisionFilter filter,
        PhysicsMaterial material,
        Vec3 localOffset,
        Quat localRotation,
        bool isSensor)
    {
        bodies.Slot(bodyId);

        if (!(density > 0.0f) || float.IsNaN(density))
        {
            ThrowHelper.ThrowInvalidOperation("Density must be positive.");
        }

        if (filter.Equals(default))
        {
            filter = CollisionFilter.Default;
        }

        if (material.Equals(default))
        {
            material = PhysicsMaterial.Default;
        }

        if (localRotation.Equals(default(Quat)))
        {
            localRotation = Quat.Identity;
        }

        int slot = AllocateSlot();
        ref ShapeRecord shape = ref _shapes[slot];

        shape = default;
        shape.Live = true;
        shape.Kind = kind;
        shape.Body = bodyId;
        shape.LocalFrame = new RigidTransform3D(localOffset, localRotation);
        shape.Density = density;
        shape.Filter = filter;
        shape.Material = material;
        shape.IsSensor = isSensor;
        shape.TreeItem = SpatialItemId.Invalid;
        shape.NextShape = -1;
        return slot;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void RefreshWorld(BodyStore bodies, int slot)
    {
        ref ShapeRecord shape = ref _shapes[slot];
        ref readonly BodyState body = ref bodies.Slot(shape.Body);
        var bodyFrame = new RigidTransform3D(body.Position, body.Rotation);
        RigidTransform3D world = RigidTransform3D.Concat(bodyFrame, shape.LocalFrame);
        shape.WorldFrame = world;

        if (shape.Kind == ColliderKind.Sphere)
        {
            shape.WorldCentroid = world.TransformPoint(shape.LocalSphere.Center);
            shape.WorldAabb = Sphere3.ComputeBounds(new Sphere3(shape.WorldCentroid, shape.LocalSphere.Radius));
            return;
        }

        Vec3 center = world.TransformPoint(shape.LocalBox.Center);
        shape.WorldCentroid = center;
        shape.WorldAabb = RotatedBoxBounds(center, world.Rotation, shape.LocalBox.HalfExtents);
    }

    /// <summary>Bounds of a box rotated by <paramref name="rotation"/>: projects each half-axis and sums the absolute spans.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static Aabb3D RotatedBoxBounds(Vec3 center, Quat rotation, Vec3 halfExtents)
    {
        Vec3 ax = rotation * new Vec3(halfExtents.X, 0.0f, 0.0f);
        Vec3 ay = rotation * new Vec3(0.0f, halfExtents.Y, 0.0f);
        Vec3 az = rotation * new Vec3(0.0f, 0.0f, halfExtents.Z);
        var span = new Vec3(
            MathF.Abs(ax.X) + MathF.Abs(ay.X) + MathF.Abs(az.X),
            MathF.Abs(ax.Y) + MathF.Abs(ay.Y) + MathF.Abs(az.Y),
            MathF.Abs(ax.Z) + MathF.Abs(ay.Z) + MathF.Abs(az.Z));
        return Aabb3D.FromCenterExtents(center, span);
    }

    /// <summary>
    /// Re-sums a body's mass from its shapes with parallel-axis transfer to
    /// the center of mass. Shapeless bodies keep zero mass (infinite
    /// response); locked rotation pins the inverse inertia. Shapes are
    /// authored axis-aligned: a rotated local frame moves the centroid
    /// exactly but its inertia stays in the canonical diagonal, so attach
    /// rotated boxes only when the approximation is acceptable.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal void RecalculateMass(BodyStore bodies, BodyId bodyId)
    {
        ref BodyState body = ref bodies.Slot(bodyId);

        float totalMass = 0.0f;
        Vec3 weightedCenter = Vec3.Zero;
        Vec3 inertiaOrigin = Vec3.Zero;

        int current = body.FirstShape;
        while (current != -1)
        {
            ref readonly ShapeRecord shape = ref _shapes[current];
            MassData3D data = shape.Kind == ColliderKind.Sphere
                ? LocalMassSphere(in shape)
                : LocalMassBox(in shape);

            totalMass += data.Mass;
            weightedCenter += data.Centroid * data.Mass;
            inertiaOrigin += data.InertiaDiagonal + (data.Mass * OffsetSquares(data.Centroid));
            current = shape.NextShape;
        }

        if (totalMass <= 0.0f)
        {
            body.Mass = 0.0f;
            body.InverseMass = 0.0f;
            body.InverseInertia = Vec3.Zero;
            body.LocalCenter = Vec3.Zero;
            return;
        }

        Vec3 center = weightedCenter * (1.0f / totalMass);
        Vec3 inertia = inertiaOrigin - (totalMass * OffsetSquares(center));
        inertia = new Vec3(
            MathF.Max(inertia.X, 0.0f),
            MathF.Max(inertia.Y, 0.0f),
            MathF.Max(inertia.Z, 0.0f));

        // Static and kinematic bodies answer with infinite mass no matter
        // what is attached: the raw total stays readable, but the inverse
        // that the solver multiplies by is pinned to zero.
        bool infinite = body.Type != BodyType.Dynamic;
        bool locked = (body.Flags & BodyFlags.FixedRotation) != 0;
        body.Mass = totalMass;
        body.InverseMass = !infinite ? 1.0f / totalMass : 0.0f;
        body.InverseInertia = new Vec3(
            !infinite && !locked && inertia.X > 0.0f ? 1.0f / inertia.X : 0.0f,
            !infinite && !locked && inertia.Y > 0.0f ? 1.0f / inertia.Y : 0.0f,
            !infinite && !locked && inertia.Z > 0.0f ? 1.0f / inertia.Z : 0.0f);
        body.LocalCenter = center;
    }

    /// <summary>Per-axis parallel-axis offsets: (dy^2+dz^2, dx^2+dz^2, dx^2+dy^2).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vec3 OffsetSquares(Vec3 offset) => new(
        (offset.Y * offset.Y) + (offset.Z * offset.Z),
        (offset.X * offset.X) + (offset.Z * offset.Z),
        (offset.X * offset.X) + (offset.Y * offset.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static MassData3D LocalMassSphere(in ShapeRecord shape)
    {
        Vec3 center = shape.LocalFrame.TransformPoint(shape.LocalSphere.Center);
        float mass = (4.0f / 3.0f) * MathF.PI * shape.LocalSphere.Radius * shape.LocalSphere.Radius * shape.LocalSphere.Radius * shape.Density;
        if (mass <= 0.0f)
        {
            return MassData3D.Zero;
        }

        float inertia = 0.4f * mass * shape.LocalSphere.Radius * shape.LocalSphere.Radius;
        var diagonal = new Vec3(inertia, inertia, inertia);
        return new MassData3D(mass, 0.0f, diagonal, Vec3.Zero, center);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static MassData3D LocalMassBox(in ShapeRecord shape)
    {
        Vec3 center = shape.LocalFrame.TransformPoint(shape.LocalBox.Center);
        Vec3 h = shape.LocalBox.HalfExtents;
        float mass = 8.0f * h.X * h.Y * h.Z * shape.Density;
        if (mass <= 0.0f)
        {
            return MassData3D.Zero;
        }

        var diagonal = new Vec3(
            (mass / 3.0f) * ((h.Y * h.Y) + (h.Z * h.Z)),
            (mass / 3.0f) * ((h.X * h.X) + (h.Z * h.Z)),
            (mass / 3.0f) * ((h.X * h.X) + (h.Y * h.Y)));
        return new MassData3D(mass, 0.0f, diagonal, Vec3.Zero, center);
    }

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
        Array.Resize(ref _shapes, newCapacity);
        Array.Resize(ref _freeStack, newCapacity);
        _capacity = newCapacity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Validate(ShapeId id)
    {
        int slot = id.ToIndex();
        if (!id.IsValid || slot < 0 || slot >= _capacity || !_shapes[slot].Live)
        {
            ThrowHelper.ThrowShapeNotFound(id);
        }

        return slot;
    }
}
