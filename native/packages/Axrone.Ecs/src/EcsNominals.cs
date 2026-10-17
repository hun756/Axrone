namespace Axrone.Ecs;

using System.Globalization;

/// <summary>
/// Entity index: slot in the entity store, zero is reserved for null.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct EntityId(int Value) :
    IComparable<EntityId>,
    IComparisonOperators<EntityId, EntityId, bool>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(EntityId other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(EntityId left, EntityId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(EntityId left, EntityId right) => left.Value >= right.Value;

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(EntityId left, EntityId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(EntityId left, EntityId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Entity generation: recycled slots bump the version, stale handles mismatch.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct EntityVersion(int Value) :
    IComparable<EntityVersion>,
    IComparisonOperators<EntityVersion, EntityVersion, bool>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(EntityVersion other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(EntityVersion left, EntityVersion right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(EntityVersion left, EntityVersion right) => left.Value >= right.Value;

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(EntityVersion left, EntityVersion right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(EntityVersion left, EntityVersion right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// 64-bit entity handle: 32-bit index plus 32-bit generation.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 8)]
public readonly record struct Entity :
    IComparable<Entity>,
    IComparisonOperators<Entity, Entity, bool>
{
    /// <summary>Store slot.</summary>
    [FieldOffset(0)]
    public readonly EntityId Id;

    /// <summary>Slot generation.</summary>
    [FieldOffset(4)]
    public readonly EntityVersion Version;

    /// <summary>Null entity.</summary>
    public static readonly Entity Null = new(new EntityId(0), new EntityVersion(0));

    /// <summary>Creates an entity handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity(EntityId id, EntityVersion version)
    {
        Id = id;
        Version = version;
    }

    /// <summary>Whether both halves are zero.</summary>
    public bool IsNull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Id.Value == 0 && Version.Value == 0;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Entity other)
    {
        int c = Id.CompareTo(other.Id);
        return c != 0 ? c : Version.CompareTo(other.Version);
    }

    /// <summary>Determines whether the left entity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Entity left, Entity right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left entity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Entity left, Entity right) => left.CompareTo(right) >= 0;

    /// <summary>Determines whether the left entity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Entity left, Entity right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left entity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Entity left, Entity right) => left.CompareTo(right) <= 0;

    /// <inheritdoc/>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"Entity({Id.Value}v{Version.Value})");
}

/// <summary>
/// Component type index, at most 256 registered types.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 2)]
public readonly record struct ComponentId(ushort Value) :
    IComparable<ComponentId>,
    IComparisonOperators<ComponentId, ComponentId, bool>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(ComponentId other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(ComponentId left, ComponentId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(ComponentId left, ComponentId right) => left.Value >= right.Value;

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(ComponentId left, ComponentId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(ComponentId left, ComponentId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Archetype index in the graph.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ArchetypeId(int Value) :
    IComparable<ArchetypeId>,
    IComparisonOperators<ArchetypeId, ArchetypeId, bool>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(ArchetypeId other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(ArchetypeId left, ArchetypeId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(ArchetypeId left, ArchetypeId right) => left.Value >= right.Value;

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(ArchetypeId left, ArchetypeId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(ArchetypeId left, ArchetypeId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// 256-bit component bitmask over four words.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly struct ComponentMask : IEquatable<ComponentMask>
{
    /// <summary>First word.</summary>
    public readonly ulong B0;

    /// <summary>Second word.</summary>
    public readonly ulong B1;

    /// <summary>Third word.</summary>
    public readonly ulong B2;

    /// <summary>Fourth word.</summary>
    public readonly ulong B3;

    /// <summary>Empty mask.</summary>
    public static readonly ComponentMask Empty = new(0UL, 0UL, 0UL, 0UL);

    /// <summary>Creates a mask.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComponentMask(ulong b0, ulong b1, ulong b2, ulong b3)
    {
        B0 = b0;
        B1 = b1;
        B2 = b2;
        B3 = b3;
    }

    /// <summary>Whether no bits are set.</summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (B0 | B1 | B2 | B3) == 0UL;
    }

    /// <summary>Returns a copy with a bit set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComponentMask With(ComponentId id)
    {
        ushort val = id.Value;
        int word = val >> 6;
        ulong bit = 1UL << (val & 63);

        return word switch
        {
            0 => new ComponentMask(B0 | bit, B1, B2, B3),
            1 => new ComponentMask(B0, B1 | bit, B2, B3),
            2 => new ComponentMask(B0, B1, B2 | bit, B3),
            3 => new ComponentMask(B0, B1, B2, B3 | bit),
            _ => this,
        };
    }

    /// <summary>Returns a copy with a bit cleared.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComponentMask Without(ComponentId id)
    {
        ushort val = id.Value;
        int word = val >> 6;
        ulong mask = ~(1UL << (val & 63));

        return word switch
        {
            0 => new ComponentMask(B0 & mask, B1, B2, B3),
            1 => new ComponentMask(B0, B1 & mask, B2, B3),
            2 => new ComponentMask(B0, B1, B2 & mask, B3),
            3 => new ComponentMask(B0, B1, B2, B3 & mask),
            _ => this,
        };
    }

    /// <summary>Whether a bit is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSet(ComponentId id)
    {
        ushort val = id.Value;
        int word = val >> 6;
        ulong bit = 1UL << (val & 63);

        return word switch
        {
            0 => (B0 & bit) != 0UL,
            1 => (B1 & bit) != 0UL,
            2 => (B2 & bit) != 0UL,
            3 => (B3 & bit) != 0UL,
            _ => false,
        };
    }

    /// <summary>Whether every bit of the other mask is set here.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsAll(in ComponentMask other) =>
        ((B0 & other.B0) == other.B0) &&
        ((B1 & other.B1) == other.B1) &&
        ((B2 & other.B2) == other.B2) &&
        ((B3 & other.B3) == other.B3);

    /// <summary>Whether any bit of the other mask is set here.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsAny(in ComponentMask other) =>
        ((B0 & other.B0) | (B1 & other.B1) | (B2 & other.B2) | (B3 & other.B3)) != 0UL;

    /// <summary>Unions two masks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComponentMask operator |(in ComponentMask a, in ComponentMask b) =>
        new(a.B0 | b.B0, a.B1 | b.B1, a.B2 | b.B2, a.B3 | b.B3);

    /// <summary>Intersects two masks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComponentMask operator &(in ComponentMask a, in ComponentMask b) =>
        new(a.B0 & b.B0, a.B1 & b.B1, a.B2 & b.B2, a.B3 & b.B3);

    /// <summary>Exclusive-ors two masks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComponentMask operator ^(in ComponentMask a, in ComponentMask b) =>
        new(a.B0 ^ b.B0, a.B1 ^ b.B1, a.B2 ^ b.B2, a.B3 ^ b.B3);

    /// <summary>Negates a mask.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComponentMask operator ~(in ComponentMask a) =>
        new(~a.B0, ~a.B1, ~a.B2, ~a.B3);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ComponentMask other) =>
        B0 == other.B0 && B1 == other.B1 && B2 == other.B2 && B3 == other.B3;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ComponentMask other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(B0, B1, B2, B3);

    /// <summary>Determines whether two masks are equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in ComponentMask left, in ComponentMask right) => left.Equals(right);

    /// <summary>Determines whether two masks differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in ComponentMask left, in ComponentMask right) => !left.Equals(right);
}
