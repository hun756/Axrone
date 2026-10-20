namespace Axrone.Asset.Tests;

public sealed class AssetFingerprintTests
{
    [Fact]
    public void Fingerprint_Is_Stable_And_Prefixed()
    {
        var kind = "clip";
        var key = new AssetKey("sfx/boom");
        var meta = AssetMetadata.Empty;

        AssetFingerprint first = DeterministicFingerprinter.Compute<string, Utf8StringCodecPolicy>(kind, key, "{\"v\":1}", meta);
        AssetFingerprint second = DeterministicFingerprinter.Compute<string, Utf8StringCodecPolicy>(kind, key, "{\"v\":1}", meta);
        first.Should().Be(second);
        first.ToString().Should().StartWith("fp:");
        first.ToString().Should().HaveLength(3 + 16);

        AssetFingerprint changed = DeterministicFingerprinter.Compute<string, Utf8StringCodecPolicy>(kind, key, "{\"v\":2}", meta);
        changed.Should().NotBe(first);

        AssetFingerprint.FromHex(first.ToString().AsSpan()).Should().Be(first);
    }
}
