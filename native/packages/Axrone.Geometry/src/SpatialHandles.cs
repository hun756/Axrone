namespace Axrone.Geometry;

using System.Globalization;

/// <summary>
/// Generation stamp of a spatial-tree node slot, bumped every time the slot is
/// recycled. A handle pairs a slot index with the generation observed at insert,
/// so a handle held across a remove/realloc cycle no longer matches and the
/// stale access is rejected instead of aliasing a different item.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct NodeGeneration(uint Value) : IComparable<NodeGeneration>
{
    /// <summary>Generation bits carried inside a <see cref="SpatialHandle"/>.</summary>
    public const uint Mask = 0xFFF;

    /// <summary>First generation assigned to a freshly linked slot.</summary>
    public static NodeGeneration Initial => new(1);

    /// <summary>Advances to the next generation, wrapping inside <see cref="Mask"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NodeGeneration Next() => new((Value + 1) & Mask);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(NodeGeneration other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"Gen({Value})");

    /// <summary>Determines whether the left generation sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(NodeGeneration left, NodeGeneration right) => left.Value < right.Value;

    /// <summary>Determines whether the left generation sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(NodeGeneration left, NodeGeneration right) => left.Value > right.Value;

    /// <summary>Determines whether the left generation sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(NodeGeneration left, NodeGeneration right) => left.Value <= right.Value;

    /// <summary>Determines whether the left generation sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(NodeGeneration left, NodeGeneration right) => left.Value >= right.Value;
}

/// <summary>
/// Generational identity of an item in a dynamic spatial tree.
/// The slot index names the node pool entry and the generation names the
/// allocation epoch, so a recycled slot never answers to a handle issued for
/// its previous occupant. The zero value is the invalid sentinel.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SpatialHandle(uint Raw) : IComparable<SpatialHandle>
{
    /// <summary>Invalid sentinel; never names a live item.</summary>
    public static SpatialHandle Invalid => default;

    private const uint IndexMask = 0x000FFFFF;
    private const int GenerationShift = 20;

    /// <summary>Creates a handle for <paramref name="index"/> at <paramref name="generation"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpatialHandle Create(NodeIndex index, NodeGeneration generation)
    {
        if (!index.IsValid)
        {
            return Invalid;
        }

        return new SpatialHandle(
            ((generation.Value & NodeGeneration.Mask) << GenerationShift)
            | ((uint)(index.RawValue + 1) & IndexMask));
    }

    /// <summary>Whether this handle names a possibly-live item.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Raw != 0;
    }

    /// <summary>Node pool slot named by this handle.</summary>
    public NodeIndex Index
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => NodeIndex.FromRaw((int)(Raw & IndexMask) - 1);
    }

    /// <summary>Allocation epoch observed when this handle was issued.</summary>
    public NodeGeneration Generation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(Raw >> GenerationShift);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(SpatialHandle other) => Raw.CompareTo(other.Raw);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() =>
        IsValid
            ? string.Create(CultureInfo.InvariantCulture, $"Handle({Index.RawValue}:{Generation.Value})")
            : "Handle(Invalid)";

    /// <summary>Determines whether the left handle sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SpatialHandle left, SpatialHandle right) => left.Raw < right.Raw;

    /// <summary>Determines whether the left handle sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SpatialHandle left, SpatialHandle right) => left.Raw > right.Raw;

    /// <summary>Determines whether the left handle sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SpatialHandle left, SpatialHandle right) => left.Raw <= right.Raw;

    /// <summary>Determines whether the left handle sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SpatialHandle left, SpatialHandle right) => left.Raw >= right.Raw;
}

/// <summary>
/// Linear distance along a ray, kept as a nominal so a miss (positive infinity)
/// and a touch (zero) read as values rather than sentinel floats at query sites.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Distance(float Value) : IComparable<Distance>
{
    /// <summary>Coincident contact.</summary>
    public static Distance Zero => new(0.0f);

    /// <summary>Default self-intersection guard: hits closer than this are ignored.</summary>
    public static Distance DefaultEpsilon => new(1e-4f);

    /// <summary>No contact; the "no candidate" answer of a ray query.</summary>
    public static Distance PositiveInfinity => new(float.PositiveInfinity);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(Distance other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Value.ToString("G", CultureInfo.InvariantCulture);

    /// <summary>Unwraps the underlying distance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float(Distance distance) => distance.Value;

    /// <summary>Wraps a raw distance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Distance(float value) => new(value);

    /// <summary>Determines whether the left distance is below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Distance left, Distance right) => left.Value < right.Value;

    /// <summary>Determines whether the left distance exceeds the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Distance left, Distance right) => left.Value > right.Value;

    /// <summary>Determines whether the left distance is at or below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Distance left, Distance right) => left.Value <= right.Value;

    /// <summary>Determines whether the left distance is at or above the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Distance left, Distance right) => left.Value >= right.Value;
}
