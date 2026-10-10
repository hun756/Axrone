using System;
using Xunit;
using FluentAssertions;
using Axrone.Geometry;
using Axrone.Numeric;

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
        NativeBuffer<VertexP3N3T2> vertices = new(4);
        vertices.Append(new VertexP3N3T2(new Position3D(0, 0, 0), Normal3D.UnitZ, new TexCoord(0, 0)));
        vertices.Append(new VertexP3N3T2(new Position3D(1, 0, 0), Normal3D.UnitZ, new TexCoord(1, 0)));
        vertices.Append(new VertexP3N3T2(new Position3D(1, 1, 0), Normal3D.UnitZ, new TexCoord(1, 1)));
        vertices.Append(new VertexP3N3T2(new Position3D(0, 1, 0), Normal3D.UnitZ, new TexCoord(0, 1)));

        NativeBuffer<ushort> indices = new(6);
        indices.Append(0);
        indices.Append(1);
        indices.Append(2);
        indices.Append(0);
        indices.Append(2);
        indices.Append(3);

        return new NativeMesh<VertexP3N3T2, ushort>(ref vertices, ref indices);
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
