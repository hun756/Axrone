namespace Axrone.Terrain.Chunks;

/// <summary>Snapshot of one committed chunk's header.</summary>
/// <param name="Coord">Chunk coordinate.</param>
/// <param name="MinHeight">Smallest normalized height.</param>
/// <param name="MaxHeight">Largest normalized height.</param>
/// <param name="Generation">Slot generation.</param>
/// <param name="State">Lifecycle state.</param>
public readonly record struct TerrainChunkInfo(
    TerrainChunkCoord Coord,
    float MinHeight,
    float MaxHeight,
    uint Generation,
    TerrainChunkState State);

/// <summary>
/// Chunked terrain engine. Chunks are fixed 33x33 slots rented from a
/// <see cref="ContiguousSlabPool{T}"/>; the directory is guarded by a read/write lock so
/// queries run concurrently while commit/decommit stay exclusive. All public operations are
/// safe under concurrent readers.
/// </summary>
public sealed partial class TerrainChunkEngine : IDisposable
{
    private readonly TerrainChunkEngineOptions _options;
    private readonly ContiguousSlabPool<TerrainChunkHeader> _pool;
    private readonly Dictionary<TerrainChunkCoord, ChunkEntry> _chunks;
    private readonly ReaderWriterLockSlim _gate;
    private readonly float _chunkSpan;
    private uint _nextGeneration;
    private int _disposeState;
    private volatile bool _disposed;

    /// <summary>Creates an engine and reserves its chunk slab pool.</summary>
    /// <param name="options">Engine configuration.</param>
    /// <exception cref="TerrainException">The options are invalid.</exception>
    public TerrainChunkEngine(TerrainChunkEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _options = options;
        _chunkSpan = TerrainChunkLayout.Subdivisions * options.CellSize;
        _pool = new ContiguousSlabPool<TerrainChunkHeader>(
            TerrainChunkLayout.SlotBlocks,
            options.PoolCapacity,
            MemoryClearMode.Never);
        _chunks = new Dictionary<TerrainChunkCoord, ChunkEntry>(options.PoolCapacity);
        _gate = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
    }

    /// <summary>Gets the engine configuration.</summary>
    public TerrainChunkEngineOptions Options => _options;

    /// <summary>Gets the world-space size of one chunk side.</summary>
    public float ChunkSpan => _chunkSpan;

    /// <summary>Gets the world-space size of one grid cell.</summary>
    public float CellSize => _options.CellSize;

    /// <summary>Gets the world-space height applied to a normalized height of 1.</summary>
    public float MaxHeight => _options.MaxHeight;

    /// <summary>Gets the number of committed chunks.</summary>
    public int ChunkCount
    {
        get
        {
            _gate.EnterReadLock();
            try
            {
                ThrowIfDisposed();
                return _chunks.Count;
            }
            finally
            {
                _gate.ExitReadLock();
            }
        }
    }

    /// <summary>Maps a world-space position to its chunk coordinate.</summary>
    /// <param name="worldX">World X.</param>
    /// <param name="worldZ">World Z.</param>
    /// <returns>The containing chunk coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TerrainChunkCoord WorldToCoord(float worldX, float worldZ) =>
        new((int)MathF.Floor(worldX / _chunkSpan), (int)MathF.Floor(worldZ / _chunkSpan));

    /// <summary>Generates and publishes a chunk.</summary>
    /// <param name="coord">Chunk coordinate.</param>
    /// <returns><see langword="true"/> when the chunk was committed; <see langword="false"/> when it already exists or the pool is exhausted.</returns>
    /// <exception cref="ObjectDisposedException">The engine is disposed.</exception>
    public bool CommitChunk(TerrainChunkCoord coord)
    {
        _gate.EnterWriteLock();
        try
        {
            ThrowIfDisposed();
            if (_chunks.ContainsKey(coord))
            {
                return false;
            }

            if (!_pool.TryRentLease(TerrainChunkLayout.SlotBlocks, out ValueMemoryLease<TerrainChunkHeader> lease))
            {
                return false;
            }

            ChunkEntry entry = new(lease, ++_nextGeneration);
            try
            {
                Populate(entry, coord);
                _chunks.Add(coord, entry);
                return true;
            }
            catch
            {
                entry.Lease.Dispose();
                throw;
            }
        }
        finally
        {
            _gate.ExitWriteLock();
        }
    }

