namespace Axrone.Terrain.Chunks;

/// <summary>
/// Sculpt, splat and foliage-density painting. Every stamp takes the engine write lock, so
/// a reader never observes a half-applied brush, and smoothing snapshots all affected
/// chunks before writing so the result does not depend on iteration order.
/// </summary>
public sealed partial class TerrainChunkEngine
{
    /// <summary>Applies a height brush around a world-space position.</summary>
    /// <param name="worldX">Brush center X.</param>
    /// <param name="worldZ">Brush center Z.</param>
    /// <param name="brush">Resolved brush parameters.</param>
    /// <returns><see langword="true"/> when at least one height changed.</returns>
    /// <exception cref="TerrainException">The brush is invalid.</exception>
    /// <exception cref="ObjectDisposedException">The engine is disposed.</exception>
    public bool ApplyHeightBrush(float worldX, float worldZ, in TerrainBrushOptions brush)
    {
        brush.Validate();
        _gate.EnterWriteLock();
        try
        {
            ThrowIfDisposed();
            List<BrushTarget> targets = CollectTargets(worldX, worldZ, brush.Radius);
            if (targets.Count == 0)
            {
                return false;
            }

            if (brush.Kind == TerrainBrushKind.Smooth)
            {
                foreach (BrushTarget target in targets)
                {
                    float[] snapshot = ArrayPool<float>.Shared.Rent(TerrainChunkLayout.VertexCount);
                    target.Entry.CreateView().Heights.CopyTo(snapshot);
                    target.Snapshot = snapshot;
                }
            }

            try
            {
                var changed = false;
                foreach (BrushTarget target in targets)
                {
                    changed |= ApplyHeightBrushToChunk(target, worldX, worldZ, brush, targets);
                }

                return changed;
            }
            finally
            {
                foreach (BrushTarget target in targets)
                {
                    if (target.Snapshot is not null)
                    {
                        ArrayPool<float>.Shared.Return(target.Snapshot);
                        target.Snapshot = null;
                    }
                }
            }
        }
        finally
        {
            _gate.ExitWriteLock();
        }
    }

