using System;
using Xunit;
using FluentAssertions;
using Axrone.Geometry;
using Axrone.Numeric;
using Axrone.Utility.NativeBuffer;

namespace Axrone.Geometry.Tests;

file struct VertexCollector : IVertexConsumer<VertexP3N3T2>
{
    public int Count;

    public void Accept(in VertexP3N3T2 vertex) => Count++;
}

file struct TriangleCollector : ITriangleConsumer<ushort>
{
    public int Triangles;

    public void Accept(ushort i0, ushort i1, ushort i2) => Triangles++;
}

public class MeshNativeTests
{
    private static NativeMesh<VertexP3N3T2, ushort> BuildQuad()
    {
        var vertices = new NativeBuffer<VertexP3N3T2, AlignedNativeAllocator>(
            ElementCount.From(4), MemoryAlignment.CacheLine);
        vertices[BufferIndex.From(0)] = new VertexP3N3T2(new Position3D(0, 0, 0), Normal3D.UnitZ, new TexCoord(0, 0));
        vertices[BufferIndex.From(1)] = new VertexP3N3T2(new Position3D(1, 0, 0), Normal3D.UnitZ, new TexCoord(1, 0));
        vertices[BufferIndex.From(2)] = new VertexP3N3T2(new Position3D(1, 1, 0), Normal3D.UnitZ, new TexCoord(1, 1));
        vertices[BufferIndex.From(3)] = new VertexP3N3T2(new Position3D(0, 1, 0), Normal3D.UnitZ, new TexCoord(0, 1));

        var indices = new NativeBuffer<ushort, AlignedNativeAllocator>(
            ElementCount.From(6), MemoryAlignment.CacheLine);
        indices[BufferIndex.From(0)] = 0;
        indices[BufferIndex.From(1)] = 1;
        indices[BufferIndex.From(2)] = 2;
        indices[BufferIndex.From(3)] = 0;
        indices[BufferIndex.From(4)] = 2;
        indices[BufferIndex.From(5)] = 3;

        return new NativeMesh<VertexP3N3T2, ushort>(vertices, 4, indices, 6);
    }

    [Fact]
    public void Mesh_ExposesCounts_Stride_Topology()
    {
        using NativeMesh<VertexP3N3T2, ushort> mesh = BuildQuad();

        mesh.VertexCount.Should().Be(4);
        mesh.IndexCount.Should().Be(6);
        mesh.Stride.Should().Be(32);
        mesh.Topology.Should().Be(PrimitiveTopology.Triangles);
        mesh.Attributes.Length.Should().Be(3);
        mesh.Vertices.Length.Should().Be(4);
        mesh.Indices.Length.Should().Be(6);
    }

    [Fact]
    public void Mesh_Enumerates_AllVertices()
    {
        using NativeMesh<VertexP3N3T2, ushort> mesh = BuildQuad();

        int count = 0;
        foreach (VertexP3N3T2 vertex in mesh)
        {
            count++;
            vertex.Normal.Should().Be(Normal3D.UnitZ);
        }

        count.Should().Be(4);
    }

    [Fact]
    public void Mesh_Copies_ToConsumers()
    {
        using NativeMesh<VertexP3N3T2, ushort> mesh = BuildQuad();
        var vertices = new VertexCollector();
        var triangles = new TriangleCollector();

        mesh.CopyVerticesTo(ref vertices);
        mesh.CopyIndicesTo(ref triangles);

        vertices.Count.Should().Be(4);
        triangles.Triangles.Should().Be(2);
    }

    [Fact]
    public void Mesh_AfterDispose_Throws()
    {
        NativeMesh<VertexP3N3T2, ushort> mesh = BuildQuad();
        mesh.Dispose();

        Action vertices = () => mesh.Vertices.ToArray();
        Action enumerate = () => mesh.GetEnumerator();

        vertices.Should().Throw<ObjectDisposedException>();
        enumerate.Should().Throw<ObjectDisposedException>();
    }
}
