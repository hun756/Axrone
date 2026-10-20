namespace Axrone.Asset;

/// <summary>Internal storage row: untyped payload plus bookkeeping.</summary>
public sealed class StoredAsset
{
    /// <summary>Asset kind.</summary>
    public required string Kind { get; init; }

    /// <summary>Unique identity.</summary>
    public required AssetId Id { get; init; }

    /// <summary>Stable lookup key.</summary>
    public required AssetKey Key { get; init; }

    /// <summary>Alternative lookup keys.</summary>
    public required IReadOnlyList<AssetKey> Aliases { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Untyped payload.</summary>
    public required object? Data { get; init; }

    /// <summary>Monotonic revision.</summary>
    public required AssetRevision Revision { get; init; }

    /// <summary>Content fingerprint.</summary>
    public required AssetFingerprint Fingerprint { get; init; }

    /// <summary>Creation time in Unix milliseconds.</summary>
    public required long CreatedAtEpochMs { get; init; }

    /// <summary>Last update time in Unix milliseconds.</summary>
    public required long UpdatedAtEpochMs { get; init; }

    /// <summary>Asset metadata.</summary>
    public required AssetMetadata Metadata { get; init; }

    /// <summary>Dependency identities.</summary>
    public required IReadOnlyList<AssetId> DependencyIds { get; init; }

    /// <summary>Projects the row into a typed record.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetRecord<TData> ToRecord<TData>() => new(
        Kind,
        Id,
        Key,
        Aliases,
        Name,
        (TData)Data!,
        Revision,
        Fingerprint,
        CreatedAtEpochMs,
        UpdatedAtEpochMs,
        Metadata,
        DependencyIds);

    /// <summary>Clones the row at a new revision with replaced fields.</summary>
    public StoredAsset CloneWithNewRevision(
        AssetRevision newRevision,
        object? newData,
        AssetFingerprint newFp,
        long updatedEpoch,
        AssetMetadata? newMeta,
        IReadOnlyList<AssetId>? newDeps,
        IReadOnlyList<AssetKey>? newAliases,
        string? newName) => new()
        {
            Kind = Kind,
            Id = Id,
            Key = Key,
            Aliases = newAliases ?? Aliases,
            Name = newName ?? Name,
            Data = newData,
            Revision = newRevision,
            Fingerprint = newFp,
            CreatedAtEpochMs = CreatedAtEpochMs,
            UpdatedAtEpochMs = updatedEpoch,
            Metadata = newMeta ?? Metadata,
            DependencyIds = newDeps ?? DependencyIds,
        };
}
