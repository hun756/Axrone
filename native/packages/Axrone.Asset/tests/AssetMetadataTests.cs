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
            Properties = new Dictionary<string, AssetJsonValue>
            {
                ["z"] = 1.0,
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
            Properties = new Dictionary<string, AssetJsonValue>
            {
                ["a"] = "qu\"ote",
                ["z"] = 1.0,
            },
        });

        stable.Should().Be(reordered);
        stable.Should().Contain("\\\"");
        stable.Should().Contain("\"a\":\"qu\\\"ote\"");
        stable.Should().Contain("\"z\":1");
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
        ByteArrayCodecPolicy.Format.Should().Be("binary");

        byte[] payload = [1, 2, 3, 250];
        Span<byte> buffer = stackalloc byte[ByteArrayCodecPolicy.MaxSerializedBytes(payload)];
        ByteArrayCodecPolicy.TrySerialize(in payload, buffer, out int written).Should().BeTrue();
        ByteArrayCodecPolicy.TryDeserialize(buffer[..written], out byte[] restored).Should().BeTrue();
        restored.Should().BeEquivalentTo(payload);
    }

    [Fact]
    public void Text_Codec_RoundTrips()
    {
        Utf8StringCodecPolicy.Format.Should().Be("text");

        string payload = "hello";
        Span<byte> buffer = stackalloc byte[Utf8StringCodecPolicy.MaxSerializedBytes(payload)];
        Utf8StringCodecPolicy.TrySerialize(in payload, buffer, out int written).Should().BeTrue();
        Utf8StringCodecPolicy.TryDeserialize(buffer[..written], out string restored).Should().BeTrue();
        restored.Should().Be("hello");
    }

    [Fact]
    public void Unmanaged_Codec_RoundTrips()
    {
        UnmanagedBinaryCodecPolicy<int>.Format.Should().Be("unmanaged");

        int payload = 42;
        Span<byte> buffer = stackalloc byte[UnmanagedBinaryCodecPolicy<int>.MaxSerializedBytes(payload)];
        UnmanagedBinaryCodecPolicy<int>.TrySerialize(in payload, buffer, out int written).Should().BeTrue();
        UnmanagedBinaryCodecPolicy<int>.TryDeserialize(buffer[..written], out int restored).Should().BeTrue();
        restored.Should().Be(42);
    }
}
