using Axrone.Geometry;
using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public class Solver3DTests
{
    private static DynamicAabbTree<ShapeId, SurfaceAreaHeuristicStrategy, NullSpatialMetricsSink> NewTree() => new();

    [Fact]
    public void RestingContact_Holds_WithoutSinking()
    {
        using var tree = NewTree();
        var bodies = new BodyStore();
        var shapes = new ShapeStore();

        BodyId ground = bodies.Create(new BodyDef { Type = BodyType.Static });
        ShapeId groundShape = shapes.AttachBox(bodies, tree, ground, new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));

        BodyId ball = bodies.Create(new BodyDef());
        ShapeId ballShape = shapes.AttachSphere(bodies, tree, ball, Vec3.Zero, 1.0f);
        bodies.SetTransform(ball, new Vec3(0.0f, 0.9f, 0.0f), Quat.Identity);
        shapes.SyncBody(bodies, tree, ball);

        var contacts = new Contact3D[1];
        contacts[0] = MakeContact(bodies, shapes, ball, ballShape, ground, groundShape);

        ContactSolver.SolveVelocity(bodies, shapes, contacts, 8);
        ContactSolver.SolvePositions(bodies, contacts, 3);

        bodies.GetLinearVelocity(ball).Length().Should().BeLessThan(0.05f);
        bodies.GetTransform(ball).Position.Y.Should().BeGreaterThan(0.85f);
    }

    [Fact]
    public void Restitution_Bounces_IncidentSpeed()
    {
        using var tree = NewTree();
        var bodies = new BodyStore();
        var shapes = new ShapeStore();
        var bouncy = new PhysicsMaterial(0.0f, 1.0f);

        BodyId ground = bodies.Create(new BodyDef { Type = BodyType.Static });
        ShapeId groundShape = shapes.AttachBox(bodies, tree, ground, new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f), material: bouncy);

        BodyId ball = bodies.Create(new BodyDef());
        ShapeId ballShape = shapes.AttachSphere(bodies, tree, ball, Vec3.Zero, 1.0f, material: bouncy);
        bodies.SetTransform(ball, new Vec3(0.0f, 0.9f, 0.0f), Quat.Identity);
        bodies.SetLinearVelocity(ball, new Vec3(0.0f, -5.0f, 0.0f));
        shapes.SyncBody(bodies, tree, ball);

        var contacts = new Contact3D[1];
        contacts[0] = MakeContact(bodies, shapes, ball, ballShape, ground, groundShape);

        ContactSolver.SolveVelocity(bodies, shapes, contacts, 8);

        bodies.GetLinearVelocity(ball).Y.Should().BeApproximately(5.0f, 0.2f);
    }

    [Fact]
    public void Friction_Brakes_TangentialSlide()
    {
        using var tree = NewTree();
        var bodies = new BodyStore();
        var shapes = new ShapeStore();
        var grippy = new PhysicsMaterial(1.0f, 0.0f);

        BodyId ground = bodies.Create(new BodyDef { Type = BodyType.Static });
        ShapeId groundShape = shapes.AttachBox(bodies, tree, ground, new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f), material: grippy);

        BodyId ball = bodies.Create(new BodyDef());
        ShapeId ballShape = shapes.AttachSphere(bodies, tree, ball, Vec3.Zero, 1.0f, material: grippy);
        bodies.SetTransform(ball, new Vec3(0.0f, 0.9f, 0.0f), Quat.Identity);
        bodies.SetLinearVelocity(ball, new Vec3(3.0f, -1.0f, 0.0f));
        shapes.SyncBody(bodies, tree, ball);

        var contacts = new Contact3D[1];
        contacts[0] = MakeContact(bodies, shapes, ball, ballShape, ground, groundShape);

        ContactSolver.SolveVelocity(bodies, shapes, contacts, 8);

        bodies.GetLinearVelocity(ball).X.Should().BeLessThan(3.0f);
    }

    private static Contact3D MakeContact(
        BodyStore bodies, ShapeStore shapes, BodyId ball, ShapeId ballShape, BodyId ground, ShapeId groundShape)
    {
        Sphere3 worldSphere = shapes.GetWorldSphere(ballShape);
        ref ShapeRecord groundRecord = ref shapes.Record(groundShape);
        Box3 groundBox = new(groundRecord.LocalBox.Center, groundRecord.LocalBox.HalfExtents);
        RigidTransform3D groundFrame = shapes.GetWorldFrame(groundShape);

        Penetration3D.SphereBox(worldSphere, groundBox, groundFrame, out Manifold3D manifold).Should().BeTrue();

        return new Contact3D
        {
            BodyA = ball,
            BodyB = ground,
            ShapeA = ballShape,
            ShapeB = groundShape,
            IsSensor = false,
            Manifold = manifold,
        };
    }
}
