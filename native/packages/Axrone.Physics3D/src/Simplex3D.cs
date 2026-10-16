namespace Axrone.Physics3D;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Complete Minkowski difference vertex with co-dependent witness point carrier fields.
/// Exactly 36 bytes, tightly packed for spatial cache prefetching.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SimplexVertex(Vec3 Difference, Vec3 WitnessA, Vec3 WitnessB)
{
    /// <summary>Builds a vertex from a witness pair.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static SimplexVertex Create(in Vec3 pointA, in Vec3 pointB) =>
        new(pointA - pointB, pointA, pointB);
}

/// <summary>
/// Tetrahedral simplex buffer holding up to 4 vertices inline with zero dynamic memory allocation.
/// Eliminates stackalloc bounds checks through contiguous unrolled cell layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Simplex3D
{
    private SimplexVertex _v0;
    private SimplexVertex _v1;
    private SimplexVertex _v2;
    private SimplexVertex _v3;
    private uint _count;

    /// <summary>Live vertices.</summary>
    public readonly uint Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _count;
    }

    /// <summary>Reads a live vertex by index.</summary>
    public SimplexVertex this[uint index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if (index == 0)
            {
                return _v0;
            }

            if (index == 1)
            {
                return _v1;
            }

            if (index == 2)
            {
                return _v2;
            }

            if (index == 3)
            {
                return _v3;
            }

            return ThrowOutOfRange(index);
        }
    }

    /// <summary>Keeps one vertex.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Set1(in SimplexVertex v0)
    {
        _v0 = v0;
        _count = 1;
    }

    /// <summary>Keeps two vertices.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Set2(in SimplexVertex v0, in SimplexVertex v1)
    {
        _v0 = v0;
        _v1 = v1;
        _count = 2;
    }

    /// <summary>Keeps three vertices.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Set3(in SimplexVertex v0, in SimplexVertex v1, in SimplexVertex v2)
    {
        _v0 = v0;
        _v1 = v1;
        _v2 = v2;
        _count = 3;
    }

    /// <summary>Keeps four vertices.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Set4(in SimplexVertex v0, in SimplexVertex v1, in SimplexVertex v2, in SimplexVertex v3)
    {
        _v0 = v0;
        _v1 = v1;
        _v2 = v2;
        _v3 = v3;
        _count = 4;
    }

    /// <summary>Appends a vertex; overflows past the tetrahedral capacity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Add(in SimplexVertex v)
    {
        if (_count == 0)
        {
            _v0 = v;
        }
        else if (_count == 1)
        {
            _v1 = v;
        }
        else if (_count == 2)
        {
            _v2 = v;
        }
        else if (_count == 3)
        {
            _v3 = v;
        }
        else
        {
            ThrowHelper.ThrowSimplexOverflow();
        }
        _count++;
    }

    /// <summary>Whether the difference already sits in the buffer within tolerance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool ContainsDuplicate(in Vec3 difference, DistanceSquared toleranceSq)
    {
        float tol = toleranceSq.Value.Value;
        if (_count > 0 && LengthSquared(_v0.Difference - difference) <= tol)
        {
            return true;
        }

        if (_count > 1 && LengthSquared(_v1.Difference - difference) <= tol)
        {
            return true;
        }

        if (_count > 2 && LengthSquared(_v2.Difference - difference) <= tol)
        {
            return true;
        }

        if (_count > 3 && LengthSquared(_v3.Difference - difference) <= tol)
        {
            return true;
        }

        return false;
    }

    /// <summary>Allocation-free duck-typed enumerator over the live vertices.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly Enumerator GetEnumerator() => new(this);

    /// <summary>
    /// Allocation-free duck-typed ref struct enumerator.
    /// </summary>
    public ref struct Enumerator
    {
        private readonly Simplex3D _simplex;
        private int _index;

        /// <summary>Creates an enumerator over a simplex.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal Enumerator(Simplex3D simplex)
        {
            _simplex = simplex;
            _index = -1;
        }

        /// <summary>Advances to the next vertex.</summary>
        /// <returns><see langword="true"/> while vertices remain.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool MoveNext()
        {
            int next = _index + 1;
            if ((uint)next < _simplex._count)
            {
                _index = next;
                return true;
            }
            return false;
        }

        /// <summary>Current vertex.</summary>
        public SimplexVertex Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => _simplex[(uint)_index];
        }
    }

    /// <summary>Squared length as a raw float.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float LengthSquared(in Vec3 vector) => Vec3.Dot(vector, vector);

    /// <summary>Cold out-of-range indexer path.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SimplexVertex ThrowOutOfRange(uint index) =>
        throw new ArgumentOutOfRangeException(nameof(index), index, $"Simplex vertex index '{index}' is out of range.");
}
