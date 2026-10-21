namespace Axrone.Asset2D.Tests;

public sealed class Asset2DAtlasTests
{
    private static SpriteAtlas<TopLeftOriginPolicy> BuildHeroAtlas() =>
        AtlasBuilder.Create()
            .WithIdentity("hero", "hero.png", new Size2D(256f, 256f))
            .AddFrame(new SpriteFrameDefinition
            {
                Id = "hero_idle_0",
                Region = new Rect2D(0f, 0f, 32f, 32f),
            })
            .AddFrame(new SpriteFrameDefinition
            {
                Id = "hero_idle_1",
                Region = new Rect2D(32f, 0f, 32f, 32f),
                Duration = new Milliseconds(120f),
            })
            .AddAnimation(new SpriteClipDefinition
            {
                Id = "idle",
                Frames = [new FrameId("hero_idle_0"), new FrameId("hero_idle_1")],
                Fps = 10f,
                Loop = true,
            })
            .Build();

    [Fact]
    public void Build_Resolves_Frames_With_Uv()
    {
        SpriteAtlas<TopLeftOriginPolicy> atlas = BuildHeroAtlas();
        atlas.FrameCount.Should().Be(2);
        atlas.AnimationCount.Should().Be(1);
        atlas.Id.Value.Should().Be("hero");

        atlas.TryGetFrame("hero_idle_0".AsSpan(), out SpriteAtlasFrame frame).Should().BeTrue();
        frame.UvRect.U.Value.Should().BeApproximately(0f, 1e-6f);
        frame.UvRect.Width.Value.Should().BeApproximately(0.125f, 1e-6f);
        frame.Pivot.Should().Be(new Vec2(0.5f, 0.5f));
        frame.HasDuration.Should().BeFalse();

        atlas.TryGetFrame(new FrameId("hero_idle_1"), out SpriteAtlasFrame second).Should().BeTrue();
        second.HasDuration.Should().BeTrue();
        second.Duration.Value.Should().Be(120f);
    }

    [Fact]
    public void Misses_Return_False_And_Null()
    {
        SpriteAtlas<TopLeftOriginPolicy> atlas = BuildHeroAtlas();
        atlas.TryGetFrame("nope".AsSpan(), out _).Should().BeFalse();
        atlas.GetFrame(new FrameId("nope")).Should().BeNull();
        atlas.TryGetAnimation("nope".AsSpan(), out _).Should().BeFalse();
        atlas.GetAnimation(new ClipId("nope")).Should().BeNull();
    }

    [Fact]
    public void Sample_Loops_And_Clamps()
    {
        SpriteAtlas<TopLeftOriginPolicy> atlas = BuildHeroAtlas();
        SpriteAnimationClip? clip = atlas.GetAnimation(new ClipId("idle"));
        clip.Should().NotBeNull();
        clip!.FrameCount.Should().Be(2);

        ref readonly SpriteAtlasFrame first = ref clip.Sample(new Milliseconds(50f), out Milliseconds offset);
        first.Id.Value.Should().Be("hero_idle_0");
        offset.Value.Should().BeApproximately(50f, 1e-4f);

        ref readonly SpriteAtlasFrame second = ref clip.Sample(new Milliseconds(150f), out _);
        second.Id.Value.Should().Be("hero_idle_1");

        ref readonly SpriteAtlasFrame wrapped = ref clip.Sample(new Milliseconds(1000f), out _);
        wrapped.Id.Value.Should().BeOneOf("hero_idle_0", "hero_idle_1");
    }

    [Fact]
    public void Builder_Rejects_Duplicates_And_Bad_Geometry()
    {
        Action duplicate = () => AtlasBuilder.Create()
            .WithIdentity("a", "a.png", new Size2D(64f, 64f))
            .AddFrame(new SpriteFrameDefinition { Id = "f", Region = new Rect2D(0f, 0f, 8f, 8f) })
            .AddFrame(new SpriteFrameDefinition { Id = "f", Region = new Rect2D(8f, 0f, 8f, 8f) });
        duplicate.Should().Throw<Asset2DValidationException>();

        Action overflow = () => AtlasBuilder.Create()
            .WithIdentity("a", "a.png", new Size2D(64f, 64f))
            .AddFrame(new SpriteFrameDefinition { Id = "f", Region = new Rect2D(60f, 60f, 8f, 8f) })
            .Build();
        overflow.Should().Throw<Asset2DValidationException>();

        Action dangling = () => AtlasBuilder.Create()
            .WithIdentity("a", "a.png", new Size2D(64f, 64f))
            .AddFrame(new SpriteFrameDefinition { Id = "f", Region = new Rect2D(0f, 0f, 8f, 8f) })
            .AddAnimation(new SpriteClipDefinition
            {
                Id = "c",
                Frames = [new FrameId("ghost")],
            })
            .Build();
        dangling.Should().Throw<Asset2DValidationException>();
    }

    [Fact]
    public void Enumerators_Walk_Frames_And_Clips()
    {
        SpriteAtlas<TopLeftOriginPolicy> atlas = BuildHeroAtlas();
        int frames = 0;
        foreach (ref readonly SpriteAtlasFrame frame in atlas)
        {
            frames++;
            frame.Id.Value.Should().StartWith("hero_idle_");
        }

        frames.Should().Be(2);

        int clips = 0;
        foreach (SpriteAnimationClip clip in atlas.EnumerateAnimations())
        {
            clips++;
            clip.Id.Value.Should().Be("idle");
        }

        clips.Should().Be(1);
    }
}
