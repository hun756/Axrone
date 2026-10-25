namespace Axrone.Particles;

/// <summary>Spatial query visitor contract.</summary>
/// <typeparam name="TState">Visitor state.</typeparam>
public interface ISpatialVisitor<TState>
    where TState : allows ref struct
{
    /// <summary>Visits a found particle.</summary>
    static abstract bool OnFound(ParticleId id, in Vec3 position, float distanceSquared, scoped ref TState state);
}

/// <summary>3D spatial hash grid.</summary>
public sealed unsafe class SpatialHashGrid3D : IDisposable
{
    private const uint PrimeX = 73856093u;
    private const uint PrimeY = 19349663u;
    private const uint PrimeZ = 83492791u;

    /// <summary>Hash cell entry.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CellEntry
    {
        /// <summary>Particle identity.</summary>
        public ParticleId Id;

        /// <summary>Cell-space position.</summary>
        public Vec3 Position;

        /// <summary>Next entry index.</summary>
        public int NextEntry;
    }

    private readonly float _cellSize;
    private readonly float _invCellSize;
    private readonly int _tableMask;
    private readonly int _tableCapacity;

    private int* _buckets;
    private CellEntry* _entries;
    private int* _freeEntries;
    private int _freeEntryTop;
    private readonly int _entryPoolCapacity;
    private bool _disposed;

    /// <summary>Creates a grid.</summary>
    public SpatialHashGrid3D(float cellSize = 10.0f, int tablePowerOfTwo = 4096, int maxEntries = 65536)
    {
        _cellSize = Math.Max(cellSize, 0.001f);
        _invCellSize = 1.0f / _cellSize;
        _tableCapacity = (int)BitOperations.RoundUpToPowerOf2((uint)tablePowerOfTwo);
        _tableMask = _tableCapacity - 1;
        _entryPoolCapacity = maxEntries;

        const nuint align = 64;
        _buckets = (int*)NativeMemory.AlignedAlloc((nuint)(_tableCapacity * sizeof(int)), align);
        _entries = (CellEntry*)NativeMemory.AlignedAlloc((nuint)(_entryPoolCapacity * sizeof(CellEntry)), align);
        _freeEntries = (int*)NativeMemory.AlignedAlloc((nuint)(_entryPoolCapacity * sizeof(int)), align);

        Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private uint ComputeHash(int cx, int cy, int cz)
    {
        uint h = ((uint)cx * PrimeX) ^ ((uint)cy * PrimeY) ^ ((uint)cz * PrimeZ);
        return h & (uint)_tableMask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void WorldToGrid(in Vec3 pos, out int cx, out int cy, out int cz)
    {
        cx = (int)MathF.Floor(pos.X * _invCellSize);
        cy = (int)MathF.Floor(pos.Y * _invCellSize);
        cz = (int)MathF.Floor(pos.Z * _invCellSize);
    }

    /// <summary>Clears all cells.</summary>
    public void Clear()
    {
        new Span<int>(_buckets, _tableCapacity).Fill(-1);
        _freeEntryTop = _entryPoolCapacity;
        for (int i = 0; i < _entryPoolCapacity; i++)
        {
            _freeEntries[i] = _entryPoolCapacity - 1 - i;
        }
    }

    /// <summary>Inserts a particle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Insert(ParticleId id, in Vec3 position)
    {
        if (_freeEntryTop == 0)
        {
            return;
        }

        WorldToGrid(position, out int cx, out int cy, out int cz);
        uint hash = ComputeHash(cx, cy, cz);

        int entryIdx = _freeEntries[--_freeEntryTop];
        _entries[entryIdx].Id = id;
        _entries[entryIdx].Position = position;
        _entries[entryIdx].NextEntry = _buckets[hash];

        _buckets[hash] = entryIdx;
    }

    /// <summary>Queries particles within a radius.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void QueryRadius<TVisitor, TState>(in Vec3 center, float radius, scoped ref TState state)
        where TVisitor : struct, ISpatialVisitor<TState>
        where TState : allows ref struct
    {
        float radiusSq = radius * radius;
        WorldToGrid(center - new Vec3(radius), out int minX, out int minY, out int minZ);
        WorldToGrid(center + new Vec3(radius), out int maxX, out int maxY, out int maxZ);
        QueryCells<TVisitor, TState>(minX, minY, minZ, maxX, maxY, maxZ, center, radiusSq, null, ref state);
    }

    /// <summary>Queries particles inside bounds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void QueryBounds<TVisitor, TState>(in ParticleBounds bounds, scoped ref TState state)
        where TVisitor : struct, ISpatialVisitor<TState>
        where TState : allows ref struct
    {
        WorldToGrid(bounds.Min, out int minX, out int minY, out int minZ);
        WorldToGrid(bounds.Max, out int maxX, out int maxY, out int maxZ);
        QueryCells<TVisitor, TState>(minX, minY, minZ, maxX, maxY, maxZ, default, float.PositiveInfinity, bounds, ref state);
    }

    /// <summary>Finds up to maxCount nearest particles, sorted ascending.</summary>
    /// <returns>Filled result count.</returns>
    public int QueryNearest(in Vec3 center, int maxCount, float maxRadius, Span<ParticleId> destination)
    {
        if (maxCount <= 0 || destination.IsEmpty)
        {
            return 0;
        }

        int limit = Math.Min(maxCount, destination.Length);
        float maxRadiusSq = maxRadius * maxRadius;
        int found = 0;

        while (found < limit)
        {
            uint best = uint.MaxValue;
            float bestDsq = float.PositiveInfinity;
            bool hasBest = false;

            for (int b = 0; b < _tableCapacity; b++)
            {
                int curr = _buckets[b];
                while (curr != -1)
                {
                    Vec3 delta = _entries[curr].Position - center;
                    float dsq = delta.LengthSquared();
                    if (dsq <= maxRadiusSq && dsq < bestDsq && !ContainsId(destination[..found], _entries[curr].Id))
                    {
                        best = (uint)curr;
                        bestDsq = dsq;
                        hasBest = true;
                    }

                    curr = _entries[curr].NextEntry;
                }
            }

            if (!hasBest)
            {
                break;
            }

            destination[found++] = _entries[(int)best].Id;
        }

        return found;
    }

    private static bool ContainsId(ReadOnlySpan<ParticleId> ids, ParticleId id)
    {
        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] == id)
            {
                return true;
            }
        }

