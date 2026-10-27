namespace Axrone.Terrain;

/// <summary>
/// Interleaved terrain mesh in the GPU-ready <see cref="TerrainVertex"/> layout. Use
/// <see cref="TerrainEngine{TSurface,TBackoff,TObserver}.BuildChunkVertexMesh"/> or the
/// allocation-free span overload to fill it.
/// </summary>
public sealed class TerrainVertexMesh
{
    /// <summary>Creates an interleaved mesh from validated buffers.</summary>
    /// <param name="vertices">Interleaved vertices.</param>
    /// <param name="indices">Triangle indices.</param>
    /// <exception cref="TerrainException">The index count is not a multiple of three.</exception>
    public TerrainVertexMesh(TerrainVertex[] vertices, uint[] indices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);

        if (indices.Length % 3 != 0)
        {
            throw new TerrainException("Index count must be a multiple of three.", TerrainErrorCode.ValidationFailed);
        }

        Vertices = vertices;
        Indices = indices;
    }

    /// <summary>Gets the interleaved vertices.</summary>
    public TerrainVertex[] Vertices { get; }

    /// <summary>Gets the triangle indices.</summary>
    public uint[] Indices { get; }

    /// <summary>Gets the vertex count.</summary>
    public int VertexCount => Vertices.Length;

    /// <summary>Gets the triangle count.</summary>
    public int TriangleCount => Indices.Length / 3;
}