    /// <summary>Removes a chunk and returns its slab slot to the pool.</summary>
    /// <param name="coord">Chunk coordinate.</param>
    /// <returns><see langword="true"/> when the chunk existed.</returns>
    /// <exception cref="ObjectDisposedException">The engine is disposed.</exception>
    public bool DecommitChunk(TerrainChunkCoord coord)
    {
        _gate.EnterWriteLock();
        try
        {
            ThrowIfDisposed();
            if (!_chunks.Remove(coord, out ChunkEntry? entry))
            {
                return false;
            }

            entry.Lease.Dispose();
            return true;
        }
        finally
        {
            _gate.ExitWriteLock();
        }
    }

    /// <summary>Tests whether a chunk is committed.</summary>
    /// <param name="coord">Chunk coordinate.</param>
    /// <returns><see langword="true"/> when the chunk is resident.</returns>
    public bool IsChunkResident(TerrainChunkCoord coord)
    {
        _gate.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            return _chunks.ContainsKey(coord);
        }
        finally
        {
            _gate.ExitReadLock();
        }
    }

    /// <summary>Reads a chunk header snapshot.</summary>
    /// <param name="coord">Chunk coordinate.</param>
    /// <param name="info">Receives the header when the chunk is resident.</param>
    /// <returns><see langword="true"/> when the chunk is resident.</returns>
    public bool TryGetChunkInfo(TerrainChunkCoord coord, out TerrainChunkInfo info)
    {
        _gate.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            if (!_chunks.TryGetValue(coord, out ChunkEntry? entry))
            {
                info = default;
                return false;
            }

            ref TerrainChunkHeader header = ref entry.CreateView().Header;
            info = new TerrainChunkInfo(
                header.Coord,
                header.MinHeight,
                header.MaxHeight,
                header.Generation,
                header.State);
            return true;
        }
        finally
        {
            _gate.ExitReadLock();
        }
    }

    /// <summary>
    /// Samples world-space elevation. A missing chunk falls back to the procedural surface,
    /// so unloaded terrain still answers queries; sculpted chunks return their edited heights.
    /// </summary>
    /// <param name="worldX">World X.</param>
    /// <param name="worldZ">World Z.</param>
    /// <returns>World-space elevation.</returns>
    public float SampleElevation(float worldX, float worldZ)
    {
        _gate.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            TerrainChunkCoord coord = WorldToCoord(worldX, worldZ);
            return _chunks.TryGetValue(coord, out ChunkEntry? entry)
                ? SampleBilinearHeight(entry, coord, worldX, worldZ) * _options.MaxHeight
                : _options.Surface.SampleHeight(worldX, worldZ) * _options.MaxHeight;
        }
        finally
        {
            _gate.ExitReadLock();
        }
    }

    /// <summary>Samples elevation from a resident chunk only.</summary>
    /// <param name="worldX">World X.</param>
    /// <param name="worldZ">World Z.</param>
    /// <param name="worldHeight">Receives the world-space elevation.</param>
    /// <returns><see langword="true"/> when the containing chunk is resident.</returns>
    public bool TryGetChunkElevation(float worldX, float worldZ, out float worldHeight)
    {
        _gate.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            TerrainChunkCoord coord = WorldToCoord(worldX, worldZ);
            if (!_chunks.TryGetValue(coord, out ChunkEntry? entry))
            {
                worldHeight = 0f;
                return false;
            }

            worldHeight = SampleBilinearHeight(entry, coord, worldX, worldZ) * _options.MaxHeight;
            return true;
        }
        finally
        {
            _gate.ExitReadLock();
        }
    }

    /// <summary>Samples a world-space surface normal.</summary>
    /// <param name="worldX">World X.</param>
    /// <param name="worldZ">World Z.</param>
    /// <param name="normal">Receives the unit normal.</param>
    /// <returns><see langword="true"/> when the containing chunk is resident.</returns>
    public bool TryGetNormal(float worldX, float worldZ, out Vec3 normal)
    {
        _gate.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            TerrainChunkCoord coord = WorldToCoord(worldX, worldZ);
            if (_chunks.TryGetValue(coord, out ChunkEntry? entry))
            {
                normal = SampleBilinearNormal(entry, coord, worldX, worldZ);
                return true;
            }

            normal = ProceduralNormal(worldX, worldZ);
            return false;
        }
        finally
        {
            _gate.ExitReadLock();
        }
    }

    /// <summary>Samples normalized RGBA splat weights.</summary>
    /// <param name="worldX">World X.</param>
    /// <param name="worldZ">World Z.</param>
    /// <param name="weights">Receives the weights when the chunk is resident.</param>
    /// <returns><see langword="true"/> when the containing chunk is resident.</returns>
    public bool TryGetSplatWeights(float worldX, float worldZ, out SplatWeights weights)
    {
        _gate.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            TerrainChunkCoord coord = WorldToCoord(worldX, worldZ);
            if (!_chunks.TryGetValue(coord, out ChunkEntry? entry))
            {
                weights = new SplatWeights(1f, 0f, 0f, 0f);
                return false;
            }

            weights = SampleBilinearSplat(entry, coord, worldX, worldZ);
            return true;
        }
        finally
        {
            _gate.ExitReadLock();
        }
    }

    /// <summary>Computes height statistics from a resident chunk.</summary>
    /// <param name="coord">Chunk coordinate.</param>
    /// <param name="statistics">Receives normalized height statistics.</param>
    /// <returns><see langword="true"/> when the chunk is resident.</returns>
    public bool TryGetChunkStatistics(TerrainChunkCoord coord, out TerrainStatistics statistics)
    {
        _gate.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            if (!_chunks.TryGetValue(coord, out ChunkEntry? entry))
            {
                statistics = default;
                return false;
            }

            Span<float> heights = entry.CreateView().Heights;
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            double total = 0d;
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

                total += height;
            }

            statistics = new TerrainStatistics(min, max, (float)(total / heights.Length));
            return true;
        }
        finally
        {
            _gate.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _disposed = true;
        _gate.EnterWriteLock();
        try
        {
            foreach (ChunkEntry entry in _chunks.Values)
            {
                entry.Lease.Dispose();
            }

            _chunks.Clear();
            _pool.Dispose();
        }
        finally
        {
            _gate.ExitWriteLock();
            _gate.Dispose();
        }
    }

    private void Populate(ChunkEntry entry, TerrainChunkCoord coord)
    {
        TerrainChunkView view = entry.CreateView();
        ref TerrainChunkHeader header = ref view.Header;
        header.Coord = coord;
        header.Generation = entry.Generation;
        header.State = TerrainChunkState.Building;

        float originX = coord.X * _chunkSpan;
        float originZ = coord.Z * _chunkSpan;
        float cellSize = _options.CellSize;
        Span<float> heights = view.Heights;
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;

        for (var z = 0; z < TerrainChunkLayout.VerticesPerAxis; z++)
        {
            Span<float> row = heights.Slice(z * TerrainChunkLayout.VerticesPerAxis, TerrainChunkLayout.VerticesPerAxis);
            _options.Surface.SampleRow(originX, originZ + (z * cellSize), cellSize, row);

            for (var x = 0; x < row.Length; x++)
            {
                float height = float.IsFinite(row[x]) ? Math.Clamp(row[x], 0f, 1f) : 0f;
                row[x] = height;
                if (height < min)
                {
                    min = height;
                }

                if (height > max)
                {
                    max = height;
                }
            }
        }

        header.MinHeight = min;
        header.MaxHeight = max;
        ComputeNormals(view);

        Span<byte> splat = view.Splat;
        for (var index = 0; index < splat.Length; index += 4)
        {
            splat[index] = TerrainSplatMath.WeightTotal;
            splat[index + 1] = 0;
            splat[index + 2] = 0;
            splat[index + 3] = 0;
        }

        view.Density.Clear();
        header.State = TerrainChunkState.Ready;
    }

    private void ComputeNormals(TerrainChunkView view)
    {
        Span<float> heights = view.Heights;
        float cellSize = _options.CellSize;
        const int stride = TerrainChunkLayout.VerticesPerAxis;
        const int last = TerrainChunkLayout.Subdivisions;

        for (var z = 0; z < stride; z++)
        {
            for (var x = 0; x < stride; x++)
            {
                int index = (z * stride) + x;
                float left = x > 0 ? heights[index - 1] : heights[index];
                float right = x < last ? heights[index + 1] : heights[index];
                float near = z > 0 ? heights[index - stride] : heights[index];
                float far = z < last ? heights[index + stride] : heights[index];

                int spanX = (x > 0 ? 1 : 0) + (x < last ? 1 : 0);
                int spanZ = (z > 0 ? 1 : 0) + (z < last ? 1 : 0);
                float dx = (right - left) / (spanX * cellSize);
                float dz = (far - near) / (spanZ * cellSize);

                float inverse = 1f / MathF.Sqrt((dx * dx) + 1f + (dz * dz));
                view.SetNormal(index, new Vec3(-dx * inverse, inverse, -dz * inverse));
            }
        }
    }

    private float SampleBilinearHeight(ChunkEntry entry, TerrainChunkCoord coord, float worldX, float worldZ)
    {
        TerrainChunkView view = entry.CreateView();
        Span<float> heights = view.Heights;

        float u = Math.Clamp((worldX - (coord.X * _chunkSpan)) / _chunkSpan, 0f, 1f);
        float v = Math.Clamp((worldZ - (coord.Z * _chunkSpan)) / _chunkSpan, 0f, 1f);
        float gridX = u * TerrainChunkLayout.Subdivisions;
        float gridZ = v * TerrainChunkLayout.Subdivisions;
        int x0 = (int)MathF.Floor(gridX);
        int z0 = (int)MathF.Floor(gridZ);
        int x1 = Math.Min(x0 + 1, TerrainChunkLayout.Subdivisions);
        int z1 = Math.Min(z0 + 1, TerrainChunkLayout.Subdivisions);
        float tx = gridX - x0;
        float tz = gridZ - z0;

        const int stride = TerrainChunkLayout.VerticesPerAxis;
        float topLeft = heights[(z0 * stride) + x0];
        float topRight = heights[(z0 * stride) + x1];
        float bottomLeft = heights[(z1 * stride) + x0];
        float bottomRight = heights[(z1 * stride) + x1];

        float top = topLeft + ((topRight - topLeft) * tx);
        float bottom = bottomLeft + ((bottomRight - bottomLeft) * tx);
        return top + ((bottom - top) * tz);
    }

    private Vec3 SampleBilinearNormal(ChunkEntry entry, TerrainChunkCoord coord, float worldX, float worldZ)
    {
        TerrainChunkView view = entry.CreateView();

        float u = Math.Clamp((worldX - (coord.X * _chunkSpan)) / _chunkSpan, 0f, 1f);
        float v = Math.Clamp((worldZ - (coord.Z * _chunkSpan)) / _chunkSpan, 0f, 1f);
        float gridX = u * TerrainChunkLayout.Subdivisions;
        float gridZ = v * TerrainChunkLayout.Subdivisions;
        int x0 = (int)MathF.Floor(gridX);
        int z0 = (int)MathF.Floor(gridZ);
        int x1 = Math.Min(x0 + 1, TerrainChunkLayout.Subdivisions);
        int z1 = Math.Min(z0 + 1, TerrainChunkLayout.Subdivisions);
        float tx = gridX - x0;
        float tz = gridZ - z0;

        const int stride = TerrainChunkLayout.VerticesPerAxis;
        Vec3 topLeft = view.GetNormal((z0 * stride) + x0);
        Vec3 topRight = view.GetNormal((z0 * stride) + x1);
        Vec3 bottomLeft = view.GetNormal((z1 * stride) + x0);
        Vec3 bottomRight = view.GetNormal((z1 * stride) + x1);

        Vec3 top = Vec3.Lerp(topLeft, topRight, tx);
        Vec3 bottom = Vec3.Lerp(bottomLeft, bottomRight, tx);
        return Vec3.Normalize(Vec3.Lerp(top, bottom, tz));
    }

    private SplatWeights SampleBilinearSplat(ChunkEntry entry, TerrainChunkCoord coord, float worldX, float worldZ)
    {
        TerrainChunkView view = entry.CreateView();
        Span<byte> splat = view.Splat;

        float u = Math.Clamp((worldX - (coord.X * _chunkSpan)) / _chunkSpan, 0f, 1f);
        float v = Math.Clamp((worldZ - (coord.Z * _chunkSpan)) / _chunkSpan, 0f, 1f);
        float gridX = u * TerrainChunkLayout.Subdivisions;
        float gridZ = v * TerrainChunkLayout.Subdivisions;
        int x0 = (int)MathF.Floor(gridX);
        int z0 = (int)MathF.Floor(gridZ);
        int x1 = Math.Min(x0 + 1, TerrainChunkLayout.Subdivisions);
        int z1 = Math.Min(z0 + 1, TerrainChunkLayout.Subdivisions);
        float tx = gridX - x0;
        float tz = gridZ - z0;

        const int stride = TerrainChunkLayout.VerticesPerAxis;
        Span<float> channels = stackalloc float[4];
        for (var channel = 0; channel < 4; channel++)
        {
            float topLeft = splat[((((z0 * stride) + x0) * 4) + channel)];
            float topRight = splat[((((z0 * stride) + x1) * 4) + channel)];
            float bottomLeft = splat[((((z1 * stride) + x0) * 4) + channel)];
            float bottomRight = splat[((((z1 * stride) + x1) * 4) + channel)];
            float top = topLeft + ((topRight - topLeft) * tx);
            float bottom = bottomLeft + ((bottomRight - bottomLeft) * tx);
            channels[channel] = (top + ((bottom - top) * tz)) / TerrainSplatMath.WeightTotal;
        }

        return new SplatWeights(channels[0], channels[1], channels[2], channels[3]);
    }

    private Vec3 ProceduralNormal(float worldX, float worldZ)
    {
        float epsilon = _options.CellSize * 0.25f;
        float left = _options.Surface.SampleHeight(worldX - epsilon, worldZ);
        float right = _options.Surface.SampleHeight(worldX + epsilon, worldZ);
        float near = _options.Surface.SampleHeight(worldX, worldZ - epsilon);
        float far = _options.Surface.SampleHeight(worldX, worldZ + epsilon);

        float dx = (right - left) / (2f * epsilon);
        float dz = (far - near) / (2f * epsilon);
        float inverse = 1f / MathF.Sqrt((dx * dx) + 1f + (dz * dz));
        return new Vec3(-dx * inverse, inverse, -dz * inverse);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class ChunkEntry
    {
        public ChunkEntry(ValueMemoryLease<TerrainChunkHeader> lease, uint generation)
        {
            Lease = lease;
            Generation = generation;
        }

        public ValueMemoryLease<TerrainChunkHeader> Lease { get; }

        public uint Generation { get; }

        public TerrainChunkView CreateView() => new(MemoryMarshal.AsBytes(Lease.Span));
    }
}
