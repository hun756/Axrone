using Axrone.Geometry;
using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public sealed class World3DTests : IDisposable
{
    private readonly World _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void FallingBall_Lands_OnGround()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        _world.Shapes.AttachBox(_world.Bodies, _world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));

        BodyId ball = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, ball, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(ball, new Vec3(0.0f, 5.0f, 0.0f), Quat.Identity);

        for (int i = 0; i < 240; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        float restingY = _world.Bodies.GetTransform(ball).Position.Y;
        restingY.Should().BeApproximately(0.5f, 0.1f);
        _world.ContactCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Sensor_Pair_Raises_BeginAndEnd()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        _world.Shapes.AttachBox(_world.Bodies, _world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f), isSensor: true);

        BodyId ball = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, ball, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(ball, new Vec3(0.0f, 5.0f, 0.0f), Quat.Identity);

        bool began = false;
        for (int i = 0; i < 240 && !began; i++)
        {
            _world.Step(1.0f / 60.0f);
            for (int e = 0; e < _world.ContactEventCount; e++)
            {
                if (_world.GetContactEvent(e).Type == ContactEventType.Began)
                {
                    began = true;
                }
            }
        }

        began.Should().BeTrue();

        _world.Bodies.SetTransform(ball, new Vec3(0.0f, 50.0f, 0.0f), Quat.Identity);
        _world.Step(1.0f / 60.0f);

        bool ended = false;
        for (int e = 0; e < _world.ContactEventCount; e++)
        {
            if (_world.GetContactEvent(e).Type == ContactEventType.Ended)
            {
                ended = true;
            }
        }

        ended.Should().BeTrue();
    }

    [Fact]
    public void DestroyBody_Removes_ShapesAndContacts()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        _world.Shapes.AttachBox(_world.Bodies, _world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));

        BodyId ball = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, ball, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(ball, new Vec3(0.0f, 0.4f, 0.0f), Quat.Identity);
        _world.Step(1.0f / 60.0f);
        _world.ContactCount.Should().BeGreaterThan(0);

        _world.DestroyBody(ball);

        _world.Bodies.IsValid(ball).Should().BeFalse();
        _world.ContactCount.Should().Be(0);
        _world.Shapes.Count.Should().Be(1);
    }

    [Fact]
    public void Raycast_Hits_NearestShape_First()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        _world.Shapes.AttachBox(_world.Bodies, _world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));

        BodyId ball = _world.Bodies.Create(new BodyDef { Type = BodyType.Kinematic });
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, ball, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(ball, new Vec3(0.0f, 3.0f, 0.0f), Quat.Identity);
        _world.Step(1.0f / 60.0f);

        var ray = new Ray3D(new Vec3(0.0f, 10.0f, 0.0f), new Vec3(0.0f, -1.0f, 0.0f));
        Span<RaycastHit3D> hits = stackalloc RaycastHit3D[8];
        int count = _world.Raycast(ray, 100.0f, hits);

        count.Should().BeGreaterThanOrEqualTo(2);
        hits[0].Distance.Should().BeLessThan(hits[1].Distance);
        hits[0].Point.Y.Should().BeApproximately(3.5f, 0.05f);
    }

    [Fact]
    public void OverlapAabb_Finds_ShapesInVolume()
    {
        BodyId ball = _world.Bodies.Create(new BodyDef());
        ShapeId shape = _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, ball, Vec3.Zero, 0.5f);
        _world.Step(1.0f / 60.0f);

        Span<ShapeId> results = stackalloc ShapeId[8];
        int count = _world.OverlapAabb(new Aabb3D(new Vec3(-2, -2, -2), new Vec3(2, 2, 2)), results);

        count.Should().Be(1);
        results[0].Should().Be(shape);
    }

    [Fact]
    public void StayEvent_Fires_WhileContactPersists()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        _world.Shapes.AttachBox(_world.Bodies, _world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f), isSensor: true);

        BodyId ball = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, ball, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(ball, new Vec3(0.0f, 0.4f, 0.0f), Quat.Identity);

        _world.Step(1.0f / 60.0f);
        bool began = false;
        for (int e = 0; e < _world.ContactEventCount; e++)
        {
            began |= _world.GetContactEvent(e).Type == ContactEventType.Began;
        }

        began.Should().BeTrue();

        _world.Bodies.SetTransform(ball, new Vec3(0.0f, 0.4f, 0.0f), Quat.Identity);
        _world.Step(1.0f / 60.0f);
        bool stayed = false;
        for (int e = 0; e < _world.ContactEventCount; e++)
        {
            stayed |= _world.GetContactEvent(e).Type == ContactEventType.Stay;
        }

        stayed.Should().BeTrue();
    }

    [Fact]
    public void QueryPoint_Finds_NearestSurface()
    {
        BodyId ball = _world.Bodies.Create(new BodyDef());
        ShapeId shape = _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, ball, Vec3.Zero, 0.5f);
        _world.Step(1.0f / 60.0f);

        Span<ClosestPointHit3D> hits = stackalloc ClosestPointHit3D[8];
        int count = _world.QueryPoint(new Vec3(0.0f, 2.0f, 0.0f), 10.0f, hits);

        count.Should().Be(1);
        hits[0].Shape.Should().Be(shape);
        hits[0].Distance.Should().BeApproximately(1.5f, 0.05f);
        hits[0].Point.Y.Should().BeApproximately(0.5f, 0.05f);
    }

    [Fact]
    public void ShiftOrigin_Translates_BodiesExactly()
    {
        BodyId ball = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, ball, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(ball, new Vec3(100.0f, 0.0f, 0.0f), Quat.Identity);
        _world.Step(1.0f / 60.0f);

        _world.ShiftOrigin(new Vec3(100.0f, 0.0f, 0.0f));

        _world.Bodies.GetTransform(ball).Position.X.Should().BeApproximately(0.0f, 1e-4f);
    }
}
