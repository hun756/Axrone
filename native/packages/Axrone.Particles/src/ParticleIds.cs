namespace Axrone.Particles;

/// <summary>Generational particle identity (20-bit index + 12-bit generation).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ParticleId(uint Raw) :
    IComparable<ParticleId>,
    IEquatable<ParticleId>,
    IComparisonOperators<ParticleId, ParticleId, bool>
{
    /// <summary>Index bit mask.</summary>
    public const uint IndexMask = 0x000F_FFFF;

    /// <summary>Generation bit shift.</summary>
    public const int GenerationShift = 20;

    /// <summary>Invalid identity.</summary>
    public static readonly ParticleId Invalid = new(0);

    /// <summary>Slot index.</summary>
    public uint Index
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => Raw & IndexMask;
    }

    /// <summary>Slot generation.</summary>
    public uint Generation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => Raw >> GenerationShift;
    }

    /// <summary>Whether the identity is valid.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => Raw != 0;
    }

    /// <summary>Creates an identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ParticleId Create(uint index, uint generation) =>
        new(((generation & 0x0FFF) << GenerationShift) | (index & IndexMask));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(ParticleId other) => Raw.CompareTo(other.Raw);

    /// <inheritdoc/>
    public override string ToString() =>
        $"ParticleId(Index={Index.ToString(CultureInfo.InvariantCulture)}, Gen={Generation.ToString(CultureInfo.InvariantCulture)})";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(ParticleId left, ParticleId right) => left.Raw < right.Raw;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(ParticleId left, ParticleId right) => left.Raw > right.Raw;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(ParticleId left, ParticleId right) => left.Raw <= right.Raw;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(ParticleId left, ParticleId right) => left.Raw >= right.Raw;
}

/// <summary>Nominal particle capacity.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ParticleCapacity(int Value) :
    IComparable<ParticleCapacity>,
    IEquatable<ParticleCapacity>,
    IComparisonOperators<ParticleCapacity, ParticleCapacity, bool>
{
    /// <summary>Converts to an integer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int ToInt32() => Value;

    /// <summary>Creates from an integer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ParticleCapacity FromInt32(int value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator int(ParticleCapacity cap) => cap.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static explicit operator ParticleCapacity(int value) => new(value);

    /// <inheritdoc/>
    public int CompareTo(ParticleCapacity other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(ParticleCapacity left, ParticleCapacity right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(ParticleCapacity left, ParticleCapacity right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(ParticleCapacity left, ParticleCapacity right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(ParticleCapacity left, ParticleCapacity right) => left.Value >= right.Value;
}

/// <summary>Nominal simulation timestamp.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SimulationTime(float Seconds) :
    IComparable<SimulationTime>,
    IEquatable<SimulationTime>,
    IComparisonOperators<SimulationTime, SimulationTime, bool>
{
    /// <summary>Advances by a delta.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static SimulationTime Add(SimulationTime time, DeltaTime dt) => new(time.Seconds + dt.Seconds);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static SimulationTime operator +(SimulationTime a, DeltaTime dt) => Add(a, dt);

    /// <inheritdoc/>
    public int CompareTo(SimulationTime other) => Seconds.CompareTo(other.Seconds);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(SimulationTime left, SimulationTime right) => left.Seconds < right.Seconds;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(SimulationTime left, SimulationTime right) => left.Seconds > right.Seconds;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(SimulationTime left, SimulationTime right) => left.Seconds <= right.Seconds;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(SimulationTime left, SimulationTime right) => left.Seconds >= right.Seconds;
}

/// <summary>Nominal simulation delta.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct DeltaTime(float Seconds) :
    IComparable<DeltaTime>,
    IEquatable<DeltaTime>,
    IComparisonOperators<DeltaTime, DeltaTime, bool>
{
    /// <summary>Converts to seconds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ToSingle() => Seconds;

    /// <summary>Creates from seconds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static DeltaTime FromSingle(float seconds) => new(seconds);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator float(DeltaTime dt) => dt.Seconds;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static explicit operator DeltaTime(float seconds) => new(seconds);

    /// <inheritdoc/>
    public int CompareTo(DeltaTime other) => Seconds.CompareTo(other.Seconds);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(DeltaTime left, DeltaTime right) => left.Seconds < right.Seconds;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(DeltaTime left, DeltaTime right) => left.Seconds > right.Seconds;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(DeltaTime left, DeltaTime right) => left.Seconds <= right.Seconds;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(DeltaTime left, DeltaTime right) => left.Seconds >= right.Seconds;
}
