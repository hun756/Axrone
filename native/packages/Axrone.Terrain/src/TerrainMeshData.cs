namespace Axrone.Terrain;

/// <summary>
/// Grid mesh data produced from a terrain heightmap. Buffers are laid out for
/// direct upload: positions and normals as XYZ triples, UVs as pairs, indices as
/// 32-bit triangle lists.
/// </summary>
public sealed class TerrainMeshData
{
    /// <summary>Initializes a mesh from validated buffers.</summary>
    /// <param name="positions">XYZ positions, length <c>vertexCount * 3</c>.</param>
    /// <param name="normals">XYZ normals, length <c>vertexCount * 3</c>.</param>
    /// <param name="uvs">UV pairs, length <c>vertexCount * 2</c>.</param>
    /// <param name="indices">Triangle indices, length <c>triangleCount * 3</c>.</param>
    /// <exception cref="TerrainException">The buffer lengths are inconsistent.</exception>
    public TerrainMeshData(float[] positions, float[] normals, float[] uvs, uint[] indices)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(normals);
        ArgumentNullException.ThrowIfNull(uvs);
        ArgumentNullException.ThrowIfNull(indices);

        if (positions.Length % 3 != 0 ||
            normals.Length != positions.Length ||
            uvs.Length != (positions.Length / 3) * 2 ||
            indices.Length % 3 != 0)
        {
            throw new TerrainException(
                "Terrain mesh buffers are inconsistent (positions/normals XYZ triples, UVs pairs, indices triangles).",
                TerrainErrorCode.ValidationFailed);
        }

        Positions = positions;
        Normals = normals;
        Uvs = uvs;
        Indices = indices;
        VertexCount = positions.Length / 3;
        TriangleCount = indices.Length / 3;
    }

    /// <summary>Gets the XYZ position buffer.</summary>
    public float[] Positions { get; }

    /// <summary>Gets the XYZ normal buffer.</summary>
    public float[] Normals { get; }

    /// <summary>Gets the UV buffer.</summary>
    public float[] Uvs { get; }

    /// <summary>Gets the 32-bit triangle index buffer.</summary>
    public uint[] Indices { get; }

    /// <summary>Gets the vertex count.</summary>
    public int VertexCount { get; }

    /// <summary>Gets the triangle count.</summary>
    public int TriangleCount { get; }
}
