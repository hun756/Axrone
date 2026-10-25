namespace Axrone.Particles;

/// <summary>Native struct-of-arrays particle storage with generational handles.</summary>
public sealed unsafe partial class NativeSoaParticleStorage : IDisposable
{
    /// <summary>Zero-alloc enumerator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ParticleEnumerator GetEnumerator() => new(this);
}

/// <summary>Storage enumerator.</summary>
public ref struct ParticleEnumerator
{
    private readonly NativeSoaParticleStorage _storage;
    private readonly int _count;
    private int _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ParticleEnumerator(NativeSoaParticleStorage storage)
    {
        _storage = storage;
        _count = storage.ActiveCount;
        _index = -1;
    }

    /// <summary>Advances to the next particle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        int next = _index + 1;
        if ((uint)next < (uint)_count)
        {
            _index = next;
            return true;
        }

        return false;
    }

    /// <summary>Current particle reference.</summary>
    public readonly ParticleReference Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_storage, _index);
    }
}

/// <summary>Indexed particle view over SoA streams.</summary>
public unsafe ref struct ParticleReference
{
    private readonly NativeSoaParticleStorage _storage;
    private readonly int _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ParticleReference(NativeSoaParticleStorage storage, int index)
    {
        _storage = storage;
        _index = index;
    }

    /// <summary>Particle identity.</summary>
    public readonly ParticleId Id
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _storage.DenseToId[_index];
    }

    /// <summary>Position.</summary>
    public Vec3 Position
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_storage.PosX[_index], _storage.PosY[_index], _storage.PosZ[_index]);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _storage.PosX[_index] = value.X;
            _storage.PosY[_index] = value.Y;
            _storage.PosZ[_index] = value.Z;
        }
    }

    /// <summary>Velocity.</summary>
    public Vec3 Velocity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_storage.VelX[_index], _storage.VelY[_index], _storage.VelZ[_index]);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _storage.VelX[_index] = value.X;
            _storage.VelY[_index] = value.Y;
            _storage.VelZ[_index] = value.Z;
        }
    }

    /// <summary>Color.</summary>
    public Vec4 Color
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_storage.ColR[_index], _storage.ColG[_index], _storage.ColB[_index], _storage.ColA[_index]);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _storage.ColR[_index] = value.X;
            _storage.ColG[_index] = value.Y;
            _storage.ColB[_index] = value.Z;
            _storage.ColA[_index] = value.W;
        }
    }

    /// <summary>Age.</summary>
    public float Age
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _storage.Age[_index];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _storage.Age[_index] = value;
    }

    /// <summary>Lifetime.</summary>
    public float Lifetime
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _storage.Lifetime[_index];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _storage.Lifetime[_index] = value;
    }
}
