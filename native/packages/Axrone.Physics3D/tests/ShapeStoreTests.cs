using Axrone.Geometry;
using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public class ShapeStoreTests
{
    private readonly BodyStore _bodies = new();
    private readonly ShapeStore _shapes = new();

    private static DynamicAabbTree<ShapeId, SurfaceAreaHeuristicStrategy, NullSpatialMetricsSink> NewTree() => new();

    [Fact]
    public void AttachSphere_Registers_BroadphaseAndMass()
    {
        using var tree = NewTree();
        BodyId body = _bodies.Create(new BodyDef());

        ShapeId shape = _shapes.AttachSphere(_bodies, tree, body, Vec3.Zero, 1.0f);

        _shapes.IsValid(shape).Should().BeTrue();
        _shapes.GetBody(shape).Should().Be(body);
        _shapes.GetKind(shape).Should().Be(ColliderKind.Sphere);
        _bodies.GetMass(body).Should().BeApproximately((4.0f / 3.0f) * MathF.PI, 1e-4f);
    }

    [Fact]
    public void AttachBox_TwoShapes_SumsMassAndCentroid()
    {
        using var tree = NewTree();
        BodyId body = _bodies.Create(new BodyDef());

        _shapes.AttachBox(_bodies, tree, body, new Vec3(-1.0f, 0.0f, 0.0f), new Vec3(1.0f, 1.0f, 1.0f));
        _shapes.AttachBox(_bodies, tree, body, new Vec3(1.0f, 0.0f, 0.0f), new Vec3(1.0f, 1.0f, 1.0f));

        _bodies.GetMass(body).Should().BeApproximately(16.0f, 1e-4f);
    }

    [Fact]
    public void Detach_Removes_ShapeAndMass()
    {
        using var tree = NewTree();
        BodyId body = _bodies.Create(new BodyDef());
        ShapeId shape = _shapes.AttachSphere(_bodies, tree, body, Vec3.Zero, 1.0f);

        _shapes.Detach(_bodies, tree, shape);

        _shapes.IsValid(shape).Should().BeFalse();
        _bodies.GetMass(body).Should().Be(0.0f);
        _shapes.Count.Should().Be(0);
    }

    [Fact]
    public void SyncBody_Follows_BodyMotion()
    {
        using var tree = NewTree();
        BodyId body = _bodies.Create(new BodyDef());
        ShapeId shape = _shapes.AttachSphere(_bodies, tree, body, Vec3.Zero, 1.0f);

        _bodies.SetTransform(body, new Vec3(5.0f, 0.0f, 0.0f), Quat.Identity);
        _shapes.SyncBody(_bodies, tree, body);

        _shapes.GetWorldSphere(shape).Center.Should().Be(new Vec3(5.0f, 0.0f, 0.0f));
        _shapes.GetWorldAabb(shape).Center.Should().Be(new Vec3(5.0f, 0.0f, 0.0f));
    }

    [Fact]
    public void RotatedBox_WorldAabb_CoversCorners()
    {
        using var tree = NewTree();
        BodyId body = _bodies.Create(new BodyDef
        {
            Rotation = Quat.CreateFromAxisAngle(new Vec3(0.0f, 0.0f, 1.0f), MathF.PI / 4.0f),
        });
        ShapeId shape = _shapes.AttachBox(_bodies, tree, body, Vec3.Zero, new Vec3(1.0f, 1.0f, 1.0f));

        Aabb3D bounds = _shapes.GetWorldAabb(shape);

        float planar = MathF.Sqrt(2.0f);
        (bounds.Max.X - bounds.Min.X).Should().BeApproximately(2.0f * planar, 1e-4f);
        (bounds.Max.Z - bounds.Min.Z).Should().BeApproximately(2.0f, 1e-4f);
    }

    [Fact]
    public void Sensor_Flag_Toggles()
    {
        using var tree = NewTree();
        BodyId body = _bodies.Create(new BodyDef());
        ShapeId shape = _shapes.AttachSphere(_bodies, tree, body, Vec3.Zero, 1.0f, isSensor: true);

        _shapes.IsSensor(shape).Should().BeTrue();
        _shapes.SetSensor(shape, false);
        _shapes.IsSensor(shape).Should().BeFalse();
    }

    [Fact]
    public void DeadShape_Throws_OnAccess()
    {
        var act = () => _shapes.GetWorldAabb(ShapeId.FromRaw(999u));

        act.Should().Throw<InvalidOperationException>();
    }
}
