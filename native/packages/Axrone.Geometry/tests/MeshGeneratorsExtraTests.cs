using System;
using System.Runtime.CompilerServices;
using Xunit;
using FluentAssertions;
using Axrone.Geometry;
using Axrone.Numeric;

namespace Axrone.Geometry.Tests;

public class MeshGeneratorsExtraTests
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
    public void EmitCapsule_KnownCounts()
    {
        var sink = new MeshListSink();
        var config = new CapsuleConfig { CapSegments = new SegmentResolution(2), RadialSegments = new SegmentResolution(3) };

        ProceduralPrimitives.EmitCapsule<MeshListSink, VertexP3N3T2, ushort>(ref sink, in config);

        uint rows = 2 * 2 + 1 + 1;
        sink.Vertices.Count.Should().Be((int)(rows * 4));
        sink.Indices.Count.Should().Be((int)((rows - 1) * 3 * 6));
    }

    [Fact]
    public void EmitPill_SingleLatheGrid()
    {
        var sink = new MeshListSink();
        var config = new CapsuleConfig { CapSegments = new SegmentResolution(4), RadialSegments = new SegmentResolution(3) };

        ProceduralPrimitives.EmitPill<MeshListSink, VertexP3N3T2, ushort>(ref sink, in config);

        sink.Vertices.Count.Should().Be(7 * 4);
        sink.Indices.Count.Should().Be(6 * 3 * 6);
        foreach (VertexP3N3T2 vertex in sink.Vertices)
        {
            vertex.Normal.ToVec3().LengthSquared().Should().BeApproximately(1.0f, 1e-4f);
        }
    }

    [Fact]
    public void EmitPlane_FacesPositiveZ()
    {
        var sink = new MeshListSink();

        ProceduralPrimitives.EmitPlane<MeshListSink, VertexP3N3T2, ushort>(ref sink, new PlaneConfig());

        sink.Vertices.Count.Should().Be(4);
        sink.Indices.Count.Should().Be(6);
        foreach (VertexP3N3T2 vertex in sink.Vertices)
        {
            vertex.Normal.Should().Be(Normal3D.UnitZ);
            vertex.Position.Z.Should().Be(0.0f);
        }
    }

    [Fact]
    public void EmitTorus_KnownCounts()
    {
        var sink = new MeshListSink();
        var config = new TorusConfig { RadialSegments = new SegmentResolution(3), TubularSegments = new SegmentResolution(3) };

        ProceduralPrimitives.EmitTorus<MeshListSink, VertexP3N3T2, ushort>(ref sink, in config);

        sink.Vertices.Count.Should().Be(16);
        sink.Indices.Count.Should().Be(54);
    }

    [Fact]
    public void EmitCircle_FanTopology()
    {
        var sink = new MeshListSink();

        ProceduralPrimitives.EmitCircle<MeshListSink, VertexP3N3T2, ushort>(ref sink, new Metric(2.0f), new SegmentResolution(3));

        sink.Vertices.Count.Should().Be(5);
        sink.Indices.Count.Should().Be(9);
    }

    [Fact]
    public void EmitRing_Annulus()
    {
        var sink = new MeshListSink();

        ProceduralPrimitives.EmitRing<MeshListSink, VertexP3N3T2, ushort>(
            ref sink, new Metric(1.0f), new Metric(2.0f), new SegmentResolution(3), new SegmentResolution(1));

        sink.Vertices.Count.Should().Be(8);
        sink.Indices.Count.Should().Be(18);
    }

    [Fact]
    public void EmitGrid_FacesPositiveY()
    {
        var sink = new MeshListSink();

        ProceduralPrimitives.EmitGrid<MeshListSink, VertexP3N3T2, ushort>(
            ref sink, new Metric(2.0f), new Metric(2.0f), new SegmentResolution(1), new SegmentResolution(1));

        sink.Vertices.Count.Should().Be(4);
        sink.Indices.Count.Should().Be(6);
        foreach (VertexP3N3T2 vertex in sink.Vertices)
        {
            vertex.Normal.Should().Be(Normal3D.UnitY);
        }
    }

    [Fact]
    public void EmitTube_HasOuterInnerAndRims()
    {
        var sink = new MeshListSink();

        ProceduralPrimitives.EmitTube<MeshListSink, VertexP3N3T2, ushort>(
            ref sink, new Metric(2.0f), new Metric(1.0f), Metric.One,
            new SegmentResolution(3), new SegmentResolution(1));

        sink.Vertices.Count.Should().BeGreaterThan(0);
        (sink.Indices.Count % 3).Should().Be(0);
    }

    [Fact]
    public void EmitTorusKnot_ClosedTube()
    {
        var sink = new MeshListSink();

        ProceduralPrimitives.EmitTorusKnot<MeshListSink, VertexP3N3T2, ushort>(
            ref sink, new Metric(1.0f), new Metric(0.2f),
            new SegmentResolution(4), new SegmentResolution(3), 2, 3);

        sink.Vertices.Count.Should().Be(5 * 4);
        sink.Indices.Count.Should().Be(4 * 3 * 6);
        foreach (VertexP3N3T2 vertex in sink.Vertices)
        {
            vertex.Normal.ToVec3().LengthSquared().Should().BeApproximately(1.0f, 1e-4f);
        }
    }

    [Fact]
    public void EmitSpring_KnownCounts()
    {
        var sink = new MeshListSink();

        ProceduralPrimitives.EmitSpring<MeshListSink, VertexP3N3T2, ushort>(
            ref sink, new Metric(1.0f), new Metric(0.1f), Metric.One, 2,
            new SegmentResolution(8), new SegmentResolution(3));

        sink.Vertices.Count.Should().Be(9 * 4);
        sink.Indices.Count.Should().Be(8 * 3 * 6);
    }

    [Fact]
    public void EmitShape_DispatchesAllArms()
    {
        foreach (ShapeKind kind in Enum.GetValues<ShapeKind>())
        {
            var sink = new MeshListSink();
            ShapeDescriptor descriptor = kind switch
            {
                ShapeKind.Sphere => ShapeDescriptor.FromSphere(new SphereConfig
                {
                    WidthSegments = new SegmentResolution(3),
                    HeightSegments = new SegmentResolution(2)
                }),
                ShapeKind.Box => ShapeDescriptor.FromBox(new BoxConfig()),
                ShapeKind.Cylinder => ShapeDescriptor.FromCylinder(new CylinderConfig
                {
                    RadialSegments = new SegmentResolution(3),
                    HeightSegments = new SegmentResolution(1)
                }),
                ShapeKind.Capsule => ShapeDescriptor.FromCapsule(new CapsuleConfig
                {
                    CapSegments = new SegmentResolution(2),
                    RadialSegments = new SegmentResolution(3)
                }),
                ShapeKind.Plane => ShapeDescriptor.FromPlane(new PlaneConfig()),
                _ => ShapeDescriptor.FromTorus(new TorusConfig
                {
                    RadialSegments = new SegmentResolution(3),
                    TubularSegments = new SegmentResolution(3)
                }),
            };

            ProceduralPrimitives.EmitShape<MeshListSink, VertexP3N3T2, ushort>(ref sink, in descriptor);

            sink.Vertices.Count.Should().BeGreaterThan(0, $"kind {kind} must emit");
            sink.Indices.Count.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void EmitShape_UnknownKind_Throws()
    {
        var sink = new MeshListSink();
        ShapeDescriptor descriptor = ShapeDescriptor.FromSphere(new SphereConfig());
        Unsafe.AsRef(in descriptor.Kind) = (ShapeKind)99;

        Action act = () => ProceduralPrimitives.EmitShape<MeshListSink, VertexP3N3T2, ushort>(ref sink, in descriptor);

        act.Should().Throw<InvalidOperationException>();
    }
}
