namespace Axrone.Terrain;

/// <summary>
/// Source mesh expanded for every scattered foliage instance. Buffers use the
/// same layout as <see cref="TerrainMeshData"/> minus the instance count.
/// </summary>
public sealed class TerrainFoliageSourceMesh
{
    /// <summary>Initializes a foliage source mesh.</summary>
    /// <param name="positions">XYZ positions.</param>
    /// <param name="normals">XYZ normals.</param>
    /// <param name="uvs">UV pairs.</param>
    /// <param name="indices">Triangle indices.</param>
    /// <exception cref="TerrainException">The buffers are inconsistent.</exception>
    public TerrainFoliageSourceMesh(float[] positions, float[] normals, float[] uvs, uint[] indices)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(normals);
        ArgumentNullException.ThrowIfNull(uvs);
        ArgumentNullException.ThrowIfNull(indices);

        int vertexCount = positions.Length / 3;
        if (positions.Length % 3 != 0 ||
            normals.Length != positions.Length ||
            uvs.Length != vertexCount * 2 ||
            indices.Length % 3 != 0)
        {
            throw new TerrainException(
                "Foliage source mesh buffers are inconsistent (positions/normals XYZ triples, UVs pairs, indices triangles).",
                TerrainErrorCode.ValidationFailed);
        }

        Positions = positions;
        Normals = normals;
        Uvs = uvs;
        Indices = indices;
    }

    /// <summary>Gets the XYZ position buffer.</summary>
    public float[] Positions { get; }

    /// <summary>Gets the XYZ normal buffer.</summary>
    public float[] Normals { get; }

    /// <summary>Gets the UV buffer.</summary>
    public float[] Uvs { get; }

    /// <summary>Gets the triangle index buffer.</summary>
    public uint[] Indices { get; }

    /// <summary>Gets the vertex count.</summary>
    public int VertexCount => Positions.Length / 3;
}

/// <summary>
/// Batches all scattered instances of one foliage layer into a single
/// <see cref="TerrainMeshData"/>, avoiding per-instance draw calls. Positions
/// are terrain-local, matching the terrain surface mesh space.
/// </summary>
public static class TerrainFoliageMesh
{
    /// <summary>
    /// Creates the built-in grass card: two perpendicular unit quads crossing at
    /// the origin, 1 unit tall. Renderers should disable backface culling for
    /// foliage.
    /// </summary>
    /// <returns>An 8-vertex, 12-index cross-quad card.</returns>
    public static TerrainFoliageSourceMesh CreateCardMesh()
    {
        var positions = new float[24];
        var normals = new float[24];
        var uvs = new float[16];
        var indices = new uint[12];

        var vertexOffset = 0;
        var indexOffset = 0;

        AddQuad(0f, 1f);
        AddQuad(1f, 0f);

        return new TerrainFoliageSourceMesh(positions, normals, uvs, indices);

        void AddQuad(float nx, float nz)
        {
            uint baseIndex = (uint)vertexOffset;
            float halfX = nz * 0.5f;
            float halfZ = -nx * 0.5f;

            SetPosition(vertexOffset, -halfX, 0f, -halfZ);
            SetPosition(vertexOffset + 1, halfX, 0f, halfZ);
            SetPosition(vertexOffset + 2, halfX, 1f, halfZ);
            SetPosition(vertexOffset + 3, -halfX, 1f, -halfZ);

            for (var corner = 0; corner < 4; corner++)
            {
                int normalOffset = (vertexOffset + corner) * 3;
                normals[normalOffset] = nx;
                normals[normalOffset + 1] = 0f;
                normals[normalOffset + 2] = nz;
            }

            int uvOffset = vertexOffset * 2;
            uvs[uvOffset] = 0f;
            uvs[uvOffset + 1] = 1f;
            uvs[uvOffset + 2] = 1f;
            uvs[uvOffset + 3] = 1f;
            uvs[uvOffset + 4] = 1f;
            uvs[uvOffset + 5] = 0f;
            uvs[uvOffset + 6] = 0f;
            uvs[uvOffset + 7] = 0f;

            indices[indexOffset] = baseIndex;
            indices[indexOffset + 1] = baseIndex + 1;
            indices[indexOffset + 2] = baseIndex + 2;
            indices[indexOffset + 3] = baseIndex;
            indices[indexOffset + 4] = baseIndex + 2;
            indices[indexOffset + 5] = baseIndex + 3;

            vertexOffset += 4;
            indexOffset += 6;
        }

        void SetPosition(int vertex, float x, float y, float z)
        {
            int offset = vertex * 3;
            positions[offset] = x;
            positions[offset + 1] = y;
            positions[offset + 2] = z;
        }
    }

