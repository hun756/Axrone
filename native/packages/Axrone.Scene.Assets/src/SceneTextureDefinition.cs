namespace Axrone.Scene.Assets;

/// <summary>Texture storage formats understood by the scene asset layer.</summary>
public enum SceneTextureFormat : byte
{
    /// <summary>8-bit RGBA, linear.</summary>
    Rgba8 = 0,

    /// <summary>8-bit RGBA, sRGB.</summary>
    Srgba8 = 1,

    /// <summary>16-bit floating-point RGBA.</summary>
    Rgba16Float = 2,

    /// <summary>32-bit floating-point RGBA.</summary>
    Rgba32Float = 3,

    /// <summary>8-bit single channel.</summary>
    R8 = 4,

    /// <summary>8-bit two channel.</summary>
    Rg8 = 5
}

/// <summary>Where a texture definition obtains its pixel data.</summary>
public abstract class SceneTextureSource
{
    private protected SceneTextureSource()
    {
    }
}

/// <summary>Raw, already encoded pixel bytes.</summary>
public sealed class SceneTextureData : SceneTextureSource
{
    /// <summary>Creates a data source.</summary>
    public SceneTextureData(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                "Texture data sources require at least one byte.");
        }

        Data = data;
    }

    /// <summary>Encoded pixel bytes.</summary>
    public ReadOnlyMemory<byte> Data { get; }
}

/// <summary>Texture loaded asynchronously from a URI.</summary>
public sealed class SceneTextureUrl : SceneTextureSource
{
    /// <summary>Creates a URL source from a relative or absolute URI.</summary>
    public SceneTextureUrl(string url)
        : this(ParseUrl(url))
    {
    }

    /// <summary>Creates a URL source.</summary>
    public SceneTextureUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        Url = url;
    }

    /// <summary>Source URI.</summary>
    public Uri Url { get; }

    private static Uri ParseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out Uri? parsed))
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                "Texture URL sources require a valid URI.");
        }

        return parsed;
    }
}

/// <summary>Solid-color texture generated without external data.</summary>
public sealed class SceneTextureColor : SceneTextureSource
{
    /// <summary>Creates a solid-color source.</summary>
    public SceneTextureColor(Vec4 color, int width = 1, int height = 1)
    {
        if (width <= 0 || height <= 0)
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                "Solid-color textures require positive dimensions.");
        }

        Color = color;
        Width = width;
        Height = height;
    }

    /// <summary>Fill color.</summary>
    public Vec4 Color { get; }

    /// <summary>Texture width.</summary>
    public int Width { get; }

    /// <summary>Texture height.</summary>
    public int Height { get; }
}

/// <summary>Immutable texture definition.</summary>
public sealed class SceneTextureDefinition : ISceneAssetDefinition<TextureAssetTag>
{
    /// <summary>Creates a texture definition.</summary>
    public SceneTextureDefinition(
        SceneAssetId<TextureAssetTag> id,
        SceneTextureSource source,
        SceneTextureFormat format = SceneTextureFormat.Rgba8,
        int width = 1,
        int height = 1,
        SceneAssetId<SamplerAssetTag>? samplerId = null,
        bool generateMipmaps = true)
    {
        if (!id.IsValid)
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                "Texture definitions require a non-empty id.");
        }

        ArgumentNullException.ThrowIfNull(source);
        if (width <= 0 || height <= 0)
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                $"Texture '{id}' requires positive dimensions.");
        }

        Id = id;
        Source = source;
        Format = format;
        Width = width;
        Height = height;
        SamplerId = samplerId;
        GenerateMipmaps = generateMipmaps;
    }

    /// <inheritdoc/>
    public SceneAssetId<TextureAssetTag> Id { get; }

    /// <summary>Pixel source.</summary>
    public SceneTextureSource Source { get; }

    /// <summary>Pixel storage format.</summary>
    public SceneTextureFormat Format { get; }

    /// <summary>Texture width.</summary>
    public int Width { get; }

    /// <summary>Texture height.</summary>
    public int Height { get; }

    /// <summary>Optional sampler binding.</summary>
    public SceneAssetId<SamplerAssetTag>? SamplerId { get; }

    /// <summary>Whether the backend should generate mip levels.</summary>
    public bool GenerateMipmaps { get; }
}
