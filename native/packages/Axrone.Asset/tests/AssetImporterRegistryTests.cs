namespace Axrone.Asset.Tests;

public sealed class AssetImporterRegistryTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private sealed class JsonSniffer : IAssetImporter
    {
        public AssetImporterId Id => new("test.sniffer");
        public int Priority => 5;
        public IReadOnlyList<string> Extensions => [".effect.json"];
        public bool CanImport(AssetImportContext context) => true;
        public ValueTask<AssetImportResult> ImportAsync(AssetImportContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AssetImportResult
            {
                Primary = new AssetWriteInput<object>("test", null, context.BaseKey, "t", new object()),
            });
    }

    [Fact]
    public void Register_Unregister_List_RoundTrip()
    {
        int builtIn = _db.Inner.ListImporters().Count;
        builtIn.Should().Be(2);

        _db.Inner.RegisterImporter(new JsonSniffer());
        _db.Inner.ListImporters().Should().HaveCount(builtIn + 1);
        _db.Inner.ListImporters()[0].Id.Value.Should().Be("test.sniffer");

        _db.Inner.UnregisterImporter(new AssetImporterId("test.sniffer")).Should().BeTrue();
        _db.Inner.UnregisterImporter(new AssetImporterId("test.sniffer")).Should().BeFalse();
        _db.Inner.ListImporters().Should().HaveCount(builtIn);
    }

    [Fact]
    public void Extension_Prefilter_Routes_Before_CanImport()
    {
        _db.Inner.RegisterImporter(new JsonSniffer());

        AssetImportContext context = new(
            new AssetTextSource("{}"),
            new AssetKey("shaders/a"),
            new AssetUri("shaders/a.effect.json"));
        IAssetImporter? selected = null;
        foreach (IAssetImporter importer in _db.Inner.ListImporters())
        {
            if (AssetImporterRegistry.Matches(importer, context) && importer.CanImport(context))
            {
                selected = importer;
                break;
            }
        }

        selected.Should().NotBeNull();
        selected!.Id.Value.Should().Be("test.sniffer");
    }

    [Fact]
    public void Stage_Registry_Orders_By_Priority_And_Phase()
    {
        var first = new ProbeStage("s.first", 10, [AssetImportStagePhase.BeforeImport]);
        var second = new ProbeStage("s.second", 1, []);
        _db.Inner.RegisterStage(first);
        _db.Inner.RegisterStage(second);

        _db.Inner.ListStages().Should().HaveCount(2);
        _db.Inner.ListStages()[0].Id.Value.Should().Be("s.first");

        _db.Inner.UnregisterStage(new AssetImporterId("s.second")).Should().BeTrue();
        _db.Inner.ListStages().Should().ContainSingle();
    }

    private sealed class ProbeStage(string id, int priority, IReadOnlyList<AssetImportStagePhase> phases) : IAssetImportStage
    {
        public AssetImporterId Id { get; } = new(id);
        public int Priority { get; } = priority;
        public IReadOnlyList<AssetImportStagePhase> Phases { get; } = phases;
        public ValueTask<AssetStageOutput?> RunAsync(AssetImportContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AssetStageOutput?>(null);
    }
}
