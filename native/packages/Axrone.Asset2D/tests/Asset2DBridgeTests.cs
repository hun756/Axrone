using Axrone.Asset;

namespace Axrone.Asset2D.Tests;

internal readonly struct Asset2DSchema;

internal sealed class Asset2DTestDatabase : IDisposable
{
    public AssetDatabase<Asset2DSchema, DefaultDataCodecPolicy<object>, DefaultLifecyclePolicy<object>> Inner { get; } = new();

    public void Dispose() => Inner.Dispose();
}

public sealed class Asset2DBridgeTests : IDisposable
{
    private readonly Asset2DTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private const string CanonicalJson = """
        {
          "id": "hero",
          "textureId": "hero.png",
          "textureSize": { "width": 128, "height": 128 },
          "frames": [
            { "id": "idle_0", "region": { "x": 0, "y": 0, "width": 32, "height": 32 } },
            { "id": "idle_1", "region": { "x": 32, "y": 0, "width": 32, "height": 32 } }
          ],
          "animations": [
            { "id": "idle", "fps": 10, "loop": true, "frames": ["idle_0", "idle_1"] }
          ]
        }
        """;

    [Fact]
    public async Task Import_Text_Atlas_Through_Pipeline()
    {
        _db.Inner.RegisterImporter(new SpriteAtlasAssetImporter());

        AssetImportReceipt<object> receipt = await _db.Inner.ImportAsync<object>(
            new AssetTextSource(CanonicalJson),
            new AssetKey("sprites/hero"),
            sourceUri: new AssetUri("sprites/hero.spriteatlas.json"));

        receipt.ImporterId.Value.Should().Be("axrone.importer.sprite-atlas");
        receipt.Primary.Kind.Should().Be("sprite-atlas");
        receipt.Primary.Data.Should().BeOfType<SpriteAtlas<TopLeftOriginPolicy>>();

        var atlas = (SpriteAtlas<TopLeftOriginPolicy>)receipt.Primary.Data!;
        atlas.FrameCount.Should().Be(2);
        atlas.TryGetFrame("idle_0".AsSpan(), out _).Should().BeTrue();

        AssetRecord<object>? stored = _db.Inner.Get<object>(AssetSelector.FromKey(new AssetKey("sprites/hero")));
        stored.Should().NotBeNull();
        stored!.Value.Data.Should().BeOfType<SpriteAtlas<TopLeftOriginPolicy>>();
    }

    [Fact]
    public async Task Import_Bytes_TexturePacker_Atlas_Through_Pipeline()
    {
        const string packed = """
            {
              "frames": {
                "run_0": { "frame": { "x": 0, "y": 0, "w": 16, "h": 16 } }
              },
              "meta": { "image": "run.png", "size": { "w": 16, "h": 16 } }
            }
            """;
        _db.Inner.RegisterImporter(new SpriteAtlasAssetImporter());

        AssetImportReceipt<object> receipt = await _db.Inner.ImportAsync<object>(
            new AssetBytesSource(Encoding.UTF8.GetBytes(packed)),
            new AssetKey("sprites/run"),
            sourceUri: new AssetUri("sprites/run.json"));

        receipt.Primary.Data.Should().BeOfType<SpriteAtlas<TopLeftOriginPolicy>>();
    }

    [Fact]
    public void Non_Atlas_Source_Is_Rejected()
    {
        var importer = new SpriteAtlasAssetImporter();
        var context = new Axrone.Asset.AssetImportContext(
            new AssetTextSource("hello"),
            new AssetKey("docs/readme"),
            sourceUri: new AssetUri("docs/readme.txt"));
        importer.CanImport(context).Should().BeFalse();
    }
}
