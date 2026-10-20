namespace Axrone.Asset.Tests;

public sealed class AssetQueryTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Find_Filters_By_Property_And_Scans_All()
    {
        _db.Inner.Upsert(new AssetWriteInput<string>(
            "text", null, new AssetKey("a"), "a", "x",
            Metadata: new AssetMetadata
            {
                Properties = new Dictionary<string, AssetJsonValue> { ["tier"] = "gold", ["level"] = 3.0 },
            }));
        _db.Inner.Upsert(new AssetWriteInput<string>(
            "text", null, new AssetKey("b"), "b", "y",
            Metadata: new AssetMetadata
            {
                Properties = new Dictionary<string, AssetJsonValue> { ["tier"] = "silver" },
            }));

        IReadOnlyList<AssetRecord<object>> gold = _db.Inner.Find(new AssetQuery
        {
            Properties = new Dictionary<string, AssetJsonValue> { ["tier"] = "gold" },
        });
        gold.Should().ContainSingle().Which.Key.Should().Be(new AssetKey("a"));

        IReadOnlyList<AssetRecord<object>> missing = _db.Inner.Find(new AssetQuery
        {
            Properties = new Dictionary<string, AssetJsonValue> { ["tier"] = "bronze" },
        });
        missing.Should().BeEmpty();

        IReadOnlyList<AssetRecord<object>> all = _db.Inner.Find(new AssetQuery());
        all.Should().HaveCount(2);

        IReadOnlyList<AssetRecord<object>> kindOnly = _db.Inner.Find(new AssetQuery { Kind = "text" });
        kindOnly.Should().HaveCount(2);
    }
}
