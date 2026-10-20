namespace Axrone.Asset;

/// <summary>
/// Asset record: the stored value object surfaced to readers.
/// </summary>
public readonly record struct AssetRecord<TData>
{
    /// <summary>Asset kind.</summary>
    public string Kind { get; }

    /// <summary>Unique identity.</summary>
    public AssetId Id { get; }

    /// <summary>Stable lookup key.</summary>
    public AssetKey Key { get; }

    /// <summary>Alternative lookup keys.</summary>
    public IReadOnlyList<AssetKey> Aliases { get; }

    /// <summary>Display name.</summary>
    public string Name { get; }

    /// <summary>Asset payload.</summary>
    public TData Data { get; }

    /// <summary>Monotonic revision.</summary>
    public AssetRevision Revision { get; }

    /// <summary>Content fingerprint.</summary>
    public AssetFingerprint Fingerprint { get; }

    /// <summary>Creation time in Unix milliseconds.</summary>
    public long CreatedAtEpochMs { get; }

    /// <summary>Last update time in Unix milliseconds.</summary>
    public long UpdatedAtEpochMs { get; }

    /// <summary>Asset metadata.</summary>
    public AssetMetadata Metadata { get; }

    /// <summary>Dependency identities.</summary>
    public IReadOnlyList<AssetId> DependencyIds { get; }

    /// <summary>Creates a record.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetRecord(
        string kind,
        AssetId id,
        AssetKey key,
        IReadOnlyList<AssetKey>? aliases,
        string name,
        TData data,
        AssetRevision revision,
        AssetFingerprint fingerprint,
        long createdAtEpochMs,
        long updatedAtEpochMs,
        AssetMetadata metadata,
        IReadOnlyList<AssetId>? dependencyIds)
    {
        Kind = kind;
        Id = id;
        Key = key;
        Aliases = aliases ?? [];
        Name = name;
        Data = data;
        Revision = revision;
        Fingerprint = fingerprint;
        CreatedAtEpochMs = createdAtEpochMs;
        UpdatedAtEpochMs = updatedAtEpochMs;
        Metadata = metadata;
        DependencyIds = dependencyIds ?? [];
    }
}

/// <summary>
/// Write input for upserts: the caller supplies identity and payload,
/// the database assigns revision, fingerprint and timestamps.
/// </summary>
public readonly record struct AssetWriteInput<TData>(
    string Kind,
    AssetId? Id,
    AssetKey Key,
    string Name,
    TData Data,
    IReadOnlyList<AssetKey>? Aliases = null,
    AssetMetadata? Metadata = null,
    IReadOnlyList<AssetId>? DependencyIds = null);

/// <summary>
/// Source binding: an external source identity pinned to one asset.
/// </summary>
public readonly record struct AssetSourceBinding(
    AssetSourceIdentity Identity,
    AssetId TargetId,
    long BoundAtEpochMs);
