namespace Axrone.Scene.Runtime;

using System.Buffers.Binary;
using Axrone.Scene.Assets;

/// <summary>
/// Converts an interleaved <see cref="TerrainVertexMesh"/> into the scene
/// P3N3T2 mesh layout. Splat weights stay out of the vertex stream because the
/// scene renders splat layers as textures.
/// </summary>
public static class TerrainChunkMeshAdapter
{
    /// <summary>Interleaved scene vertex stride in bytes.</summary>
    public const int VertexStride = 32;

    private const int PositionOffset = 0;
    private const int NormalOffset = 12;
    private const int UvOffset = 24;

    /// <summary>Builds a scene mesh definition from a terrain chunk mesh.</summary>
    /// <param name="id">Mesh asset id.</param>
    /// <param name="mesh">Interleaved terrain mesh.</param>
    /// <returns>A scene mesh definition with computed bounds when positions are valid.</returns>
    /// <exception cref="TerrainException">The mesh is inconsistent.</exception>
    public static SceneMeshDefinition Create(SceneAssetId<MeshAssetTag> id, TerrainVertexMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.VertexCount == 0 || mesh.Indices.Length % 3 != 0)
        {
            throw new TerrainException("Terrain mesh requires vertices and triangle indices.", TerrainErrorCode.ValidationFailed);
        }

        var vertices = new byte[mesh.VertexCount * VertexStride];
        Span<float> packed = MemoryMarshal.Cast<byte, float>(vertices.AsSpan());
        for (var index = 0; index < mesh.VertexCount; index++)
        {
            TerrainVertex vertex = mesh.Vertices[index];
            int offset = index * 8;
            packed[offset] = vertex.Position.X;
            packed[offset + 1] = vertex.Position.Y;
            packed[offset + 2] = vertex.Position.Z;
            packed[offset + 3] = vertex.Normal.X;
            packed[offset + 4] = vertex.Normal.Y;
            packed[offset + 5] = vertex.Normal.Z;
            packed[offset + 6] = vertex.TexCoord.X;
            packed[offset + 7] = vertex.TexCoord.Y;
        }

        var indices = new byte[mesh.Indices.Length * sizeof(uint)];
        mesh.Indices.AsSpan().CopyTo(MemoryMarshal.Cast<byte, uint>(indices.AsSpan()));

        SceneVertexAttribute[] attributes =
        [
            new SceneVertexAttribute(SceneMeshSemantic.Position, 3, PositionOffset, VertexStride),
            new SceneVertexAttribute(SceneMeshSemantic.Normal, 3, NormalOffset, VertexStride),
            new SceneVertexAttribute(SceneMeshSemantic.Uv0, 2, UvOffset, VertexStride),
        ];

        var definition = new SceneMeshDefinition(
            id,
            vertices,
            attributes,
            mesh.VertexCount,
            indices,
            SceneIndexFormat.UInt32,
            mesh.Indices.Length,
            SceneMeshTopology.Triangles);

        return SceneMeshBounds.TryCompute(definition, out BoundingSphere3 bounds)
            ? new SceneMeshDefinition(
                id,
                vertices,
                attributes,
                mesh.VertexCount,
                indices,
                SceneIndexFormat.UInt32,
                mesh.Indices.Length,
                SceneMeshTopology.Triangles,
                bounds)
            : definition;
    }

    /// <summary>Reads a position from an interleaved scene vertex buffer.</summary>
    /// <param name="vertices">Interleaved P3N3T2 bytes.</param>
    /// <param name="vertexIndex">Vertex index.</param>
    /// <returns>The position.</returns>
    public static Vec3 ReadPosition(ReadOnlySpan<byte> vertices, int vertexIndex)
    {
        ReadOnlySpan<byte> bytes = vertices.Slice((vertexIndex * VertexStride) + PositionOffset, 12);
        return new Vec3(
            BinaryPrimitives.ReadSingleLittleEndian(bytes),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[4..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[8..]));
    }
}
