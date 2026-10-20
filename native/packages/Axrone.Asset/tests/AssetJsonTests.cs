namespace Axrone.Asset.Tests;

public sealed class AssetJsonValueTests
{
    [Fact]
    public void Primitives_Format_Canonically()
    {
        AssetJsonValue.Null.ToString().Should().Be("null");
        ((AssetJsonValue)true).ToString().Should().Be("true");
        ((AssetJsonValue)1.5).ToString().Should().Be("1.5");
        ((AssetJsonValue)"a\"b").ToString().Should().Be("\"a\\\"b\"");
        ((AssetJsonValue)2L).ToString().Should().Be("2");
    }

    [Fact]
    public void Containers_Sort_Keys_And_Compare()
    {
        var left = AssetJsonValue.FromObject(new Dictionary<string, AssetJsonValue>
        {
            ["b"] = 1.0,
            ["a"] = AssetJsonValue.FromArray([true, AssetJsonValue.Null]),
        }.ToImmutableDictionary());
        var right = AssetJsonValue.FromObject(new Dictionary<string, AssetJsonValue>
        {
            ["a"] = AssetJsonValue.FromArray([true, AssetJsonValue.Null]),
            ["b"] = 1.0,
        }.ToImmutableDictionary());

        left.ToString().Should().Be("{\"a\":[true,null],\"b\":1}");
        left.Should().Be(right);
        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    [Fact]
    public void Fingerprint_Ignores_Property_Order()
    {
        AssetFingerprint first = DeterministicFingerprinter.Compute<string, Utf8StringCodecPolicy>(
            "text",
            new AssetKey("k"),
            in "v",
            new AssetMetadata
            {
                Properties = new Dictionary<string, AssetJsonValue>
                {
                    ["b"] = 2.0,
                    ["a"] = "x",
                },
            });
        AssetFingerprint second = DeterministicFingerprinter.Compute<string, Utf8StringCodecPolicy>(
            "text",
            new AssetKey("k"),
            in "v",
            new AssetMetadata
            {
                Properties = new Dictionary<string, AssetJsonValue>
                {
                    ["a"] = "x",
                    ["b"] = 2.0,
                },
            });
        first.Should().Be(second);
    }

    [Fact]
    public void Upsert_Normalizes_Metadata()
    {
        using var db = new TestDatabase();
        AssetRecord<string> record = db.Inner.Upsert(new AssetWriteInput<string>(
            "text",
            null,
            new AssetKey("norm"),
            "norm",
            "v",
            Metadata: new AssetMetadata
            {
                MimeType = "  text/plain  ",
                Uri = new AssetUri("file://x\\y"),
                Tags = ["b", "a ", "b", ""],
            }));

        record.Metadata.MimeType.Should().Be("text/plain");
        record.Metadata.Uri.Should().Be(new AssetUri("file://x/y"));
        record.Metadata.Tags.Should().Equal("a", "b");
    }
}
