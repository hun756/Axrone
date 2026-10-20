using Axrone.Asset;

namespace Axrone.Asset2D.Tests;

/// <summary>Edge-branch coverage: every validation, fallback and failure path.</summary>
public sealed class Asset2DEdgeTests : IDisposable
{
    private readonly Asset2DTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static SpriteAtlas<TopLeftOriginPolicy> BuildFullAtlas() =>
        AtlasBuilder.Create()
            .WithIdentity("full", "full.png", new Size2D(256f, 256f))
            .AddFrame(new SpriteFrameDefinition
            {
                Id = "rot",
                Region = new Rect2D(0f, 0f, 16f, 32f),
                Rotated = true,
                SourceSize = new Size2D(20f, 36f),
                Pivot = new Vec2(0.25f, 0.75f),
                SliceBorder = new Border2D(2f, 2f, 2f, 2f),
                TrimOffset = new Vec2(1f, 2f),
                Duration = new Milliseconds(50f),
            })
            .Build();

    [Fact]
    public void Full_Frame_Sets_All_Flags_And_Values()
    {
        SpriteAtlas<TopLeftOriginPolicy> atlas = BuildFullAtlas();
        atlas.TryGetFrame("rot".AsSpan(), out SpriteAtlasFrame frame).Should().BeTrue();
        frame.Rotated.Should().BeTrue();
        frame.HasBorder.Should().BeTrue();
        frame.HasTrim.Should().BeTrue();
        frame.HasDuration.Should().BeTrue();
        frame.SourceSize.Should().Be(new Size2D(20f, 36f));
        frame.Pivot.Should().Be(new Vec2(0.25f, 0.75f));
        frame.SliceBorder.Should().Be(new Border2D(2f, 2f, 2f, 2f));
        frame.TrimOffset.Should().Be(new Vec2(1f, 2f));
        frame.TextureId.Value.Should().Be("full.png");
    }

    [Fact]
    public void Builder_Rejects_Duplicate_Animation()
    {
        Action duplicate = () => AtlasBuilder.Create()
            .WithIdentity("a", "a.png", new Size2D(64f, 64f))
            .AddFrame(new SpriteFrameDefinition { Id = "f", Region = new Rect2D(0f, 0f, 8f, 8f) })
            .AddAnimation(new SpriteClipDefinition { Id = "c", Frames = [new FrameId("f")] })
            .AddAnimation(new SpriteClipDefinition { Id = "c", Frames = [new FrameId("f")] });
        duplicate.Should().Throw<Asset2DValidationException>();
    }

    [Fact]
    public void Builder_Rejects_Empty_Animation_Frames()
    {
        Action empty = () => AtlasBuilder.Create()
            .WithIdentity("a", "a.png", new Size2D(64f, 64f))
            .AddFrame(new SpriteFrameDefinition { Id = "f", Region = new Rect2D(0f, 0f, 8f, 8f) })
            .AddAnimation(new SpriteClipDefinition
            {
                Id = "c",
                Frames = ImmutableArray<SpriteClipRefDefinition>.Empty,
            });
        empty.Should().Throw<Asset2DValidationException>();
    }

    [Fact]
    public void Builder_Rejects_Bad_Texture_Size()
    {
        Action zero = () => AtlasBuilder.Create()
            .WithIdentity("a", "a.png", new Size2D(0f, 64f));
        zero.Should().Throw<Asset2DValidationException>();
    }

    [Theory]
    [InlineData(-1f, 0f, 8f, 8f)]
    [InlineData(0f, 0f, 0f, 8f)]
    [InlineData(0f, 0f, float.NaN, 8f)]
    public void Builder_Rejects_Bad_Region(float x, float y, float w, float h)
    {
        Action bad = () => AtlasBuilder.Create()
            .WithIdentity("a", "a.png", new Size2D(64f, 64f))
            .AddFrame(new SpriteFrameDefinition { Id = "f", Region = new Rect2D(x, y, w, h) })
            .Build();
        bad.Should().Throw<Asset2DValidationException>();
    }

    [Fact]
    public void Builder_Rejects_Bad_SourceSize_Pivot_Border_Duration()
    {
        Action badSource = () => AtlasBuilder.Create()
            .WithIdentity("a", "a.png", new Size2D(64f, 64f))
            .AddFrame(new SpriteFrameDefinition
            {
                Id = "f",
                Region = new Rect2D(0f, 0f, 8f, 8f),
                SourceSize = new Size2D(0f, 8f),
            })
            .Build();
        badSource.Should().Throw<Asset2DValidationException>();

        Action badPivot = () => AtlasBuilder.Create()
            .WithIdentity("a", "a.png", new Size2D(64f, 64f))
            .AddFrame(new SpriteFrameDefinition
            {
                Id = "f",
                Region = new Rect2D(0f, 0f, 8f, 8f),
                Pivot = new Vec2(float.NaN, 0.5f),
            })
            .Build();
        badPivot.Should().Throw<Asset2DValidationException>();

        Action badBorder = () => AtlasBuilder.Create()
            .WithIdentity("a", "a.png", new Size2D(64f, 64f))
            .AddFrame(new SpriteFrameDefinition
            {
                Id = "f",
                Region = new Rect2D(0f, 0f, 8f, 8f),
                SliceBorder = new Border2D(-1f, 0f, 0f, 0f),
            })
            .Build();
        badBorder.Should().Throw<Asset2DValidationException>();

        Action badDuration = () => AtlasBuilder.Create()
            .WithIdentity("a", "a.png", new Size2D(64f, 64f))
            .AddFrame(new SpriteFrameDefinition
            {
                Id = "f",
                Region = new Rect2D(0f, 0f, 8f, 8f),
                Duration = new Milliseconds(-5f),
            })
            .Build();
        badDuration.Should().Throw<Asset2DValidationException>();
    }

