namespace Axrone.Asset2D.Tests;

public sealed class Asset2DImportTests
{
    private readonly struct CountVisitor : IImportResultVisitor<TopLeftOriginPolicy, int>
    {
        public int OnAtlas(SpriteAtlas<TopLeftOriginPolicy> atlas) => atlas.FrameCount;
        public int OnFailure(string error) => -1;
    }

    private static AssetImportContext CanonicalContext(string path, string json) =>
        new(path.AsSpan(), Encoding.UTF8.GetBytes(json));

    private const string CanonicalJson = """
        {
          "id": "hero",
          "textureId": "hero.png",
          "textureSize": { "width": 256, "height": 256 },
          "frames": [
            { "id": "idle_0", "region": { "x": 0, "y": 0, "width": 32, "height": 32 } },
            { "id": "idle_1", "region": { "x": 32, "y": 0, "width": 32, "height": 32 }, "durationMs": 120 }
          ],
          "animations": [
            { "id": "idle", "fps": 10, "loop": true, "frames": ["idle_0", { "frameId": "idle_1", "durationMs": 120 }] }
          ]
        }
        """;

    private const string TexturePackerJson = """
        {
          "frames": {
            "run_0": { "frame": { "x": 0, "y": 0, "w": 16, "h": 16 }, "rotated": false, "sourceSize": { "w": 16, "h": 16 } },
            "run_1": { "frame": { "x": 16, "y": 0, "w": 16, "h": 16 }, "rotated": false, "sourceSize": { "w": 16, "h": 16 } },
            "run_2": { "frame": { "x": 32, "y": 0, "w": 16, "h": 16 }, "rotated": false, "sourceSize": { "w": 16, "h": 16 } }
          },
          "meta": {
            "image": "run.png",
            "size": { "w": 64, "h": 16 },
            "frameTags": [{ "name": "run", "from": 0, "to": 2, "direction": "pingpong" }]
          }
        }
        """;

    [Fact]
    public void Canonical_Import_Builds_Atlas_And_Visitor_Dispatches()
    {
        AssetImportContext context = CanonicalContext("sprites/hero.spriteatlas.json", CanonicalJson);
        SpriteAtlasJsonImporter<TopLeftOriginPolicy>.CanImport(in context).Should().BeTrue();

        Asset2DImportResult<TopLeftOriginPolicy> result = SpriteAtlasJsonImporter<TopLeftOriginPolicy>.Import(in context);
        result.IsSuccess.Should().BeTrue();
        result.Atlas!.FrameCount.Should().Be(2);
        result.Atlas.GetAnimation(new ClipId("idle"))!.FrameCount.Should().Be(2);

        var visitor = new CountVisitor();
        result.Match<int, CountVisitor>(ref visitor).Should().Be(2);
    }

    [Fact]
    public void TexturePacker_Import_Expands_Pingpong_Tag()
    {
        AssetImportContext context = CanonicalContext("sprites/run.json", TexturePackerJson);
        TexturePackerSpriteAtlasImporter<TopLeftOriginPolicy>.CanImport(in context).Should().BeTrue();
        SpriteAtlasJsonImporter<TopLeftOriginPolicy>.CanImport(in context).Should().BeFalse();

        Asset2DImportResult<TopLeftOriginPolicy> result = TexturePackerSpriteAtlasImporter<TopLeftOriginPolicy>.Import(in context);
        result.IsSuccess.Should().BeTrue();
        result.Atlas!.TextureId.Value.Should().Be("run.png");
        result.Atlas.GetAnimation(new ClipId("run"))!.FrameCount.Should().Be(4);
    }

    [Fact]
    public void Chain_Routes_To_Secondary_And_Fails_On_Unknown()
    {
        AssetImportContext canonical = CanonicalContext("sprites/hero.spriteatlas.json", CanonicalJson);
        Asset2DImportResult<TopLeftOriginPolicy> routed =
            ImporterChain<TexturePackerSpriteAtlasImporter<TopLeftOriginPolicy>, SpriteAtlasJsonImporter<TopLeftOriginPolicy>, TopLeftOriginPolicy>.Import(in canonical);
        routed.IsSuccess.Should().BeTrue();

        AssetImportContext unknown = CanonicalContext("sprites/readme.txt", "hello");
        Asset2DImportResult<TopLeftOriginPolicy> failed =
            ImporterChain<TexturePackerSpriteAtlasImporter<TopLeftOriginPolicy>, SpriteAtlasJsonImporter<TopLeftOriginPolicy>, TopLeftOriginPolicy>.Import(in unknown);
        failed.IsSuccess.Should().BeFalse();
        failed.ErrorMessage.Should().Contain("No registered importer");

        var visitor = new CountVisitor();
        failed.Match<int, CountVisitor>(ref visitor).Should().Be(-1);
    }

    [Fact]
    public void Broken_Json_Returns_Failure_Not_Throw()
    {
        AssetImportContext context = CanonicalContext("sprites/bad.spriteatlas.json", "{ not json");
        Asset2DImportResult<TopLeftOriginPolicy> result = SpriteAtlasJsonImporter<TopLeftOriginPolicy>.Import(in context);
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("a/b/hero.spriteatlas.json", "hero")]
    [InlineData("hero.atlas.json", "hero")]
    [InlineData("hero.json?ver=2", "hero")]
    [InlineData("", "atlas")]
    public void DeriveAtlasId_Strips_Extensions_And_Queries(string path, string expected)
    {
        new string(SpriteAtlasUtil.DeriveAtlasId(path.AsSpan())).Should().Be(expected);
    }
}
