namespace Axrone.Scene.Runtime;

/// <summary>Configuration for the terrain residency loop.</summary>
public sealed class SceneTerrainStreamingOptions
{
    /// <summary>Gets the view radius in chunks, measured as a square. Defaults to 4.</summary>
    public int ViewRadiusChunks { get; init; } = 4;

    /// <summary>Gets the Chebyshev distance that still renders at LOD zero. Defaults to 1.</summary>
    public int Lod0Radius { get; init; } = 1;

    /// <summary>Gets the Chebyshev distance that still renders at LOD one. Defaults to 3.</summary>
    public int Lod1Radius { get; init; }

    /// <summary>Gets the maximum chunks committed per update. Defaults to 2.</summary>
    public int MaxCommitsPerUpdate { get; init; } = 2;

    /// <summary>Gets the maximum chunks rebuilt at a new LOD per update. Defaults to 1.</summary>
    public int MaxRebuildsPerUpdate { get; init; } = 1;

    /// <summary>Gets the maximum chunks evicted per update. Defaults to 2.</summary>
    public int MaxDecommitsPerUpdate { get; init; } = 2;

    /// <summary>Validates the options.</summary>
    /// <exception cref="TerrainException">A radius or budget is outside its documented domain.</exception>
    public void Validate()
    {
        if (ViewRadiusChunks < 0 || Lod0Radius < 0 || Lod1Radius < 0)
        {
            throw new TerrainException("Terrain streaming radii must not be negative.", TerrainErrorCode.ValidationFailed);
        }

        if (Lod0Radius > Lod1Radius || Lod1Radius > ViewRadiusChunks)
        {
            throw new TerrainException(
                "Terrain streaming radii must satisfy lod0 <= lod1 <= view.",
                TerrainErrorCode.ValidationFailed);
        }

        if (MaxCommitsPerUpdate <= 0 || MaxRebuildsPerUpdate <= 0 || MaxDecommitsPerUpdate <= 0)
        {
            throw new TerrainException("Terrain streaming budgets must be positive.", TerrainErrorCode.ValidationFailed);
        }
    }
}

/// <summary>Operations performed by one residency update.</summary>
/// <param name="Committed">Newly committed chunks.</param>
/// <param name="Rebuilt">Chunks rebuilt at a different LOD.</param>
/// <param name="Decommitted">Chunks evicted from the view.</param>
public readonly record struct SceneTerrainStreamingStats(
    int Committed,
    int Rebuilt,
    int Decommitted);

/// <summary>
/// Residency loop around <see cref="SceneTerrainRuntime"/>. Each update derives the desired
/// chunk square around a focus point, selects an LOD by distance, evicts chunks outside the
/// view and commits/rebuilds a bounded number of chunks per call.
/// </summary>
public sealed class SceneTerrainStreamingRuntime : IDisposable
{
    private readonly SceneTerrainRuntime _terrain;
    private readonly SceneTerrainStreamingOptions _options;
    private bool _disposed;

    /// <summary>Creates a streaming loop over a terrain runtime.</summary>
    /// <param name="terrain">Scene-bound terrain runtime.</param>
    /// <param name="options">Residency and LOD settings.</param>
    public SceneTerrainStreamingRuntime(SceneTerrainRuntime terrain, SceneTerrainStreamingOptions options)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _terrain = terrain;
        _options = options;
    }

    /// <summary>Gets the underlying scene terrain runtime.</summary>
    public SceneTerrainRuntime Terrain => _terrain;

    /// <summary>Gets the number of scene-bound chunks.</summary>
    public int ChunkCount => _terrain.ChunkCount;

    /// <summary>Advances residency around a world-space focus point.</summary>
    /// <param name="focus">Camera or player position.</param>
    /// <returns>Operations performed by this update.</returns>
    /// <exception cref="ObjectDisposedException">The streaming runtime is disposed.</exception>
    public SceneTerrainStreamingStats Update(Vec3 focus)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        float span = _terrain.Engine.ChunkSpan;
        int centerX = (int)MathF.Floor(focus.X / span);
        int centerZ = (int)MathF.Floor(focus.Z / span);
        int radius = _options.ViewRadiusChunks;

        var desired = new Dictionary<TerrainChunkCoord, int>();
        for (var dz = -radius; dz <= radius; dz++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                int distance = Math.Max(Math.Abs(dx), Math.Abs(dz));
                int lod = distance <= _options.Lod0Radius
                    ? 0
                    : distance <= _options.Lod1Radius
                        ? 1
                        : 2;
                desired.Add(new TerrainChunkCoord(centerX + dx, centerZ + dz), lod);
            }
        }

        var decommitted = 0;
        if (desired.Count < _terrain.ChunkCount || !AllDesiredAreLoaded(desired))
        {
            List<TerrainChunkCoord> existing = [.. _terrain.ChunkCoords];
            for (var index = 0; index < existing.Count && decommitted < _options.MaxDecommitsPerUpdate; index++)
            {
                TerrainChunkCoord coord = existing[index];
                if (!desired.ContainsKey(coord) && _terrain.DecommitChunk(coord))
                {
                    decommitted++;
                }
            }
        }

        var rebuilt = 0;
        var committed = 0;
        foreach ((TerrainChunkCoord coord, int lod) in desired)
        {
            if (_terrain.TryGetChunk(coord, out SceneTerrainChunkHandle handle))
            {
                if (handle.LodLevel != lod && rebuilt < _options.MaxRebuildsPerUpdate &&
                    _terrain.RebuildChunk(coord, lod))
                {
                    rebuilt++;
                }

                continue;
            }

            if (committed < _options.MaxCommitsPerUpdate && _terrain.CommitChunk(coord, lod))
            {
                committed++;
            }
        }

        return new SceneTerrainStreamingStats(committed, rebuilt, decommitted);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _terrain.Dispose();
    }

    private bool AllDesiredAreLoaded(Dictionary<TerrainChunkCoord, int> desired)
    {
        if (desired.Count != _terrain.ChunkCount)
        {
            return false;
        }

        foreach (TerrainChunkCoord coord in desired.Keys)
        {
            if (!_terrain.TryGetChunk(coord, out _))
            {
                return false;
            }
        }

        return true;
    }
}
