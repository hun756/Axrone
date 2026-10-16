using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public class Distance3DTests
{
    [Fact]
    public void SphereSphere_Separated_ReportsGapAndWitnesses()
    {
        var a = new Sphere3(new Vec3(0.0f, 0.0f, 0.0f), 1.0f);
        var b = new Sphere3(new Vec3(5.0f, 0.0f, 0.0f), 1.0f);

        bool separated = Gjk3D.ComputeDistance(a, b, out float distance, out Vec3 pointA, out Vec3 pointB);

        separated.Should().BeTrue();
        distance.Should().BeApproximately(3.0f, 1e-4f);
        pointA.X.Should().BeApproximately(1.0f, 1e-4f);
        pointB.X.Should().BeApproximately(4.0f, 1e-4f);
    }

    [Fact]
    public void SphereSphere_Overlapping_ReportsContact()
    {
        var a = new Sphere3(Vec3.Zero, 1.0f);
        var b = new Sphere3(new Vec3(1.0f, 0.0f, 0.0f), 1.0f);

        bool separated = Gjk3D.ComputeDistance(a, b, out float distance, out _, out _);

        separated.Should().BeFalse();
        distance.Should().Be(0.0f);
    }

    [Fact]
    public void BoxBox_Separated_ReportsGap()
    {
        var a = new Box3(Vec3.Zero, new Vec3(1.0f, 1.0f, 1.0f));
        var b = new Box3(new Vec3(5.0f, 0.0f, 0.0f), new Vec3(1.0f, 1.0f, 1.0f));

        bool separated = Gjk3D.ComputeDistance(a, b, out float distance, out Vec3 pointA, out Vec3 pointB);

        separated.Should().BeTrue();
        distance.Should().BeApproximately(3.0f, 1e-3f);
        pointA.X.Should().BeApproximately(1.0f, 1e-3f);
        pointB.X.Should().BeApproximately(4.0f, 1e-3f);
    }

    [Fact]
    public void BoxBox_Overlapping_ReportsContact()
    {
        var a = new Box3(Vec3.Zero, new Vec3(1.0f, 1.0f, 1.0f));
        var b = new Box3(new Vec3(1.0f, 0.0f, 0.0f), new Vec3(1.0f, 1.0f, 1.0f));

        bool separated = Gjk3D.ComputeDistance(a, b, out float distance, out _, out _);

        separated.Should().BeFalse();
        distance.Should().Be(0.0f);
    }

    [Fact]
    public void SphereBox_Separated_ReportsGap()
    {
        var sphere = new Sphere3(new Vec3(0.0f, 4.0f, 0.0f), 1.0f);
        var box = new Box3(Vec3.Zero, new Vec3(1.0f, 1.0f, 1.0f));

        bool separated = Gjk3D.ComputeDistance(sphere, box, out float distance, out _, out _);

        separated.Should().BeTrue();
        distance.Should().BeApproximately(2.0f, 1e-3f);
    }

    [Fact]
    public void SphereInsideBox_ReportsContact()
    {
        var sphere = new Sphere3(Vec3.Zero, 0.5f);
        var box = new Box3(Vec3.Zero, new Vec3(2.0f, 2.0f, 2.0f));

        bool separated = Gjk3D.ComputeDistance(sphere, box, out float distance, out _, out _);

        separated.Should().BeFalse();
        distance.Should().Be(0.0f);
    }
}
