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

    /// <summary>Parses a reference token.</summary>
    public static AssetReference<TKind> Parse(ReadOnlySpan<char> token)
    {
        if (!TryParse(token, out AssetReference<TKind> reference))
        {
            ThrowHelper.ThrowReferenceError("Failed to parse asset reference token.");
        }

        return reference;
    }

    /// <summary>Tries to parse a reference token.</summary>
    public static bool TryParse(ReadOnlySpan<char> token, out AssetReference<TKind> reference)
    {
        reference = default;
        if (!token.StartsWith("asset:", StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> rest = token["asset:".Length..];
        int colon = rest.IndexOf(':');
        if (colon <= 0 || !rest[..colon].Equals(typeof(TKind).Name.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!AssetId.TryParse(rest[(colon + 1)..], CultureInfo.InvariantCulture, out AssetId id))
        {
            return false;
        }

        reference = new AssetReference<TKind>(id);
        return true;
    }
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

    /// <summary>Parses a versioned reference token.</summary>
    public static AssetVersionedReference<TKind> Parse(ReadOnlySpan<char> token)
    {
        if (!TryParse(token, out AssetVersionedReference<TKind> reference))
        {
            ThrowHelper.ThrowReferenceError("Failed to parse versioned asset reference token.");
        }

        return reference;
    }

    /// <summary>Tries to parse a versioned reference token.</summary>
    public static bool TryParse(ReadOnlySpan<char> token, out AssetVersionedReference<TKind> reference)
    {
        reference = default;
        if (!token.StartsWith("asset:", StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> rest = token["asset:".Length..];
        int colon = rest.IndexOf(':');
        if (colon <= 0 || !rest[..colon].Equals(typeof(TKind).Name.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        ReadOnlySpan<char> tail = rest[(colon + 1)..];
        int at = tail.IndexOf('@');
        if (at <= 0)
        {
            return false;
        }

        if (!AssetId.TryParse(tail[..at], CultureInfo.InvariantCulture, out AssetId id) ||
            !long.TryParse(tail[(at + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out long rev))
        {
            return false;
        }

        reference = new AssetVersionedReference<TKind>(id, new AssetRevision(rev));
        return true;
    }
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
