namespace Axrone.Asset.Tests;

public sealed class AssetCoreParityTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Keys_Canonicalize_Separators()
    {
        var backslash = new AssetKey("docs\\readme");
        var slash = new AssetKey("docs/readme");
        backslash.Should().Be(slash);
        backslash.Value.Should().Be("docs/readme");
        AssetKey.Canonicalize("  a\\b  ").Should().Be("a/b");
    }

    [Fact]
    public void Clear_Empties_And_Emits_Deletes()
    {
        _db.Inner.Upsert(new AssetWriteInput<string>("text", null, new AssetKey("a"), "a", "x"));
        _db.Inner.Upsert(new AssetWriteInput<string>("text", null, new AssetKey("b"), "b", "y"));

        int deletes = 0;
        using var sub = _db.Inner.Subscribe(evt =>
        {
            if (evt.Type == AssetEventType.Delete)
            {
                Interlocked.Increment(ref deletes);
            }
        });

        _db.Inner.Clear();
        _db.Inner.Size.Should().Be(0);
        SpinWait.SpinUntil(() => Volatile.Read(ref deletes) == 2, TimeSpan.FromSeconds(5));
        Volatile.Read(ref deletes).Should().Be(2);
    }

    [Fact]
    public async Task Json_Source_Matches_Json_Importer()
    {
        var importer = new JsonProbeImporter();
        _db.Inner.RegisterImporter(importer);

        AssetImportReceipt<object> receipt = await _db.Inner.ImportAsync<object>(
            new AssetJsonSource(new { hello = "world" }),
            new AssetKey("data/blob"));
        receipt.Primary.Kind.Should().Be("json-probe");
    }

    private sealed class JsonProbeImporter : IAssetImporter
    {
        public AssetImporterId Id => new("test.json-probe");
        public int Priority => 5;
        public IReadOnlyList<string> SourceKinds => ["json"];
        public bool CanImport(AssetImportContext context) => true;
        public ValueTask<AssetImportResult> ImportAsync(AssetImportContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AssetImportResult
            {
                Primary = new AssetWriteInput<object>("json-probe", null, context.BaseKey, "t", new object()),
            });
    }
}
