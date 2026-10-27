namespace Axrone.Scene.Runtime;

using Axrone.Physics2D;

/// <summary>
/// Bridges scene actors carrying <see cref="SceneRigidbody2D"/> and
/// <see cref="SceneCollider2D"/> components into a <c>Axrone.Physics2D.World</c>.
/// The bridge creates and destroys physics bodies, syncs 2D transforms in both
/// directions, steps the simulation and exposes contact/sensor events.
/// </summary>
public sealed class ScenePhysicsBridge2D : IDisposable
{
    private readonly SceneWorld _world;
    private readonly World _physicsWorld;
    private readonly Dictionary<SceneActor, BodyId> _bodies;
    private bool _disposed;

    /// <summary>Creates a 2D physics bridge over a scene world.</summary>
    public ScenePhysicsBridge2D(SceneWorld world, in WorldDef? physicsDef = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
        if (physicsDef.HasValue)
        {
            WorldDef def = physicsDef.Value;
            _physicsWorld = new World(in def);
        }
        else
        {
            _physicsWorld = new World();
        }
        _bodies = new Dictionary<SceneActor, BodyId>();
    }

    /// <summary>Backing physics world.</summary>
    public World PhysicsWorld => _physicsWorld;

    /// <summary>Number of live physics bodies.</summary>
    public int BodyCount => _bodies.Count;

    /// <summary>Creates or removes physics bodies to match scene components.</summary>
    public void SyncBodies()
    {
        ThrowIfDisposed();

        var seen = new HashSet<SceneActor>();

        foreach (SceneActor actor in _world.Actors)
        {
            if (!_world.IsActiveInHierarchy(actor) ||
                !_world.TryGetData(actor, out SceneRigidbody2D rigidbody) ||
                !rigidbody.Enabled)
            {
                continue;
            }

            seen.Add(actor);

            if (_bodies.ContainsKey(actor))
            {
                SyncBodyProperties(actor, in rigidbody);
            }
            else
            {
                CreateBody(actor, in rigidbody);
                AttachCollider(actor, _bodies[actor]);
                ApplyMassOverride(actor, in rigidbody);
            }
        }

        List<SceneActor>? stale = null;
        foreach (SceneActor actor in _bodies.Keys)
        {
            bool stillActive = seen.Contains(actor) &&
                _world.TryGetData(actor, out SceneRigidbody2D rb) &&
                rb.Enabled;
            if (!stillActive)
            {
                (stale ??= new List<SceneActor>()).Add(actor);
            }
        }

        if (stale is not null)
        {
            for (int i = stale.Count - 1; i >= 0; i--)
            {
                DestroyBody(stale[i]);
            }
        }
    }

    /// <summary>Advances the physics simulation.</summary>
    public void Step(float deltaSeconds)
    {
        ThrowIfDisposed();
        _physicsWorld.Step(deltaSeconds);
    }

    /// <summary>Writes physics body transforms back into scene transforms.</summary>
    public void SyncTransformsFromPhysics()
    {
        ThrowIfDisposed();

        if (!_world.HasTransform2D)
        {
            return;
        }

        foreach ((SceneActor actor, BodyId bodyId) in _bodies)
        {
            Transform2D transform = _physicsWorld.Bodies.GetTransform(bodyId);
            ref SceneTransform2D sceneTransform = ref _world.GetTransform2D(actor);
            sceneTransform.SetPosition(transform.Position);
            sceneTransform.SetRotation(MathF.Atan2(transform.Sin, transform.Cos));
        }

        _world.UpdateTransforms();
    }

    /// <summary>Number of contact events from the last step.</summary>
    public int ContactEventCount => _physicsWorld.ContactEventCount;

    /// <summary>Returns a contact event by index.</summary>
    public ContactEvent GetContactEvent(int index) => _physicsWorld.GetContactEvent(index);

    /// <summary>Number of sensor events from the last step.</summary>
    public int SensorEventCount => _physicsWorld.SensorEventCount;

    /// <summary>Returns a sensor event by index.</summary>
    public SensorEvent GetSensorEvent(int index) => _physicsWorld.GetSensorEvent(index);

    /// <summary>Resolves the actor that owns a physics body, or null.</summary>
    public SceneActor? GetActor(BodyId bodyId)
    {
        foreach ((SceneActor actor, BodyId id) in _bodies)
        {
            if (id == bodyId)
            {
                return actor;
            }
        }

        return null;
    }

