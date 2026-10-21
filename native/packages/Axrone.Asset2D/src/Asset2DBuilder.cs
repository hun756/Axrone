namespace Axrone.Asset2D;

/// <summary>Frame reference inside a clip, with an optional per-ref duration override.</summary>
public readonly record struct SpriteClipRefDefinition(FrameId FrameId, Milliseconds? Duration = null)
{
    /// <summary>Creates a reference from a frame id.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator SpriteClipRefDefinition(FrameId frameId) => new(frameId);

    /// <summary>Creates a reference from a frame name.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator SpriteClipRefDefinition(string frameId) => new(new FrameId(frameId));
}

/// <summary>Frame definition consumed by the typestate builder.</summary>
public readonly record struct SpriteFrameDefinition
{
    /// <summary>Frame identity.</summary>
    public required FrameId Id { get; init; }

    /// <summary>Pixel region.</summary>
    public required Rect2D Region { get; init; }

    /// <summary>Source dimensions override.</summary>
    public Size2D? SourceSize { get; init; }

    /// <summary>Pivot override.</summary>
    public Vec2? Pivot { get; init; }

    /// <summary>9-slice border.</summary>
    public Border2D? SliceBorder { get; init; }

    /// <summary>Explicit duration.</summary>
    public Milliseconds? Duration { get; init; }

    /// <summary>Whether the frame is stored rotated.</summary>
    public bool Rotated { get; init; }

    /// <summary>Trim offset.</summary>
    public Vec2? TrimOffset { get; init; }
}

/// <summary>Animation clip definition consumed by the typestate builder.</summary>
public readonly record struct SpriteClipDefinition
{
    /// <summary>Clip identity.</summary>
    public required ClipId Id { get; init; }

    /// <summary>Frame references in playback order.</summary>
    public required ImmutableArray<SpriteClipRefDefinition> Frames { get; init; }

    /// <summary>Frames per second fallback.</summary>
    public float? Fps { get; init; }

    /// <summary>Loop override.</summary>
    public bool? Loop { get; init; }
}

/// <summary>Atlas-level configuration validated at build time.</summary>
public sealed class AtlasConfiguration
{
    /// <summary>Atlas identity.</summary>
    public AtlasId Id
    {
        get;
        init
        {
            if (string.IsNullOrWhiteSpace(value.Value))
            {
                ThrowHelper.ThrowValidation("Atlas Id cannot be empty.");
            }

            field = value;
        }
    }

    /// <summary>Backing texture identity.</summary>
    public TextureId TextureId
    {
        get;
        init
        {
            if (string.IsNullOrWhiteSpace(value.Value))
            {
                ThrowHelper.ThrowValidation("Texture Id cannot be empty.");
            }

            field = value;
        }
    }

    /// <summary>Backing texture size.</summary>
    public Size2D TextureSize
    {
        get;
        init
        {
            float w = value.Width.Value;
            float h = value.Height.Value;
            if (!float.IsFinite(w) || w <= 0f || !float.IsFinite(h) || h <= 0f)
            {
                ThrowHelper.ThrowValidation($"Texture dimensions must be positive finite values. Got: {value}.");
            }

            field = value;
        }
    }
}

/// <summary>Typestate stages for the atlas builder.</summary>
public static class AtlasBuildStage
{
    /// <summary>No identity assigned yet.</summary>
    public readonly struct Unconfigured;

    /// <summary>Identity assigned; frames can be added.</summary>
    public readonly struct Configured;

    /// <summary>At least one frame added; animations and build are available.</summary>
    public readonly struct FramesDefined;
}

/// <summary>Mutable builder state shared across typestate phases.</summary>
internal sealed class AtlasBuilderState
{
    /// <summary>Atlas identity.</summary>
    public AtlasId Id;

    /// <summary>Backing texture identity.</summary>
    public TextureId TextureId;

    /// <summary>Backing texture size.</summary>
    public Size2D TextureSize;

    /// <summary>Frame definitions in insertion order.</summary>
    public readonly List<SpriteFrameDefinition> Frames = new(32);

    /// <summary>Animation definitions in insertion order.</summary>
    public readonly List<SpriteClipDefinition> Animations = new(8);

    /// <summary>Seen frame ids for duplicate detection.</summary>
    public readonly HashSet<FrameId> FrameIds = new();

    /// <summary>Seen clip ids for duplicate detection.</summary>
    public readonly HashSet<ClipId> ClipIds = new();
}

