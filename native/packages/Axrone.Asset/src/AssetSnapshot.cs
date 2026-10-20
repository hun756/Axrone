namespace Axrone.Asset;

/// <summary>Serialized asset revision, including history entries.</summary>
public sealed record AssetSnapshotRecord
{
    /// <summary>Asset kind.</summary>
    public required string Kind { get; init; }

    /// <summary>Asset id.</summary>
    public required ulong Id { get; init; }

    /// <summary>Stable lookup key.</summary>
    public required string Key { get; init; }

    /// <summary>Alternative lookup keys.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Serialized payload.</summary>
    public required string SerializedData { get; init; }

    /// <summary>Revision number.</summary>
    public required long Revision { get; init; }

    /// <summary>Content fingerprint.</summary>
    public required string Fingerprint { get; init; }

    /// <summary>Creation time in Unix milliseconds.</summary>
    public required long CreatedAtEpochMs { get; init; }

    /// <summary>Last update time in Unix milliseconds.</summary>
    public required long UpdatedAtEpochMs { get; init; }

    /// <summary>Asset metadata.</summary>
    public required AssetMetadata Metadata { get; init; }

    /// <summary>Dependency identities.</summary>
    public IReadOnlyList<ulong> DependencyIds { get; init; } = [];

    /// <summary>Superseded revisions, oldest first.</summary>
    public IReadOnlyList<AssetSnapshotRecord>? History { get; init; }
}

/// <summary>Serialized source binding.</summary>
public sealed record AssetSnapshotSourceBindingRecord(string Identity, ulong TargetId, long BoundAtEpochMs);

/// <summary>Versioned database snapshot; version 4 is current.</summary>
public sealed record AssetDatabaseSnapshot
{
    /// <summary>Snapshot format version.</summary>
#pragma warning disable CA1822 // Instance member mirrors the serialized shape; call sites read it off the snapshot.
    public int Version => 4;
#pragma warning restore CA1822

    /// <summary>Database locale.</summary>
    public required string Locale { get; init; }

    /// <summary>Capture time in Unix milliseconds.</summary>
    public required long CapturedAtEpochMs { get; init; }

    /// <summary>Stored assets.</summary>
    public required IReadOnlyList<AssetSnapshotRecord> Assets { get; init; }

    /// <summary>Source bindings.</summary>
    public IReadOnlyList<AssetSnapshotSourceBindingRecord>? SourceBindings { get; init; }
}

/// <summary>Source-generated JSON metadata for asset snapshots; keeps trimming intact.</summary>
[JsonSerializable(typeof(AssetBinaryValue))]
internal sealed partial class AssetJsonContext : JsonSerializerContext
{
}

/// <summary>Hydration options.</summary>
public sealed record AssetHydrateOptions(bool Replace = false);
