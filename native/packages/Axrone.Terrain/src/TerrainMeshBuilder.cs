namespace Axrone.Terrain;

/// <summary>
/// Builds an origin-centered grid mesh from a normalized heightmap. Vertex Y
/// values are scaled by <see cref="TerrainDescriptor.MaxHeight"/>; normals are
/// derived with central differences over the world-space grid.
/// </summary>
public static class TerrainMeshBuilder
{
    /// <summary>Builds the terrain surface mesh.</summary>
    /// <param name="heightmap">Heightmap whose resolution matches the descriptor.</param>
    /// <param name="descriptor">Terrain footprint and resolution.</param>
    /// <returns>A mesh with positions, normals, UVs and 32-bit indices.</returns>
    /// <exception cref="TerrainException">
    /// The descriptor is invalid, or the heightmap resolution differs from it.
    /// </exception>
    public static TerrainMeshData Build(TerrainHeightmap heightmap, in TerrainDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(heightmap);
        descriptor.Validate();

        if (heightmap.Resolution != descriptor.Resolution)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Heightmap resolution {heightmap.Resolution} does not match descriptor resolution {descriptor.Resolution}."),
                TerrainErrorCode.HeightmapSizeMismatch);
        }

        int resolution = descriptor.Resolution;
        ReadOnlySpan<float> heights = heightmap.Heights;
        int vertexCount = resolution * resolution;
        int lastIndex = resolution - 1;
        int triangleCount = lastIndex * lastIndex * 2;

        var positions = new float[vertexCount * 3];
        var normals = new float[vertexCount * 3];
        var uvs = new float[vertexCount * 2];
        var indices = new uint[triangleCount * 3];

        float stepX = descriptor.Width / lastIndex;
        float stepZ = descriptor.Length / lastIndex;

        for (var gridZ = 0; gridZ < resolution; gridZ++)
        {
            float v = gridZ / (float)lastIndex;
            float worldZ = (v - 0.5f) * descriptor.Length;

            for (var gridX = 0; gridX < resolution; gridX++)
            {
                float u = gridX / (float)lastIndex;
                int vertexIndex = (gridZ * resolution) + gridX;
                int positionOffset = vertexIndex * 3;

                positions[positionOffset] = (u - 0.5f) * descriptor.Width;
                positions[positionOffset + 1] = heights[vertexIndex] * descriptor.MaxHeight;
                positions[positionOffset + 2] = worldZ;

                uvs[vertexIndex * 2] = u;
                uvs[(vertexIndex * 2) + 1] = v;

                int leftX = gridX > 0 ? gridX - 1 : 0;
                int rightX = gridX < lastIndex ? gridX + 1 : lastIndex;
                int nearZ = gridZ > 0 ? gridZ - 1 : 0;
                int farZ = gridZ < lastIndex ? gridZ + 1 : lastIndex;

                float slopeX =
                    ((heights[(gridZ * resolution) + rightX] - heights[(gridZ * resolution) + leftX]) *
                     descriptor.MaxHeight) /
                    ((rightX - leftX) * stepX);
                float slopeZ =
                    ((heights[(farZ * resolution) + gridX] - heights[(nearZ * resolution) + gridX]) *
                     descriptor.MaxHeight) /
                    ((farZ - nearZ) * stepZ);

                float inverseMagnitude =
                    1f / MathF.Max(MathF.Sqrt((slopeX * slopeX) + 1f + (slopeZ * slopeZ)), TerrainMath.Epsilon);
                normals[positionOffset] = -slopeX * inverseMagnitude;
                normals[positionOffset + 1] = inverseMagnitude;
                normals[positionOffset + 2] = -slopeZ * inverseMagnitude;
            }
        }

        var indexOffset = 0;
        for (var cellZ = 0; cellZ < lastIndex; cellZ++)
        {
            for (var cellX = 0; cellX < lastIndex; cellX++)
            {
                uint topLeft = (uint)((cellZ * resolution) + cellX);
                uint topRight = topLeft + 1;
                uint bottomLeft = topLeft + (uint)resolution;
                uint bottomRight = bottomLeft + 1;

                indices[indexOffset] = topLeft;
                indices[indexOffset + 1] = bottomLeft;
                indices[indexOffset + 2] = topRight;
                indices[indexOffset + 3] = topRight;
                indices[indexOffset + 4] = bottomLeft;
                indices[indexOffset + 5] = bottomRight;
                indexOffset += 6;
            }
        }

        return new TerrainMeshData(positions, normals, uvs, indices);
    }
}
