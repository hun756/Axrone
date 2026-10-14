namespace Axrone.Physics2D.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class ShapeStoreTests
{
    private const float Tolerance = 1e-4f;

    private sealed class Fixture : IDisposable
    {
        public readonly BodyStore Bodies = new();
        public readonly ShapeStore Shapes = new();
        public readonly DynamicAabbTree2D<ShapeId, SurfaceAreaHeuristicStrategy2D, NullSpatialMetricsSink> Tree = new(new TreeOptions());

        public void Dispose() => Tree.Dispose();
    }

    private static Polygon2D Box(float minX, float minY, float maxX, float maxY) =>
        Polygon2D.Create(new Vec2[] { new(minX, minY), new(maxX, minY), new(maxX, maxY), new(minX, maxY) });

    [Fact]
    public void AttachCircle_SumsMass_And_RegistersBroadphase()
    {
        var fx = new Fixture();
        BodyId body = fx.Bodies.Create(new BodyDef());

        ShapeId shape = fx.Shapes.AttachCircle(fx.Bodies, fx.Tree, body, new Vec2(0, 0), 1f, density: 2f);

        shape.IsValid.Should().BeTrue();
        fx.Shapes.IsValid(shape).Should().BeTrue();
        fx.Shapes.GetBody(shape).Should().Be(body);
        fx.Shapes.GetKind(shape).Should().Be(ColliderKind.Circle);
        fx.Tree.Count.Should().Be(1);

        fx.Bodies.GetMass(body).Should().BeApproximately(MathF.PI * 2f, Tolerance);
        fx.Bodies.GetInverseMass(body).Should().BeApproximately(1f / (MathF.PI * 2f), Tolerance);

        Circle2 world = fx.Shapes.GetWorldCircle(shape);
        world.Center.Should().Be(new Vec2(0, 0));
        world.Radius.Should().Be(1f);
        fx.Shapes.GetWorldAabb(shape).Should().Be(new Aabb2D(new Vec2(-1, -1), new Vec2(1, 1)));
    }

    [Fact]
    public void AttachBox_MassMatchesAnalytic()
    {
        var fx = new Fixture();
        BodyId body = fx.Bodies.Create(new BodyDef());

        fx.Shapes.AttachPolygon(fx.Bodies, fx.Tree, body, new Vec2[] { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) });

        fx.Bodies.GetMass(body).Should().BeApproximately(4f, Tolerance);
        fx.Bodies.GetInverseMass(body).Should().BeApproximately(0.25f, Tolerance);
        fx.Bodies.GetInverseInertia(body).Should().BeApproximately(1f / (8f / 3f), Tolerance);
    }

    [Fact]
    public void OffsetShape_ShiftsCenterOfMass()
    {
        var fx = new Fixture();
        BodyId body = fx.Bodies.Create(new BodyDef());

        fx.Shapes.AttachCircle(fx.Bodies, fx.Tree, body, new Vec2(2, 0), 1f);

        fx.Bodies.GetMass(body).Should().BeApproximately(MathF.PI, Tolerance);
        ref BodyState state = ref fx.Bodies.Slot(body);
        state.LocalCenter.X.Should().BeApproximately(2f, Tolerance);
        state.LocalCenter.Y.Should().BeApproximately(0f, Tolerance);
        state.Inertia.Should().BeApproximately(0.5f * MathF.PI, Tolerance);
    }

    [Fact]
    public void BodyMove_SyncBody_UpdatesWorldAndTree()
    {
        var fx = new Fixture();
        BodyId body = fx.Bodies.Create(new BodyDef());
        ShapeId shape = fx.Shapes.AttachCircle(fx.Bodies, fx.Tree, body, new Vec2(0, 0), 1f);

        fx.Bodies.SetTransform(body, new Vec2(5, 0), 0f);
        fx.Shapes.SyncBody(fx.Bodies, fx.Tree, body);

        fx.Shapes.GetWorldCircle(shape).Center.Should().Be(new Vec2(5, 0));

        Span<SpatialItemId> results = stackalloc SpatialItemId[4];
        fx.Tree.QueryOverlaps(new Aabb2D(new Vec2(3.5f, -1.5f), new Vec2(6.5f, 1.5f)), results).Should().Be(1);
        fx.Tree.QueryOverlaps(new Aabb2D(new Vec2(-1.5f, -1.5f), new Vec2(1.5f, 1.5f)), results).Should().Be(0);
    }

    [Fact]
    public void Rotation_MovesWorldShape()
    {
        var fx = new Fixture();
        BodyId body = fx.Bodies.Create(new BodyDef());
        ShapeId shape = fx.Shapes.AttachCircle(fx.Bodies, fx.Tree, body, new Vec2(2, 0), 0.5f);

        fx.Bodies.SetTransform(body, Vec2.Zero, MathF.PI / 2f);
        fx.Shapes.SyncBody(fx.Bodies, fx.Tree, body);

        Circle2 world = fx.Shapes.GetWorldCircle(shape);
        world.Center.X.Should().BeApproximately(0f, Tolerance);
        world.Center.Y.Should().BeApproximately(2f, Tolerance);
    }

    [Fact]
    public void PolygonWorldVertices_FollowBody()
    {
        var fx = new Fixture();
        BodyId body = fx.Bodies.Create(new BodyDef());
        ShapeId shape = fx.Shapes.AttachPolygon(
            fx.Bodies, fx.Tree, body, new Vec2[] { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) });

        fx.Bodies.SetTransform(body, new Vec2(10, 0), 0f);
        fx.Shapes.SyncShape(fx.Bodies, fx.Tree, shape);

        Span<Vec2> destination = stackalloc Vec2[4];
        fx.Shapes.GetWorldVertices(shape, destination);
        destination[0].Should().Be(new Vec2(9, -1));
        destination[2].Should().Be(new Vec2(11, 1));
        fx.Shapes.GetWorldAabb(shape).Should().Be(new Aabb2D(new Vec2(9, -1), new Vec2(11, 1)));
    }

    [Fact]
    public void Detach_RemovesBroadphase_And_ResetsMass()
    {
        var fx = new Fixture();
        BodyId body = fx.Bodies.Create(new BodyDef());
        ShapeId first = fx.Shapes.AttachCircle(fx.Bodies, fx.Tree, body, new Vec2(0, 0), 1f);
        fx.Shapes.AttachCircle(fx.Bodies, fx.Tree, body, new Vec2(5, 0), 1f);

        fx.Tree.Count.Should().Be(2);

        fx.Shapes.Detach(fx.Bodies, fx.Tree, first);

        fx.Shapes.IsValid(first).Should().BeFalse();
        fx.Tree.Count.Should().Be(1);
        fx.Bodies.GetMass(body).Should().BeApproximately(MathF.PI, Tolerance);

        Action read = () => fx.Shapes.GetWorldCircle(first);
        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Attach_RejectsBadInput()
    {
        var fx = new Fixture();
        BodyId body = fx.Bodies.Create(new BodyDef());

        Action negativeRadius = () => fx.Shapes.AttachCircle(fx.Bodies, fx.Tree, body, new Vec2(0, 0), -1f);
        Action zeroDensity = () => fx.Shapes.AttachCircle(fx.Bodies, fx.Tree, body, new Vec2(0, 0), 1f, density: 0f);
        Action badPolygon = () => fx.Shapes.AttachPolygon(fx.Bodies, fx.Tree, body, new Vec2[] { new(0, 0), new(1, 1) });
        Action deadBody = () => fx.Shapes.AttachCircle(fx.Bodies, fx.Tree, BodyId.FromRaw(999u), new Vec2(0, 0), 1f);

        negativeRadius.Should().Throw<ArgumentOutOfRangeException>();
        zeroDensity.Should().Throw<InvalidOperationException>();
        badPolygon.Should().Throw<ArgumentException>();
        deadBody.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FixedRotationBody_InverseInertiaIsZero()
    {
        var fx = new Fixture();
        BodyId body = fx.Bodies.Create(new BodyDef { FixedRotation = true });

        fx.Shapes.AttachCircle(fx.Bodies, fx.Tree, body, new Vec2(0, 0), 1f);

        fx.Bodies.GetMass(body).Should().BeGreaterThan(0f);
        fx.Bodies.GetInverseInertia(body).Should().Be(0f);
    }

    [Fact]
    public void Capsule_WorldAabb_CoversSegment()
    {
        var fx = new Fixture();
        BodyId body = fx.Bodies.Create(new BodyDef());
        ShapeId shape = fx.Shapes.AttachCapsule(fx.Bodies, fx.Tree, body, new Vec2(-2, 0), new Vec2(2, 0), 1f);

        Capsule2D world = fx.Shapes.GetWorldCapsule(shape);
        world.P0.Should().Be(new Vec2(-2, 0));
        world.P1.Should().Be(new Vec2(2, 0));
        fx.Shapes.GetWorldAabb(shape).Should().Be(new Aabb2D(new Vec2(-3, -1), new Vec2(3, 1)));

        Action wrongKind = () => fx.Shapes.GetWorldCircle(shape);
        wrongKind.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LocalFrameOffset_ComposesWithBody()
    {
        var fx = new Fixture();
        BodyId body = fx.Bodies.Create(new BodyDef());
        ShapeId shape = fx.Shapes.AttachCircle(
            fx.Bodies, fx.Tree, body, new Vec2(0, 0), 0.5f, localOffset: new Vec2(0, 3));

        fx.Bodies.SetTransform(body, Vec2.Zero, MathF.PI / 2f);
        fx.Shapes.SyncBody(fx.Bodies, fx.Tree, body);

        Circle2 world = fx.Shapes.GetWorldCircle(shape);
        world.Center.X.Should().BeApproximately(-3f, Tolerance);
        world.Center.Y.Should().BeApproximately(0f, Tolerance);
    }
}
