namespace Axrone.Asset.Tests;

public sealed class AssetDependencyTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static AssetWriteInput<string> TextInput(string key, string data) =>
        new("text", null, new AssetKey(key), key, data);

    [Fact]
    public void Dependencies_Resolve_By_Key_Token_Reference_And_Lookup()
    {
        AssetRecord<string> root = _db.Inner.Upsert(TextInput("root", "r"));

        AssetRecord<string> byKey = _db.Inner.Upsert(new AssetWriteInput<string>(
            "text", null, new AssetKey("by-key"), "by-key", "x",
            Dependencies: new AssetDependencyInput[] { new AssetKey("root") }));
        byKey.DependencyIds.Should().ContainSingle().Which.Should().Be(root.Id);

        AssetRecord<string> byToken = _db.Inner.Upsert(new AssetWriteInput<string>(
            "text", null, new AssetKey("by-token"), "by-token", "x",
            Dependencies: new AssetDependencyInput[] { $"asset:text:{root.Id}" }));
        byToken.DependencyIds.Should().ContainSingle().Which.Should().Be(root.Id);

        AssetRecord<string> byRef = _db.Inner.Upsert(new AssetWriteInput<string>(
            "text", null, new AssetKey("by-ref"), "by-ref", "x",
            Dependencies: new AssetDependencyInput[] { AssetDependencyInput.FromReference(new AssetReference<TestSchema>(root.Id)) }));
        byRef.DependencyIds.Should().ContainSingle().Which.Should().Be(root.Id);

        AssetRecord<string> byRecord = _db.Inner.Upsert(new AssetWriteInput<string>(
            "text", null, new AssetKey("by-record"), "by-record", "x",
            Dependencies: new AssetDependencyInput[] { AssetDependencyInput.FromRecord(in root) }));
        byRecord.DependencyIds.Should().ContainSingle().Which.Should().Be(root.Id);

        AssetRecord<string> byLookup = _db.Inner.Upsert(new AssetWriteInput<string>(
            "text", null, new AssetKey("by-lookup"), "by-lookup", "x",
            Dependencies: new AssetDependencyInput[] { new AssetLookupByKey(new AssetKey("root"), "text") }));
        byLookup.DependencyIds.Should().ContainSingle().Which.Should().Be(root.Id);
    }

    [Fact]
    public void Missing_Key_And_Kind_Filter_Throw()
    {
        _db.Inner.Upsert(TextInput("root", "r"));

        Action missingKey = () => _db.Inner.Upsert(new AssetWriteInput<string>(
            "text", null, new AssetKey("child"), "child", "x",
            Dependencies: new AssetDependencyInput[] { new AssetKey("ghost") }));
        missingKey.Should().Throw<AssetDependencyError>();

        Action kindMismatch = () => _db.Inner.Upsert(new AssetWriteInput<string>(
            "text", null, new AssetKey("child"), "child", "x",
            Dependencies: new AssetDependencyInput[] { AssetDependencyInput.FromKey(new AssetKey("root"), "other-kind") }));
        kindMismatch.Should().Throw<AssetDependencyError>();
    }

    [Fact]
    public void Duplicates_Are_Collapsed()
    {
        AssetRecord<string> root = _db.Inner.Upsert(TextInput("root", "r"));
        AssetRecord<string> child = _db.Inner.Upsert(new AssetWriteInput<string>(
            "text", null, new AssetKey("child"), "child", "x",
            Dependencies: new AssetDependencyInput[] { root.Id, new AssetKey("root"), AssetDependencyInput.FromRecord(in root) }));
        child.DependencyIds.Should().ContainSingle().Which.Should().Be(root.Id);
    }

    [Fact]
    public void GetDependencies_And_GetDependents()
    {
        AssetRecord<string> root = _db.Inner.Upsert(TextInput("root", "r"));
        _db.Inner.Upsert(new AssetWriteInput<string>(
            "text", null, new AssetKey("child"), "child", "x",
            Dependencies: new AssetDependencyInput[] { root.Id }));

        _db.Inner.GetDependencies(AssetSelector.FromKey(new AssetKey("child")))
            .Should().ContainSingle().Which.Id.Should().Be(root.Id);
        _db.Inner.GetDependents(AssetSelector.FromId(root.Id))
            .Should().ContainSingle().Which.Key.Should().Be(new AssetKey("child"));
        _db.Inner.GetDependents(AssetSelector.FromKey(new AssetKey("ghost"))).Should().BeEmpty();
    }

    [Fact]
    public void Delete_Unbinds_Source_Identities()
    {
        AssetRecord<string> record = _db.Inner.Upsert(TextInput("audio/music", "data"));
        var identity = new AssetSourceIdentity("soundbank://music");
        _db.Inner.BindSourceIdentity(identity, AssetSelector.FromId(record.Id));
        _db.Inner.ResolveSourceIdentity(identity).Should().NotBeNull();

        _db.Inner.Delete(AssetSelector.FromId(record.Id)).Should().BeTrue();
        _db.Inner.ResolveSourceIdentity(identity).Should().BeNull();
    }
}
