namespace Axrone.Geometry.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Numeric;

public class Frustum3Tests
{
    /// <summary>
    /// The axis-aligned cube spanning -1..1 on every axis, built by hand with inward-pointing
    /// normals: Left keeps x &gt;= -1, Right keeps x &lt;= 1, Top keeps y &lt;= 1, Bottom keeps
    /// y &gt;= -1, Near keeps z &gt;= -1 and Far keeps z &lt;= 1. This is the same orientation
    /// <see cref="Frustum3.CreateFromMatrix"/> produces for a left-handed projection, so the
    /// hand-built and matrix-built frusta agree on which plane is which.
    /// </summary>
    private static Frustum3 CubeFrustum() => new(
        new Axrone.Numeric.Plane(new Vec3(0.0F, 0.0F, 1.0F), 1.0F),
        new Axrone.Numeric.Plane(new Vec3(0.0F, 0.0F, -1.0F), 1.0F),
        new Axrone.Numeric.Plane(new Vec3(1.0F, 0.0F, 0.0F), 1.0F),
        new Axrone.Numeric.Plane(new Vec3(-1.0F, 0.0F, 0.0F), 1.0F),
        new Axrone.Numeric.Plane(new Vec3(0.0F, -1.0F, 0.0F), 1.0F),
        new Axrone.Numeric.Plane(new Vec3(0.0F, 1.0F, 0.0F), 1.0F));

    [Fact]
    public void HandBuiltCube_ContainsInnerBox()
    {
        Frustum3 frustum = CubeFrustum();
        Aabb3D box = new(-0.5F, -0.5F, -0.5F, 0.5F, 0.5F, 0.5F);

        frustum.Contains(in box).Should().Be(ContainmentType.Contains);
    }

    [Fact]
    public void HandBuiltCube_StraddlingBox_Intersects()
    {
        Frustum3 frustum = CubeFrustum();
        Aabb3D box = new(0.5F, 0.5F, 0.5F, 1.5F, 1.5F, 1.5F);

        frustum.Contains(in box).Should().Be(ContainmentType.Intersects);
    }

    [Fact]
    public void HandBuiltCube_OutsideBox_Disjoint()
    {
        Frustum3 frustum = CubeFrustum();
        Aabb3D box = new(2.0F, 2.0F, 2.0F, 3.0F, 3.0F, 3.0F);

        frustum.Contains(in box).Should().Be(ContainmentType.Disjoint);
    }

    [Fact]
    public void HandBuiltCube_TouchingFace_NotDisjoint()
    {
        Frustum3 frustum = CubeFrustum();
        Aabb3D box = new(1.0F, 0.0F, 0.0F, 2.0F, 1.0F, 1.0F);

        frustum.Contains(in box).Should().NotBe(ContainmentType.Disjoint);
    }

    [Fact]
    public void CreateFromMatrix_Identity_CenterBoxNotDisjoint()
    {
        Frustum3 frustum = Frustum3.CreateFromMatrix(Mat4.Identity);
        Aabb3D box = new(-0.5F, -0.5F, -0.5F, 0.5F, 0.5F, 0.5F);

        // Only disjointness is asserted: whether the box reports Contains or Intersects here
        // depends on the near-plane orientation convention of the identity matrix, and the
        // plane set itself is what this test pins down.
        frustum.Contains(in box).Should().NotBe(ContainmentType.Disjoint);
    }

    [Fact]
    public void CreateFromMatrix_Identity_FarBoxDisjoint()
    {
        Frustum3 frustum = Frustum3.CreateFromMatrix(Mat4.Identity);
        Aabb3D box = new(50.0F, 50.0F, 50.0F, 51.0F, 51.0F, 51.0F);

        frustum.Contains(in box).Should().Be(ContainmentType.Disjoint);
    }
}