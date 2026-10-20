namespace Axrone.Asset;

/// <summary>Selector discriminant.</summary>
public enum SelectorKind : byte
{
    /// <summary>No selection.</summary>
    None = 0,

    /// <summary>By identity.</summary>
    Id = 1,

    /// <summary>By key, with optional kind filter.</summary>
    Key = 2,

    /// <summary>By versioned reference token.</summary>
    VersionedReference = 3,
}

/// <summary>
/// Flat tagged selector: id, key or versioned reference. Parses
/// <c>asset:{kind}:{id}[@{revision}]</c> tokens, hex identities and keys.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct AssetSelector : IEquatable<AssetSelector>
{
    /// <summary>Active arm.</summary>
    public SelectorKind Kind { get; }

    /// <summary>Identity arm.</summary>
    public AssetId Id { get; }

    /// <summary>Key arm.</summary>
    public AssetKey Key { get; }

    /// <summary>Revision arm.</summary>
    public AssetRevision Revision { get; }

    /// <summary>Kind filter hash for key lookups.</summary>
    public ulong KindFilterHash { get; }

    private AssetSelector(SelectorKind kind, AssetId id, AssetKey key, AssetRevision revision, ulong kindHash)
    {
        Kind = kind;
        Id = id;
        Key = key;
        Revision = revision;
        KindFilterHash = kindHash;
    }

    /// <summary>Selects by identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetSelector FromId(AssetId id) => new(SelectorKind.Id, id, default, default, 0UL);

    /// <summary>Selects by key, optionally filtered to a kind.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetSelector FromKey(AssetKey key, string? kind = null) =>
        new(SelectorKind.Key, default, key, default, kind is not null ? Fnv1a64.ComputeUtf8Chars(kind.AsSpan()) : 0UL);

    /// <summary>Selects by versioned reference.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetSelector FromVersioned(AssetId id, AssetRevision revision, string? kind = null) =>
        new(SelectorKind.VersionedReference, id, default, revision, kind is not null ? Fnv1a64.ComputeUtf8Chars(kind.AsSpan()) : 0UL);

    /// <summary>Parses a token, identity or key.</summary>
    public static AssetSelector Parse(ReadOnlySpan<char> span)
    {
        if (span.StartsWith("asset:", StringComparison.Ordinal))
        {
            ReadOnlySpan<char> rest = span["asset:".Length..];
            int colon = rest.IndexOf(':');
            if (colon > 0)
            {
                ReadOnlySpan<char> kindSpan = rest[..colon];
                ReadOnlySpan<char> tail = rest[(colon + 1)..];
                ulong kindHash = Fnv1a64.ComputeUtf8Chars(kindSpan);
                int at = tail.IndexOf('@');
                if (at > 0)
                {
                    ReadOnlySpan<char> idSpan = tail[..at];
                    ReadOnlySpan<char> revSpan = tail[(at + 1)..];
                    if (AssetId.TryParse(idSpan, CultureInfo.InvariantCulture, out AssetId parsedId) &&
                        long.TryParse(revSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out long revVal))
                    {
                        return new AssetSelector(SelectorKind.VersionedReference, parsedId, default, new AssetRevision(revVal), kindHash);
                    }
                }
                else if (AssetId.TryParse(tail, CultureInfo.InvariantCulture, out AssetId parsedDirect))
                {
                    return new AssetSelector(SelectorKind.Id, parsedDirect, default, default, kindHash);
                }
            }
        }

        if (AssetId.TryParse(span, CultureInfo.InvariantCulture, out AssetId parsed))
        {
            return FromId(parsed);
        }

        return FromKey(new AssetKey(span.ToString()));
    }

    /// <summary>Creates an id selector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator AssetSelector(AssetId id) => FromId(id);

    /// <summary>Creates a key selector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator AssetSelector(AssetKey key) => FromKey(key);

    /// <summary>Creates a selector from text.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator AssetSelector(string keyOrToken)
    {
        ArgumentNullException.ThrowIfNull(keyOrToken);
        return Parse(keyOrToken.AsSpan());
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(AssetSelector left, AssetSelector right) => left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(AssetSelector left, AssetSelector right) => !left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AssetSelector other) =>
        Kind == other.Kind && Id.Equals(other.Id) && Key.Equals(other.Key) &&
        Revision.Equals(other.Revision) && KindFilterHash == other.KindFilterHash;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is AssetSelector other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine((byte)Kind, Id, Key, Revision, KindFilterHash);
}
