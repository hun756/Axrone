using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public class ShapeTests
{
    [Fact]
    public void Sphere_Mass_Matches_Analytic()
    {
        var sphere = new Sphere3(Vec3.Zero, 1.0f);

        MassData3D mass = sphere.ComputeMass(1.0f);

        mass.Mass.Should().BeApproximately((4.0f / 3.0f) * MathF.PI, 1e-5f);
        mass.InertiaDiagonal.X.Should().BeApproximately(0.4f * mass.Mass, 1e-5f);
        mass.Centroid.Should().Be(Vec3.Zero);
    }

    [Fact]
    public void Sphere_Rejects_NegativeRadius()
    {
        var act = () => new Sphere3(Vec3.Zero, -1.0f);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Sphere_Support_Points_AlongDirection()
    {
        var sphere = new Sphere3(new Vec3(1.0f, 0.0f, 0.0f), 2.0f);

        Vec3 support = Sphere3.Support(sphere, new Vec3(1.0f, 0.0f, 0.0f));

        support.Should().Be(new Vec3(3.0f, 0.0f, 0.0f));
    }

    [Fact]
    public void Box_Mass_Matches_Analytic()
    {
        var box = new Box3(Vec3.Zero, new Vec3(1.0f, 2.0f, 3.0f));

        MassData3D mass = box.ComputeMass(1.0f);

        mass.Mass.Should().BeApproximately(48.0f, 1e-4f);
        mass.InertiaDiagonal.X.Should().BeApproximately((48.0f / 3.0f) * (4.0f + 9.0f), 1e-3f);
        mass.InertiaDiagonal.Y.Should().BeApproximately((48.0f / 3.0f) * (1.0f + 9.0f), 1e-3f);
        mass.InertiaDiagonal.Z.Should().BeApproximately((48.0f / 3.0f) * (1.0f + 4.0f), 1e-3f);
    }

    [Fact]
    public void Box_Support_Selects_CorrectCorner()
    {
        var box = new Box3(Vec3.Zero, new Vec3(1.0f, 1.0f, 1.0f));

        Vec3 support = Box3.Support(box, new Vec3(1.0f, -1.0f, 1.0f));

        support.Should().Be(new Vec3(1.0f, -1.0f, 1.0f));
    }

    [Fact]
    public void Box_Rejects_NegativeExtents()
    {
        var act = () => new Box3(Vec3.Zero, new Vec3(1.0f, -1.0f, 1.0f));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ZeroDensity_Yields_MasslessData()
    {
        var sphere = new Sphere3(Vec3.Zero, 1.0f);

        sphere.ComputeMass(0.0f).Should().Be(MassData3D.Zero);
    }
}
