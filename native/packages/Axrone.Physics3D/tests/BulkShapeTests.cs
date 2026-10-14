using Axrone.Geometry;
using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public class BulkShapeTests
{
    private static readonly Vec3[] UnitCube = new Vec3[]
    {
        new(-1.0f, -1.0f, -1.0f), new(1.0f, -1.0f, -1.0f),
        new(-1.0f, 1.0f, -1.0f), new(1.0f, 1.0f, -1.0f),
        new(-1.0f, -1.0f, 1.0f), new(1.0f, -1.0f, 1.0f),
        new(-1.0f, 1.0f, 1.0f), new(1.0f, 1.0f, 1.0f),
    };

    private static readonly Vec3[] HalfCube = new Vec3[]
    {
        new(-0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, -0.5f),
        new(-0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, -0.5f),
        new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f),
        new(-0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, 0.5f),
    };

    private static readonly int[] CubeTriangles = new int[]
    {
        0, 1, 2, 1, 3, 2, 4, 6, 5, 5, 6, 7,
        0, 4, 1, 1, 4, 5, 2, 3, 6, 3, 7, 6,
        0, 2, 4, 2, 6, 4, 1, 5, 3, 3, 5, 7,
    };

    private static readonly Vec3[] SingleTriangle = new Vec3[]
    {
        new(0.0f, 0.0f, 0.0f), new(2.0f, 0.0f, 0.0f), new(0.0f, 2.0f, 0.0f),
    };

    private static readonly int[] SingleTriangleIndices = new int[] { 0, 1, 2 };

    private static readonly float[] TinyGrid = new float[] { 0.0f, 1.0f, 2.0f, 3.0f };

    private static Vec3[] CubeVertices(float half)
    {
        Vec3[] source = half == 1.0f ? UnitCube : HalfCube;
        Vec3[] copy = new Vec3[source.Length];
        Array.Copy(source, copy, source.Length);
        return copy;
    }

    [Fact]
    public void Hull_Mass_Matches_BoundingBox()
    {
        var hull = new Hull3D(CubeVertices(1.0f));

        MassData3D mass = hull.ComputeMass(1.0f);

        mass.Mass.Should().BeApproximately(8.0f, 1e-4f);
        mass.InertiaDiagonal.X.Should().BeApproximately((8.0f * (4.0f + 4.0f)) / 12.0f, 1e-3f);
    }

    [Fact]
    public void Hull_Support_Selects_FurthestVertex()
    {
        var hull = new Hull3D(CubeVertices(1.0f));

        Vec3 support = Hull3D.Support(hull, new Vec3(1.0f, 1.0f, 1.0f));

        support.Should().Be(new Vec3(1.0f, 1.0f, 1.0f));
    }

    [Fact]
    public void Mesh_Raycast_Strikes_Triangle()
    {
        var mesh = new Mesh3D(SingleTriangle, SingleTriangleIndices);
        var ray = new Ray3D(new Vec3(0.5f, 0.5f, 5.0f), new Vec3(0.0f, 0.0f, -1.0f));

        bool hit = ShapeRaycast3D.RayMesh(ray, 100.0f, mesh, RigidTransform3D.Identity, out float distance, out Vec3 normal);

        hit.Should().BeTrue();
        distance.Should().BeApproximately(5.0f, 1e-4f);
        normal.Z.Should().BeApproximately(1.0f, 1e-4f);
    }

    [Fact]
    public void Heightfield_Vertices_Follow_Grid()
    {
        var field = new Heightfield3D(TinyGrid, 2, 2);

        field.Vertex(0, 0).Should().Be(new Vec3(-0.5f, 0.0f, -0.5f));
        field.Vertex(1, 1).Should().Be(new Vec3(0.5f, 3.0f, 0.5f));
    }

    [Fact]
    public void HullWorld_Drops_OnGround()
    {
        using var world = new World();
        BodyId ground = world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        world.Shapes.AttachBox(world.Bodies, world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));

        BodyId body = world.Bodies.Create(new BodyDef());
        world.Shapes.AttachHull(world.Bodies, world.Broadphase, body, CubeVertices(0.5f));
        world.Bodies.SetTransform(body, new Vec3(0.0f, 5.0f, 0.0f), Quat.Identity);

        for (int i = 0; i < 240; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        float restingY = world.Bodies.GetTransform(body).Position.Y;
        restingY.Should().BeApproximately(0.5f, 0.15f);
    }

    [Fact]
    public void MeshWorld_Drops_OnGround()
    {
        using var world = new World();
        BodyId ground = world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        world.Shapes.AttachBox(world.Bodies, world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));

        BodyId body = world.Bodies.Create(new BodyDef());
        world.Shapes.AttachMesh(world.Bodies, world.Broadphase, body, CubeVertices(0.5f), CubeTriangles);
        world.Bodies.SetTransform(body, new Vec3(0.0f, 5.0f, 0.0f), Quat.Identity);

        for (int i = 0; i < 240; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        float restingY = world.Bodies.GetTransform(body).Position.Y;
        restingY.Should().BeApproximately(0.5f, 0.15f);
    }
}
