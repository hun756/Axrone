namespace Axrone.Physics2D.Tests;

using FluentAssertions;
using Xunit;

public sealed class ShapeTypeMappingTests
{
    [Fact]
    public void ColliderKinds_Map_To_Shared_Contract()
    {
        ColliderKind.Circle.ToShapeType().Should().Be(ShapeType.Circle);
        ColliderKind.Capsule.ToShapeType().Should().Be(ShapeType.Capsule);
        ColliderKind.Polygon.ToShapeType().Should().Be(ShapeType.Polygon);
        ColliderKind.Edge.ToShapeType().Should().Be(ShapeType.Segment);
    }
}
