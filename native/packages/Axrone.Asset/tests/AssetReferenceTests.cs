namespace Axrone.Asset.Tests;

public sealed class AssetReferenceTests
{
    [Fact]
    public void Tokens_RoundTrip()
    {
        var reference = new AssetReference(new AssetKind("clip"), new AssetId("sfx/boom"));
        reference.Token.Should().Be("asset:clip:sfx/boom");
        AssetReference.Parse("asset:clip:sfx/boom").Should().Be(reference);

        var versioned = new AssetVersionedReference(new AssetKind("clip"), new AssetId("sfx/boom"), new AssetRevision(3U));
        versioned.Token.Should().Be("asset:clip:sfx/boom@3");
        AssetVersionedReference.Parse("asset:clip:sfx/boom@3").Should().Be(versioned);
        versioned.Unversioned().Should().Be(reference);
    }

    [Fact]
    public void Malformed_Tokens_Fail_With_Reference_Code()
    {
        string[] bad = ["", "asset:", "asset:clip", "asset:clip:", "asset::x", "asset:clip:x@y@z", "nope:clip:x", "asset:clip:sfx/boom@"];
        foreach (string token in bad)
        {
            AssetReference.TryParse(token, out _).Should().BeFalse($"'{token}'");
            Action parse = () => AssetReference.Parse(token);
            parse.Should().Throw<AssetReferenceException>().Where(e => e.Code == "asset.reference.invalid");
            AssetVersionedReference.TryParse(token, out _).Should().BeFalse($"'{token}'");
        }

        AssetVersionedReference.TryParse("asset:clip:sfx/boom@0", out _).Should().BeFalse();
        AssetVersionedReference.TryParse("asset:clip:sfx/boom@abc", out _).Should().BeFalse();
    }
}

public sealed class AssetFingerprintTests
{
    [Fact]
    public void Fingerprint_Is_Stable_Prefixed_And_Sensitive()
    {
        var kind = new AssetKind("clip");
        var key = new AssetKey("sfx/boom");

        AssetFingerprint first = AssetFingerprintComputer.Compute(kind, key, "{\"v\":1}", "{}");
        AssetFingerprint second = AssetFingerprintComputer.Compute(kind, key, "{\"v\":1}", "{}");
        first.Should().Be(second);
        first.Value.Should().StartWith("fp:");
        first.Value.Should().HaveLength(3 + 8);

        AssetFingerprint changed = AssetFingerprintComputer.Compute(kind, key, "{\"v\":2}", "{}");
        changed.Should().NotBe(first);

        AssetFingerprint otherKey = AssetFingerprintComputer.Compute(kind, new AssetKey("sfx/bang"), "{\"v\":1}", "{}");
        otherKey.Should().NotBe(first);
    }
}
