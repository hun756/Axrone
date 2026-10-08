namespace Axrone.Geometry.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Numeric;

public class Triangle3Tests
{
    private const float Tolerance = 1e-6f;

    [Fact]
    public void Normal_IsUnitLength_AndFacing()
    {
        var triangle = new Triangle3(new Vec3(0F, 0F, 0F), new Vec3(1F, 0F, 0F), new Vec3(0F, 1F, 0F));

        Vec3 normal = triangle.Normal;

        normal.X.Should().BeApproximately(0F, Tolerance);
        normal.Y.Should().BeApproximately(0F, Tolerance);
        normal.Z.Should().BeApproximately(1F, Tolerance);
        normal.Length().Should().BeApproximately(1F, Tolerance);
    }

    [Fact]
    public void Normal_FlipsByWinding()
    {
        var reversed = new Triangle3(new Vec3(0F, 0F, 0F), new Vec3(0F, 1F, 0F), new Vec3(1F, 0F, 0F));

        Vec3 normal = reversed.Normal;

        normal.X.Should().BeApproximately(0F, Tolerance);
        normal.Y.Should().BeApproximately(0F, Tolerance);
        normal.Z.Should().BeApproximately(-1F, Tolerance);
    }

    [Fact]
    public void Equality_ByValue()
    {
        var a = new Triangle3(new Vec3(0F, 0F, 0F), new Vec3(1F, 0F, 0F), new Vec3(0F, 1F, 0F));
        var b = new Triangle3(new Vec3(0F, 0F, 0F), new Vec3(1F, 0F, 0F), new Vec3(0F, 1F, 0F));
        var c = new Triangle3(new Vec3(0F, 0F, 0F), new Vec3(1F, 0F, 0F), new Vec3(0F, 1F, 1F));

        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
        (a == c).Should().BeFalse();
        (a != c).Should().BeTrue();
        a.Equals(b).Should().BeTrue();
        a.Equals(c).Should().BeFalse();
        a.Equals((object)b).Should().BeTrue();
        a.Equals("not a triangle").Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}