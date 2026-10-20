namespace Axrone.Asset.Tests;

public sealed class AssetMetadataTests
{
    [Fact]
    public void Stable_Form_Sorts_Properties_And_Escapes()
    {
        var metadata = new AssetMetadata
        {
            Uri = new AssetUri("file://x/y"),
            MimeType = "text/plain",
            Locale = new AssetLocale("en"),
            Tags = ["b", "a"],
            Properties = new Dictionary<string, string>
            {
                ["z"] = "1",
                ["a"] = "qu\"ote",
            },
        };

        string stable = StableJson.StableMetadataString(metadata);
        string reordered = StableJson.StableMetadataString(new AssetMetadata
        {
            Uri = new AssetUri("file://x/y"),
            MimeType = "text/plain",
            Locale = new AssetLocale("en"),
            Tags = ["b", "a"],
            Properties = new Dictionary<string, string>
            {
                ["a"] = "qu\"ote",
                ["z"] = "1",
            },
        });

        stable.Should().Be(reordered);
        stable.Should().Contain("\\\"");
        stable.Should().Contain("\"a\":\"qu\\\"ote\"");
    }

    [Fact]
    public void Empty_Metadata_Is_Stable()
    {
        string first = StableJson.StableMetadataString(AssetMetadata.Empty);
        string second = StableJson.StableMetadataString(new AssetMetadata());
        first.Should().Be(second);
    }
}

public sealed class AssetCodecTests
{
    [Fact]
    public void Binary_Codec_RoundTrips()
    {
        var codec = new BinaryAssetCodec();
        codec.Format.Should().Be("binary");

        byte[] payload = [1, 2, 3, 250];
        string stable = codec.SerializeStable(payload);
        codec.Deserialize(stable).Should().BeEquivalentTo(payload);

        Action wrong = () => codec.SerializeStable("nope");
        wrong.Should().Throw<AssetConfigurationException>();
    }

    [Fact]
    public void Text_Codec_RoundTrips()
    {
        var codec = new TextAssetCodec();
        codec.Format.Should().Be("text");

        codec.SerializeStable("hello").Should().Be("hello");
        codec.Deserialize("hello").Should().Be("hello");

        Action wrong = () => codec.SerializeStable(42);
        wrong.Should().Throw<AssetConfigurationException>();
    }

    [Fact]
    public void Registry_Resolves_And_Rejects_Unknown()
    {
        var registry = AssetCodecRegistry.WithDefaults();
        registry.Get("binary").Format.Should().Be("binary");
        registry.TryGet("nope", out _).Should().BeFalse();

        Action missing = () => registry.Get("nope");
        missing.Should().Throw<AssetConfigurationException>();
    }
}
