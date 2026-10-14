using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public class Penetration3DTests
{
    [Fact]
    public void SphereSphere_Reports_DepthAndNormal()
    {
        var a = new Sphere3(Vec3.Zero, 1.0f);
        var b = new Sphere3(new Vec3(1.5f, 0.0f, 0.0f), 1.0f);

        bool hit = Penetration3D.SphereSphere(a, b, out Manifold3D manifold);

        hit.Should().BeTrue();
        manifold.Count.Should().Be(1);
        manifold.Normal.X.Should().BeApproximately(-1.0f, 1e-5f);
        manifold[0].Separation.Should().BeApproximately(-0.5f, 1e-5f);
    }

    [Fact]
    public void SphereSphere_Disjoint_Reports_NoContact()
    {
        var a = new Sphere3(Vec3.Zero, 1.0f);
        var b = new Sphere3(new Vec3(3.0f, 0.0f, 0.0f), 1.0f);

        Penetration3D.SphereSphere(a, b, out _).Should().BeFalse();
    }

    [Fact]
    public void SphereBox_FaceContact_Reports_Depth()
    {
        var sphere = new Sphere3(new Vec3(0.0f, 1.5f, 0.0f), 1.0f);
        var box = new Box3(Vec3.Zero, new Vec3(1.0f, 1.0f, 1.0f));

        bool hit = Penetration3D.SphereBox(sphere, box, RigidTransform3D.Identity, out Manifold3D manifold);

        hit.Should().BeTrue();
        manifold.Normal.Y.Should().BeApproximately(1.0f, 1e-5f);
        manifold[0].Separation.Should().BeApproximately(-0.5f, 1e-5f);
    }

    [Fact]
    public void SphereBox_Disjoint_Reports_NoContact()
    {
        var sphere = new Sphere3(new Vec3(0.0f, 5.0f, 0.0f), 1.0f);
        var box = new Box3(Vec3.Zero, new Vec3(1.0f, 1.0f, 1.0f));

        Penetration3D.SphereBox(sphere, box, RigidTransform3D.Identity, out _).Should().BeFalse();
    }

    [Fact]
    public void SphereInsideBox_Pushes_OutDeepestAxis()
    {
        var sphere = new Sphere3(new Vec3(0.9f, 0.0f, 0.0f), 0.5f);
        var box = new Box3(Vec3.Zero, new Vec3(1.0f, 1.0f, 1.0f));

        bool hit = Penetration3D.SphereBox(sphere, box, RigidTransform3D.Identity, out Manifold3D manifold);

        hit.Should().BeTrue();
        manifold.Normal.X.Should().BeApproximately(1.0f, 1e-5f);
        manifold[0].Separation.Should().BeApproximately(-0.6f, 1e-5f);
    }

    [Fact]
    public void BoxBox_FaceStack_Reports_TwoPoints()
    {
        var boxA = new Box3(new Vec3(0.0f, 1.0f, 0.0f), new Vec3(1.0f, 1.0f, 1.0f));
        var boxB = new Box3(Vec3.Zero, new Vec3(1.0f, 1.0f, 1.0f));

        bool hit = Penetration3D.BoxBox(boxA, RigidTransform3D.Identity, boxB, RigidTransform3D.Identity, out Manifold3D manifold);

        hit.Should().BeTrue();
        manifold.Normal.Y.Should().BeApproximately(1.0f, 1e-4f);
        manifold.Count.Should().Be(2);
        manifold[0].Separation.Should().BeApproximately(-1.0f, 1e-3f);
    }

    [Fact]
    public void BoxBox_Disjoint_Reports_NoContact()
    {
        var boxA = new Box3(Vec3.Zero, new Vec3(1.0f, 1.0f, 1.0f));
        var boxB = new Box3(new Vec3(5.0f, 0.0f, 0.0f), new Vec3(1.0f, 1.0f, 1.0f));

        Penetration3D.BoxBox(boxA, RigidTransform3D.Identity, boxB, RigidTransform3D.Identity, out _).Should().BeFalse();
    }

    [Fact]
    public void SphereBox_OffsetCenter_Resolves_InBoxFrame()
    {
        var sphere = new Sphere3(new Vec3(0.0f, 0.4f, 0.0f), 0.5f);
        var box = new Box3(new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));

        bool hit = Penetration3D.SphereBox(sphere, box, RigidTransform3D.Identity, out Manifold3D manifold);

        hit.Should().BeTrue();
        manifold.Normal.Y.Should().BeApproximately(1.0f, 1e-5f);
        manifold[0].Separation.Should().BeApproximately(-0.1f, 1e-4f);
    }

    [Fact]
    public void TransformedBox_Support_Matches_RotatedCorner()
    {
        var local = new Box3(Vec3.Zero, new Vec3(1.0f, 1.0f, 1.0f));
        var frame = new RigidTransform3D(
            Vec3.Zero, Quat.CreateFromAxisAngle(new Vec3(0.0f, 0.0f, 1.0f), MathF.PI / 4.0f));
        var shape = new TransformedBox(local, frame);

        Vec3 support = TransformedBox.Support(shape, new Vec3(1.0f, 0.0f, 0.0f));

        float planar = MathF.Sqrt(2.0f);
        support.X.Should().BeApproximately(planar, 1e-4f);
        support.Y.Should().BeApproximately(0.0f, 1e-4f);
    }
}
