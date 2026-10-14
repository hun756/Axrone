namespace Axrone.Physics2D.Tests;

using FluentAssertions;
using Xunit;

public class BodyNominalTests
{
    [Fact]
    public void Ids_InvalidByDefault_ValidWhenWrapped()
    {
        BodyId.Invalid.IsValid.Should().BeFalse();
        ShapeId.Invalid.IsValid.Should().BeFalse();

        var body = BodyId.FromRaw(3u);
        var shape = ShapeId.FromRaw(7u);

        body.IsValid.Should().BeTrue();
        shape.IsValid.Should().BeTrue();
        body.RawValue.Should().Be(3u);
        shape.RawValue.Should().Be(7u);
        body.ToString().Should().Be("Body(3)");
        shape.ToString().Should().Be("Shape(7)");
        (body < BodyId.FromRaw(4u)).Should().BeTrue();
    }

    [Fact]
    public void CollisionFilter_MatchesBox2DRules()
    {
        CollisionFilter a = new(0x0001u, 0xFFFFu, 0);
        CollisionFilter b = new(0x0002u, 0xFFFFu, 0);

        CollisionFilter.ShouldCollide(in a, in b).Should().BeTrue();

        CollisionFilter masked = new(0x0002u, 0x0002u, 0);
        CollisionFilter.ShouldCollide(in a, in masked).Should().BeFalse();

        CollisionFilter positiveA = new(0x0001u, 0x0000u, 5);
        CollisionFilter positiveB = new(0x0002u, 0x0000u, 5);
        CollisionFilter.ShouldCollide(in positiveA, in positiveB).Should().BeTrue();

        CollisionFilter negativeA = new(0x0001u, 0xFFFFu, -5);
        CollisionFilter negativeB = new(0x0002u, 0xFFFFu, -5);
        CollisionFilter.ShouldCollide(in negativeA, in negativeB).Should().BeFalse();
    }

    [Fact]
    public void PhysicsMaterial_GuardsDomain()
    {
        Action negativeFriction = () => _ = new PhysicsMaterial(-0.1f, 0.0f);
        Action highRestitution = () => _ = new PhysicsMaterial(0.2f, 1.5f);
        Action negativeRestitution = () => _ = new PhysicsMaterial(0.2f, -0.5f);

        negativeFriction.Should().Throw<InvalidOperationException>();
        highRestitution.Should().Throw<InvalidOperationException>();
        negativeRestitution.Should().Throw<InvalidOperationException>();

        PhysicsMaterial.Default.Friction.Should().BeApproximately(0.2f, 1e-6f);
        PhysicsMaterial.Default.Restitution.Should().Be(0.0f);
    }
}
