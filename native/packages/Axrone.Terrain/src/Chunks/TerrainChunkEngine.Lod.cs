namespace Axrone.Terrain;

/// <summary>
/// Chunk LOD arithmetic. Level zero keeps the full 32x32 cell grid; each level halves the
/// cell count. Every level adds a downward skirt ring so neighbouring chunks at different
/// levels do not expose cracks.
/// </summary>
public static class TerrainChunkLod
{
    /// <summary>Highest supported LOD level.</summary>
    public const int MaxLevel = 2;

    /// <summary>Whether a level is supported.</summary>
    /// <param name="level">LOD level.</param>
    /// <returns><see langword="true"/> for levels zero through <see cref="MaxLevel"/>.</returns>
    public static bool IsValid(int level) => level is >= 0 and <= MaxLevel;

    /// <summary>Grid step in cells for a level.</summary>
    public static int Step(int level) => 1 << level;

    /// <summary>Vertices per side of the LOD grid, including the skirt index space.</summary>
    public static int VerticesPerAxis(int level) => (TerrainChunkLayout.Subdivisions >> level) + 1;

    /// <summary>Main grid vertex count.</summary>
    public static int GridVertexCount(int level)
    {
        int axis = VerticesPerAxis(level);
        return axis * axis;
    }

    /// <summary>Main grid triangle index count.</summary>
    public static int GridIndexCount(int level)
    {
        int cells = TerrainChunkLayout.Subdivisions >> level;
        return cells * cells * 6;
    }

    /// <summary>Skirt vertex count.</summary>
    public static int SkirtVertexCount(int level) => 4 * VerticesPerAxis(level);

    /// <summary>Skirt triangle index count.</summary>
    public static int SkirtIndexCount(int level) => 24 * (VerticesPerAxis(level) - 1);

    /// <summary>Total vertex count including the skirt ring.</summary>
    public static int VertexCount(int level) => GridVertexCount(level) + SkirtVertexCount(level);

    /// <summary>Total index count including the skirt ring.</summary>
    public static int IndexCount(int level) => GridIndexCount(level) + SkirtIndexCount(level);
}

