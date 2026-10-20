namespace Axrone.Asset.Tests;

public sealed class AssetImportPipelineTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private sealed class FlakyImporter : IAssetImporter
    {
        public int Calls;
        public AssetImporterId Id => new("test.flaky");
        public int Priority => 10;
        public IReadOnlyList<string> SourceKinds => ["text"];
        public bool CanImport(AssetImportContext context) => true;
        public ValueTask<AssetImportResult> ImportAsync(AssetImportContext context, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Calls < 3)
            {
                throw new InvalidOperationException("transient");
            }

            return ValueTask.FromResult(new AssetImportResult
            {
                Primary = new AssetWriteInput<object>("text", null, context.BaseKey, "t", new object()),
            });
        }
    }

    private sealed class TagStage : IAssetImportStage
    {
        public AssetImporterId Id { get; } = new("test.tag");
        public int Priority => 5;
        public IReadOnlyList<AssetImportStagePhase> Phases { get; } = [AssetImportStagePhase.AfterImport];
        public ValueTask<AssetStageOutput?> RunAsync(AssetImportContext context, CancellationToken cancellationToken = default)
        {
            context.AddDiagnostic(new AssetImportDiagnostic("info", "stage.ran", "tag"));
            return ValueTask.FromResult<AssetStageOutput?>(new AssetStageOutput());
        }
    }

    private sealed class SourceSwapStage : IAssetImportStage
    {
        public AssetImporterId Id { get; } = new("test.swap");
        public IReadOnlyList<AssetImportStagePhase> Phases { get; } = [AssetImportStagePhase.Source];
        public ValueTask<AssetStageOutput?> RunAsync(AssetImportContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AssetStageOutput?>(new AssetStageOutput(Source: new AssetTextSource("swapped")));
    }

    [Fact]
    public async Task Retry_Exhaustion_Wraps_AssetImportError()
    {
        _db.Inner.RegisterImporter(new FlakyImporter());

        AssetImportReceipt<object> receipt = await _db.Inner.ImportAsync<object>(
            new AssetTextSource("x"),
            new AssetKey("flaky"),
            options: new AssetImportOptions(new AssetRetryPolicy(Attempts: 3)));
        receipt.Primary.Should().NotBeNull();

        using var retryDb = new TestDatabase();
        retryDb.Inner.RegisterImporter(new FlakyImporter());
        Func<Task> failing = () => retryDb.Inner.ImportAsync<object>(
            new AssetTextSource("x"),
            new AssetKey("flaky2"),
            options: new AssetImportOptions(new AssetRetryPolicy(Attempts: 2))).AsTask();
        (await failing.Should().ThrowAsync<AssetImportError>())
            .Where(e => e.ImporterId == "test.flaky" && e.Attempt == 2);
    }

    [Fact]
    public async Task Stages_Run_In_Phases_And_Append_Diagnostics()
    {
        _db.Inner.RegisterStage(new TagStage());
        _db.Inner.RegisterStage(new SourceSwapStage());

        AssetImportReceipt<object> receipt = await _db.Inner.ImportAsync<object>(
            new AssetTextSource("original"),
            new AssetKey("staged"));
        receipt.Primary.Data.Should().Be("swapped");
        receipt.Diagnostics.Should().Contain(d => d.Code == "stage.ran");
    }

    [Fact]
    public async Task ImportMany_Preserves_Order()
    {
        var requests = new[]
        {
            new AssetImportRequest(new AssetTextSource("a"), new AssetKey("m.a")),
            new AssetImportRequest(new AssetTextSource("b"), new AssetKey("m.b")),
        };

        IReadOnlyList<AssetImportReceipt<object>> receipts = await _db.Inner.ImportManyAsync<object>(
            requests,
            new AssetImportOptions(Concurrency: 2));
        receipts.Should().HaveCount(2);
        receipts[0].BaseKey.Should().Be(new AssetKey("m.a"));
        receipts[1].BaseKey.Should().Be(new AssetKey("m.b"));
    }

    [Fact]
    public void Direct_Fragment_Dependency_Throws_When_Missing()
    {
        Action missing = () => _db.Inner.Upsert(new AssetWriteInput<string>(
            "text", null, new AssetKey("docs/child"), "child", "x",
            Dependencies: new AssetDependencyInput[] { "#child-frag" }));
        missing.Should().Throw<AssetDependencyError>();
    }

    [Fact]
    public async Task Fragment_Dependency_Rebases_During_Import()
    {
        AssetRecord<string> target = _db.Inner.Upsert(
            new AssetWriteInput<string>("text", null, new AssetKey("docs/page#section"), "section", "s"));

        var importer = new FragmentImporter();
        _db.Inner.RegisterImporter(importer);

        AssetImportReceipt<object> receipt = await _db.Inner.ImportAsync<object>(
            new AssetTextSource("x"),
            new AssetKey("docs/page"));
        receipt.Primary.DependencyIds.Should().ContainSingle().Which.Should().Be(target.Id);
    }

    private sealed class FragmentImporter : IAssetImporter
    {
        public AssetImporterId Id => new("test.fragment");
        public int Priority => 10;
        public IReadOnlyList<string> SourceKinds => ["text"];
        public bool CanImport(AssetImportContext context) => context.BaseKey.Value == "docs/page";
        public ValueTask<AssetImportResult> ImportAsync(AssetImportContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AssetImportResult
            {
                Primary = new AssetWriteInput<object>(
                    "text", null, context.BaseKey, "page", new object(),
                    Dependencies: new AssetDependencyInput[] { "#section" }),
            });
    }
}
