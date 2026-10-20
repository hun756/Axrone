namespace Axrone.Asset;

/// <summary>
/// Asset metadata: source location, mime, locale, tags and string
/// properties. Properties stay a string map (not JSON values) so the
/// canonical form needs no reflection and trims cleanly.
/// </summary>
public sealed record AssetMetadata
{
    /// <summary>Empty metadata.</summary>
    public static readonly AssetMetadata Empty = new();

    /// <summary>Source URI.</summary>
    public AssetUri? Uri { get; init; }

    /// <summary>MIME type.</summary>
    public string? MimeType { get; init; }

    /// <summary>Locale tag.</summary>
    public AssetLocale? Locale { get; init; }

    /// <summary>Search tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Custom string properties.</summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } =
        System.Collections.Frozen.FrozenDictionary<string, string>.Empty;
}
