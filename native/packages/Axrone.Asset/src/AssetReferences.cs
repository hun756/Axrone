namespace Axrone.Asset;

/// <summary>
/// Unversioned asset reference: kind plus id, tokenized as
/// <c>asset:{kind}:{id}</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct AssetReference
{
    /// <summary>Asset kind.</summary>
    public AssetKind Kind { get; }

    /// <summary>Asset id.</summary>
    public AssetId Id { get; }

    /// <summary>Creates a reference.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetReference(AssetKind kind, AssetId id)
    {
        Kind = kind;
        Id = id;
    }

    /// <summary>Canonical token.</summary>
    public string Token => string.Create(
        CultureInfo.InvariantCulture,
        $"asset:{Kind.Value}:{Id.Value}");

    /// <summary>Parses a token; throws <see cref="AssetReferenceException"/> when malformed.</summary>
    public static AssetReference Parse(string token)
    {
        if (!TryParse(token, out AssetReference reference))
        {
            throw new AssetReferenceException($"Invalid asset reference: '{token}'.");
        }

        return reference;
    }

    /// <summary>Tries to parse a token.</summary>
    public static bool TryParse(string? token, out AssetReference reference)
    {
        reference = default;
        if (string.IsNullOrEmpty(token) || !token.StartsWith("asset:", StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> body = token.AsSpan("asset:".Length);
        int separator = body.IndexOf(':');
        if (separator <= 0 || separator == body.Length - 1)
        {
            return false;
        }

        ReadOnlySpan<char> kind = body[..separator];
        ReadOnlySpan<char> id = body[(separator + 1)..];
        if (id.IndexOf('@') >= 0 || id.IndexOf(':') >= 0)
        {
            return false;
        }

        try
        {
            reference = new AssetReference(new AssetKind(kind.ToString()), new AssetId(id.ToString()));
            return true;
        }
        catch (AssetConfigurationException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Token;
}

/// <summary>
/// Versioned asset reference: kind plus id plus revision, tokenized as
/// <c>asset:{kind}:{id}@{revision}</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct AssetVersionedReference
{
    /// <summary>Asset kind.</summary>
    public AssetKind Kind { get; }

    /// <summary>Asset id.</summary>
    public AssetId Id { get; }

    /// <summary>Asset revision.</summary>
    public AssetRevision Revision { get; }

    /// <summary>Creates a versioned reference.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetVersionedReference(AssetKind kind, AssetId id, AssetRevision revision)
    {
        Kind = kind;
        Id = id;
        Revision = revision;
    }

    /// <summary>Drops the revision.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetReference Unversioned() => new(Kind, Id);

    /// <summary>Canonical token.</summary>
    public string Token => string.Create(
        CultureInfo.InvariantCulture,
        $"asset:{Kind.Value}:{Id.Value}@{Revision.Value}");

    /// <summary>Parses a token; throws <see cref="AssetReferenceException"/> when malformed.</summary>
    public static AssetVersionedReference Parse(string token)
    {
        if (!TryParse(token, out AssetVersionedReference reference))
        {
            throw new AssetReferenceException($"Invalid asset reference: '{token}'.");
        }

        return reference;
    }

    /// <summary>Tries to parse a token.</summary>
    public static bool TryParse(string? token, out AssetVersionedReference reference)
    {
        reference = default;
        if (string.IsNullOrEmpty(token) || !token.StartsWith("asset:", StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> body = token.AsSpan("asset:".Length);
        int at = body.LastIndexOf('@');
        if (at < 0)
        {
            return false;
        }

        ReadOnlySpan<char> head = body[..at];
        ReadOnlySpan<char> revision = body[(at + 1)..];
        int separator = head.IndexOf(':');
        if (separator <= 0 || separator == head.Length - 1)
        {
            return false;
        }

        ReadOnlySpan<char> kind = head[..separator];
        ReadOnlySpan<char> id = head[(separator + 1)..];
        if (id.IndexOf(':') >= 0)
        {
            return false;
        }

        try
        {
            reference = new AssetVersionedReference(
                new AssetKind(kind.ToString()),
                new AssetId(id.ToString()),
                AssetRevision.FromSpan(revision));
            return true;
        }
        catch (AssetConfigurationException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Token;
}