    /// <summary>Expands every instance with the built-in cross-quad card mesh.</summary>
    /// <param name="instances">Scattered instances in terrain-local space.</param>
    /// <returns>A merged batch mesh; empty when there are no instances.</returns>
    public static TerrainMeshData BuildBatch(ReadOnlySpan<TerrainFoliageInstance> instances) =>
        BuildBatch(instances, CreateCardMesh());

    /// <summary>Expands every instance with a custom source mesh.</summary>
    /// <param name="instances">Scattered instances in terrain-local space.</param>
    /// <param name="sourceMesh">Source mesh to replicate.</param>
    /// <returns>A merged batch mesh; empty when there are no instances.</returns>
    /// <exception cref="TerrainException">The source mesh is inconsistent.</exception>
    public static TerrainMeshData BuildBatch(
        ReadOnlySpan<TerrainFoliageInstance> instances,
        TerrainFoliageSourceMesh sourceMesh)
    {
        ArgumentNullException.ThrowIfNull(sourceMesh);

        int sourceVertexCount = sourceMesh.VertexCount;
        int vertexCount = sourceVertexCount * instances.Length;
        var positions = new float[vertexCount * 3];
        var normals = new float[vertexCount * 3];
        var uvs = new float[vertexCount * 2];
        var indices = new uint[sourceMesh.Indices.Length * instances.Length];

        for (var instanceIndex = 0; instanceIndex < instances.Length; instanceIndex++)
        {
            TerrainFoliageInstance instance = instances[instanceIndex];
            float cos = MathF.Cos(instance.RotationY);
            float sin = MathF.Sin(instance.RotationY);
            int vertexOffset = instanceIndex * sourceVertexCount;

            for (var vertex = 0; vertex < sourceVertexCount; vertex++)
            {
                int sourceOffset = vertex * 3;
                float localX = sourceMesh.Positions[sourceOffset] * instance.Scale;
                float localY = sourceMesh.Positions[sourceOffset + 1] * instance.Scale;
                float localZ = sourceMesh.Positions[sourceOffset + 2] * instance.Scale;

                int targetOffset = (vertexOffset + vertex) * 3;
                positions[targetOffset] = (localX * cos) + (localZ * sin) + instance.X;
                positions[targetOffset + 1] = localY + instance.Y;
                positions[targetOffset + 2] = (-localX * sin) + (localZ * cos) + instance.Z;

                float normalX = sourceMesh.Normals[sourceOffset];
                float normalY = sourceMesh.Normals[sourceOffset + 1];
                float normalZ = sourceMesh.Normals[sourceOffset + 2];
                normals[targetOffset] = (normalX * cos) + (normalZ * sin);
                normals[targetOffset + 1] = normalY;
                normals[targetOffset + 2] = (-normalX * sin) + (normalZ * cos);

                int uvSource = vertex * 2;
                int uvTarget = (vertexOffset + vertex) * 2;
                uvs[uvTarget] = sourceMesh.Uvs[uvSource];
                uvs[uvTarget + 1] = sourceMesh.Uvs[uvSource + 1];
            }

            int indexOffset = instanceIndex * sourceMesh.Indices.Length;
            for (var index = 0; index < sourceMesh.Indices.Length; index++)
            {
                indices[indexOffset + index] = sourceMesh.Indices[index] + (uint)vertexOffset;
            }
        }

        return new TerrainMeshData(positions, normals, uvs, indices);
    }
}
