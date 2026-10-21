namespace Axrone.Asset.Tests;

public sealed class AssetHydrateTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static AssetWriteInput<string> TextInput(string key, string data) =>
        new("text", null, new AssetKey(key), key, data);

    [Fact]
    public void Hydrate_Restores_History_For_Versioned_Reads()
    {
        _db.Inner.Upsert(TextInput("docs/readme", "v1"));
        _db.Inner.Upsert(new AssetWriteInput<string>("text", null, new AssetKey("docs/readme"), "readme", "v2"));

        AssetDatabaseSnapshot snapshot = _db.Inner.Snapshot();
        snapshot.Assets.Should().ContainSingle();
        snapshot.Assets[0].History.Should().ContainSingle();

        using var other = new TestDatabase();
        other.Inner.Hydrate(snapshot, new AssetHydrateOptions(Replace: true));

        AssetRecord<string>? current = other.Inner.Get<string>(AssetSelector.FromKey(new AssetKey("docs/readme")));
        current.Should().NotBeNull();
        current!.Value.Revision.Should().Be(new AssetRevision(2L));

        AssetRecord<string>? first = other.Inner.Get<string>(AssetSelector.FromVersioned(
            current.Value.Id, new AssetRevision(1L)));
        first.Should().NotBeNull();
        first!.Value.Data.Should().Be("v1");
    }

    [Fact]
    public void Hydrate_Merge_Throws_On_Key_Conflict()
    {
        _db.Inner.Upsert(TextInput("docs/readme", "db-version"));

        AssetDatabaseSnapshot snapshot = _db.Inner.Snapshot();
        using var other = new TestDatabase();
        other.Inner.Upsert(TextInput("docs/readme", "other-version"));

        Action hydrate = () => other.Inner.Hydrate(snapshot);
        hydrate.Should().Throw<AssetConflictError>().Where(e => e.Code == "key-bound");
    }

    [Fact]
    public void Hydrate_Rejects_Missing_Dependency()
    {
        _db.Inner.Upsert(TextInput("docs/readme", "v1"));
        AssetDatabaseSnapshot snapshot = _db.Inner.Snapshot();
        AssetSnapshotRecord broken = snapshot.Assets[0] with { DependencyIds = [999UL] };
        var tampered = new AssetDatabaseSnapshot
        {
            Locale = snapshot.Locale,
            CapturedAtEpochMs = snapshot.CapturedAtEpochMs,
            Assets = [broken],
        };

        using var other = new TestDatabase();
        Action hydrate = () => other.Inner.Hydrate(tampered, new AssetHydrateOptions(Replace: true));
        hydrate.Should().Throw<AssetSnapshotError>();
    }

    [Fact]
    public void Hydrate_Rejects_Non_Monotonic_History()
    {
        _db.Inner.Upsert(TextInput("docs/readme", "v1"));
        _db.Inner.Upsert(new AssetWriteInput<string>("text", null, new AssetKey("docs/readme"), "readme", "v2"));
        AssetDatabaseSnapshot snapshot = _db.Inner.Snapshot();

        AssetSnapshotRecord head = snapshot.Assets[0];
        AssetSnapshotRecord tamperedHead = head with
        {
            History = [head.History![0] with { Revision = 5L }],
        };
        var tampered = new AssetDatabaseSnapshot
        {
            Locale = snapshot.Locale,
            CapturedAtEpochMs = snapshot.CapturedAtEpochMs,
            Assets = [tamperedHead],
        };

        using var other = new TestDatabase();
        Action hydrate = () => other.Inner.Hydrate(tampered, new AssetHydrateOptions(Replace: true));
        hydrate.Should().Throw<AssetSnapshotError>();
    }

    [Fact]
    public void Hydrate_Rejects_Dangling_Binding()
    {
        _db.Inner.Upsert(TextInput("docs/readme", "v1"));
        AssetDatabaseSnapshot snapshot = _db.Inner.Snapshot() with
        {
            SourceBindings = [new AssetSnapshotSourceBindingRecord("sound://x", 424242UL, 1L)],
        };

        using var other = new TestDatabase();
        Action hydrate = () => other.Inner.Hydrate(snapshot, new AssetHydrateOptions(Replace: true));
        hydrate.Should().Throw<AssetSnapshotError>();
    }

    [Fact]
    public void Hydrate_Emits_Upsert_Events()
    {
        _db.Inner.Upsert(TextInput("docs/readme", "v1"));
        AssetDatabaseSnapshot snapshot = _db.Inner.Snapshot();

        using var other = new TestDatabase();
        using var done = new ManualResetEventSlim(false);
        using var sub = other.Inner.Subscribe(evt =>
        {
            if (evt.Type == AssetEventType.Upsert)
            {
                done.Set();
            }
        });

        other.Inner.Hydrate(snapshot, new AssetHydrateOptions(Replace: true));
        done.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
    }
}
