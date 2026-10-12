namespace Axrone.Geometry.Tests;

using System;
using Xunit;
using FluentAssertions;
using Axrone.Numeric;

public class BoundingSphere3Tests
{
    [Fact]
    public void FromAabb_CoversBox()
    {
        var box = new Aabb3D(0.0F, 0.0F, 0.0F, 2.0F, 4.0F, 6.0F);

        BoundingSphere3 sphere = BoundingSphere3.FromAabb(in box);

        sphere.Center.Should().Be(new Vec3(1.0F, 2.0F, 3.0F));
        sphere.Radius.Should().BeApproximately(MathF.Sqrt(14.0F), 1e-5F);
        sphere.Contains(new Vec3(0.0F, 0.0F, 0.0F)).Should().BeTrue();
        sphere.Contains(new Vec3(2.0F, 4.0F, 6.0F)).Should().BeTrue();
    }

    [Fact]
    public void Ctor_ClampsNegativeRadius_ToZero()
    {
        var sphere = new BoundingSphere3(new Vec3(1F, 2F, 3F), -4F);

        sphere.Center.Should().Be(new Vec3(1F, 2F, 3F));
        sphere.Radius.Should().Be(0F);
    }

    [Fact]
    public void Contains_CenterAndSurface_True_OutsideFalse()
    {
        var sphere = new BoundingSphere3(new Vec3(0F), 5F);

        sphere.Contains(new Vec3(0F)).Should().BeTrue();
        sphere.Contains(new Vec3(5F, 0F, 0F)).Should().BeTrue();
        sphere.Contains(new Vec3(6F, 0F, 0F)).Should().BeFalse();
    }

    [Fact]
    public void Intersects_TouchingSpheres_True()
    {
        var left = new BoundingSphere3(new Vec3(0F), 5F);
        var right = new BoundingSphere3(new Vec3(10F, 0F, 0F), 5F);

        left.Intersects(right).Should().BeTrue();
        right.Intersects(left).Should().BeTrue();
    }

    [Fact]
    public void Intersects_SeparatedSpheres_False()
    {
        var left = new BoundingSphere3(new Vec3(0F), 5F);
        var right = new BoundingSphere3(new Vec3(11F, 0F, 0F), 5F);

        left.Intersects(right).Should().BeFalse();
        right.Intersects(left).Should().BeFalse();
    }

    [Fact]
    public void Equality_ByValue()
    {
        var left = new BoundingSphere3(new Vec3(1F, 2F, 3F), 4F);
        var same = new BoundingSphere3(new Vec3(1F, 2F, 3F), 4F);
        var otherRadius = new BoundingSphere3(new Vec3(1F, 2F, 3F), 5F);

        left.Should().Be(same);
        (left == same).Should().BeTrue();
        (left != otherRadius).Should().BeTrue();
        left.GetHashCode().Should().Be(same.GetHashCode());
    }
}