namespace Axrone.Asset;

/// <summary>Import source contract.</summary>
public interface IAssetSource
{
    /// <summary>Source kind: bytes, text, json or custom.</summary>
    string Kind { get; }

    /// <summary>Raw payload.</summary>
    object RawData { get; }
}

/// <summary>Binary import source.</summary>
public readonly record struct AssetBytesSource : IAssetSource
{
    private readonly byte[] _bytes;

    /// <summary>Creates a source.</summary>
    public AssetBytesSource(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        _bytes = bytes;
    }

    /// <summary>Payload bytes.</summary>
    public IReadOnlyList<byte> Bytes => _bytes;

    /// <inheritdoc/>
    public string Kind => "bytes";

    /// <inheritdoc/>
    public object RawData => _bytes;
}

/// <summary>Text import source.</summary>
public readonly record struct AssetTextSource(string Text) : IAssetSource
{
    /// <summary>Source kind.</summary>
    public string Kind => "text";

    /// <inheritdoc/>
    public object RawData => Text;
}

/// <summary>One import diagnostic.</summary>
public readonly record struct AssetImportDiagnostic(string Severity, string Code, string Message);

/// <summary>Mutable import context threading source, tuning and diagnostics.</summary>
public sealed class AssetImportContext
{
    /// <summary>Creates a context.</summary>
    public AssetImportContext(
        IAssetSource source,
        AssetKey baseKey,
        AssetUri? sourceUri = null,
        AssetSourceIdentity? sourceIdentity = null,
        AssetMetadata? metadata = null)
    {
        Source = source;
        BaseKey = baseKey;
        SourceUri = sourceUri;
        SourceIdentity = sourceIdentity;
        Metadata = metadata ?? AssetMetadata.Empty;
    }

    /// <summary>Import source.</summary>
    public IAssetSource Source { get; set; }

    /// <summary>Source URI, when known.</summary>
    public AssetUri? SourceUri { get; }

    /// <summary>Base key for produced assets.</summary>
    public AssetKey BaseKey { get; set; }

    /// <summary>Source identity binding, when known.</summary>
    public AssetSourceIdentity? SourceIdentity { get; }

    /// <summary>Metadata seed.</summary>
    public AssetMetadata Metadata { get; set; }

    /// <summary>Accumulated diagnostics.</summary>
    public IReadOnlyList<AssetImportDiagnostic> Diagnostics => _diagnostics;

    private readonly List<AssetImportDiagnostic> _diagnostics = new();

    /// <summary>Appends a diagnostic.</summary>
    public void AddDiagnostic(in AssetImportDiagnostic diagnostic) => _diagnostics.Add(diagnostic);
}

/// <summary>Import result: one primary write plus optional extras.</summary>
public sealed class AssetImportResult
{
    /// <summary>Primary write.</summary>
    public required AssetWriteInput<object> Primary { get; init; }

    /// <summary>Additional writes.</summary>
    public IReadOnlyList<AssetWriteInput<object>>? Additional { get; init; }

    /// <summary>Diagnostics.</summary>
    public IReadOnlyList<AssetImportDiagnostic>? Diagnostics { get; init; }
}

/// <summary>Import receipt: what the pipeline produced.</summary>
public readonly record struct AssetImportReceipt<TData>(
    AssetImporterId ImporterId,
    string SourceKind,
    AssetUri? SourceUri,
    AssetKey BaseKey,
    AssetSourceIdentity? SourceIdentity,
    long ImportedAtEpochMs,
    IReadOnlyList<AssetImportDiagnostic> Diagnostics,
    AssetRecord<TData> Primary,
    IReadOnlyList<AssetRecord<object>> Assets);

/// <summary>Importer contract: match, then import.</summary>
public interface IAssetImporter
{
    /// <summary>Importer identity.</summary>
    AssetImporterId Id { get; }

    /// <summary>Priority; higher wins.</summary>
    int Priority { get; }

    /// <summary>Accepted source kinds; empty matches all.</summary>
    IReadOnlyList<string> SourceKinds => [];

    /// <summary>Accepted path extensions (with or without dot); empty matches all.</summary>
    IReadOnlyList<string> Extensions => [];

    /// <summary>Accepted mime types; empty matches all.</summary>
    IReadOnlyList<string> MimeTypes => [];

    /// <summary>Whether this importer accepts the source.</summary>
    bool CanImport(AssetImportContext context);

    /// <summary>Imports the source.</summary>
    ValueTask<AssetImportResult> ImportAsync(AssetImportContext context, CancellationToken cancellationToken = default);
}

/// <summary>Bytes importer producing binary assets.</summary>
public sealed class RawBytesImporter : IAssetImporter
{
    /// <inheritdoc/>
    public AssetImporterId Id => new("axrone.importer.bytes");

    /// <inheritdoc/>
    public int Priority => 0;

    /// <inheritdoc/>
    public IReadOnlyList<string> SourceKinds => ["bytes"];

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanImport(AssetImportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Source.Kind == "bytes";
    }

    /// <inheritdoc/>
    public ValueTask<AssetImportResult> ImportAsync(AssetImportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        AssetWriteInput<object> primary = new(
            Kind: "binary",
            Id: null,
            Key: context.BaseKey,
            Name: context.BaseKey.Value ?? "bytes_asset",
            Data: context.Source.RawData,
            Metadata: context.Metadata);

        return ValueTask.FromResult(new AssetImportResult { Primary = primary });
    }
}

/// <summary>Text importer producing text assets.</summary>
public sealed class TextImporter : IAssetImporter
{
    /// <inheritdoc/>
    public AssetImporterId Id => new("axrone.importer.text");

    /// <inheritdoc/>
    public int Priority => 0;

    /// <inheritdoc/>
    public IReadOnlyList<string> SourceKinds => ["text"];

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanImport(AssetImportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Source.Kind == "text";
    }

    /// <inheritdoc/>
    public ValueTask<AssetImportResult> ImportAsync(AssetImportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        AssetWriteInput<object> primary = new(
            Kind: "text",
            Id: null,
            Key: context.BaseKey,
            Name: context.BaseKey.Value ?? "text_asset",
            Data: context.Source.RawData,
            Metadata: context.Metadata);

        return ValueTask.FromResult(new AssetImportResult { Primary = primary });
    }
}
