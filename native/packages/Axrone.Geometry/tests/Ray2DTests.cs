namespace Axrone.Geometry.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class Ray2DTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void Constructor_Normalizes_And_GuardsReciprocal()
    {
        var ray = new Ray2D(new Vec2(1, 2), new Vec2(0, 4));

        ray.Direction.X.Should().BeApproximately(0f, Tolerance);
        ray.Direction.Y.Should().BeApproximately(1f, Tolerance);
        MathF.Abs(ray.InvDirection.X).Should().Be(1e9f);
        ray.InvDirection.Y.Should().BeApproximately(1f, Tolerance);
        ray.At(3f).Should().Be(new Vec2(1, 5));
    }

    [Fact]
    public void Constructor_ZeroDirection_Throws()
    {
        Action act = () => _ = new Ray2D(new Vec2(0, 0), Vec2.Zero);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Slab_HitKnownDistance()
    {
        var box = new Aabb2D(0f, 0f, 2f, 2f);
        var ray = new Ray2D(new Vec2(-1, 1), new Vec2(1, 0));

        box.Intersects(in ray, out float distance).Should().BeTrue();
        distance.Should().BeApproximately(1f, Tolerance);
    }

    [Fact]
    public void Slab_Miss_And_WindowLimit()
    {
        var box = new Aabb2D(0f, 0f, 2f, 2f);
        var parallel = new Ray2D(new Vec2(-1, 5), new Vec2(1, 0));

        box.Intersects(in parallel, out _).Should().BeFalse();
        box.Intersects(new Ray2D(new Vec2(-5, 1), new Vec2(1, 0)), 2f, out _).Should().BeFalse();
    }

    [Fact]
    public void Slab_OriginInside_YieldsZero()
    {
        var box = new Aabb2D(0f, 0f, 2f, 2f);
        var ray = new Ray2D(new Vec2(1, 1), new Vec2(1, 0));

        box.Intersects(in ray, out float distance).Should().BeTrue();
        distance.Should().Be(0f);
    }

    [Fact]
    public void Equality_RoundTrips()
    {
        var left = new Ray2D(new Vec2(0, 0), new Vec2(1, 0));
        var right = new Ray2D(new Vec2(0, 0), new Vec2(1, 0));
        var other = new Ray2D(new Vec2(0, 0), new Vec2(0, 1));

        (left == right).Should().BeTrue();
        (left != other).Should().BeTrue();
        left.GetHashCode().Should().Be(right.GetHashCode());
    }
}
