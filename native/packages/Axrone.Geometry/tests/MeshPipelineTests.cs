using System;
using Xunit;
using FluentAssertions;
using Axrone.Geometry;
using Axrone.Numeric;

namespace Axrone.Geometry.Tests;

public class MeshPipelineTests
{
    private static MeshPipeline<VertexP3N3T2, ushort, CounterClockwiseWinding, AssemblyStage> QuadPipeline()
    {
        var assembly = MeshPipeline<VertexP3N3T2, ushort, CounterClockwiseWinding, ConfigurationStage>
            .Create()
            .Allocate(new CapacityAllocation(4, 6));

        assembly.AddVertex(new Position3D(0, 0, 0), Normal3D.UnitZ, new TexCoord(0, 0));
        assembly.AddVertex(new Position3D(1, 0, 0), Normal3D.UnitZ, new TexCoord(1, 0));
        assembly.AddVertex(new Position3D(1, 1, 0), Normal3D.UnitZ, new TexCoord(1, 1));
        assembly.AddVertex(new Position3D(0, 1, 0), Normal3D.UnitZ, new TexCoord(0, 1));
        assembly.AddQuad(0, 1, 2, 3);

        return assembly;
    }

    [Fact]
    public void Pipeline_Assembles_AndSeals()
    {
        var assembly = QuadPipeline();

        assembly.VertexCount.Should().Be(4u);
        assembly.IndexCount.Should().Be(6u);

        using NativeMesh<VertexP3N3T2, ushort> mesh = assembly.Seal();

        mesh.VertexCount.Should().Be(4);
        mesh.IndexCount.Should().Be(6);
    }

    [Fact]
    public void Pipeline_WrongStage_Throws()
    {
        var config = MeshPipeline<VertexP3N3T2, ushort, CounterClockwiseWinding, ConfigurationStage>.Create();

        Action addVertex = () => config.AddVertex(new Position3D(0, 0, 0), Normal3D.UnitZ, TexCoord.Zero);
        Action seal = () => config.Seal();

        addVertex.Should().Throw<InvalidOperationException>();
        seal.Should().Throw<InvalidOperationException>();
        config.Dispose();
    }

    [Fact]
    public void Pipeline_RecalculateNormals_UnitZQuad()
    {
        var assembly = MeshPipeline<VertexP3N3T2, ushort, CounterClockwiseWinding, ConfigurationStage>
            .Create()
            .Allocate(new CapacityAllocation(4, 6));

        assembly.AddVertex(new Position3D(0, 0, 0), Normal3D.UnitY, new TexCoord(0, 0));
        assembly.AddVertex(new Position3D(1, 0, 0), Normal3D.UnitY, new TexCoord(1, 0));
        assembly.AddVertex(new Position3D(1, 1, 0), Normal3D.UnitY, new TexCoord(1, 1));
        assembly.AddVertex(new Position3D(0, 1, 0), Normal3D.UnitY, new TexCoord(0, 1));
        assembly.AddQuad(0, 1, 2, 3);
        assembly.RecalculateNormals();

        using NativeMesh<VertexP3N3T2, ushort> mesh = assembly.Seal();

        foreach (VertexP3N3T2 vertex in mesh)
        {
            vertex.Normal.Should().Be(Normal3D.UnitZ);
        }
    }

    [Fact]
    public void Pipeline_RecalculateTangents_UnitQuad()
    {
        var assembly = QuadPipeline();
        assembly.RecalculateTangents();

        using NativeMesh<VertexP3N3T2, ushort> mesh = assembly.Seal();

        mesh.VertexCount.Should().Be(4);
    }

    [Fact]
    public void Pipeline_Sink_Appends()
    {
        var assembly = MeshPipeline<VertexP3N3T2, ushort, CounterClockwiseWinding, ConfigurationStage>
            .Create()
            .Allocate(new CapacityAllocation(4, 6));
        var sink = assembly.AsSink();

        sink.AppendVertex(new VertexP3N3T2(new Position3D(0, 0, 0), Normal3D.UnitZ, TexCoord.Zero));
        sink.CurrentVertexCount.Should().Be(1u);
        sink.AppendTriangle(0, 0, 0);

        using NativeMesh<VertexP3N3T2, ushort> mesh = assembly.Seal();

        mesh.VertexCount.Should().Be(1);
        mesh.IndexCount.Should().Be(3);
    }
}
