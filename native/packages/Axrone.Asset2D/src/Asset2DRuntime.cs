namespace Axrone.Asset2D;

/// <summary>
/// Resolved runtime sprite frame representation with direct UV normalization.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct SpriteAtlasFrame
{
    /// <summary>Frame identity.</summary>
    public FrameId Id { get; }

    /// <summary>Backing texture identity.</summary>
    public TextureId TextureId { get; }

    /// <summary>Pixel region.</summary>
    public Rect2D Region { get; }

    /// <summary>Normalized UV rectangle.</summary>
    public UvRect UvRect { get; }

    /// <summary>Source dimensions.</summary>
    public Size2D SourceSize { get; }

    /// <summary>Pivot point.</summary>
    public Vec2 Pivot { get; }

    /// <summary>Trim offset.</summary>
    public Vec2 TrimOffset { get; }

    /// <summary>9-slice border.</summary>
    public Border2D SliceBorder { get; }

    /// <summary>Explicit duration.</summary>
    public Milliseconds Duration { get; }

    /// <summary>Frame flags.</summary>
    public SpriteFrameAttributes Flags { get; }

    /// <summary>Whether the frame is stored rotated.</summary>
    public bool Rotated => (Flags & SpriteFrameAttributes.Rotated) != 0;

    /// <summary>Whether the frame carries a 9-slice border.</summary>
    public bool HasBorder => (Flags & SpriteFrameAttributes.HasBorder) != 0;

    /// <summary>Whether the frame carries a trim offset.</summary>
    public bool HasTrim => (Flags & SpriteFrameAttributes.HasTrim) != 0;

    /// <summary>Whether the frame carries an explicit duration.</summary>
    public bool HasDuration => (Flags & SpriteFrameAttributes.HasDuration) != 0;

    /// <summary>Creates a resolved frame.</summary>
    public SpriteAtlasFrame(
        FrameId id,
        TextureId textureId,
        in Rect2D region,
        in UvRect uvRect,
        in Size2D sourceSize,
        in Vec2 pivot,
        in Vec2 trimOffset,
        in Border2D sliceBorder,
        Milliseconds duration,
        SpriteFrameAttributes flags)
    {
        Id = id;
        TextureId = textureId;
        Region = region;
        UvRect = uvRect;
        SourceSize = sourceSize;
        Pivot = pivot;
        TrimOffset = trimOffset;
        SliceBorder = sliceBorder;
        Duration = duration;
        Flags = flags;
    }

    /// <inheritdoc/>
    public override string ToString() => $"Frame[{Id.Value}, {Region}]";
}

/// <summary>One resolved animation keyframe.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct SpriteAnimationFrame
{
    /// <summary>Resolved frame.</summary>
    public readonly SpriteAtlasFrame Frame;

    /// <summary>Keyframe duration.</summary>
    public readonly Milliseconds Duration;

    /// <summary>Creates a keyframe.</summary>
    public SpriteAnimationFrame(SpriteAtlasFrame frame, Milliseconds duration)
    {
        Frame = frame;
        Duration = duration;
    }
}

/// <summary>Resolved animation clip with sampling.</summary>
public sealed class SpriteAnimationClip
{
    private readonly SpriteAnimationFrame[] _frames;

    /// <summary>Clip identity.</summary>
    public ClipId Id { get; }

    /// <summary>Total duration.</summary>
    public Milliseconds Duration { get; }

    /// <summary>Whether the clip loops.</summary>
    public bool Loop { get; }

    /// <summary>Keyframe count.</summary>
    public int FrameCount => _frames.Length;

    /// <summary>Creates a clip.</summary>
    public SpriteAnimationClip(ClipId id, SpriteAnimationFrame[] frames, Milliseconds duration, bool loop)
    {
        Id = id;
        _frames = frames;
        Duration = duration;
        Loop = loop;
    }

    /// <summary>Returns the keyframe at the given index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ref readonly SpriteAnimationFrame GetFrame(int index)
    {
        if ((uint)index >= (uint)_frames.Length)
        {
            ThrowHelper.ThrowValidation($"Frame index {index} out of range in clip '{Id.Value}'.");
        }

        return ref _frames[index];
    }

    /// <summary>Samples the clip at the given elapsed time.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ref readonly SpriteAtlasFrame Sample(Milliseconds elapsed, out Milliseconds localFrameOffset)
    {
        if (_frames.Length == 0)
        {
            ThrowHelper.ThrowValidation($"Clip '{Id.Value}' has no frames.");
        }

        float totalDur = Duration.Value;
        if (totalDur <= 0f)
        {
            localFrameOffset = Milliseconds.Zero;
            return ref _frames[0].Frame;
        }

        float t = elapsed.Value;
        if (Loop)
        {
            t %= totalDur;
            if (t < 0f)
            {
                t += totalDur;
            }
        }
        else
        {
            if (t >= totalDur)
            {
                ref readonly SpriteAnimationFrame last = ref _frames[^1];
                localFrameOffset = last.Duration;
                return ref last.Frame;
            }

            if (t < 0f)
            {
                t = 0f;
            }
        }

        float accumulated = 0f;
        for (nuint i = 0; i < (nuint)_frames.Length; i++)
        {
            ref readonly SpriteAnimationFrame frame = ref _frames[i];
            float frameDur = frame.Duration.Value;
            if (t < accumulated + frameDur)
            {
                localFrameOffset = new Milliseconds(t - accumulated);
                return ref frame.Frame;
            }

            accumulated += frameDur;
        }

        ref readonly SpriteAnimationFrame fallback = ref _frames[^1];
        localFrameOffset = fallback.Duration;
        return ref fallback.Frame;
    }

    /// <summary>Enumerates keyframes without allocating.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FrameEnumerator GetEnumerator() => new(_frames);

    /// <summary>Zero-allocation keyframe enumerator.</summary>
    public ref struct FrameEnumerator
    {
        private readonly ReadOnlySpan<SpriteAnimationFrame> _frames;
        private int _index;

        /// <summary>Creates an enumerator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal FrameEnumerator(SpriteAnimationFrame[] frames)
        {
            _frames = frames;
            _index = -1;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int next = _index + 1;
            if (next < _frames.Length)
            {
                _index = next;
                return true;
            }

            return false;
        }

        /// <summary>Current keyframe.</summary>
        public ref readonly SpriteAnimationFrame Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _frames[_index];
        }
    }
}
