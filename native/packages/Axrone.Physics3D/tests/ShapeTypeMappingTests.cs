using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public sealed class ShapeTypeMappingTests
{
    [Fact]
    public void ColliderKinds_Map_To_Shared_Contract()
    {
        ColliderKind.Sphere.ToShapeType().Should().Be(ShapeType3D.Sphere);
        ColliderKind.Box.ToShapeType().Should().Be(ShapeType3D.Box);
        ColliderKind.Capsule.ToShapeType().Should().Be(ShapeType3D.Capsule);
        ColliderKind.Cylinder.ToShapeType().Should().Be(ShapeType3D.Cylinder);
        ColliderKind.Cone.ToShapeType().Should().Be(ShapeType3D.Cone);
        ColliderKind.Hull.ToShapeType().Should().Be(ShapeType3D.ConvexHull);
        ColliderKind.Mesh.ToShapeType().Should().Be(ShapeType3D.TriangleMesh);
        ColliderKind.Heightfield.ToShapeType().Should().Be(ShapeType3D.HeightField);
    }
}