    /// <summary>Paints one splat layer around a world-space position.</summary>
    /// <param name="worldX">Brush center X.</param>
    /// <param name="worldZ">Brush center Z.</param>
    /// <param name="brush">Resolved brush parameters.</param>
    /// <param name="layerIndex">Target layer in <c>[0, 3]</c>.</param>
    /// <returns><see langword="true"/> when at least one texel changed.</returns>
    /// <exception cref="TerrainException">The brush or layer index is invalid.</exception>
    /// <exception cref="ObjectDisposedException">The engine is disposed.</exception>
    public bool ApplySplatBrush(float worldX, float worldZ, in TerrainBrushOptions brush, int layerIndex)
    {
        brush.Validate();
        if (layerIndex < 0 || layerIndex >= TerrainLimits.MaxLayers)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid splat layer index: {layerIndex}."),
                TerrainErrorCode.ValidationFailed);
        }

        _gate.EnterWriteLock();
        try
        {
            ThrowIfDisposed();
            List<BrushTarget> targets = CollectTargets(worldX, worldZ, brush.Radius);
            var changed = false;
            foreach (BrushTarget target in targets)
            {
                changed |= ApplySplatBrushToChunk(target, worldX, worldZ, brush, layerIndex);
            }

            return changed;
        }
        finally
        {
            _gate.ExitWriteLock();
        }
    }

    /// <summary>Paints one foliage density layer around a world-space position.</summary>
    /// <param name="worldX">Brush center X.</param>
    /// <param name="worldZ">Brush center Z.</param>
    /// <param name="brush">Resolved brush parameters.</param>
    /// <param name="layerIndex">Target layer in <c>[0, 3]</c>.</param>
    /// <param name="erase"><see langword="true"/> removes density instead of adding it.</param>
    /// <returns><see langword="true"/> when at least one texel changed.</returns>
    /// <exception cref="TerrainException">The brush or layer index is invalid.</exception>
    /// <exception cref="ObjectDisposedException">The engine is disposed.</exception>
    public bool ApplyFoliageDensityBrush(
        float worldX,
        float worldZ,
        in TerrainBrushOptions brush,
        int layerIndex,
        bool erase = false)
    {
        brush.Validate();
        if (layerIndex < 0 || layerIndex >= TerrainChunkLayout.FoliageLayers)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid foliage layer index: {layerIndex}."),
                TerrainErrorCode.ValidationFailed);
        }

        _gate.EnterWriteLock();
        try
        {
            ThrowIfDisposed();
            List<BrushTarget> targets = CollectTargets(worldX, worldZ, brush.Radius);
            var changed = false;
            foreach (BrushTarget target in targets)
            {
                changed |= ApplyFoliageBrushToChunk(target, worldX, worldZ, brush, layerIndex, erase);
            }

            return changed;
        }
        finally
        {
            _gate.ExitWriteLock();
        }
    }

    private List<BrushTarget> CollectTargets(float worldX, float worldZ, float radius)
    {
        var targets = new List<BrushTarget>(4);
        int minX = (int)MathF.Floor((worldX - radius) / _chunkSpan);
        int maxX = (int)MathF.Floor((worldX + radius) / _chunkSpan);
        int minZ = (int)MathF.Floor((worldZ - radius) / _chunkSpan);
        int maxZ = (int)MathF.Floor((worldZ + radius) / _chunkSpan);

        for (var z = minZ; z <= maxZ; z++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var coord = new TerrainChunkCoord(x, z);
                if (_chunks.TryGetValue(coord, out ChunkEntry? entry))
                {
                    targets.Add(new BrushTarget(coord, entry));
                }
            }
        }

        return targets;
    }

    private bool ApplyHeightBrushToChunk(
        BrushTarget target,
        float worldX,
        float worldZ,
        in TerrainBrushOptions brush,
        List<BrushTarget> targets)
    {
        TerrainChunkView view = target.Entry.CreateView();
        Span<float> heights = view.Heights;

        float originX = target.Coord.X * _chunkSpan;
        float originZ = target.Coord.Z * _chunkSpan;
        float cellSize = _options.CellSize;
        float gridCenterX = (worldX - originX) / cellSize;
        float gridCenterZ = (worldZ - originZ) / cellSize;
        float gridRadius = brush.Radius / cellSize;

        int minX = Math.Max(0, (int)MathF.Floor(gridCenterX - gridRadius));
        int maxX = Math.Min(TerrainChunkLayout.Subdivisions, (int)MathF.Ceiling(gridCenterX + gridRadius));
        int minZ = Math.Max(0, (int)MathF.Floor(gridCenterZ - gridRadius));
        int maxZ = Math.Min(TerrainChunkLayout.Subdivisions, (int)MathF.Ceiling(gridCenterZ + gridRadius));
        if (minX > maxX || minZ > maxZ)
        {
            return false;
        }

        float flattenTarget = brush.FlattenTarget;
        if (brush.Kind == TerrainBrushKind.Flatten)
        {
            int sampleX = Math.Clamp(TerrainMathRound(gridCenterX), 0, TerrainChunkLayout.Subdivisions);
            int sampleZ = Math.Clamp(TerrainMathRound(gridCenterZ), 0, TerrainChunkLayout.Subdivisions);
            flattenTarget = GetPreStampHeight(target, sampleX, sampleZ, targets);
        }

        float radiusSquared = brush.Radius * brush.Radius;
        float stampScale = brush.Strength * TerrainLimits.BrushHeightStampScale;
        var changed = false;

        for (var gridZ = minZ; gridZ <= maxZ; gridZ++)
        {
            for (var gridX = minX; gridX <= maxX; gridX++)
            {
                float dx = (originX + (gridX * cellSize)) - worldX;
                float dz = (originZ + (gridZ * cellSize)) - worldZ;
                float distanceSquared = (dx * dx) + (dz * dz);
                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                float distance = MathF.Sqrt(distanceSquared);
                float weight = MathF.Pow(Smoothstep(1f - (distance / brush.Radius)), brush.Falloff);
                if (weight <= 0f)
                {
                    continue;
                }

                int index = (gridZ * TerrainChunkLayout.VerticesPerAxis) + gridX;
                float current = heights[index];
                float next = brush.Kind switch
                {
                    TerrainBrushKind.Raise => current + (stampScale * weight),
                    TerrainBrushKind.Lower => current - (stampScale * weight),
                    TerrainBrushKind.Flatten => current + ((flattenTarget - current) * brush.Strength * weight),
                    TerrainBrushKind.Smooth => current +
                        ((AverageNeighborhood(target, gridX, gridZ, targets) - current) * brush.Strength * weight),
                    _ => current,
                };

                next = Math.Clamp(next, 0f, 1f);
                if (next != current)
                {
                    heights[index] = next;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            UpdateExtentsAndNormals(view);
        }

        return changed;
    }

    private bool ApplySplatBrushToChunk(
        BrushTarget target,
        float worldX,
        float worldZ,
        in TerrainBrushOptions brush,
        int layerIndex)
    {
        TerrainChunkView view = target.Entry.CreateView();
        Span<byte> splat = view.Splat;

        float originX = target.Coord.X * _chunkSpan;
        float originZ = target.Coord.Z * _chunkSpan;
        float cellSize = _options.CellSize;
        float gridCenterX = (worldX - originX) / cellSize;
        float gridCenterZ = (worldZ - originZ) / cellSize;
        float gridRadius = brush.Radius / cellSize;

        int minX = Math.Max(0, (int)MathF.Floor(gridCenterX - gridRadius));
        int maxX = Math.Min(TerrainChunkLayout.Subdivisions, (int)MathF.Ceiling(gridCenterX + gridRadius));
        int minZ = Math.Max(0, (int)MathF.Floor(gridCenterZ - gridRadius));
        int maxZ = Math.Min(TerrainChunkLayout.Subdivisions, (int)MathF.Ceiling(gridCenterZ + gridRadius));
        if (minX > maxX || minZ > maxZ)
        {
            return false;
        }

        float radiusSquared = brush.Radius * brush.Radius;
        var changed = false;

        for (var gridZ = minZ; gridZ <= maxZ; gridZ++)
        {
            for (var gridX = minX; gridX <= maxX; gridX++)
            {
                float dx = (originX + (gridX * cellSize)) - worldX;
                float dz = (originZ + (gridZ * cellSize)) - worldZ;
                float distanceSquared = (dx * dx) + (dz * dz);
                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                float distance = MathF.Sqrt(distanceSquared);
                float weight = MathF.Pow(Smoothstep(1f - (distance / brush.Radius)), brush.Falloff);
                if (weight <= 0f)
                {
                    continue;
                }

                int index = (gridZ * TerrainChunkLayout.VerticesPerAxis) + gridX;
                if (TerrainSplatMath.Paint(
                        splat.Slice(index * 4, 4),
                        layerIndex,
                        brush.Strength * weight * TerrainLimits.SplatStampScale))
                {
                    changed = true;
                }
            }
        }

        return changed;
    }

    private bool ApplyFoliageBrushToChunk(
        BrushTarget target,
        float worldX,
        float worldZ,
        in TerrainBrushOptions brush,
        int layerIndex,
        bool erase)
    {
        TerrainChunkView view = target.Entry.CreateView();
        Span<byte> density = view.FoliageDensity(layerIndex);

        float originX = target.Coord.X * _chunkSpan;
        float originZ = target.Coord.Z * _chunkSpan;
        float cellSize = _options.CellSize;
        float gridCenterX = (worldX - originX) / cellSize;
        float gridCenterZ = (worldZ - originZ) / cellSize;
        float gridRadius = brush.Radius / cellSize;

        int minX = Math.Max(0, (int)MathF.Floor(gridCenterX - gridRadius));
        int maxX = Math.Min(TerrainChunkLayout.Subdivisions, (int)MathF.Ceiling(gridCenterX + gridRadius));
        int minZ = Math.Max(0, (int)MathF.Floor(gridCenterZ - gridRadius));
        int maxZ = Math.Min(TerrainChunkLayout.Subdivisions, (int)MathF.Ceiling(gridCenterZ + gridRadius));
        if (minX > maxX || minZ > maxZ)
        {
            return false;
        }

        float radiusSquared = brush.Radius * brush.Radius;
        float stampScale = brush.Strength * TerrainLimits.FoliageDensityStampScale;
        var changed = false;

        for (var gridZ = minZ; gridZ <= maxZ; gridZ++)
        {
            for (var gridX = minX; gridX <= maxX; gridX++)
            {
                float dx = (originX + (gridX * cellSize)) - worldX;
                float dz = (originZ + (gridZ * cellSize)) - worldZ;
                float distanceSquared = (dx * dx) + (dz * dz);
                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                float distance = MathF.Sqrt(distanceSquared);
                float weight = MathF.Pow(Smoothstep(1f - (distance / brush.Radius)), brush.Falloff);
                if (weight <= 0f)
                {
                    continue;
                }

                int index = (gridZ * TerrainChunkLayout.VerticesPerAxis) + gridX;
                int current = density[index];
                int targetValue = erase
                    ? Math.Max(0, TerrainMathRound(current - (255f * stampScale * weight)))
                    : Math.Min(255, TerrainMathRound(current + ((255 - current) * stampScale * weight)));
                if (targetValue != current)
                {
                    density[index] = (byte)targetValue;
                    changed = true;
                }
            }
        }

        return changed;
    }

    private float AverageNeighborhood(BrushTarget target, int gridX, int gridZ, List<BrushTarget> targets)
    {
        float total = 0f;
        var count = 0;

        for (var offsetZ = -1; offsetZ <= 1; offsetZ++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                total += GetPreStampHeight(target, gridX + offsetX, gridZ + offsetZ, targets);
                count++;
            }
        }

        return total / count;
    }

    private float GetPreStampHeight(BrushTarget current, int gridX, int gridZ, List<BrushTarget> targets)
    {
        if ((uint)gridX < TerrainChunkLayout.VerticesPerAxis &&
            (uint)gridZ < TerrainChunkLayout.VerticesPerAxis)
        {
            return ReadTargetHeight(current, gridX, gridZ);
        }

        int neighborX = current.Coord.X + (gridX < 0 ? -1 : gridX >= TerrainChunkLayout.VerticesPerAxis ? 1 : 0);
        int neighborZ = current.Coord.Z + (gridZ < 0 ? -1 : gridZ >= TerrainChunkLayout.VerticesPerAxis ? 1 : 0);
        int localX = gridX < 0 ? TerrainChunkLayout.Subdivisions : gridX >= TerrainChunkLayout.VerticesPerAxis ? 0 : gridX;
        int localZ = gridZ < 0 ? TerrainChunkLayout.Subdivisions : gridZ >= TerrainChunkLayout.VerticesPerAxis ? 0 : gridZ;

        var neighborCoord = new TerrainChunkCoord(neighborX, neighborZ);
        foreach (BrushTarget candidate in targets)
        {
            if (candidate.Coord == neighborCoord)
            {
                return ReadTargetHeight(candidate, localX, localZ);
            }
        }

        float worldX = (current.Coord.X * _chunkSpan) + (gridX * _options.CellSize);
        float worldZ = (current.Coord.Z * _chunkSpan) + (gridZ * _options.CellSize);
        return _options.Surface.SampleHeight(worldX, worldZ);
    }

    private static float ReadTargetHeight(BrushTarget target, int gridX, int gridZ)
    {
        ReadOnlySpan<float> heights = target.Snapshot is not null
            ? target.Snapshot.AsSpan(0, TerrainChunkLayout.VertexCount)
            : target.Entry.CreateView().Heights;
        return heights[(gridZ * TerrainChunkLayout.VerticesPerAxis) + gridX];
    }

    private void UpdateExtentsAndNormals(TerrainChunkView view)
    {
        Span<float> heights = view.Heights;
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        foreach (float height in heights)
        {
            if (height < min)
            {
                min = height;
            }

            if (height > max)
            {
                max = height;
            }
        }

        ref TerrainChunkHeader header = ref view.Header;
        header.MinHeight = min;
        header.MaxHeight = max;
        ComputeNormals(view);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int TerrainMathRound(float value) => (int)MathF.Floor(value + 0.5f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Smoothstep(float value) => value * value * (3f - (2f * value));

    private sealed class BrushTarget
    {
        public BrushTarget(TerrainChunkCoord coord, ChunkEntry entry)
        {
            Coord = coord;
            Entry = entry;
        }

        public TerrainChunkCoord Coord { get; }

        public ChunkEntry Entry { get; }

        public float[]? Snapshot { get; set; }
    }
}
