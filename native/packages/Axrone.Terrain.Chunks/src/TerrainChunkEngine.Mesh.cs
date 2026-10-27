namespace Axrone.Terrain.Chunks;

/// <summary>Mesh extraction from committed chunks.</summary>
public sealed partial class TerrainChunkEngine
{
    /// <summary>
    /// Writes a chunk's surface mesh into caller-owned buffers. Positions are chunk-local and
    /// origin-centered, so the actor that renders the chunk sits at the chunk center. Heights
    /// are scaled by <see cref="MaxHeight"/>.
    /// </summary>
    /// <param name="coord">Chunk coordinate.</param>
    /// <param name="positions">Destination XYZ positions; at least <c>VertexCount * 3</c> floats.</param>
    /// <param name="normals">Destination XYZ normals; at least <c>VertexCount * 3</c> floats.</param>
    /// <param name="uvs">Destination UV pairs; at least <c>VertexCount * 2</c> floats.</param>
    /// <param name="indices">Destination triangle indices; at least <c>IndexCount</c> entries.</param>
    /// <returns><see langword="true"/> when the chunk is resident.</returns>
    /// <exception cref="TerrainException">A destination buffer is too small.</exception>
    public bool TryBuildChunkMesh(
        TerrainChunkCoord coord,
        Span<float> positions,
        Span<float> normals,
        Span<float> uvs,
        Span<uint> indices)
    {
        if (positions.Length < TerrainChunkLayout.VertexCount * 3 ||
            normals.Length < TerrainChunkLayout.VertexCount * 3 ||
            uvs.Length < TerrainChunkLayout.VertexCount * 2 ||
            indices.Length < TerrainChunkLayout.IndexCount)
        {
            throw new TerrainException("Chunk mesh destination buffers are too small.", TerrainErrorCode.ValidationFailed);
        }

        _gate.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            if (!_chunks.TryGetValue(coord, out ChunkEntry? entry))
            {
                return false;
            }

            TerrainChunkView view = entry.CreateView();
            Span<float> heights = view.Heights;
            float span = _chunkSpan;
            float maxHeight = _options.MaxHeight;

            for (var z = 0; z < TerrainChunkLayout.VerticesPerAxis; z++)
            {
                float v = z / (float)TerrainChunkLayout.Subdivisions;
                for (var x = 0; x < TerrainChunkLayout.VerticesPerAxis; x++)
                {
                    float u = x / (float)TerrainChunkLayout.Subdivisions;
                    int vertex = (z * TerrainChunkLayout.VerticesPerAxis) + x;
                    int positionOffset = vertex * 3;

                    positions[positionOffset] = (u - 0.5f) * span;
                    positions[positionOffset + 1] = heights[vertex] * maxHeight;
                    positions[positionOffset + 2] = (v - 0.5f) * span;

                    Vec3 normal = view.GetNormal(vertex);
                    normals[positionOffset] = normal.X;
                    normals[positionOffset + 1] = normal.Y;
                    normals[positionOffset + 2] = normal.Z;

                    uvs[vertex * 2] = u;
                    uvs[(vertex * 2) + 1] = v;
                }
            }

            var indexOffset = 0;
            for (var z = 0; z < TerrainChunkLayout.Subdivisions; z++)
            {
                for (var x = 0; x < TerrainChunkLayout.Subdivisions; x++)
                {
                    uint topLeft = (uint)((z * TerrainChunkLayout.VerticesPerAxis) + x);
                    uint topRight = topLeft + 1;
                    uint bottomLeft = topLeft + TerrainChunkLayout.VerticesPerAxis;
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

            return true;
        }
        finally
        {
            _gate.ExitReadLock();
        }
    }

    /// <summary>Copies a chunk's normalized heights into a caller-owned destination.</summary>
    /// <param name="coord">Chunk coordinate.</param>
    /// <param name="destination">Destination of at least <c>VertexCount</c> floats.</param>
    /// <returns><see langword="true"/> when the chunk is resident.</returns>
    /// <exception cref="TerrainException">The destination is too small.</exception>
    public bool TryCopyChunkHeights(TerrainChunkCoord coord, Span<float> destination)
    {
        if (destination.Length < TerrainChunkLayout.VertexCount)
        {
            throw new TerrainException("Chunk height destination is too small.", TerrainErrorCode.ValidationFailed);
        }

        _gate.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            if (!_chunks.TryGetValue(coord, out ChunkEntry? entry))
            {
                return false;
            }

            entry.CreateView().Heights.CopyTo(destination);
            return true;
        }
        finally
        {
            _gate.ExitReadLock();
        }
    }

