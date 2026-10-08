namespace Axrone.Geometry.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Numeric;

public class Aabb3DCoreTests
{
    [Fact]
    public void Empty_IsInvalid_And_IsEmpty()
    {
        Aabb3D empty = Aabb3D.Empty;

        empty.IsValid.Should().BeFalse();
        empty.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Zero_IsValid()
    {
        Aabb3D zero = Aabb3D.Zero;

        zero.IsValid.Should().BeTrue();
        zero.IsEmpty.Should().BeFalse();
        zero.Min.Should().Be(Vec3.Zero);
        zero.Max.Should().Be(Vec3.Zero);
    }

    [Fact]
    public void UnitCube_CenterZero_SizeOne()
    {
        Aabb3D cube = Aabb3D.UnitCube;

        cube.Min.Should().Be(new Vec3(-0.5F));
        cube.Max.Should().Be(new Vec3(0.5F));
        cube.Center.Should().Be(Vec3.Zero);
        cube.Size.Should().Be(new Vec3(1F));
        cube.Extents.Should().Be(new Vec3(0.5F));
    }

    [Fact]
    public void FromCenterExtents_AbsoluteExtents()
    {
        Aabb3D box = Aabb3D.FromCenterExtents(new Vec3(1F, 2F, 3F), new Vec3(-2F, -2F, -2F));

        box.Min.Should().Be(new Vec3(-1F, 0F, 1F));
        box.Max.Should().Be(new Vec3(3F, 4F, 5F));
    }

    [Fact]
    public void FromSphere_CoversCenterPlusMinusRadius()
    {
        Aabb3D box = Aabb3D.FromSphere(new BoundingSphere3(new Vec3(1F, 2F, 3F), 4F));

        box.Min.Should().Be(new Vec3(-3F, -2F, -1F));
        box.Max.Should().Be(new Vec3(5F, 6F, 7F));
        box.Center.Should().Be(new Vec3(1F, 2F, 3F));
        box.Size.Should().Be(new Vec3(8F));
    }

    [Fact]
    public void CreateFromPoints_EmptySpan_ReturnsEmpty()
    {
        Aabb3D box = Aabb3D.CreateFromPoints();

        box.Should().Be(Aabb3D.Empty);
        box.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void CreateFromPoints_KnownSet()
    {
        Vec3[] points = [new Vec3(1F, 5F, -2F), new Vec3(3F, 0F, 4F)];

        Aabb3D box = Aabb3D.CreateFromPoints(points);

        box.Min.Should().Be(new Vec3(1F, 0F, -2F));
        box.Max.Should().Be(new Vec3(3F, 5F, 4F));
    }

    [Fact]
    public void CreateMerged_SpansBoth()
    {
        var a = new Aabb3D(0F, 0F, 0F, 2F, 2F, 2F);
        var b = new Aabb3D(1F, -1F, 1F, 4F, 0F, 5F);

        Aabb3D merged = Aabb3D.CreateMerged(a, b);

        merged.Min.Should().Be(new Vec3(0F, -1F, 0F));
        merged.Max.Should().Be(new Vec3(4F, 2F, 5F));
    }

    [Fact]
    public void Overlaps_TouchingFaces_True()
    {
        var box = new Aabb3D(0F, 0F, 0F, 2F, 2F, 2F);

        box.Overlaps(new Aabb3D(2F, 0F, 0F, 4F, 2F, 2F)).Should().BeTrue();
        box.Overlaps(new Aabb3D(1F, 1F, 1F, 3F, 3F, 3F)).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_Separated_False()
    {
        var box = new Aabb3D(0F, 0F, 0F, 2F, 2F, 2F);

        box.Overlaps(new Aabb3D(2.001F, 0F, 0F, 4F, 2F, 2F)).Should().BeFalse();
        box.Overlaps(new Aabb3D(0F, 2.001F, 0F, 2F, 4F, 2F)).Should().BeFalse();
        box.Overlaps(new Aabb3D(0F, 0F, 2.001F, 2F, 2F, 4F)).Should().BeFalse();
    }

    [Fact]
    public void Contains_Box_InsideVsPartial()
    {
        var box = new Aabb3D(0F, 0F, 0F, 10F, 10F, 10F);

        box.Contains(new Aabb3D(1F, 1F, 1F, 9F, 9F, 9F)).Should().BeTrue();
        box.Contains(new Aabb3D(0F, 0F, 0F, 10F, 10F, 10F)).Should().BeTrue();
        box.Contains(new Aabb3D(5F, 5F, 5F, 15F, 15F, 15F)).Should().BeFalse();
        box.Contains(new Aabb3D(-5F, -5F, -5F, 5F, 5F, 5F)).Should().BeFalse();
    }

    [Fact]
    public void ContainsWithState_ThreeStates()
    {
        var box = new Aabb3D(0F, 0F, 0F, 10F, 10F, 10F);

        box.ContainsWithState(new Aabb3D(1F, 1F, 1F, 9F, 9F, 9F)).Should().Be(ContainmentType.Contains);
        box.ContainsWithState(new Aabb3D(5F, 5F, 5F, 15F, 15F, 15F)).Should().Be(ContainmentType.Intersects);
        box.ContainsWithState(new Aabb3D(20F, 20F, 20F, 30F, 30F, 30F)).Should().Be(ContainmentType.Disjoint);
    }

    [Fact]
    public void Contains_Point_EdgesInclusive()
    {
        var box = new Aabb3D(0F, 0F, 0F, 4F, 4F, 4F);

        box.Contains(new Vec3(0F, 0F, 0F)).Should().BeTrue();
        box.Contains(new Vec3(4F, 4F, 4F)).Should().BeTrue();
        box.Contains(new Vec3(2F, 3F, 1F)).Should().BeTrue();
        box.Contains(new Vec3(4.001F, 2F, 2F)).Should().BeFalse();
        box.Contains(new Vec3(2F, -0.001F, 2F)).Should().BeFalse();
        box.Contains(new Vec3(2F, 2F, -1F)).Should().BeFalse();
    }

    [Fact]
    public void TryIntersect_Overlap_ReturnsBox()
    {
        var a = new Aabb3D(0F, 0F, 0F, 4F, 4F, 4F);
        var b = new Aabb3D(2F, 1F, -1F, 6F, 5F, 3F);

        bool hit = a.TryIntersect(b, out Aabb3D intersection);

        hit.Should().BeTrue();
        intersection.Min.Should().Be(new Vec3(2F, 1F, 0F));
        intersection.Max.Should().Be(new Vec3(4F, 4F, 3F));
    }

    [Fact]
    public void TryIntersect_Disjoint_FalseAndEmpty()
    {
        var a = new Aabb3D(0F, 0F, 0F, 1F, 1F, 1F);
        var b = new Aabb3D(2F, 2F, 2F, 3F, 3F, 3F);

        bool hit = a.TryIntersect(b, out Aabb3D intersection);

        hit.Should().BeFalse();
        intersection.Should().Be(Aabb3D.Empty);
    }

    [Fact]
    public void Expanded_ByMargin()
    {
        var box = new Aabb3D(0F, 0F, 0F, 4F, 4F, 4F);

        Aabb3D uniform = box.Expanded(1F);

        uniform.Min.Should().Be(new Vec3(-1F));
        uniform.Max.Should().Be(new Vec3(5F));

        Aabb3D perAxis = box.Expanded(new Vec3(1F, 2F, 3F));

        perAxis.Min.Should().Be(new Vec3(-1F, -2F, -3F));
        perAxis.Max.Should().Be(new Vec3(5F, 6F, 7F));
    }

    [Fact]
    public void Translated_ShiftsBothCorners()
    {
        var box = new Aabb3D(0F, 0F, 0F, 4F, 4F, 4F);

        Aabb3D moved = box.Translated(new Vec3(10F, -1F, 0.5F));

        moved.Min.Should().Be(new Vec3(10F, -1F, 0.5F));
        moved.Max.Should().Be(new Vec3(14F, 3F, 4.5F));
        moved.Size.Should().Be(box.Size);
    }

    [Fact]
    public void SurfaceArea_UnitCube_Six()
    {
        Aabb3D.UnitCube.SurfaceArea().Should().Be(6F);

        var box = new Aabb3D(0F, 0F, 0F, 2F, 3F, 4F);
        box.SurfaceArea().Should().Be(2F * ((6F) + (12F) + (8F)));
    }

    [Fact]
    public void Volume_KnownValue()
    {
        var box = new Aabb3D(0F, 0F, 0F, 2F, 3F, 4F);

        box.Volume().Should().Be(24F);
    }

    [Fact]
    public void Extrude_ZAxis_MapsComponents()
    {
        var flat = new Aabb2D(0F, 0F, 2F, 4F);

        Aabb3D solid = flat.Extrude(1F, 3F, Axis.Z);

        solid.Min.Should().Be(new Vec3(0F, 0F, 1F));
        solid.Max.Should().Be(new Vec3(2F, 4F, 3F));
    }

    [Fact]
    public void Triangle_GetBounds_KnownBox()
    {
        var triangle = new Triangle3(new Vec3(0F, 0F, 0F), new Vec3(1F, 0F, 0F), new Vec3(0F, 2F, 3F));

        Aabb3D bounds = triangle.GetBounds();

        bounds.Min.Should().Be(new Vec3(0F, 0F, 0F));
        bounds.Max.Should().Be(new Vec3(1F, 2F, 3F));
    }
}
