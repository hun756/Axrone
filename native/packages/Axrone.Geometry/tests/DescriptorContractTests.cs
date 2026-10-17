using System;
using Xunit;
using FluentAssertions;
using Axrone.Geometry;

namespace Axrone.Geometry.Tests;

public class DescriptorContractTests
{
    [Fact]
    public void Shape_Factories_Pass_Validation()
    {
        ShapeDescriptor sphere = ShapeDescriptor.FromSphere(new SphereConfig());
        Action sphereAct = () => ShapeDescriptor.Validate(in sphere);
        sphereAct.Should().NotThrow();

        ShapeDescriptor box = ShapeDescriptor.FromBox(new BoxConfig());
        Action boxAct = () => ShapeDescriptor.Validate(in box);
        boxAct.Should().NotThrow();

        ShapeDescriptor torus = ShapeDescriptor.FromTorus(new TorusConfig());
        Action torusAct = () => ShapeDescriptor.Validate(in torus);
        torusAct.Should().NotThrow();
    }

    [Fact]
    public void Shape_Rejects_Degenerate_Arms()
    {
        ShapeDescriptor badRadius = ShapeDescriptor.FromSphere(new SphereConfig { Radius = new Metric(0.0f) });
        Action radiusAct = () => ShapeDescriptor.Validate(in badRadius);
        radiusAct.Should().Throw<ArgumentOutOfRangeException>();

        ShapeDescriptor badSegments = ShapeDescriptor.FromBox(new BoxConfig { WidthSegments = new SegmentResolution(0U) });
        Action segmentsAct = () => ShapeDescriptor.Validate(in badSegments);
        segmentsAct.Should().Throw<ArgumentOutOfRangeException>();

        ShapeDescriptor zeroed = default;
        Action kindAct = () => ShapeDescriptor.Validate(in zeroed);
        kindAct.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Vertex_Attribute_Accepts_And_Rejects()
    {
        var good = new VertexAttributeDescriptor(AttributeSemantic.Position, 3, GLAttributeType.Float, false, 0);
        Action goodAct = () => VertexAttributeDescriptor.Validate(in good);
        goodAct.Should().NotThrow();

        var badCount = new VertexAttributeDescriptor(AttributeSemantic.Normal, 5, GLAttributeType.Float, false, 0);
        Action countAct = () => VertexAttributeDescriptor.Validate(in badCount);
        countAct.Should().Throw<ArgumentOutOfRangeException>();

        var badOffset = new VertexAttributeDescriptor(AttributeSemantic.TexCoord, 2, GLAttributeType.Float, false, -4);
        Action offsetAct = () => VertexAttributeDescriptor.Validate(in badOffset);
        offsetAct.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Describe_RoundTrips_ToString()
    {
        ShapeDescriptor sphere = ShapeDescriptor.FromSphere(new SphereConfig());
        ShapeDescriptor.Describe(in sphere).Should().Be(sphere.ToString());

        var attribute = new VertexAttributeDescriptor(AttributeSemantic.Position, 3, GLAttributeType.Float, false, 0);
        VertexAttributeDescriptor.Describe(in attribute).Should().Be(attribute.ToString());
    }
}
