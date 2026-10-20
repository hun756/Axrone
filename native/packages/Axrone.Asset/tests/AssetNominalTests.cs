namespace Axrone.Asset.Tests;

public sealed class AssetNominalTests
{
    [Fact]
    public void Brands_Reject_Blank_And_Padded()
    {
        FluentActions.Invoking(() => new AssetId(" ")).Should().Throw<AssetConfigurationException>()
            .Where(e => e.Code == "asset.invalid-id");
        FluentActions.Invoking(() => new AssetKey(" key")).Should().Throw<AssetConfigurationException>()
            .Where(e => e.Code == "asset.invalid-key");
        FluentActions.Invoking(() => new AssetKind("")).Should().Throw<AssetConfigurationException>()
            .Where(e => e.Code == "asset.invalid-kind");
        FluentActions.Invoking(() => new AssetImporterId("")).Should().Throw<AssetConfigurationException>()
            .Where(e => e.Code == "asset.invalid-importer");
        FluentActions.Invoking(() => new AssetSourceIdentity(" ")).Should().Throw<AssetConfigurationException>()
            .Where(e => e.Code == "asset.invalid-source-identity");
        FluentActions.Invoking(() => new AssetRevision(0U)).Should().Throw<AssetConfigurationException>()
            .Where(e => e.Code == "asset.invalid-revision");
    }

    [Fact]
    public void Brands_Accept_Canonical_Values()
    {
        new AssetId("audio:music:1").Value.Should().Be("audio:music:1");
        new AssetKey("sfx/explosion").Value.Should().Be("sfx/explosion");
        new AssetUri("file://clips/boom.wav").Value.Should().Be("file://clips/boom.wav");
        AssetLocale.Invariant.Value.Should().Be("iv");
    }

    [Fact]
    public void Revision_Advances_And_Orders()
    {
        AssetRevision.Initial.Value.Should().Be(1U);
        (AssetRevision.Initial < AssetRevision.Initial.Next()).Should().BeTrue();
        AssetRevision.FromSpan("7".AsSpan()).Value.Should().Be(7U);
    }

    [Fact]
    public void Codes_Match_Web_Contract()
    {
        new AssetDisposedException("gone").Code.Should().Be("asset.disposed");
        new AssetImporterNotFoundException("none").Code.Should().Be("asset.importer.not-found");
        new AssetImportException("boom", new AssetImporterId("imp"), 2).Code.Should().Be("asset.import.failed");
        new AssetReferenceException("bad").Code.Should().Be("asset.reference.invalid");
        new AssetSnapshotException("bad").Code.Should().Be("asset.snapshot.invalid");
        new AssetDependencyException("missing dep", "dep").Code.Should().Be("asset.dependency.missing");
    }
}
