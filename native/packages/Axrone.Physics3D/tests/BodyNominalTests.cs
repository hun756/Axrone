using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public class BodyNominalTests
{
    [Fact]
    public void Invalid_Sentinel_IsDefault_AndNotValid()
    {
        BodyId.Invalid.RawValue.Should().Be(0u);
        BodyId.Invalid.IsValid.Should().BeFalse();
        default(BodyId).Should().Be(BodyId.Invalid);
        ShapeId.Invalid.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FromIndex_RoundTrips_ToIndex()
    {
        BodyId.FromIndex(0).RawValue.Should().Be(1u);
        BodyId.FromIndex(0).ToIndex().Should().Be(0);
        BodyId.FromIndex(41).ToIndex().Should().Be(41);
        ShapeId.FromIndex(7).ToIndex().Should().Be(7);
    }

    [Fact]
    public void ToString_UsesNamedForm()
    {
        BodyId.FromRaw(42u).ToString().Should().Be("Body(42)");
        BodyId.Invalid.ToString().Should().Be("Body(Invalid)");
        ShapeId.FromRaw(7u).ToString().Should().Be("Shape(7)");
    }

    [Fact]
    public void CollisionFilter_DefaultCollides_WithItself()
    {
        CollisionFilter.ShouldCollide(CollisionFilter.Default, CollisionFilter.Default).Should().BeTrue();
    }

    [Fact]
    public void CollisionFilter_NegativeGroup_Separates()
    {
        var a = new CollisionFilter(0x0001u, 0xFFFFu, -1);
        var b = new CollisionFilter(0x0001u, 0xFFFFu, -1);

        CollisionFilter.ShouldCollide(a, b).Should().BeFalse();
    }

    [Fact]
    public void PhysicsMaterial_Rejects_InvalidValues()
    {
        var actFriction = () => new PhysicsMaterial(-1.0f, 0.0f);
        var actRestitution = () => new PhysicsMaterial(0.2f, 1.5f);

        actFriction.Should().Throw<InvalidOperationException>();
        actRestitution.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RigidTransform_Identity_MapsPointsToThemselves()
    {
        var frame = RigidTransform3D.Identity;
        var point = new Axrone.Numeric.Vec3(1.0f, 2.0f, 3.0f);

        frame.TransformPoint(point).Should().Be(point);
        frame.InverseTransformPoint(point).Should().Be(point);
    }

    [Fact]
    public void RigidTransform_Rotate_TurnsUnitX_ToUnitY()
    {
        var rotation = Axrone.Numeric.Quat.CreateFromAxisAngle(new Axrone.Numeric.Vec3(0.0f, 0.0f, 1.0f), MathF.PI / 2.0f);
        var frame = new RigidTransform3D(Axrone.Numeric.Vec3.Zero, rotation);

        var turned = frame.Rotate(new Axrone.Numeric.Vec3(1.0f, 0.0f, 0.0f));

        turned.X.Should().BeApproximately(0.0f, 1e-5f);
        turned.Y.Should().BeApproximately(1.0f, 1e-5f);
        turned.Z.Should().BeApproximately(0.0f, 1e-5f);
    }

    [Fact]
    public void BodyDef_Defaults_DescribeAwakeDynamicBody()
    {
        var def = new BodyDef();

        def.Type.Should().Be(BodyType.Dynamic);
        def.Rotation.Should().Be(Axrone.Numeric.Quat.Identity);
        def.Awake.Should().BeTrue();
        def.AllowSleep.Should().BeTrue();
    }
}
