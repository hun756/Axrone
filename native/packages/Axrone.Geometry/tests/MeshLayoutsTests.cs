using System;
using Xunit;
using FluentAssertions;
using Axrone.Geometry;
using Axrone.Numeric;

namespace Axrone.Geometry.Tests;

public class MeshLayoutsTests
{
    [Fact]
    public void Strides_MatchInterleavedSizes()
    {
        VertexP3N3T2.ByteStride.Should().Be(32);
        VertexP3N3T2T4.ByteStride.Should().Be(48);
    }

    [Fact]
    public void Descriptors_CoverAttributes_WithByteOffsets()
    {
        var p3 = VertexP3N3T2.LayoutDescriptors.ToArray();
        p3.Length.Should().Be(3);
        p3[0].Should().Be(new VertexAttributeDescriptor(AttributeSemantic.Position, 3, GLAttributeType.Float, false, 0));
        p3[2].Offset.Should().Be(24);

        var p4 = VertexP3N3T2T4.LayoutDescriptors.ToArray();
        p4.Length.Should().Be(4);
        p4[3].Should().Be(new VertexAttributeDescriptor(AttributeSemantic.Tangent, 4, GLAttributeType.Float, false, 32));
    }

    [Fact]
    public void Create_WithVariants_RoundTrip()
    {
        var pos = new Position3D(1.0f, 2.0f, 3.0f);

        var slim = VertexP3N3T2.Create(in pos, Normal3D.UnitZ, new TexCoord(0.5f, 0.5f), Tangent4D.Default);
        slim.Position.Should().Be(pos);
        ((IVertex<VertexP3N3T2>)slim).Tangent.Should().Be(Tangent4D.Default);
        slim.WithTangent(new Tangent4D(0.0f, 1.0f, 0.0f, 1.0f)).Should().Be(slim);

        var full = VertexP3N3T2T4.Create(in pos, Normal3D.UnitZ, new TexCoord(0.5f, 0.5f), new Tangent4D(0.0f, 1.0f, 0.0f, -1.0f));
        full.Tangent.W.Should().Be(-1.0f);
        full.WithNormal(Normal3D.UnitX).Normal.Should().Be(Normal3D.UnitX);
        full.WithTangent(Tangent4D.Default).Tangent.Should().Be(Tangent4D.Default);
    }
}
