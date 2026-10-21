namespace Axrone.Particles;

/// <summary>Spatial query visitor contract.</summary>
public interface ISpatialVisitor
{
    /// <summary>Visits a found particle.</summary>
    static abstract bool OnFound(ParticleId id, in Vec3 position, float distanceSquared);
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
    public void QueryRadius<TVisitor>(in Vec3 center, float radius)
        where TVisitor : struct, ISpatialVisitor
    {
        float radiusSq = radius * radius;
        WorldToGrid(center - new Vec3(radius), out int minX, out int minY, out int minZ);
        WorldToGrid(center + new Vec3(radius), out int maxX, out int maxY, out int maxZ);

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
                        if (dsq <= radiusSq)
                        {
                            if (!TVisitor.OnFound(_entries[curr].Id, _entries[curr].Position, dsq))
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
