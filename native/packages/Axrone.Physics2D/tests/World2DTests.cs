namespace Axrone.Physics2D.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public sealed class World2DTests : IDisposable
{
    private const float Tolerance = 1e-3f;

    private readonly World _world;

    public World2DTests()
    {
        _world = new World();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Polygon2D Box(float minX, float minY, float maxX, float maxY) =>
        Polygon2D.Create(new Vec2[] { new(minX, minY), new(maxX, minY), new(maxX, maxY), new(minX, maxY) });

    private BodyId AddBox(Vec2 center, Vec2 halfExtents, BodyType type = BodyType.Dynamic)
    {
        BodyId body = _world.Bodies.Create(new BodyDef { Type = type, Position = center });
        _world.Shapes.AttachPolygon(
            _world.Bodies, _world.Broadphase, body,
            new Vec2[]
            {
                new(-halfExtents.X, -halfExtents.Y),
                new(halfExtents.X, -halfExtents.Y),
                new(halfExtents.X, halfExtents.Y),
                new(-halfExtents.X, halfExtents.Y),
            });
        return body;
    }

    [Fact]
    public void Step_FallingBox_FallsAndContactsGround()
    {
        AddBox(new Vec2(0, -1), new Vec2(10, 1), BodyType.Static);
        BodyId box = AddBox(new Vec2(0, 0.25f), new Vec2(0.5f, 0.5f));

        _world.Step(1f / 60f);

        float settledY = _world.Bodies.GetTransform(box).Position.Y;
        settledY.Should().BeGreaterThan(0.2f);
        settledY.Should().BeLessThan(0.6f);
        _world.ContactCount.Should().Be(1);
        _world.GetContact(0).Manifold.Normal.Y.Should().BeApproximately(1f, Tolerance);
    }

    [Fact]
    public void Step_OverlappingBoxes_TrackContact()
    {
        AddBox(new Vec2(0, 0), new Vec2(1, 1));
        AddBox(new Vec2(0.5f, 0), new Vec2(1, 1));

        _world.Step(1f / 60f);

        _world.ContactCount.Should().Be(1);
        Contact contact = _world.GetContact(0);
        contact.Manifold.Count.Should().Be(2);
        contact.Manifold.Normal.X.Should().BeApproximately(1f, Tolerance);
    }

    [Fact]
    public void Step_SeparatedBodies_NoContacts()
    {
        AddBox(new Vec2(0, 0), new Vec2(1, 1), BodyType.Static);
        AddBox(new Vec2(10, 10), new Vec2(1, 1), BodyType.Static);

        _world.Step(1f / 60f);

        _world.ContactCount.Should().Be(0);
    }

    [Fact]
    public void Step_FilteredPair_Skipped()
    {
        BodyId a = _world.Bodies.Create(new BodyDef { Position = new Vec2(0, 0) });
        BodyId b = _world.Bodies.Create(new BodyDef { Position = new Vec2(0.5f, 0) });

        var tree = _world.Broadphase;
        _world.Shapes.AttachPolygon(
            _world.Bodies, tree, a,
            new Vec2[] { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) },
            filter: new CollisionFilter(0x0001u, 0xFFFFu, 0));
        _world.Shapes.AttachPolygon(
            _world.Bodies, tree, b,
            new Vec2[] { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) },
            filter: new CollisionFilter(0x0002u, 0x0002u, 0));

        _world.Step(1f / 60f);

        _world.ContactCount.Should().Be(0);
    }

    [Fact]
    public void Step_StaticStatic_Skipped()
    {
        AddBox(new Vec2(0, 0), new Vec2(1, 1), BodyType.Static);
        AddBox(new Vec2(0.5f, 0), new Vec2(1, 1), BodyType.Static);

        _world.Step(1f / 60f);

        _world.ContactCount.Should().Be(0);
    }

    [Fact]
    public void Step_CacheCarries_AcrossSteps()
    {
        AddBox(new Vec2(0, 0), new Vec2(1, 1));
        AddBox(new Vec2(0.5f, 0), new Vec2(1, 1));

        _world.Step(1f / 60f);
        _world.ContactCount.Should().Be(1);
        _world.GetContact(0).Cache.Count.Should().Be(2);

        _world.Step(1f / 60f);
        _world.ContactCount.Should().Be(1);
        _world.GetContact(0).Cache.Count.Should().Be(2);
    }

    [Fact]
    public void Step_Separation_DropsContact()
    {
        BodyId mover = AddBox(new Vec2(0.5f, 0), new Vec2(1, 1), BodyType.Dynamic);
        AddBox(new Vec2(0, 0), new Vec2(1, 1), BodyType.Static);

        _world.Step(1f / 60f);
        _world.ContactCount.Should().Be(1);

        _world.Bodies.SetTransform(mover, new Vec2(50, 50), 0f);
        _world.Step(1f / 60f);

        _world.ContactCount.Should().Be(0);
    }

    [Fact]
    public void DestroyBody_ClearsShapesAndContacts()
    {
        BodyId doomed = AddBox(new Vec2(0.5f, 0), new Vec2(1, 1));
        AddBox(new Vec2(0, 0), new Vec2(1, 1), BodyType.Static);

        _world.Step(1f / 60f);
        _world.ContactCount.Should().Be(1);

        _world.DestroyBody(doomed);

        _world.Bodies.IsValid(doomed).Should().BeFalse();
        _world.ContactCount.Should().Be(0);
        _world.Shapes.Count.Should().Be(1);
    }

    [Fact]
    public void Step_InvalidDt_Throws()
    {
        Action zero = () => _world.Step(0f);

        zero.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var world = new World();
        world.Dispose();

        Action again = () => world.Dispose();
        again.Should().NotThrow();

        Action step = () => world.Step(1f / 60f);
        step.Should().Throw<ObjectDisposedException>();
    }
}
