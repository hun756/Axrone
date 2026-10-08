namespace Axrone.Geometry.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Numeric;

public class Ray3DTests
{
    private const float Tolerance = 1e-6f;

    [Fact]
    public void Ctor_NormalizesDirection()
    {
        var ray = new Ray3D(new Vec3(1F, 2F, 3F), new Vec3(0F, 0F, 5F));

        ray.Direction.X.Should().BeApproximately(0F, Tolerance);
        ray.Direction.Y.Should().BeApproximately(0F, Tolerance);
        ray.Direction.Z.Should().BeApproximately(1F, Tolerance);
        ray.Direction.Length().Should().BeApproximately(1F, Tolerance);
    }

    [Fact]
    public void Ctor_ZeroDirection_ThrowsArgumentException()
    {
        Action act = () => _ = new Ray3D(Vec3.Zero, Vec3.Zero);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_NearZeroDirection_Throws()
    {
        Action act = () => _ = new Ray3D(Vec3.Zero, new Vec3(1e-7F, 0F, 0F));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void At_ReturnsKnownPoint()
    {
        var ray = new Ray3D(new Vec3(1F, 2F, 3F), new Vec3(0F, 0F, 1F));

        Vec3 point = ray.At(4F);

        point.X.Should().BeApproximately(1F, Tolerance);
        point.Y.Should().BeApproximately(2F, Tolerance);
        point.Z.Should().BeApproximately(7F, Tolerance);
    }

    [Fact]
    public void InvDirection_GuardedAgainstZero()
    {
        var ray = new Ray3D(Vec3.Zero, new Vec3(0F, 1F, 0F));

        ray.InvDirection.X.Should().BeApproximately(1e9f, Tolerance);
        ray.InvDirection.Y.Should().BeApproximately(1F, Tolerance);
    }

    [Fact]
    public void Intersects_Triangle_HitKnownValues()
    {
        var triangle = new Triangle3(new Vec3(0F, 0F, 0F), new Vec3(1F, 0F, 0F), new Vec3(0F, 1F, 0F));
        var ray = new Ray3D(new Vec3(0.25F, 0.25F, 1F), new Vec3(0F, 0F, -1F));

        bool hitAnything = ray.Intersects(triangle, out TriangleHit3 hit);

        hitAnything.Should().BeTrue();

        // The direction is unit length and the ray starts one unit above the plane.
        hit.Distance.Should().BeApproximately(1F, Tolerance);
        hit.U.Should().BeApproximately(0.25f, Tolerance);
        hit.V.Should().BeApproximately(0.25f, Tolerance);
        hit.Point.X.Should().BeApproximately(0.25f, Tolerance);
        hit.Point.Y.Should().BeApproximately(0.25f, Tolerance);
        hit.Point.Z.Should().BeApproximately(0F, Tolerance);
    }

    [Fact]
    public void Intersects_ParallelRay_Misses()
    {
        var triangle = new Triangle3(new Vec3(0F, 0F, 0F), new Vec3(1F, 0F, 0F), new Vec3(0F, 1F, 0F));
        var ray = new Ray3D(new Vec3(0.25F, 0.25F, 1F), new Vec3(1F, 0F, 0F));

        bool hitAnything = ray.Intersects(triangle, out TriangleHit3 _);

        hitAnything.Should().BeFalse();
    }

    [Fact]
    public void Intersects_RayPointingAway_Misses()
    {
        var triangle = new Triangle3(new Vec3(0F, 0F, 0F), new Vec3(1F, 0F, 0F), new Vec3(0F, 1F, 0F));
        var ray = new Ray3D(new Vec3(0.25F, 0.25F, 1F), new Vec3(0F, 0F, 1F));

        bool hitAnything = ray.Intersects(triangle, out TriangleHit3 _);

        hitAnything.Should().BeFalse();
    }

    [Fact]
    public void Equality_ByValue()
    {
        var a = new Ray3D(new Vec3(1F, 2F, 3F), new Vec3(0F, 1F, 0F));
        var b = new Ray3D(new Vec3(1F, 2F, 3F), new Vec3(0F, 1F, 0F));
        var c = new Ray3D(new Vec3(1F, 2F, 4F), new Vec3(0F, 1F, 0F));

        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        (a == c).Should().BeFalse();
        (a != c).Should().BeTrue();
        a.Equals(b).Should().BeTrue();
        a.Equals(c).Should().BeFalse();
        a.Equals((object)b).Should().BeTrue();
        a.Equals("not a ray").Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}