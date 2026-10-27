namespace Axrone.Terrain;

/// <summary>A surface hit inside a committed chunk.</summary>
/// <param name="Coord">Chunk coordinate.</param>
/// <param name="Point">World-space hit point.</param>
/// <param name="Normal">Hit normal, facing the ray.</param>
/// <param name="Distance">Distance along the ray.</param>
/// <param name="U">Normalized chunk-local X in <c>[0, 1]</c>.</param>
/// <param name="V">Normalized chunk-local Z in <c>[0, 1]</c>.</param>
public readonly record struct TerrainChunkHit(
    TerrainChunkCoord Coord,
    Vec3 Point,
    Vec3 Normal,
    float Distance,
    float U,
    float V);

/// <summary>One cell address inside a chunk grid.</summary>
internal readonly record struct TerrainGridCell(int X, int Z);

/// <summary>
/// Chunk-grid raycasting: a 2D DDA walks chunks in ray order, a second DDA walks the 32x32
/// cells of each resident chunk, and each cell tests its two triangles with the library
/// Möller-Trumbore implementation. No allocation and no per-cell AABB test.
/// </summary>
public sealed partial class TerrainChunkEngine
{
    private const float HitEpsilon = 1e-4f;

    /// <summary>Raycasts committed chunks.</summary>
    /// <param name="ray">World-space ray.</param>
    /// <param name="maxDistance">Maximum travel distance.</param>
    /// <param name="hit">Receives the nearest surface hit.</param>
    /// <returns><see langword="true"/> when the ray hits a resident chunk.</returns>
    /// <exception cref="ObjectDisposedException">The engine is disposed.</exception>
    public bool Raycast(in Ray3D ray, float maxDistance, out TerrainChunkHit hit)
    {
        if (!float.IsFinite(maxDistance) || maxDistance <= 0f)
        {
            hit = default;
            return false;
        }

        _gate.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            foreach (TerrainChunkCoord coord in new ChunkTraversal(in ray, _chunkSpan, maxDistance))
            {
                if (!_chunks.TryGetValue(coord, out ChunkEntry? entry))
                {
                    continue;
                }

                if (TryRaycastChunk(entry, coord, in ray, maxDistance, out hit))
                {
                    return true;
                }
            }

            hit = default;
            return false;
        }
        finally
        {
            _gate.ExitReadLock();
        }
    }

    private bool TryRaycastChunk(
        ChunkEntry entry,
        TerrainChunkCoord coord,
        in Ray3D ray,
        float maxDistance,
        out TerrainChunkHit hit)
    {
        TerrainChunkView view = entry.CreateView();
        ref TerrainChunkHeader header = ref view.Header;

        float originX = coord.X * _chunkSpan;
        float originZ = coord.Z * _chunkSpan;
        var bounds = new Aabb3D(
            new Vec3(originX, header.MinHeight * _options.MaxHeight, originZ),
            new Vec3(originX + _chunkSpan, header.MaxHeight * _options.MaxHeight, originZ + _chunkSpan));

        if (!bounds.Intersects(in ray, out float tMin, out float tMax))
        {
            hit = default;
            return false;
        }

        float tStart = MathF.Max(tMin, 0f);
        float tEnd = MathF.Min(tMax, maxDistance);
        if (tEnd < tStart)
        {
            hit = default;
            return false;
        }

        Span<float> heights = view.Heights;
        float cellSize = _options.CellSize;
        float maxHeight = _options.MaxHeight;
        const int stride = TerrainChunkLayout.VerticesPerAxis;

        foreach (TerrainGridCell cell in new CellTraversal(
                     in ray,
                     originX,
                     originZ,
                     cellSize,
                     TerrainChunkLayout.Subdivisions,
                     tStart,
                     tEnd))
        {
            int x = cell.X;
            int z = cell.Z;
            int index = (z * stride) + x;
            float worldX = originX + (x * cellSize);
            float worldZ = originZ + (z * cellSize);

            var v00 = new Vec3(worldX, heights[index] * maxHeight, worldZ);
            var v10 = new Vec3(worldX + cellSize, heights[index + 1] * maxHeight, worldZ);
            var v01 = new Vec3(worldX, heights[index + stride] * maxHeight, worldZ + cellSize);
            var v11 = new Vec3(worldX + cellSize, heights[index + stride + 1] * maxHeight, worldZ + cellSize);

            var bestDistance = float.PositiveInfinity;
            TriangleHit3 best = default;
            var found = false;

            if (ray.Intersects(new Triangle3(v00, v01, v10), out TriangleHit3 first) &&
                first.Distance >= tStart - HitEpsilon &&
                first.Distance <= tEnd + HitEpsilon &&
                first.Distance < bestDistance)
            {
                bestDistance = first.Distance;
                best = first;
                found = true;
            }

            if (ray.Intersects(new Triangle3(v10, v01, v11), out TriangleHit3 second) &&
                second.Distance >= tStart - HitEpsilon &&
                second.Distance <= tEnd + HitEpsilon &&
                second.Distance < bestDistance)
            {
                bestDistance = second.Distance;
                best = second;
                found = true;
            }

            if (found)
            {
                hit = new TerrainChunkHit(
                    coord,
                    best.Point,
                    best.Normal,
                    best.Distance,
                    (best.Point.X - originX) / _chunkSpan,
                    (best.Point.Z - originZ) / _chunkSpan);
                return true;
            }
        }

        hit = default;
        return false;
    }
}

