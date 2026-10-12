using System;
using Xunit;
using FluentAssertions;
using Axrone.Geometry;
using Axrone.Numeric;

namespace Axrone.Geometry.Tests;

public class MeshConfigsTests
{
    [Fact]
    public void Configs_HaveDocumentedDefaults()
    {
        new SphereConfig().Radius.Should().Be(new Metric(0.5f));
        new SphereConfig().PhiLength.Should().Be(AngleRadians.TwoPi);
        new SphereConfig().ThetaLength.Should().Be(AngleRadians.Pi);
        new BoxConfig().Width.Should().Be(Metric.One);
        new CylinderConfig().OpenEnded.Should().BeFalse();
        new CapsuleConfig().RadialSegments.Should().Be(new SegmentResolution(16));
        new PlaneConfig().HeightSegments.Should().Be(new SegmentResolution(1));
        new TorusConfig().Arc.Should().Be(AngleRadians.TwoPi);
    }

    [Fact]
    public void Descriptor_RoundTripsEachArm()
    {
        ShapeDescriptor.FromSphere(new SphereConfig()).Kind.Should().Be(ShapeKind.Sphere);
        ShapeDescriptor.FromBox(new BoxConfig { Width = new Metric(2.0f) }).Box.Width.Should().Be(new Metric(2.0f));
        ShapeDescriptor.FromCylinder(new CylinderConfig()).Kind.Should().Be(ShapeKind.Cylinder);
        ShapeDescriptor.FromCapsule(new CapsuleConfig()).Kind.Should().Be(ShapeKind.Capsule);
        ShapeDescriptor.FromPlane(new PlaneConfig()).Kind.Should().Be(ShapeKind.Plane);
        ShapeDescriptor.FromTorus(new TorusConfig { Tube = new Metric(0.3f) }).Torus.Tube.Should().Be(new Metric(0.3f));
    }
}
