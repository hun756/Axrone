namespace Axrone.Asset.Tests;

public sealed class AssetBatchTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void UpsertMany_Resolves_Forward_Refs()
    {
        var childId = new AssetId(7001UL);
        var inputs = new List<AssetWriteInput<object>>
        {
            new("text", childId, new AssetKey("batch/child"), "child", (object)"c",
                Dependencies: new AssetDependencyInput[] { new AssetKey("batch/parent") }),
            new("text", null, new AssetKey("batch/parent"), "parent", (object)"p"),
        };

        IReadOnlyList<AssetRecord<object>> records = _db.Inner.UpsertMany(inputs);
        records.Should().HaveCount(2);
        records[0].DependencyIds.Should().ContainSingle().Which.Should().Be(records[1].Id);
    }

    [Fact]
    public void UpsertMany_Is_Atomic_On_Failure()
    {
        var inputs = new List<AssetWriteInput<object>>
        {
            new("text", null, new AssetKey("batch/ok"), "ok", (object)"x"),
            new("text", null, new AssetKey("batch/bad"), "bad", (object)"x",
                Dependencies: new AssetDependencyInput[] { new AssetId(424242UL) }),
        };

        Action batch = () => _db.Inner.UpsertMany(inputs);
        batch.Should().Throw<AssetDependencyError>();
        _db.Inner.Has(AssetSelector.FromKey(new AssetKey("batch/ok"))).Should().BeFalse();
    }

    [Fact]
    public void UpsertMany_Rejects_Key_Conflicts()
    {
        var first = new AssetId(7002UL);
        var inputs = new List<AssetWriteInput<object>>
        {
            new("text", first, new AssetKey("batch/same"), "a", (object)"x"),
            new("text", new AssetId(7003UL), new AssetKey("batch/same"), "b", (object)"y"),
        };

        Action batch = () => _db.Inner.UpsertMany(inputs);
        batch.Should().Throw<AssetConflictError>().Where(e => e.Code == "key-bound");
        _db.Inner.Has(AssetSelector.FromKey(new AssetKey("batch/same"))).Should().BeFalse();
    }
}
