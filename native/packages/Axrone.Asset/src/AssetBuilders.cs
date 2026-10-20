namespace Axrone.Asset;

/// <summary>Typestate markers for the asset envelope builder.</summary>
public static class AssetStage
{
    /// <summary>Kind, key and data bound.</summary>
    public readonly struct Initial : IStageMarker
    {
    }

    /// <summary>Metadata or aliases bound.</summary>
    public readonly struct Configured : IStageMarker
    {
    }

    /// <summary>Validated and buildable.</summary>
    public readonly struct Validated : IStageMarker
    {
    }
}

/// <summary>Typestate phase marker.</summary>
public interface IStageMarker
{
}

/// <summary>Compile-time phased builder for asset write inputs.</summary>
public readonly struct AssetEnvelopeBuilder<TData, TStage>
    where TStage : struct, IStageMarker
{
    internal readonly string _kind;
    internal readonly AssetKey _key;
    internal readonly TData _data;
    internal readonly string _name;
    internal readonly AssetId? _id;
    internal readonly AssetKey[]? _aliases;
    internal readonly AssetMetadata? _metadata;
    internal readonly AssetDependencyInput[]? _dependencies;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal AssetEnvelopeBuilder(
        string kind,
        AssetKey key,
        TData data,
        string name,
        AssetId? id = null,
        AssetKey[]? aliases = null,
        AssetMetadata? metadata = null,
        AssetDependencyInput[]? dependencies = null)
    {
        _kind = kind;
        _key = key;
        _data = data;
        _name = name;
        _id = id;
        _aliases = aliases;
        _metadata = metadata;
        _dependencies = dependencies;
    }

    /// <summary>Starts an unconfigured builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetEnvelopeBuilder<TData, AssetStage.Initial> Start(string kind, AssetKey key, TData data) =>
        new(kind, key, data, key.Value ?? "asset");

    /// <summary>Binds metadata.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetEnvelopeBuilder<TData, AssetStage.Configured> WithMetadata(AssetMetadata metadata) =>
        new(_kind, _key, _data, _name, _id, _aliases, metadata, _dependencies);

    /// <summary>Binds aliases.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetEnvelopeBuilder<TData, AssetStage.Configured> WithAliases(params AssetKey[] aliases) =>
        new(_kind, _key, _data, _name, _id, aliases, _metadata, _dependencies);

    /// <summary>Binds dependencies.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetEnvelopeBuilder<TData, AssetStage.Validated> WithDependencies(params AssetDependencyInput[] dependencies) =>
        new(_kind, _key, _data, _name, _id, _aliases, _metadata, dependencies);

    /// <summary>Validates the envelope.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetEnvelopeBuilder<TData, AssetStage.Validated> Validate()
    {
        if (string.IsNullOrWhiteSpace(_kind))
        {
            ThrowHelper.ThrowConfigurationError("Asset Kind is required.");
        }

        return new AssetEnvelopeBuilder<TData, AssetStage.Validated>(_kind, _key, _data, _name, _id, _aliases, _metadata, _dependencies);
    }
}

/// <summary>Phased builder extensions.</summary>
public static class AssetEnvelopeBuilderExtensions
{
    /// <summary>Builds a validated envelope into a write input.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetWriteInput<TData> Build<TData>(in this AssetEnvelopeBuilder<TData, AssetStage.Validated> builder) => new(
        builder._kind,
        builder._id,
        builder._key,
        builder._name,
        builder._data,
        builder._aliases,
        builder._metadata,
        builder._dependencies);
}
