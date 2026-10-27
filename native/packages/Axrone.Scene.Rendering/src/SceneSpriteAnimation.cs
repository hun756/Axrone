namespace Axrone.Scene.Rendering;

/// <summary>
/// One frame of a sprite animation: the atlas window to display and optional
/// per-frame overrides for size, source size and anchor.
/// </summary>
public readonly record struct SceneSpriteAnimationFrame
{
    /// <summary>Creates an animation frame.</summary>
    /// <param name="uvRect">Atlas window as (x, y, width, height) in normalized UV space.</param>
    /// <param name="durationSeconds">Display duration; non-positive durations advance immediately.</param>
    /// <param name="size">Optional world-space size override.</param>
    /// <param name="sourceSize">Optional texture source-size override.</param>
    /// <param name="anchor">Optional normalized anchor override.</param>
    public SceneSpriteAnimationFrame(
        Vec4 uvRect,
        float durationSeconds,
        Vec2? size = null,
        Vec2? sourceSize = null,
        Vec2? anchor = null)
    {
        UvRect = uvRect;
        DurationSeconds = durationSeconds;
        Size = size;
        SourceSize = sourceSize;
        Anchor = anchor;
    }

    /// <summary>Atlas window as (x, y, width, height) in normalized UV space.</summary>
    public Vec4 UvRect { get; }

    /// <summary>Display duration in seconds.</summary>
    public float DurationSeconds { get; }

    /// <summary>Optional world-space size override.</summary>
    public Vec2? Size { get; }

    /// <summary>Optional texture source-size override.</summary>
    public Vec2? SourceSize { get; }

    /// <summary>Optional normalized anchor override.</summary>
    public Vec2? Anchor { get; }
}

/// <summary>
/// Immutable sprite animation clip: a named, ordered list of frames plus the
/// default loop policy. Clips are shared and never mutated by the animator.
/// </summary>
public sealed class SceneSpriteAnimationClip
{
    /// <summary>Creates a clip from its frames.</summary>
    /// <param name="name">Stable clip name.</param>
    /// <param name="frames">Frames in playback order; may be empty.</param>
    /// <param name="loop">Whether playback wraps by default.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="frames"/> is null.</exception>
    public SceneSpriteAnimationClip(
        string name,
        IReadOnlyList<SceneSpriteAnimationFrame> frames,
        bool loop = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(frames);

        Name = name;
        Frames = [.. frames];
        Loop = loop;

        float duration = 0.0f;
        for (int i = 0; i < Frames.Count; i++)
        {
            duration += Frames[i].DurationSeconds;
        }

        DurationSeconds = duration;
    }

    /// <summary>Stable clip name.</summary>
    public string Name { get; }

    /// <summary>Frames in playback order.</summary>
    public IReadOnlyList<SceneSpriteAnimationFrame> Frames { get; }

    /// <summary>Whether playback wraps by default.</summary>
    public bool Loop { get; }

    /// <summary>Sum of all frame durations in seconds.</summary>
    public float DurationSeconds { get; }
}