    /// <summary>Resolves the physics body owned by a scene actor.</summary>
    public bool TryGetBody(SceneActor actor, out BodyId bodyId) =>
        _bodies.TryGetValue(actor, out bodyId);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _bodies.Clear();
        _physicsWorld.Dispose();
        _disposed = true;
    }

    private void CreateBody(SceneActor actor, in SceneRigidbody2D rigidbody)
    {
        Vec2 position = _world.HasTransform2D
            ? _world.GetWorldPosition2D(actor)
            : default;
        float angle = _world.HasTransform2D
            ? GetWorldAngle(actor)
            : 0.0f;

        var def = new BodyDef
        {
            Type = rigidbody.BodyType switch
            {
                SceneBodyType.Static => BodyType.Static,
                SceneBodyType.Kinematic => BodyType.Kinematic,
                _ => BodyType.Dynamic
            },
            Position = position,
            Angle = angle,
            LinearVelocity = rigidbody.LinearVelocity,
            AngularVelocity = rigidbody.AngularVelocity,
            LinearDamping = rigidbody.LinearDamping,
            AngularDamping = rigidbody.AngularDamping,
            GravityScale = rigidbody.GravityScale
        };

        _bodies[actor] = _physicsWorld.Bodies.Create(in def);
    }

    private float GetWorldAngle(SceneActor actor)
    {
        Mat3 matrix = _world.GetWorldMatrix2D(actor);
        return MathF.Atan2(matrix.M12, matrix.M11);
    }

    private void DestroyBody(SceneActor actor)
    {
        if (_bodies.Remove(actor, out BodyId bodyId))
        {
            _physicsWorld.Bodies.Destroy(bodyId);
        }
    }

    private void SyncBodyProperties(SceneActor actor, in SceneRigidbody2D rigidbody)
    {
        if (!_bodies.TryGetValue(actor, out BodyId bodyId))
        {
            return;
        }

        if (rigidbody.BodyType != SceneBodyType.Static)
        {
            _physicsWorld.Bodies.SetLinearVelocity(bodyId, rigidbody.LinearVelocity);
            _physicsWorld.Bodies.SetAngularVelocity(bodyId, rigidbody.AngularVelocity);
        }
    }

    private void AttachCollider(SceneActor actor, BodyId bodyId)
    {
        if (!_world.TryGetData(actor, out SceneCollider2D collider) || !collider.Enabled)
        {
            return;
        }

        var material = new PhysicsMaterial(collider.Friction, collider.Restitution);
        Vec2 offset = collider.Offset;

        switch (collider.Shape)
        {
            case SceneColliderShape2D.Circle:
                _physicsWorld.Shapes.AttachCircle<
                    SurfaceAreaHeuristicStrategy2D,
                    NullSpatialMetricsSink>(
                    _physicsWorld.Bodies,
                    _physicsWorld.Broadphase,
                    bodyId,
                    offset,
                    collider.Radius,
                    collider.Density,
                    default,
                    material,
                    default,
                    0.0f,
                    collider.IsTrigger);
                break;
            case SceneColliderShape2D.Box:
                Polygon2D box = Polygon2D.CreateBox(collider.BoxHalfExtents);
                _physicsWorld.Shapes.AttachPolygon<
                    SurfaceAreaHeuristicStrategy2D,
                    NullSpatialMetricsSink>(
                    _physicsWorld.Bodies,
                    _physicsWorld.Broadphase,
                    bodyId,
                    box.Vertices,
                    collider.Density,
                    default,
                    material,
                    offset,
                    0.0f,
                    collider.IsTrigger);
                break;
            case SceneColliderShape2D.Capsule:
                var p0 = offset + new Vec2(collider.CapsuleHalfHeight, 0.0f);
                var p1 = offset - new Vec2(collider.CapsuleHalfHeight, 0.0f);
                _physicsWorld.Shapes.AttachCapsule<
                    SurfaceAreaHeuristicStrategy2D,
                    NullSpatialMetricsSink>(
                    _physicsWorld.Bodies,
                    _physicsWorld.Broadphase,
                    bodyId,
                    p0,
                    p1,
                    collider.Radius,
                    collider.Density,
                    default,
                    material,
                    default,
                    0.0f,
                    collider.IsTrigger);
                break;
        }
    }

    private void ApplyMassOverride(SceneActor actor, in SceneRigidbody2D rigidbody)
    {
        if (rigidbody.Mass <= 0.0f || rigidbody.BodyType == SceneBodyType.Static)
        {
            return;
        }

        BodyId bodyId = _bodies[actor];
        float inverseInertia = _physicsWorld.Bodies.GetInverseInertia(bodyId);
        float inertia = inverseInertia > 0.0f ? 1.0f / inverseInertia : 0.0f;
        _physicsWorld.Bodies.SetMassData(bodyId, rigidbody.Mass, inertia);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new SceneDisposedException(nameof(ScenePhysicsBridge2D));
        }
    }
}