/// <summary>2D DDA over chunk coordinates, yielding every chunk the ray crosses in order.</summary>
internal ref struct ChunkTraversal
{
    private readonly float _maxDistance;
    private readonly int _stepX;
    private readonly int _stepZ;
    private readonly float _tDeltaX;
    private readonly float _tDeltaZ;
    private int _x;
    private int _z;
    private float _tMaxX;
    private float _tMaxZ;
    private bool _started;
    private bool _completed;
    private TerrainChunkCoord _current;

    public ChunkTraversal(in Ray3D ray, float chunkSpan, float maxDistance)
    {
        _maxDistance = maxDistance;
        _x = (int)MathF.Floor(ray.Origin.X / chunkSpan);
        _z = (int)MathF.Floor(ray.Origin.Z / chunkSpan);
        _stepX = ray.Direction.X > 0f ? 1 : ray.Direction.X < 0f ? -1 : 0;
        _stepZ = ray.Direction.Z > 0f ? 1 : ray.Direction.Z < 0f ? -1 : 0;

        float boundaryX = (_stepX > 0 ? _x + 1 : _x) * chunkSpan;
        float boundaryZ = (_stepZ > 0 ? _z + 1 : _z) * chunkSpan;
        _tMaxX = _stepX != 0 ? (boundaryX - ray.Origin.X) * ray.InvDirection.X : float.PositiveInfinity;
        _tMaxZ = _stepZ != 0 ? (boundaryZ - ray.Origin.Z) * ray.InvDirection.Z : float.PositiveInfinity;
        _tDeltaX = _stepX != 0 ? MathF.Abs(chunkSpan * ray.InvDirection.X) : float.PositiveInfinity;
        _tDeltaZ = _stepZ != 0 ? MathF.Abs(chunkSpan * ray.InvDirection.Z) : float.PositiveInfinity;
        _started = false;
        _completed = false;
        _current = default;
    }

    public ChunkTraversal GetEnumerator() => this;

    public bool MoveNext()
    {
        if (_completed)
        {
            return false;
        }

        if (!_started)
        {
            _started = true;
            _current = new TerrainChunkCoord(_x, _z);
            return true;
        }

        if (MathF.Min(_tMaxX, _tMaxZ) > _maxDistance)
        {
            _completed = true;
            return false;
        }

        if (_tMaxX < _tMaxZ)
        {
            _x += _stepX;
            _tMaxX += _tDeltaX;
        }
        else
        {
            _z += _stepZ;
            _tMaxZ += _tDeltaZ;
        }

        _current = new TerrainChunkCoord(_x, _z);
        return true;
    }

    public readonly TerrainChunkCoord Current => _current;
}

/// <summary>2D DDA over the 32x32 cells of one chunk inside a ray interval.</summary>
internal ref struct CellTraversal
{
    private readonly int _subdivisions;
    private readonly int _stepX;
    private readonly int _stepZ;
    private readonly float _tDeltaX;
    private readonly float _tDeltaZ;
    private readonly float _tEnd;
    private int _x;
    private int _z;
    private float _tMaxX;
    private float _tMaxZ;
    private bool _started;
    private bool _completed;
    private TerrainGridCell _current;

    public CellTraversal(
        in Ray3D ray,
        float originX,
        float originZ,
        float cellSize,
        int subdivisions,
        float tStart,
        float tEnd)
    {
        _subdivisions = subdivisions;
        _tEnd = tEnd;

        float startX = ray.Origin.X + (ray.Direction.X * tStart);
        float startZ = ray.Origin.Z + (ray.Direction.Z * tStart);
        _x = Math.Clamp((int)MathF.Floor((startX - originX) / cellSize), 0, subdivisions - 1);
        _z = Math.Clamp((int)MathF.Floor((startZ - originZ) / cellSize), 0, subdivisions - 1);

        _stepX = ray.Direction.X > 0f ? 1 : ray.Direction.X < 0f ? -1 : 0;
        _stepZ = ray.Direction.Z > 0f ? 1 : ray.Direction.Z < 0f ? -1 : 0;

        float boundaryX = originX + ((_stepX > 0 ? _x + 1 : _x) * cellSize);
        float boundaryZ = originZ + ((_stepZ > 0 ? _z + 1 : _z) * cellSize);
        _tMaxX = _stepX != 0 ? (boundaryX - ray.Origin.X) * ray.InvDirection.X : float.PositiveInfinity;
        _tMaxZ = _stepZ != 0 ? (boundaryZ - ray.Origin.Z) * ray.InvDirection.Z : float.PositiveInfinity;
        _tDeltaX = _stepX != 0 ? MathF.Abs(cellSize * ray.InvDirection.X) : float.PositiveInfinity;
        _tDeltaZ = _stepZ != 0 ? MathF.Abs(cellSize * ray.InvDirection.Z) : float.PositiveInfinity;
        _started = false;
        _completed = false;
        _current = default;
    }

    public CellTraversal GetEnumerator() => this;

    public bool MoveNext()
    {
        if (_completed)
        {
            return false;
        }

        if (!_started)
        {
            _started = true;
            if ((uint)_x >= (uint)_subdivisions || (uint)_z >= (uint)_subdivisions)
            {
                _completed = true;
                return false;
            }

            _current = new TerrainGridCell(_x, _z);
            return true;
        }

        if (MathF.Min(_tMaxX, _tMaxZ) > _tEnd)
        {
            _completed = true;
            return false;
        }

        if (_tMaxX < _tMaxZ)
        {
            _x += _stepX;
            _tMaxX += _tDeltaX;
        }
        else
        {
            _z += _stepZ;
            _tMaxZ += _tDeltaZ;
        }

        if ((uint)_x >= (uint)_subdivisions || (uint)_z >= (uint)_subdivisions)
        {
            _completed = true;
            return false;
        }

        _current = new TerrainGridCell(_x, _z);
        return true;
    }

    public readonly TerrainGridCell Current => _current;
}