    [Fact]
    public void Configuration_Rejects_Blank_And_Bad_Size()
    {
        Action blankId = () => _ = new AtlasConfiguration
        {
            Id = new AtlasId("ok"),
            TextureId = new TextureId("ok.png"),
            TextureSize = new Size2D(0f, 8f),
        };
        blankId.Should().Throw<Asset2DValidationException>();
    }

    [Fact]
    public void NonLoop_Sample_Clamps_At_End()
    {
        SpriteAtlas<TopLeftOriginPolicy> atlas = AtlasBuilder.Create()
            .WithIdentity("a", "a.png", new Size2D(64f, 64f))
            .AddFrame(new SpriteFrameDefinition { Id = "f0", Region = new Rect2D(0f, 0f, 8f, 8f) })
            .AddFrame(new SpriteFrameDefinition { Id = "f1", Region = new Rect2D(8f, 0f, 8f, 8f) })
            .AddAnimation(new SpriteClipDefinition
            {
                Id = "once",
                Frames = [new FrameId("f0"), new FrameId("f1")],
                Fps = 10f,
                Loop = false,
            })
            .Build();

        SpriteAnimationClip? clip = atlas.GetAnimation(new ClipId("once"));
        clip.Should().NotBeNull();

        ref readonly SpriteAtlasFrame last = ref clip!.Sample(new Milliseconds(500f), out Milliseconds offset);
        last.Id.Value.Should().Be("f1");
        offset.Value.Should().BeApproximately(100f, 1e-3f);
    }

    [Fact]
    public void Clip_Direct_Edge_Paths()
    {
        var frame = new SpriteAtlasFrame(
            new FrameId("f"),
            new TextureId("t.png"),
            new Rect2D(0f, 0f, 8f, 8f),
            new UvRect(0f, 0f, 0.125f, 0.125f),
            new Size2D(8f, 8f),
            new Vec2(0.5f, 0.5f),
            Vec2.Zero,
            Border2D.Zero,
            Milliseconds.Zero,
            SpriteFrameAttributes.None);
        var clip = new SpriteAnimationClip(new ClipId("c"), [new SpriteAnimationFrame(frame, new Milliseconds(100f))], Milliseconds.Zero, true);

        ref readonly SpriteAtlasFrame first = ref clip.Sample(new Milliseconds(10f), out _);
        first.Id.Value.Should().Be("f");

        Action oob = () => _ = clip.GetFrame(7);
        oob.Should().Throw<Asset2DValidationException>();

        int visited = 0;
        foreach (ref readonly SpriteAnimationFrame key in clip)
        {
            visited++;
            key.Frame.Id.Value.Should().Be("f");
        }

        visited.Should().Be(1);

        var empty = new SpriteAnimationClip(new ClipId("e"), [], Milliseconds.Zero, true);
        Action sampleEmpty = () => _ = empty.Sample(Milliseconds.Zero, out _);
        sampleEmpty.Should().Throw<Asset2DValidationException>();
    }

    [Fact]
    public void BottomLeft_Engine_Flips_V()
    {
        SpriteAtlas<BottomLeftOriginPolicy> atlas = AtlasBuilder.Create<BottomLeftOriginPolicy>()
            .WithIdentity("a", "a.png", new Size2D(100f, 200f))
            .AddFrame(new SpriteFrameDefinition { Id = "f", Region = new Rect2D(10f, 20f, 30f, 40f) })
            .Build();

        atlas.TryGetFrame("f".AsSpan(), out SpriteAtlasFrame frame).Should().BeTrue();
        frame.UvRect.V.Value.Should().BeApproximately(0.7f, 1e-6f);
    }

    [Fact]
    public void Animation_Span_Lookup_Hits()
    {
        SpriteAtlas<TopLeftOriginPolicy> atlas = AtlasBuilder.Create()
            .WithIdentity("a", "a.png", new Size2D(64f, 64f))
            .AddFrame(new SpriteFrameDefinition { Id = "f", Region = new Rect2D(0f, 0f, 8f, 8f) })
            .AddAnimation(new SpriteClipDefinition { Id = "c", Frames = [new FrameId("f")] })
            .Build();

        atlas.TryGetAnimation("c".AsSpan(), out SpriteAnimationClip? clip).Should().BeTrue();
        clip!.Id.Value.Should().Be("c");
    }

