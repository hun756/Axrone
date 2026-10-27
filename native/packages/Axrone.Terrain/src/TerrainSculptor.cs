namespace Axrone.Terrain;

/// <summary>
/// Applies height brushes to a mutable working buffer. Sculpt sessions hold a
/// mutable copy of the heightmap; each pointer step applies one stamp in place,
/// and the session is committed back into an immutable
/// <see cref="TerrainHeightmap"/> when the drag ends.
/// </summary>
public static class TerrainSculptor
{
    /// <summary>
    /// Applies a single brush stamp in place. Returns <see langword="true"/> when
    /// at least one height sample changed, so callers can skip redundant mesh
    /// rebuilds.
    /// </summary>
    /// <param name="heights">Working buffer of <c>resolution * resolution</c> normalized heights.</param>
    /// <param name="descriptor">Terrain footprint and resolution.</param>
    /// <param name="brush">Resolved brush parameters.</param>
    /// <param name="localX">Stamp center X in terrain-local space (origin-centered grid).</param>
    /// <param name="localZ">Stamp center Z in terrain-local space (origin-centered grid).</param>
    /// <returns><see langword="true"/> if any sample changed.</returns>
    /// <exception cref="TerrainException">The descriptor, brush or buffer is invalid.</exception>
    public static bool ApplyBrushStamp(
        Span<float> heights,
        in TerrainDescriptor descriptor,
        in TerrainBrushOptions brush,
        float localX,
        float localZ)
    {
        descriptor.Validate();
        brush.Validate();

        int resolution = descriptor.Resolution;
        if (heights.Length != resolution * resolution)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Working buffer length {heights.Length} does not match resolution {resolution}."),
                TerrainErrorCode.HeightmapSizeMismatch);
        }

        int lastIndex = resolution - 1;
        float stepX = descriptor.Width / lastIndex;
        float stepZ = descriptor.Length / lastIndex;

        float gridCenterX = (localX + (descriptor.Width * 0.5f)) / stepX;
        float gridCenterZ = (localZ + (descriptor.Length * 0.5f)) / stepZ;
        float gridRadiusX = brush.Radius / stepX;
        float gridRadiusZ = brush.Radius / stepZ;

        int minX = Math.Max(0, (int)MathF.Floor(gridCenterX - gridRadiusX));
        int maxX = Math.Min(lastIndex, (int)MathF.Ceiling(gridCenterX + gridRadiusX));
        int minZ = Math.Max(0, (int)MathF.Floor(gridCenterZ - gridRadiusZ));
        int maxZ = Math.Min(lastIndex, (int)MathF.Ceiling(gridCenterZ + gridRadiusZ));
        if (minX > maxX || minZ > maxZ)
        {
            return false;
        }

        float stampDelta = brush.Strength * TerrainLimits.BrushHeightStampScale;
        float flattenTarget = brush.FlattenTarget;
        if (brush.Kind == TerrainBrushKind.Flatten)
        {
            int sampleX = Math.Clamp(TerrainMath.RoundHalfUp(gridCenterX), 0, lastIndex);
            int sampleZ = Math.Clamp(TerrainMath.RoundHalfUp(gridCenterZ), 0, lastIndex);
            flattenTarget = heights[(sampleZ * resolution) + sampleX];
        }

        float[]? rented = null;
        Span<float> source = heights;
        var sourceOffsetX = 0;
        var sourceOffsetZ = 0;
        var sourceWidth = 0;

        if (brush.Kind == TerrainBrushKind.Smooth)
        {
            int snapMinX = Math.Max(0, minX - 1);
            int snapMaxX = Math.Min(lastIndex, maxX + 1);
            int snapMinZ = Math.Max(0, minZ - 1);
            int snapMaxZ = Math.Min(lastIndex, maxZ + 1);
            sourceWidth = snapMaxX - snapMinX + 1;
            int sourceHeight = snapMaxZ - snapMinZ + 1;
            sourceOffsetX = snapMinX;
            sourceOffsetZ = snapMinZ;

            rented = ArrayPool<float>.Shared.Rent(sourceWidth * sourceHeight);
            source = rented.AsSpan(0, sourceWidth * sourceHeight);

            for (var z = snapMinZ; z <= snapMaxZ; z++)
            {
                int srcRow = (z * resolution) + snapMinX;
                int dstRow = (z - snapMinZ) * sourceWidth;
                for (var x = 0; x < sourceWidth; x++)
                {
                    source[dstRow + x] = heights[srcRow + x];
                }
            }
        }

        try
        {
            var changed = false;

            for (var gridZ = minZ; gridZ <= maxZ; gridZ++)
            {
                for (var gridX = minX; gridX <= maxX; gridX++)
                {
                    float worldDx = (gridX - gridCenterX) * stepX;
                    float worldDz = (gridZ - gridCenterZ) * stepZ;
                    float distance = MathF.Sqrt((worldDx * worldDx) + (worldDz * worldDz));
                    if (distance > brush.Radius)
                    {
                        continue;
                    }

                    float weight = MathF.Pow(
                        TerrainMath.Smoothstep(1f - (distance / brush.Radius)),
                        brush.Falloff);
                    if (weight <= 0f)
                    {
                        continue;
                    }

                    int index = (gridZ * resolution) + gridX;
                    float current = heights[index];
                    float next = brush.Kind switch
                    {
                        TerrainBrushKind.Raise => current + (stampDelta * weight),
                        TerrainBrushKind.Lower => current - (stampDelta * weight),
                        TerrainBrushKind.Flatten => current + ((flattenTarget - current) * brush.Strength * weight),
                        TerrainBrushKind.Smooth => SmoothSample(
                            source,
                            sourceOffsetX,
                            sourceOffsetZ,
                            sourceWidth,
                            lastIndex,
                            gridX,
                            gridZ,
                            current,
                            brush.Strength,
                            weight),
                        _ => current,
                    };

                    next = TerrainMath.Clamp01(next);
                    if (next != current)
                    {
                        heights[index] = next;
                        changed = true;
                    }
                }
            }

            return changed;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<float>.Shared.Return(rented);
            }
        }
    }

    private static float SmoothSample(
        ReadOnlySpan<float> source,
        int sourceOffsetX,
        int sourceOffsetZ,
        int sourceWidth,
        int lastIndex,
        int gridX,
        int gridZ,
        float current,
        float strength,
        float weight)
    {
        float total = 0f;
        var count = 0;

        for (var offsetZ = -1; offsetZ <= 1; offsetZ++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                int neighborX = gridX + offsetX;
                int neighborZ = gridZ + offsetZ;
                if (neighborX < 0 || neighborZ < 0 || neighborX > lastIndex || neighborZ > lastIndex)
                {
                    continue;
                }

                int snapX = neighborX - sourceOffsetX;
                int snapZ = neighborZ - sourceOffsetZ;
                total += source[(snapZ * sourceWidth) + snapX];
                count++;
            }
        }

        float average = total / count;
        return current + ((average - current) * strength * weight);
    }
}
