namespace Axrone.Geometry.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Numeric;

public class Aabb2DTests
{
    [Fact]
    public void Empty_IsInvalid()
    {
        Aabb2D empty = Aabb2D.Empty;

        empty.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Zero_IsValid_AndZeroArea()
    {
        Aabb2D zero = Aabb2D.Zero;

        zero.IsValid.Should().BeTrue();
        zero.Area.Should().Be(0F);
    }

    [Fact]
    public void Center_Extents_Size_MatchKnownValues()
    {
        var box = new Aabb2D(1F, 2F, 5F, 6F);

        box.Center.Should().Be(new Vec2(3F, 4F));
        box.Extents.Should().Be(new Vec2(2F, 2F));
        box.Size.Should().Be(new Vec2(4F, 4F));
    }

    [Fact]
    public void Area_ExcludesNegative()
    {
        var inverted = new Aabb2D(5F, 6F, 1F, 2F);

        inverted.Area.Should().Be(0F);
    }

    [Fact]
    public void Contains_Point_EdgesInclusive()
    {
        var box = new Aabb2D(0F, 0F, 4F, 4F);

        box.Contains(new Vec2(0F, 0F)).Should().BeTrue();
        box.Contains(new Vec2(4F, 4F)).Should().BeTrue();
        box.Contains(new Vec2(2F, 3F)).Should().BeTrue();
        box.Contains(new Vec2(4.001F, 2F)).Should().BeFalse();
        box.Contains(new Vec2(-0.001F, 2F)).Should().BeFalse();
    }

    [Fact]
    public void Contains_Box_InsideVsOutside()
    {
        var box = new Aabb2D(0F, 0F, 10F, 10F);

        box.Contains(new Aabb2D(1F, 1F, 9F, 9F)).Should().BeTrue();
        box.Contains(new Aabb2D(0F, 0F, 10F, 10F)).Should().BeTrue();
        box.Contains(new Aabb2D(5F, 5F, 15F, 15F)).Should().BeFalse();
        box.Contains(new Aabb2D(-5F, -5F, 5F, 5F)).Should().BeFalse();
    }

    [Fact]
    public void Overlaps_TouchingEdges_Count()
    {
        var box = new Aabb2D(0F, 0F, 2F, 2F);

        box.Overlaps(new Aabb2D(2F, 0F, 4F, 2F)).Should().BeTrue();
        box.Overlaps(new Aabb2D(1F, 1F, 3F, 3F)).Should().BeTrue();
        box.Overlaps(new Aabb2D(2.001F, 0F, 4F, 2F)).Should().BeFalse();
    }

    [Fact]
    public void MergedWith_TakesComponentwiseMinMax()
    {
        var box = new Aabb2D(0F, 0F, 2F, 2F);

        Aabb2D merged = box.MergedWith(new Aabb2D(1F, 1F, 4F, 5F));

        merged.Min.Should().Be(new Vec2(0F, 0F));
        merged.Max.Should().Be(new Vec2(4F, 5F));
    }

    [Fact]
    public void Expanded_GrowsSymmetrically()
    {
        var box = new Aabb2D(0F, 0F, 4F, 4F);

        Aabb2D grown = box.Expanded(1F);

        grown.Min.Should().Be(new Vec2(-1F, -1F));
        grown.Max.Should().Be(new Vec2(5F, 5F));
        grown.Center.Should().Be(box.Center);
    }

    [Fact]
    public void ClosestPoint_ClampsOutside_AndIdentityInside()
    {
        var box = new Aabb2D(0F, 0F, 4F, 4F);

        box.ClosestPoint(new Vec2(9F, -3F)).Should().Be(new Vec2(4F, 0F));
        box.ClosestPoint(new Vec2(2F, 2F)).Should().Be(new Vec2(2F, 2F));
    }

    [Fact]
    public void DistanceSquared_ZeroInside_KnownValueOutside()
    {
        var box = new Aabb2D(0F, 0F, 4F, 4F);

        box.DistanceSquared(new Vec2(1F, 1F)).Should().Be(0F);
        box.DistanceSquared(new Vec2(9F, 9F)).Should().Be(50F);
    }
}