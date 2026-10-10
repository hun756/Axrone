using System;
using Xunit;
using FluentAssertions;
using Axrone.Geometry;
using Axrone.Numeric;

namespace Axrone.Geometry.Tests;

public class MeshGeneratorsTests
{
    private struct MeshListSink : IMeshSink<VertexP3N3T2, ushort>
    {
        public readonly List<VertexP3N3T2> Vertices = new();
        public readonly List<ushort> Indices = new();

        public MeshListSink() { }

        public uint CurrentVertexCount => (uint)Vertices.Count;

        public void AppendVertex(in VertexP3N3T2 vertex) => Vertices.Add(vertex);

        public void AppendTriangle(ushort i0, ushort i1, ushort i2)
        {
            Indices.Add(i0);
            Indices.Add(i1);
            Indices.Add(i2);
        }
    }

    [Fact]
    public void EmitSphere_KnownCounts_AndUnitNormals()
    {
        var sink = new MeshListSink();
        var config = new SphereConfig { WidthSegments = new SegmentResolution(3), HeightSegments = new SegmentResolution(2) };

        ProceduralPrimitives.EmitSphere<MeshListSink, VertexP3N3T2, ushort>(ref sink, in config);

        sink.Vertices.Count.Should().Be(12);
        sink.Indices.Count.Should().Be(18);
        foreach (VertexP3N3T2 vertex in sink.Vertices)
        {
            vertex.Normal.ToVec3().LengthSquared().Should().BeApproximately(1.0f, 1e-5f);
        }
    }

    [Fact]
    public void EmitSphere_PoleRows_SqueezeU()
    {
        var sink = new MeshListSink();
        var config = new SphereConfig { WidthSegments = new SegmentResolution(4), HeightSegments = new SegmentResolution(2) };

        ProceduralPrimitives.EmitSphere<MeshListSink, VertexP3N3T2, ushort>(ref sink, in config);

        sink.Vertices[0].UV.U.Should().BeApproximately(0.125f, 1e-6f);
        sink.Vertices[4].UV.U.Should().BeApproximately(0.625f, 1e-6f);
        sink.Vertices[6].UV.U.Should().BeApproximately(0.25f, 1e-6f);
    }

    [Fact]
    public void EmitUvSphere_PolesAndWrappedRings()
    {
        var sink = new MeshListSink();
        var config = new SphereConfig { WidthSegments = new SegmentResolution(4), HeightSegments = new SegmentResolution(3) };

        ProceduralPrimitives.EmitUvSphere<MeshListSink, VertexP3N3T2, ushort>(ref sink, in config);

        sink.Vertices.Count.Should().Be(10);
        sink.Indices.Count.Should().Be(48);
        sink.Vertices[0].Position.Should().Be(new Position3D(0, 0.5f, 0));
        sink.Vertices[0].Normal.Should().Be(Normal3D.UnitY);
        sink.Vertices[9].Normal.Should().Be(Normal3D.NegativeUnitY);
    }

    [Fact]
    public void EmitIcosphere_Subdivides()
    {
        var flatSink = new MeshListSink();
        ProceduralPrimitives.EmitIcosphere<MeshListSink, VertexP3N3T2, ushort>(ref flatSink, new Metric(1.0f), 0);
        flatSink.Vertices.Count.Should().Be(12);
        flatSink.Indices.Count.Should().Be(60);

        var subdividedSink = new MeshListSink();
        ProceduralPrimitives.EmitIcosphere<MeshListSink, VertexP3N3T2, ushort>(ref subdividedSink, new Metric(1.0f), 1);
        subdividedSink.Vertices.Count.Should().Be(42);
        subdividedSink.Indices.Count.Should().Be(240);

        foreach (VertexP3N3T2 vertex in subdividedSink.Vertices)
        {
            vertex.Position.ToVec3().Length().Should().BeApproximately(1.0f, 1e-4f);
        }
    }

    [Fact]
    public void EmitBox_SixFaces()
    {
        var sink = new MeshListSink();

        ProceduralPrimitives.EmitBox<MeshListSink, VertexP3N3T2, ushort>(ref sink, new BoxConfig());

        sink.Vertices.Count.Should().Be(24);
        sink.Indices.Count.Should().Be(36);
    }

    [Fact]
    public void EmitCylinder_WithCaps()
    {
        var sink = new MeshListSink();
        var config = new CylinderConfig { RadialSegments = new SegmentResolution(3), HeightSegments = new SegmentResolution(1) };

        ProceduralPrimitives.EmitCylinder<MeshListSink, VertexP3N3T2, ushort>(ref sink, in config);

        sink.Vertices.Count.Should().Be(18);
        sink.Indices.Count.Should().Be(36);
    }
}