    /// <summary>Copies a chunk's RGBA splat weights into a caller-owned destination.</summary>
    /// <param name="coord">Chunk coordinate.</param>
    /// <param name="destination">Destination of at least <c>SplatBytes</c> entries.</param>
    /// <returns><see langword="true"/> when the chunk is resident.</returns>
    /// <exception cref="TerrainException">The destination is too small.</exception>
    public bool TryCopyChunkSplat(TerrainChunkCoord coord, Span<byte> destination)
    {
        if (destination.Length < TerrainChunkLayout.SplatBytes)
        {
            throw new TerrainException("Chunk splat destination is too small.", TerrainErrorCode.ValidationFailed);
        }

        _gate.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            if (!_chunks.TryGetValue(coord, out ChunkEntry? entry))
            {
                return false;
            }

            entry.CreateView().Splat.CopyTo(destination);
            return true;
        }
        finally
        {
            _gate.ExitReadLock();
        }
    }

    /// <summary>Copies one foliage density layer into a caller-owned destination.</summary>
    /// <param name="coord">Chunk coordinate.</param>
    /// <param name="layerIndex">Layer in <c>[0, 3]</c>.</param>
    /// <param name="destination">Destination of at least <c>VertexCount</c> entries.</param>
    /// <returns><see langword="true"/> when the chunk is resident.</returns>
    /// <exception cref="TerrainException">The layer index or destination is invalid.</exception>
    public bool TryCopyChunkFoliageDensity(TerrainChunkCoord coord, int layerIndex, Span<byte> destination)
    {
        if (layerIndex < 0 || layerIndex >= TerrainChunkLayout.FoliageLayers)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid foliage layer index: {layerIndex}."),
                TerrainErrorCode.ValidationFailed);
        }

        if (destination.Length < TerrainChunkLayout.VertexCount)
        {
            throw new TerrainException("Chunk density destination is too small.", TerrainErrorCode.ValidationFailed);
        }

        _gate.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            if (!_chunks.TryGetValue(coord, out ChunkEntry? entry))
            {
                return false;
            }

            entry.CreateView().FoliageDensity(layerIndex).CopyTo(destination);
            return true;
        }
        finally
        {
            _gate.ExitReadLock();
        }
    }

    /// <summary>Creates a physics heightfield source for a resident chunk.</summary>
    /// <param name="coord">Chunk coordinate.</param>
    /// <returns>The source, or <see langword="null"/> when the chunk is not resident.</returns>
    public TerrainHeightfieldSource? CreateHeightfieldSource(TerrainChunkCoord coord)
    {
        _gate.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            if (!_chunks.TryGetValue(coord, out ChunkEntry? entry))
            {
                return null;
            }

            var heights = new float[TerrainChunkLayout.VertexCount];
            entry.CreateView().Heights.CopyTo(heights);
            return new TerrainHeightfieldSource(
                heights,
                TerrainChunkLayout.VerticesPerAxis,
                TerrainChunkLayout.VerticesPerAxis,
                _options.CellSize,
                _options.MaxHeight,
                _options.CellSize);
        }
        finally
        {
            _gate.ExitReadLock();
        }
    }
}
