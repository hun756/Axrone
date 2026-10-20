namespace Axrone.Asset;

/// <summary>
/// Data codec contract: stable serialization without reflection so the
/// trimmer and AOT compilers see every conversion.
/// </summary>
public interface IAssetDataCodec
{
    /// <summary>Codec identifier, e.g. "binary" or "text".</summary>
    string Format { get; }

    /// <summary>Serializes data into its canonical stable form.</summary>
    string SerializeStable(object? data);

    /// <summary>Restores data from its stable form.</summary>
    object? Deserialize(string stable);
}

/// <summary>
/// Kind-keyed codec registry. Codecs arrive explicitly; there is no
/// assembly scanning so trimming stays intact.
/// </summary>
public sealed class AssetCodecRegistry
{
    private readonly Dictionary<string, IAssetDataCodec> _codecs = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Creates an empty registry.</summary>
    public AssetCodecRegistry()
    {
    }

    /// <summary>Creates a registry with the built-in binary and text codecs.</summary>
    public static AssetCodecRegistry WithDefaults()
    {
        var registry = new AssetCodecRegistry();
        registry.Register("binary", new BinaryAssetCodec());
        registry.Register("text", new TextAssetCodec());
        return registry;
    }

    /// <summary>Binds a codec to a kind name.</summary>
    public void Register(string kind, IAssetDataCodec codec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(codec);
        lock (_gate)
        {
            _codecs[kind] = codec;
        }
    }

    /// <summary>Looks up the codec for a kind.</summary>
    public bool TryGet(string kind, out IAssetDataCodec? codec)
    {
        lock (_gate)
        {
            return _codecs.TryGetValue(kind, out codec);
        }
    }

    /// <summary>Gets the codec for a kind; throws when unbound.</summary>
    public IAssetDataCodec Get(string kind)
    {
        if (TryGet(kind, out IAssetDataCodec? codec) && codec is not null)
        {
            return codec;
        }

        AssetThrowHelper.ThrowInvalid("asset.invalid-kind", $"No codec registered for kind '{kind}'.");
        throw new InvalidOperationException("Unreachable.");
    }
}

/// <summary>Binary codec: base64 stable form.</summary>
public sealed class BinaryAssetCodec : IAssetDataCodec
{
    /// <inheritdoc/>
    public string Format => "binary";

    /// <inheritdoc/>
    public string SerializeStable(object? data)
    {
        if (data is not byte[] bytes)
        {
            AssetThrowHelper.ThrowInvalid("asset.invalid-kind", "Binary codec needs a byte[] payload.");
        }

        return Convert.ToBase64String((byte[])data!);
    }

    /// <inheritdoc/>
    public object? Deserialize(string stable) => Convert.FromBase64String(stable);
}

/// <summary>Text codec: identity stable form.</summary>
public sealed class TextAssetCodec : IAssetDataCodec
{
    /// <inheritdoc/>
    public string Format => "text";

    /// <inheritdoc/>
    public string SerializeStable(object? data)
    {
        if (data is not string text)
        {
            AssetThrowHelper.ThrowInvalid("asset.invalid-kind", "Text codec needs a string payload.");
        }

        return (string)data!;
    }

    /// <inheritdoc/>
    public object? Deserialize(string stable) => stable;
}