/// <summary>LOD mesh extraction for committed chunks.</summary>
public partial class TerrainEngine<TSurface, TBackoff, TObserver>
{
    /// <summary>
    /// Writes a skirted LOD mesh into caller-owned buffers. Positions stay chunk-local and
    /// origin-centered; heights and normals come from the full-resolution slot, so reducing
    /// LOD never changes collision or sampling data.
    /// </summary>
    /// <param name="coord">Chunk coordinate.</param>
    /// <param name="level">LOD level in <c>[0, 2]</c>.</param>
    /// <param name="vertices">Destination; at least <see cref="TerrainChunkLod.VertexCount"/> entries.</param>
    /// <param name="indices">Destination; at least <see cref="TerrainChunkLod.IndexCount"/> entries.</param>
    /// <returns><see langword="true"/> when the chunk is resident.</returns>
    /// <exception cref="TerrainException">The level or a destination buffer is invalid.</exception>
    public bool TryBuildChunkLodMesh(
        TerrainChunkCoord coord,
        int level,
        Span<TerrainVertex> vertices,
        Span<uint> indices)
    {
        if (!TerrainChunkLod.IsValid(level))
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid terrain LOD level: {level}."),
                TerrainErrorCode.ValidationFailed);
        }

        if (vertices.Length < TerrainChunkLod.VertexCount(level) ||
            indices.Length < TerrainChunkLod.IndexCount(level))
        {
            throw new TerrainException("Chunk LOD destination buffers are too small.", TerrainErrorCode.ValidationFailed);
        }

        EnterRead();
        try
        {
            ThrowIfDisposed();
            if (!_chunks.TryGetValue(coord, out ChunkEntry? entry))
            {
                return false;
            }

            TerrainChunkView view = entry.CreateView();
            Span<float> heights = view.Heights;
            Span<byte> splat = view.Splat;
            int axis = TerrainChunkLod.VerticesPerAxis(level);
            int cells = TerrainChunkLayout.Subdivisions >> level;
            int step = TerrainChunkLod.Step(level);
            int last = axis - 1;
            float span = _chunkSpan;
            float maxHeight = _options.MaxHeight;

            for (var z = 0; z < axis; z++)
            {
                int sourceZ = z * step;
                float v = z / (float)last;
                for (var x = 0; x < axis; x++)
                {
                    int sourceX = x * step;
                    int sourceIndex = (sourceZ * TerrainChunkLayout.VerticesPerAxis) + sourceX;
                    int vertex = (z * axis) + x;
                    int splatOffset = sourceIndex * 4;
                    var weights = new Vec4(
                        splat[splatOffset] / 255f,
                        splat[splatOffset + 1] / 255f,
                        splat[splatOffset + 2] / 255f,
                        splat[splatOffset + 3] / 255f);

                    vertices[vertex] = new TerrainVertex(
                        new Vec3(
                            (x / (float)last - 0.5f) * span,
                            heights[sourceIndex] * maxHeight,
                            (v - 0.5f) * span),
                        view.GetNormal(sourceIndex),
                        new Vec2(x / (float)last, v),
                        weights);
                }
            }

            var indexOffset = 0;
            for (var z = 0; z < cells; z++)
            {
                for (var x = 0; x < cells; x++)
                {
                    uint topLeft = (uint)((z * axis) + x);
                    uint topRight = topLeft + 1;
                    uint bottomLeft = topLeft + (uint)axis;
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

            int skirtBase = axis * axis;
            float skirtDepth = MathF.Max(_options.CellSize * 4f, _options.MaxHeight * 0.05f);

            for (var edge = 0; edge < 4; edge++)
            {
                for (var i = 0; i < axis; i++)
                {
                    TerrainVertex top = vertices[LodTopIndex(edge, i, last, axis)];
                    vertices[skirtBase + (edge * axis) + i] = new TerrainVertex(
                        new Vec3(top.Position.X, top.Position.Y - skirtDepth, top.Position.Z),
                        top.Normal,
                        top.TexCoord,
                        top.SplatWeights);
                }

                for (var i = 0; i < last; i++)
                {
                    uint top0 = (uint)LodTopIndex(edge, i, last, axis);
                    uint top1 = (uint)LodTopIndex(edge, i + 1, last, axis);
                    uint skirt0 = (uint)(skirtBase + (edge * axis) + i);
                    uint skirt1 = skirt0 + 1;

                    indices[indexOffset] = top0;
                    indices[indexOffset + 1] = skirt0;
                    indices[indexOffset + 2] = top1;
                    indices[indexOffset + 3] = top1;
                    indices[indexOffset + 4] = skirt0;
                    indices[indexOffset + 5] = skirt1;
                    indexOffset += 6;
                }
            }

            return true;
        }
        finally
        {
            ExitRead();
        }
    }

    /// <summary>Builds a skirted LOD mesh for a resident chunk.</summary>
    /// <param name="coord">Chunk coordinate.</param>
    /// <param name="level">LOD level in <c>[0, 2]</c>.</param>
    /// <returns>The mesh, or <see langword="null"/> when the chunk is not resident.</returns>
    public TerrainVertexMesh? BuildChunkLodMesh(TerrainChunkCoord coord, int level)
    {
        if (!TerrainChunkLod.IsValid(level))
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid terrain LOD level: {level}."),
                TerrainErrorCode.ValidationFailed);
        }

        var vertices = new TerrainVertex[TerrainChunkLod.VertexCount(level)];
        var indices = new uint[TerrainChunkLod.IndexCount(level)];
        return TryBuildChunkLodMesh(coord, level, vertices, indices)
            ? new TerrainVertexMesh(vertices, indices)
            : null;
    }

    private static int LodTopIndex(int edge, int index, int last, int axis) => edge switch
    {
        0 => index,
        1 => (last * axis) + index,
        2 => index * axis,
        _ => (index * axis) + last,
    };
}