/// <summary>Typestate atlas builder.</summary>
/// <typeparam name="TStage">Build stage.</typeparam>
/// <typeparam name="TCoordPolicy">UV coordinate policy.</typeparam>
public readonly struct AtlasBuilder<TStage, TCoordPolicy>
    where TCoordPolicy : struct, ICoordinatePolicy
{
    /// <summary>Shared mutable state.</summary>
    internal readonly AtlasBuilderState State;

    /// <summary>Creates a builder over shared state.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal AtlasBuilder(AtlasBuilderState state) => State = state;
}

/// <summary>Atlas builder entry points.</summary>
public static class AtlasBuilder
{
    /// <summary>Creates a builder with the top-left origin policy.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AtlasBuilder<AtlasBuildStage.Unconfigured, TopLeftOriginPolicy> Create() => new(new AtlasBuilderState());

    /// <summary>Creates a builder with an explicit coordinate policy.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AtlasBuilder<AtlasBuildStage.Unconfigured, TCoordPolicy> Create<TCoordPolicy>()
        where TCoordPolicy : struct, ICoordinatePolicy => new(new AtlasBuilderState());
}

/// <summary>Typestate transitions for the atlas builder.</summary>
public static class AtlasBuilderExtensions
{
    /// <summary>Assigns atlas identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AtlasBuilder<AtlasBuildStage.Configured, TCoordPolicy> WithIdentity<TCoordPolicy>(
        this AtlasBuilder<AtlasBuildStage.Unconfigured, TCoordPolicy> builder,
        AtlasId id,
        TextureId textureId,
        Size2D textureSize)
        where TCoordPolicy : struct, ICoordinatePolicy
    {
        float w = textureSize.Width.Value;
        float h = textureSize.Height.Value;
        if (!float.IsFinite(w) || w <= 0f || !float.IsFinite(h) || h <= 0f)
        {
            ThrowHelper.ThrowValidation($"Atlas textureSize must have positive finite dimensions. Got: [{w}x{h}].");
        }

        builder.State.Id = id;
        builder.State.TextureId = textureId;
        builder.State.TextureSize = textureSize;

        return new AtlasBuilder<AtlasBuildStage.Configured, TCoordPolicy>(builder.State);
    }

    /// <summary>Adds the first frame.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AtlasBuilder<AtlasBuildStage.FramesDefined, TCoordPolicy> AddFrame<TCoordPolicy>(
        this AtlasBuilder<AtlasBuildStage.Configured, TCoordPolicy> builder,
        in SpriteFrameDefinition frame)
        where TCoordPolicy : struct, ICoordinatePolicy
    {
        builder.AddFrameCore(in frame);
        return new AtlasBuilder<AtlasBuildStage.FramesDefined, TCoordPolicy>(builder.State);
    }

    /// <summary>Adds another frame.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AtlasBuilder<AtlasBuildStage.FramesDefined, TCoordPolicy> AddFrame<TCoordPolicy>(
        this AtlasBuilder<AtlasBuildStage.FramesDefined, TCoordPolicy> builder,
        in SpriteFrameDefinition frame)
        where TCoordPolicy : struct, ICoordinatePolicy
    {
        builder.AddFrameCore(in frame);
        return builder;
    }

    /// <summary>Adds an animation clip.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AtlasBuilder<AtlasBuildStage.FramesDefined, TCoordPolicy> AddAnimation<TCoordPolicy>(
        this AtlasBuilder<AtlasBuildStage.FramesDefined, TCoordPolicy> builder,
        in SpriteClipDefinition clip)
        where TCoordPolicy : struct, ICoordinatePolicy
    {
        if (!builder.State.ClipIds.Add(clip.Id))
        {
            ThrowHelper.ThrowValidation($"Duplicate animation id detected: '{clip.Id.Value}'.");
        }

        if (clip.Frames.IsDefaultOrEmpty)
        {
            ThrowHelper.ThrowValidation($"Animation '{clip.Id.Value}' must contain at least one frame reference.");
        }

        builder.State.Animations.Add(clip);
        return builder;
    }

    /// <summary>Builds the atlas.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpriteAtlas<TCoordPolicy> Build<TCoordPolicy>(
        this AtlasBuilder<AtlasBuildStage.FramesDefined, TCoordPolicy> builder)
        where TCoordPolicy : struct, ICoordinatePolicy
    {
        return SpriteAtlas<TCoordPolicy>.CreateInternal(builder.State);
    }

    /// <summary>Validates and records one frame definition.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddFrameCore<TStage, TCoordPolicy>(
        this AtlasBuilder<TStage, TCoordPolicy> builder,
        in SpriteFrameDefinition frame)
        where TCoordPolicy : struct, ICoordinatePolicy
    {
        if (!builder.State.FrameIds.Add(frame.Id))
        {
            ThrowHelper.ThrowValidation($"Duplicate frame id detected: '{frame.Id.Value}'.");
        }

        builder.State.Frames.Add(frame);
    }
}