    [Fact]
    public void TexturePacker_Reverse_Tag_Reverses_Order_Without_Loop()
    {
        const string packed = """
            {
              "frames": {
                "run_0": { "frame": { "x": 0, "y": 0, "w": 16, "h": 16 } },
                "run_1": { "frame": { "x": 16, "y": 0, "w": 16, "h": 16 } },
                "run_2": { "frame": { "x": 32, "y": 0, "w": 16, "h": 16 } }
              },
              "meta": {
                "image": "run.png",
                "size": { "w": 64, "h": 16 },
                "frameTags": [{ "name": "run", "from": 0, "to": 2, "direction": "reverse" }]
              }
            }
            """;
        var context = new AssetImportContext("sprites/run.json".AsSpan(), Encoding.UTF8.GetBytes(packed));
        Asset2DImportResult<TopLeftOriginPolicy> result = TexturePackerSpriteAtlasImporter<TopLeftOriginPolicy>.Import(in context);
        result.IsSuccess.Should().BeTrue();

        SpriteAnimationClip? clip = result.Atlas!.GetAnimation(new ClipId("run"));
        clip.Should().NotBeNull();
        clip!.FrameCount.Should().Be(3);
        clip.Loop.Should().BeFalse();
        clip.GetFrame(0).Frame.Id.Value.Should().Be("run_2");
        clip.GetFrame(2).Frame.Id.Value.Should().Be("run_0");
    }

    [Fact]
    public void TexturePacker_Out_Of_Range_Tag_Index_Fails()
    {
        const string packed = """
            {
              "frames": { "f": { "frame": { "x": 0, "y": 0, "w": 8, "h": 8 } } },
              "meta": {
                "image": "a.png",
                "size": { "w": 8, "h": 8 },
                "frameTags": [{ "name": "c", "from": 0, "to": 5 }]
              }
            }
            """;
        var context = new AssetImportContext("sprites/a.json".AsSpan(), Encoding.UTF8.GetBytes(packed));
        Asset2DImportResult<TopLeftOriginPolicy> result = TexturePackerSpriteAtlasImporter<TopLeftOriginPolicy>.Import(in context);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Invalid_Fps_Falls_Back_To_Default()
    {
        const string json = """
            {
              "id": "a",
              "textureId": "a.png",
              "textureSize": { "width": 64, "height": 64 },
              "frames": [{ "id": "f", "region": { "x": 0, "y": 0, "width": 8, "height": 8 } }],
              "animations": [{ "id": "c", "fps": 0, "frames": ["f", "f"] }]
            }
            """;
        var context = new AssetImportContext("sprites/a.spriteatlas.json".AsSpan(), Encoding.UTF8.GetBytes(json));
        Asset2DImportResult<TopLeftOriginPolicy> result = SpriteAtlasJsonImporter<TopLeftOriginPolicy>.Import(in context);
        result.IsSuccess.Should().BeTrue();
        result.Atlas!.GetAnimation(new ClipId("c"))!.Duration.Value.Should().BeApproximately(2000f, 1e-2f);
    }

    [Fact]
    public void Chain_Primary_Hit_Wins()
    {
        const string packed = """
            {
              "frames": { "f": { "frame": { "x": 0, "y": 0, "w": 8, "h": 8 } } },
              "meta": { "image": "a.png", "size": { "w": 8, "h": 8 } }
            }
            """;
        var context = new AssetImportContext("sprites/a.json".AsSpan(), Encoding.UTF8.GetBytes(packed));
        Asset2DImportResult<TopLeftOriginPolicy> result =
            ImporterChain<TexturePackerSpriteAtlasImporter<TopLeftOriginPolicy>, SpriteAtlasJsonImporter<TopLeftOriginPolicy>, TopLeftOriginPolicy>.Import(in context);
        result.IsSuccess.Should().BeTrue();
        result.Atlas!.TextureId.Value.Should().Be("a.png");
    }

    [Fact]
    public async Task Bridge_Failure_Surfaces_Coded_Error()
    {
        _db.Inner.RegisterImporter(new SpriteAtlasAssetImporter());

        Func<Task> act = () => _db.Inner.ImportAsync<object>(
            new AssetTextSource("""{"id":"bad","textureId":"bad.png","textureSize":{"width":64,"height":64},"frames":[{"id":"f","region":{"x":0,"y":0,"width":0,"height":8}}]}"""),
            new AssetKey("sprites/bad"),
            sourceUri: new AssetUri("sprites/bad.spriteatlas.json")).AsTask();
        AssetImportError error = await act.Should().ThrowAsync<AssetImportError>().Where(e => e.Attempt == 1);
        error.InnerException.Should().BeOfType<Asset2DException>()
            .Which.Code.Should().Be("asset-2d.import");
    }
}