        return false;
    }

    private void QueryCells<TVisitor, TState>(
        int minX, int minY, int minZ,
        int maxX, int maxY, int maxZ,
        in Vec3 center,
        float radiusSq,
        ParticleBounds? bounds,
        scoped ref TState state)
        where TVisitor : struct, ISpatialVisitor<TState>
        where TState : allows ref struct
    {
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    uint hash = ComputeHash(x, y, z);
                    int curr = _buckets[hash];

                    while (curr != -1)
                    {
                        Vec3 delta = _entries[curr].Position - center;
                        float dsq = delta.LengthSquared();
                        bool inside = bounds.HasValue
                            ? bounds.Value.Contains(_entries[curr].Position)
                            : dsq <= radiusSq;

                        if (inside)
                        {
                            if (!TVisitor.OnFound(_entries[curr].Id, _entries[curr].Position, dsq, ref state))
                            {
                                return;
                            }
                        }

                        curr = _entries[curr].NextEntry;
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (_buckets != null)
            {
                NativeMemory.AlignedFree(_buckets);
            }

            if (_entries != null)
            {
                NativeMemory.AlignedFree(_entries);
            }

            if (_freeEntries != null)
            {
                NativeMemory.AlignedFree(_freeEntries);
            }

            _buckets = null;
            _entries = null;
            _freeEntries = null;
            _disposed = true;
        }
    }

    /// <summary>Finalizer.</summary>
    ~SpatialHashGrid3D() => Dispose(false);
}
