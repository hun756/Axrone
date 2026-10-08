namespace Axrone.Geometry.Tests;

using System.Numerics;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class Aabb3DQueryTests
{
    [Fact]
    public void Ray_HitKnownDistance()
    {
        Aabb3D box = new(0.0F, 0.0F, 0.0F, 2.0F, 2.0F, 2.0F);
        Ray3D ray = new(new Vec3(-1.0F, 1.0F, 1.0F), new Vec3(1.0F, 0.0F, 0.0F));

        bool hit = box.Intersects(in ray, out float distance);

        hit.Should().BeTrue();
        distance.Should().BeApproximately(1.0F, 1e-5F);
    }

    [Fact]
    public void Ray_MissParallel_OutsideSlab()
    {
        Aabb3D box = new(0.0F, 0.0F, 0.0F, 2.0F, 2.0F, 2.0F);
        Ray3D ray = new(new Vec3(-1.0F, 5.0F, 1.0F), new Vec3(1.0F, 0.0F, 0.0F));

        bool hit = box.Intersects(in ray, out float distance);

        hit.Should().BeFalse();
        distance.Should().Be(0.0F);
    }

    [Fact]
    public void Ray_OriginInside_DistanceZero()
    {
        Aabb3D box = new(0.0F, 0.0F, 0.0F, 2.0F, 2.0F, 2.0F);
        Ray3D ray = new(new Vec3(1.0F, 1.0F, 1.0F), new Vec3(1.0F, 0.0F, 0.0F));

        bool hit = box.Intersects(in ray, out float distance);

        hit.Should().BeTrue();
        distance.Should().Be(0.0F);
    }

    [Fact]
    public void Ray_MaxDistanceClamps()
    {
        Aabb3D box = new(0.0F, 0.0F, 0.0F, 2.0F, 2.0F, 2.0F);
        Ray3D ray = new(new Vec3(-1.0F, 1.0F, 1.0F), new Vec3(1.0F, 0.0F, 0.0F));

        bool hit = box.Intersects(in ray, 0.5F, out float distance);

        hit.Should().BeFalse();
        distance.Should().Be(0.0F);
    }

    [Fact]
    public void Sphere_Intersects_TrueAndFalse()
    {
        Aabb3D box = new(0.0F, 0.0F, 0.0F, 2.0F, 2.0F, 2.0F);
        BoundingSphere3 touching = new(new Vec3(3.0F, 1.0F, 1.0F), 1.0F);
        BoundingSphere3 far = new(new Vec3(10.0F, 1.0F, 1.0F), 1.0F);

        box.Intersects(in touching).Should().BeTrue();
        box.Intersects(in far).Should().BeFalse();
    }

    [Fact]
    public void Plane_Intersects_CrossingVsOutside()
    {
        Aabb3D straddling = new(0.0F, 0.0F, 0.0F, 1.0F, 2.0F, 1.0F);
        Aabb3D fullyAbove = new(0.0F, 2.0F, 0.0F, 1.0F, 3.0F, 1.0F);
        Plane plane = new(0.0F, 1.0F, 0.0F, -1.0F);

        straddling.Intersects(in plane).Should().BeTrue();
        fullyAbove.Intersects(in plane).Should().BeFalse();
    }

    [Fact]
    public void Penetration_KnownNormalDepth()
    {
        Aabb3D a = new(0.0F, 0.0F, 0.0F, 2.0F, 2.0F, 2.0F);
        Aabb3D b = new(1.0F, 1.0F, 1.0F, 3.0F, 3.0F, 3.0F);

        bool penetrating = a.ComputePenetration(in b, out Vec3 normal, out float depth);

        penetrating.Should().BeTrue();
        normal.Should().Be(new Vec3(1.0F, 0.0F, 0.0F));
        depth.Should().BeApproximately(1.0F, 1e-5F);
    }

    [Fact]
    public void Penetration_Disjoint_False()
    {
        Aabb3D a = new(0.0F, 0.0F, 0.0F, 1.0F, 1.0F, 1.0F);
        Aabb3D b = new(5.0F, 5.0F, 5.0F, 6.0F, 6.0F, 6.0F);

        bool penetrating = a.ComputePenetration(in b, out Vec3 normal, out float depth);

        penetrating.Should().BeFalse();
        normal.Should().Be(Vec3.Zero);
        depth.Should().Be(0.0F);
    }

    [Fact]
    public void Sweep_HitKnownTime()
    {
        Aabb3D mover = new(0.0F, 0.0F, 0.0F, 1.0F, 1.0F, 1.0F);
        Aabb3D target = new(4.0F, 0.0F, 0.0F, 5.0F, 1.0F, 1.0F);
        Vec3 velocity = new(5.0F, 0.0F, 0.0F);

        bool hit = mover.Sweep(in target, in velocity, out SweepHit3 sweep);

        hit.Should().BeTrue();
        sweep.Time.Should().BeApproximately(0.6F, 1e-4F);
        sweep.Normal.Should().Be(new Vec3(1.0F, 0.0F, 0.0F));
    }

    [Fact]
    public void Sweep_StationaryOverlap_True()
    {
        Aabb3D mover = new(0.0F, 0.0F, 0.0F, 1.0F, 1.0F, 1.0F);
        Aabb3D target = new(0.5F, 0.5F, 0.5F, 2.0F, 2.0F, 2.0F);

        bool hit = mover.Sweep(in target, Vec3.Zero, out SweepHit3 sweep);

        hit.Should().BeTrue();
        sweep.Time.Should().Be(0.0F);
    }

    [Fact]
    public void Sweep_StationaryDisjoint_False()
    {
        Aabb3D mover = new(0.0F, 0.0F, 0.0F, 1.0F, 1.0F, 1.0F);
        Aabb3D target = new(5.0F, 5.0F, 5.0F, 6.0F, 6.0F, 6.0F);

        bool hit = mover.Sweep(in target, Vec3.Zero, out SweepHit3 sweep);

        hit.Should().BeFalse();
        sweep.Time.Should().Be(0.0F);
    }

    [Fact]
    public void Sweep_MissWrongDirection_False()
    {
        Aabb3D mover = new(0.0F, 0.0F, 0.0F, 1.0F, 1.0F, 1.0F);
        Aabb3D target = new(4.0F, 0.0F, 0.0F, 5.0F, 1.0F, 1.0F);
        Vec3 velocity = new(-5.0F, 0.0F, 0.0F);

        bool hit = mover.Sweep(in target, in velocity, out SweepHit3 _);

        hit.Should().BeFalse();
    }

    [Fact]
    public void GetCorners_EightValues_FirstMin_LastMixed()
    {
        Aabb3D box = new(1.0F, 2.0F, 3.0F, 4.0F, 5.0F, 6.0F);
        Span<Vec3> corners = stackalloc Vec3[8];

        box.GetCorners(corners);

        // Bit order: index 0 is the all-min corner, 7 the all-max corner, 3 a mixed corner.
        corners[0].Should().Be(new Vec3(1.0F, 2.0F, 3.0F));
        corners[3].Should().Be(new Vec3(4.0F, 5.0F, 3.0F));
        corners[7].Should().Be(new Vec3(4.0F, 5.0F, 6.0F));
    }

    [Fact]
    public void GetCorners_ShortSpan_Throws()
    {
        Aabb3D box = new(0.0F, 0.0F, 0.0F, 1.0F, 1.0F, 1.0F);
        Vec3[] corners = new Vec3[7];

        Action act = () => box.GetCorners(corners);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Transform_Identity_Preserves()
    {
        Aabb3D box = new(1.0F, 2.0F, 3.0F, 4.0F, 5.0F, 6.0F);

        Aabb3D transformed = box.Transform(Matrix4x4.Identity);

        transformed.Min.X.Should().BeApproximately(1.0F, 1e-5F);
        transformed.Min.Y.Should().BeApproximately(2.0F, 1e-5F);
        transformed.Min.Z.Should().BeApproximately(3.0F, 1e-5F);
        transformed.Max.X.Should().BeApproximately(4.0F, 1e-5F);
        transformed.Max.Y.Should().BeApproximately(5.0F, 1e-5F);
        transformed.Max.Z.Should().BeApproximately(6.0F, 1e-5F);
    }

    [Fact]
    public void Transform_Translation_Shifts()
    {
        Aabb3D box = new(1.0F, 2.0F, 3.0F, 4.0F, 5.0F, 6.0F);
        Matrix4x4 translation = Matrix4x4.CreateTranslation(10.0F, 0.0F, 0.0F);

        Aabb3D transformed = box.Transform(in translation);

        transformed.Min.X.Should().BeApproximately(11.0F, 1e-5F);
        transformed.Max.X.Should().BeApproximately(14.0F, 1e-5F);
        transformed.Min.Y.Should().BeApproximately(2.0F, 1e-5F);
        transformed.Max.Z.Should().BeApproximately(6.0F, 1e-5F);
    }

    [Fact]
    public void TryParse_ValidFormat()
    {
        bool parsed = Aabb3D.TryParse("Aabb3D(<0, 0, 0>;<1, 1, 1>)", null, out Aabb3D box);

        parsed.Should().BeTrue();
        box.Min.Should().Be(Vec3.Zero);
        box.Max.Should().Be(new Vec3(1.0F, 1.0F, 1.0F));
    }

    [Fact]
    public void TryParse_Garbage_False()
    {
        Aabb3D.TryParse("nope", null, out _).Should().BeFalse();
        Aabb3D.TryParse("Aabb3D(", null, out _).Should().BeFalse();
        Aabb3D.TryParse(string.Empty, null, out _).Should().BeFalse();
    }

    [Fact]
    public void Parse_Invalid_ThrowsFormatException()
    {
        Action act = () => Aabb3D.Parse("nope");

        act.Should().Throw<FormatException>();
    }
}