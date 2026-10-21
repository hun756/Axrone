namespace Axrone.Asset.Tests;

internal sealed class TestDatabase : IDisposable
{
    public AssetDatabase<TestSchema, DefaultDataCodecPolicy<object>, DefaultLifecyclePolicy<object>> Inner { get; } = new();

    public void Dispose() => Inner.Dispose();
}

internal struct TestSchema;

public sealed class AssetDatabaseTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private AssetDatabase<TestSchema, DefaultDataCodecPolicy<object>, DefaultLifecyclePolicy<object>> Db => _db.Inner;

    private static AssetWriteInput<string> TextInput(string key, string text, string kind = "text") => new(
        Kind: kind,
        Id: null,
        Key: new AssetKey(key),
        Name: key,
        Data: text);

    [Fact]
    public void Upsert_Get_RoundTrip()
    {
        AssetRecord<string> record = Db.Upsert(TextInput("docs/readme", "hello"));
        record.Id.Should().NotBe(default(AssetId));
        record.Revision.Should().Be(AssetRevision.Initial);
        record.Data.Should().Be("hello");

        AssetRecord<string>? fetched = Db.Get<string>(AssetSelector.FromKey(new AssetKey("docs/readme")));
        fetched.Should().NotBeNull();
        fetched!.Value.Should().Be(record);
        Db.Has(AssetSelector.FromId(record.Id)).Should().BeTrue();
        Db.Has(AssetSelector.FromKey(new AssetKey("missing"))).Should().BeFalse();
    }

    [Fact]
    public void Upsert_Bumps_Revision_And_Keeps_History()
    {
        AssetRecord<string> first = Db.Upsert(TextInput("docs/readme", "v1"));
        AssetRecord<string> second = Db.Upsert(new AssetWriteInput<string>(
            Kind: "text",
            Id: first.Id,
            Key: first.Key,
            Name: "readme",
            Data: "v2"));

        second.Revision.Should().Be(new AssetRevision(2L));
        second.Data.Should().Be("v2");

        AssetRecord<string>? current = Db.Get<string>(AssetSelector.FromId(first.Id));
        current.Should().NotBeNull();
        current!.Value.Revision.Should().Be(new AssetRevision(2L));
    }

    [Fact]
    public void Get_By_Versioned_Reference_Returns_Old_Revision()
    {
        AssetRecord<string> first = Db.Upsert(TextInput("docs/readme", "v1"));
        Db.Upsert(new AssetWriteInput<string>("text", first.Id, first.Key, "readme", "v2"));

        var selector = AssetSelector.FromVersioned(first.Id, new AssetRevision(1L));
        AssetRecord<string>? old = Db.Get<string>(selector);
        old.Should().NotBeNull();
        old!.Value.Data.Should().Be("v1");

        var missing = AssetSelector.FromVersioned(first.Id, new AssetRevision(99L));
        Db.Get<string>(missing).Should().BeNull();
    }

    [Fact]
    public void Duplicate_Key_With_Different_Id_Throws()
    {
        Db.Upsert(TextInput("docs/readme", "v1"));

        Action clash = () => Db.Upsert(new AssetWriteInput<string>(
            "text", AssetId.CreateNew(), new AssetKey("docs/readme"), "other", "v2"));
        clash.Should().Throw<AssetConflictError>().Where(e => e.Code == "key-bound");
    }

    [Fact]
    public void Kind_Mismatch_On_Reupsert_Throws()
    {
        AssetRecord<string> first = Db.Upsert(TextInput("docs/readme", "v1"));

        Action mismatch = () => Db.Upsert(new AssetWriteInput<string>(
            "binary", first.Id, first.Key, "readme", "v2"));
        mismatch.Should().Throw<AssetConflictError>().Where(e => e.Code == "kind-mismatch");
    }

    [Fact]
    public void Missing_Dependency_Throws()
    {
        Action missing = () => Db.Upsert(new AssetWriteInput<string>(
            "text", null, new AssetKey("docs/child"), "child", "x",
            Dependencies: new AssetDependencyInput[] { new AssetId(999UL) }));
        missing.Should().Throw<AssetDependencyError>();
    }

    [Fact]
    public void Delete_Cascades_Dependents_In_Order()
    {
        AssetRecord<string> root = Db.Upsert(TextInput("root", "r"));
        AssetRecord<string> child = Db.Upsert(new AssetWriteInput<string>(
            "text", null, new AssetKey("child"), "child", "c",
            Dependencies: new AssetDependencyInput[] { root.Id }));

        using var entered = new ManualResetEventSlim(false);
        using var sub = Db.Subscribe(evt =>
        {
            if (evt.Type == AssetEventType.Delete && evt.Id == root.Id)
            {
                entered.Set();
            }
        });

        Action guarded = () => Db.Delete(AssetSelector.FromId(root.Id), cascade: false);
        guarded.Should().Throw<AssetConflictError>().Where(e => e.Code == "has-dependents");
        Db.Has(AssetSelector.FromId(root.Id)).Should().BeTrue();

        Db.Delete(AssetSelector.FromId(root.Id), cascade: true).Should().BeTrue();
        Db.Has(AssetSelector.FromId(root.Id)).Should().BeFalse();
        Db.Has(AssetSelector.FromId(child.Id)).Should().BeFalse();
    }

    [Fact]
    public void Source_Bindings_RoundTrip()
    {
        AssetRecord<string> record = Db.Upsert(TextInput("audio/music", "data"));
        var identity = new AssetSourceIdentity("soundbank://music");

        Db.BindSourceIdentity(identity, AssetSelector.FromId(record.Id));
        AssetRecord<object>? resolved = Db.ResolveSourceIdentity(identity);
        resolved.Should().NotBeNull();

        Db.UnbindSourceIdentity(identity).Should().BeTrue();
        Db.ResolveSourceIdentity(identity).Should().BeNull();
    }

    [Fact]
    public void Snapshot_Hydrate_RoundTrip()
    {
        AssetRecord<string> record = Db.Upsert(TextInput("docs/readme", "hello"));
        Db.Upsert(new AssetWriteInput<string>("text", record.Id, record.Key, "readme", "v2"));

        AssetDatabaseSnapshot snapshot = Db.Snapshot();
        snapshot.Version.Should().Be(4);
        snapshot.Locale.Should().Be("invariant");
        snapshot.Assets.Should().HaveCount(1);
        snapshot.Assets[0].Revision.Should().Be(2L);

        using var other = new TestDatabase();
        other.Inner.Hydrate(snapshot);

        AssetRecord<string>? restored = other.Inner.Get<string>(AssetSelector.FromKey(new AssetKey("docs/readme")));
        restored.Should().NotBeNull();
        restored!.Value.Data.Should().Be("v2");
        restored.Value.Revision.Should().Be(new AssetRevision(2L));
    }

    [Fact]
    public async Task Import_Bytes_Through_Registered_Importer()
    {
        using var done = new ManualResetEventSlim(false);
        using var sub = Db.Subscribe(evt =>
        {
            if (evt.Type == AssetEventType.Upsert)
            {
                done.Set();
            }
        });

        byte[] payload = [1, 2, 3, 4];
        AssetImportReceipt<object> receipt = await Db.ImportAsync<object>(
            new AssetBytesSource(payload),
            new AssetKey("bin/blob"));

        receipt.ImporterId.Value.Should().Be("axrone.importer.bytes");
        receipt.Primary.Data.Should().BeEquivalentTo(payload);
        done.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
    }

    [Fact]
    public void Require_Throws_For_Missing()
    {
        Action missing = () => Db.Require<string>(AssetSelector.FromId(new AssetId(1234UL)));
        missing.Should().Throw<AssetReferenceError>();
    }
}
