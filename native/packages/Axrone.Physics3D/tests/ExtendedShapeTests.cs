using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public class ExtendedShapeTests
{
    [Fact]
    public void Capsule_Mass_Matches_WebFormula()
    {
        var capsule = new Capsule3(new Vec3(0.0f, -1.0f, 0.0f), new Vec3(0.0f, 1.0f, 0.0f), 0.5f);

        MassData3D mass = capsule.ComputeMass(1.0f);

        float expected = (MathF.PI * 0.25f * 2.0f) + ((4.0f / 3.0f) * MathF.PI * 0.125f);
        mass.Mass.Should().BeApproximately(expected, 1e-5f);
        mass.InertiaDiagonal.X.Should().BeApproximately(mass.Mass * 0.25f, 1e-5f);
        mass.Centroid.Should().Be(Vec3.Zero);
    }

    [Fact]
    public void Capsule_Support_Picks_FurthestCap()
    {
        var capsule = new Capsule3(new Vec3(0.0f, -1.0f, 0.0f), new Vec3(0.0f, 1.0f, 0.0f), 0.5f);

        Vec3 up = Capsule3.Support(capsule, new Vec3(0.0f, 1.0f, 0.0f));
        Vec3 side = Capsule3.Support(capsule, new Vec3(1.0f, 0.0f, 0.0f));

        up.Should().Be(new Vec3(0.0f, 1.5f, 0.0f));
        side.X.Should().BeApproximately(0.5f, 1e-5f);
    }

    [Fact]
    public void Capsule_Rejects_NegativeRadius()
    {
        var act = () => new Capsule3(Vec3.Zero, Vec3.Zero, -1.0f);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cylinder_Mass_Matches_WebFormula()
    {
        var cylinder = new Cylinder3(Vec3.Zero, 1.0f, 2.0f, ShapeAxis3D.Y);

        MassData3D mass = cylinder.ComputeMass(1.0f);

        mass.Mass.Should().BeApproximately(2.0f * MathF.PI, 1e-4f);
        mass.InertiaDiagonal.Y.Should().BeApproximately(0.5f * mass.Mass, 1e-4f);
        mass.InertiaDiagonal.X.Should().BeApproximately((mass.Mass * (3.0f + 4.0f)) / 12.0f, 1e-4f);
    }

    [Fact]
    public void Cylinder_Axis_Maps_InertiaDiagonal()
    {
        var cylinder = new Cylinder3(Vec3.Zero, 1.0f, 2.0f, ShapeAxis3D.X);

        MassData3D mass = cylinder.ComputeMass(1.0f);

        mass.InertiaDiagonal.X.Should().BeApproximately(0.5f * mass.Mass, 1e-4f);
        mass.InertiaDiagonal.Y.Should().BeApproximately(mass.InertiaDiagonal.Z, 1e-5f);
    }

    [Fact]
    public void Cylinder_Support_Selects_RimPoint()
    {
        var cylinder = new Cylinder3(Vec3.Zero, 1.0f, 2.0f, ShapeAxis3D.Y);

        Vec3 support = Cylinder3.Support(cylinder, new Vec3(1.0f, 1.0f, 0.0f));

        support.Y.Should().BeApproximately(1.0f, 1e-5f);
        new Vec2(support.X, support.Z).Length().Should().BeApproximately(1.0f, 1e-5f);
    }

    [Fact]
    public void Cone_Mass_Matches_WebFormula()
    {
        var cone = new Cone3(Vec3.Zero, 1.0f, 2.0f, ShapeAxis3D.Y);

        MassData3D mass = cone.ComputeMass(1.0f);

        mass.Mass.Should().BeApproximately((2.0f * MathF.PI) / 3.0f, 1e-4f);
        mass.InertiaDiagonal.Y.Should().BeApproximately(0.3f * mass.Mass, 1e-4f);
        float transverse = (0.15f * mass.Mass) + (0.6f * mass.Mass * 4.0f);
        mass.InertiaDiagonal.X.Should().BeApproximately(transverse, 1e-3f);
    }

    [Fact]
    public void Cone_Apex_Sits_ThreeQuarters_AboveCenter()
    {
        var cone = new Cone3(Vec3.Zero, 1.0f, 2.0f, ShapeAxis3D.Y);

        cone.Apex.Should().Be(new Vec3(0.0f, 1.5f, 0.0f));
        cone.BaseCenter.Should().Be(new Vec3(0.0f, -0.5f, 0.0f));
    }

    [Fact]
    public void Cone_Support_Prefers_Apex_Upwards()
    {
        var cone = new Cone3(Vec3.Zero, 1.0f, 2.0f, ShapeAxis3D.Y);

        Vec3 support = Cone3.Support(cone, new Vec3(0.0f, 1.0f, 0.0f));

        support.Should().Be(new Vec3(0.0f, 1.5f, 0.0f));
    }
}
