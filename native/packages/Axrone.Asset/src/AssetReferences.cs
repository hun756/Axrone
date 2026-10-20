namespace Axrone.Asset;

/// <summary>
/// Typed asset reference: an id namespaced by its kind.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct AssetReference<TKind>(AssetId Id) :
    IComparable<AssetReference<TKind>>,
    IEquatable<AssetReference<TKind>>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetReference<TKind> other) => Id.CompareTo(other.Id);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AssetReference<TKind> left, AssetReference<TKind> right) => left.CompareTo(right) < 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AssetReference<TKind> left, AssetReference<TKind> right) => left.CompareTo(right) <= 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AssetReference<TKind> left, AssetReference<TKind> right) => left.CompareTo(right) > 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AssetReference<TKind> left, AssetReference<TKind> right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"asset:{typeof(TKind).Name.ToLowerInvariant()}:{Id}");
}

/// <summary>
/// Typed versioned asset reference: kind plus id plus revision.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct AssetVersionedReference<TKind>(AssetId Id, AssetRevision Revision) :
    IComparable<AssetVersionedReference<TKind>>,
    IEquatable<AssetVersionedReference<TKind>>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetVersionedReference<TKind> other)
    {
        int cmp = Id.CompareTo(other.Id);
        return cmp != 0 ? cmp : Revision.CompareTo(other.Revision);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AssetVersionedReference<TKind> left, AssetVersionedReference<TKind> right) => left.CompareTo(right) < 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AssetVersionedReference<TKind> left, AssetVersionedReference<TKind> right) => left.CompareTo(right) <= 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AssetVersionedReference<TKind> left, AssetVersionedReference<TKind> right) => left.CompareTo(right) > 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AssetVersionedReference<TKind> left, AssetVersionedReference<TKind> right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"asset:{typeof(TKind).Name.ToLowerInvariant()}:{Id}@{Revision}");
}

/// <summary>Key lookup with an optional kind hash filter.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct AssetLookupByKey(AssetKey Key, ulong KindHash) : IEquatable<AssetLookupByKey>
{
    /// <summary>Creates a lookup, hashing the kind when provided.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetLookupByKey(AssetKey key, string? kind = null)
        : this(key, kind is not null ? Fnv1a64.ComputeUtf8Chars(kind.AsSpan()) : 0UL)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AssetLookupByKey other) => Key.Equals(other.Key) && KindHash == other.KindHash;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Key, KindHash);
}
