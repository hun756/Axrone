namespace Axrone.Asset.Tests;

public sealed class AssetParityTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Fingerprint_Override_Is_Honored()
    {
        var forced = new AssetFingerprint(0xDEADBEEFUL);
        AssetRecord<string> record = _db.Inner.Upsert(new AssetWriteInput<string>(
            "text", null, new AssetKey("forced"), "forced", "v",
            Fingerprint: forced));
        record.Fingerprint.Should().Be(forced);
    }

    [Fact]
    public void List_Filters_By_Kind()
    {
        _db.Inner.Upsert(new AssetWriteInput<string>("text", null, new AssetKey("a"), "a", "x"));
        _db.Inner.Upsert(new AssetWriteInput<string>("binary", null, new AssetKey("b"), "b", "y"));

        _db.Inner.List().Should().HaveCount(2);
        _db.Inner.List("text").Should().ContainSingle().Which.Key.Should().Be(new AssetKey("a"));
        _db.Inner.List("missing").Should().BeEmpty();
    }

    [Fact]
    public void Reference_Tokens_RoundTrip()
    {
        var id = new AssetId(0xABCDEFUL);
        AssetReference<TestSchema> reference = new(id);
        string token = reference.ToString();
        AssetReference<TestSchema>.TryParse(token.AsSpan(), out AssetReference<TestSchema> parsed).Should().BeTrue();
        parsed.Should().Be(reference);
        AssetReference<TestSchema>.TryParse("asset:other:0000000000abcdef".AsSpan(), out _).Should().BeFalse();
        AssetReference<TestSchema>.TryParse("garbage".AsSpan(), out _).Should().BeFalse();

        AssetVersionedReference<TestSchema> versioned = new(id, new AssetRevision(3L));
        string versionedToken = versioned.ToString();
        AssetVersionedReference<TestSchema>.TryParse(versionedToken.AsSpan(), out AssetVersionedReference<TestSchema> parsedVersioned).Should().BeTrue();
        parsedVersioned.Should().Be(versioned);
        AssetVersionedReference<TestSchema>.TryParse(token.AsSpan(), out _).Should().BeFalse();
    }
}
