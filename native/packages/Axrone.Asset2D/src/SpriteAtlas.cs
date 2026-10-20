namespace Axrone.Asset2D;

/// <summary>
/// Hardened Sprite Atlas engine backed by a flat contiguous open-addressing linear probing hash index.
/// Provides zero-allocation hot-path queries by string nominal and ReadOnlySpan&lt;char&gt;.
/// </summary>
/// <typeparam name="TCoordPolicy">UV coordinate policy.</typeparam>
public sealed class SpriteAtlas<TCoordPolicy> : IReadOnlySpriteAtlas
    where TCoordPolicy : struct, ICoordinatePolicy
{
    /// <summary>Index slot.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private readonly struct HashSlot
    {
        /// <summary>Name hash.</summary>
        public readonly uint Hash;

        /// <summary>Target index, or -1 when empty.</summary>
        public readonly int TargetIndex;

        /// <summary>Creates a slot.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HashSlot(uint hash, int targetIndex)
        {
            Hash = hash;
            TargetIndex = targetIndex;
        }
    }

    private readonly SpriteAtlasFrame[] _frames;
    private readonly SpriteAnimationClip[] _animations;
    private readonly HashSlot[] _frameSlots;
    private readonly HashSlot[] _animationSlots;

    /// <inheritdoc/>
    public AtlasId Id { get; }

    /// <inheritdoc/>
    public TextureId TextureId { get; }

    /// <inheritdoc/>
    public Size2D TextureSize { get; }

    /// <inheritdoc/>
    public int FrameCount => _frames.Length;

    /// <inheritdoc/>
    public int AnimationCount => _animations.Length;

    private SpriteAtlas(
        AtlasId id,
        TextureId textureId,
        Size2D textureSize,
        SpriteAtlasFrame[] frames,
        SpriteAnimationClip[] animations,
        HashSlot[] frameSlots,
        HashSlot[] animationSlots)
    {
        Id = id;
        TextureId = textureId;
        TextureSize = textureSize;
        _frames = frames;
        _animations = animations;
        _frameSlots = frameSlots;
        _animationSlots = animationSlots;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryGetFrame(ReadOnlySpan<char> name, out SpriteAtlasFrame frame)
    {
        if (_frameSlots.Length == 0)
        {
            frame = default;
            return false;
        }

        uint hash = Fnv1a32Algorithm.Hash(MemoryMarshal.AsBytes(name)).Value;
        uint mask = (uint)_frameSlots.Length - 1;
        uint idx = hash & mask;

        while (true)
        {
            ref readonly HashSlot slot = ref _frameSlots[idx];
            if (slot.TargetIndex < 0)
            {
                frame = default;
                return false;
            }

            if (slot.Hash == hash)
            {
                ref readonly SpriteAtlasFrame candidate = ref _frames[slot.TargetIndex];
                if (candidate.Id.Equals(name))
                {
                    frame = candidate;
                    return true;
                }
            }

            idx = (idx + 1) & mask;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryGetFrame(FrameId id, out SpriteAtlasFrame frame) =>
        TryGetFrame(id.Value.AsSpan(), out frame);

    /// <summary>Resolves a frame by id.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public SpriteAtlasFrame? GetFrame(FrameId id) =>
        TryGetFrame(id, out SpriteAtlasFrame frame) ? frame : null;

    /// <summary>Resolves a frame by name without allocating.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public SpriteAtlasFrame? GetFrame(ReadOnlySpan<char> name) =>
        TryGetFrame(name, out SpriteAtlasFrame frame) ? frame : null;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryGetAnimation(ReadOnlySpan<char> name, [NotNullWhen(true)] out SpriteAnimationClip? animation)
    {
        if (_animationSlots.Length == 0)
        {
            animation = null;
            return false;
        }

        uint hash = Fnv1a32Algorithm.Hash(MemoryMarshal.AsBytes(name)).Value;
        uint mask = (uint)_animationSlots.Length - 1;
        uint idx = hash & mask;

        while (true)
        {
            ref readonly HashSlot slot = ref _animationSlots[idx];
            if (slot.TargetIndex < 0)
            {
                animation = null;
                return false;
            }

            if (slot.Hash == hash)
            {
                SpriteAnimationClip candidate = _animations[slot.TargetIndex];
                if (candidate.Id.Equals(name))
                {
                    animation = candidate;
                    return true;
                }
            }

            idx = (idx + 1) & mask;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryGetAnimation(ClipId id, [NotNullWhen(true)] out SpriteAnimationClip? animation) =>
        TryGetAnimation(id.Value.AsSpan(), out animation);

    /// <summary>Resolves a clip by id.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public SpriteAnimationClip? GetAnimation(ClipId id) =>
        TryGetAnimation(id, out SpriteAnimationClip? anim) ? anim : null;

    /// <summary>Resolves a clip by name without allocating.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public SpriteAnimationClip? GetAnimation(ReadOnlySpan<char> name) =>
        TryGetAnimation(name, out SpriteAnimationClip? anim) ? anim : null;

    /// <summary>Enumerates frames without allocating.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FrameEnumerator GetEnumerator() => new(_frames);

    /// <summary>Enumerates animations without allocating.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AnimationEnumerator EnumerateAnimations() => new(_animations);

    /// <summary>Zero-allocation frame enumerator.</summary>
    public ref struct FrameEnumerator
    {
        private readonly ReadOnlySpan<SpriteAtlasFrame> _frames;
        private int _index;

        /// <summary>Creates an enumerator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal FrameEnumerator(SpriteAtlasFrame[] frames)
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

        /// <summary>Current frame.</summary>
        public ref readonly SpriteAtlasFrame Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _frames[_index];
        }
    }

    /// <summary>Zero-allocation animation enumerator.</summary>
    public ref struct AnimationEnumerator
    {
        private readonly ReadOnlySpan<SpriteAnimationClip> _animations;
        private int _index;

        /// <summary>Creates an enumerator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AnimationEnumerator(SpriteAnimationClip[] animations)
        {
            _animations = animations;
            _index = -1;
        }

        /// <summary>Returns this enumerator for foreach.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AnimationEnumerator GetEnumerator() => this;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int next = _index + 1;
            if (next < _animations.Length)
            {
                _index = next;
                return true;
            }

            return false;
        }

        /// <summary>Current clip.</summary>
        public SpriteAnimationClip Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _animations[_index];
        }
    }

    internal static SpriteAtlas<TCoordPolicy> CreateInternal(AtlasBuilderState state)
    {
        if (state.Frames.Count == 0)
        {
            ThrowHelper.ThrowValidation("Atlas must contain at least one frame definition.");
        }

        float texW = state.TextureSize.Width.Value;
        float texH = state.TextureSize.Height.Value;
        const float Epsilon = 1e-4f;

        var resolvedFrames = new SpriteAtlasFrame[state.Frames.Count];
        Span<SpriteFrameDefinition> frameSpan = CollectionsMarshal.AsSpan(state.Frames);

        for (int i = 0; i < frameSpan.Length; i++)
        {
            ref readonly SpriteFrameDefinition fDef = ref frameSpan[i];

            if (!float.IsFinite(fDef.Region.X.Value) || fDef.Region.X.Value < 0f ||
                !float.IsFinite(fDef.Region.Y.Value) || fDef.Region.Y.Value < 0f)
            {
                ThrowHelper.ThrowValidation($"Frame '{fDef.Id.Value}' coordinates must be finite and >= 0. Got: ({fDef.Region.X}, {fDef.Region.Y}).");
            }

            if (!float.IsFinite(fDef.Region.Width.Value) || fDef.Region.Width.Value <= 0f ||
                !float.IsFinite(fDef.Region.Height.Value) || fDef.Region.Height.Value <= 0f)
            {
                ThrowHelper.ThrowValidation($"Frame '{fDef.Id.Value}' size must be positive and finite. Got: [{fDef.Region.Width}x{fDef.Region.Height}].");
            }

            if ((fDef.Region.X.Value + fDef.Region.Width.Value) > (texW + Epsilon) ||
                (fDef.Region.Y.Value + fDef.Region.Height.Value) > (texH + Epsilon))
            {
                ThrowHelper.ThrowValidation($"Frame '{fDef.Id.Value}' bounds [{fDef.Region}] exceed texture boundaries [{texW}x{texH}].");
            }

            Size2D sourceSize;
            if (fDef.SourceSize.HasValue)
            {
                Size2D ss = fDef.SourceSize.Value;
                if (!float.IsFinite(ss.Width.Value) || ss.Width.Value <= 0f || !float.IsFinite(ss.Height.Value) || ss.Height.Value <= 0f)
                {
                    ThrowHelper.ThrowValidation($"Frame '{fDef.Id.Value}' sourceSize must be positive and finite. Got: {ss}.");
                }

                sourceSize = ss;
            }
            else
            {
                sourceSize = fDef.Region.Size;
            }

            Vec2 pivot;
            if (fDef.Pivot.HasValue)
            {
                Vec2 pv = fDef.Pivot.Value;
                if (!float.IsFinite(pv.X) || !float.IsFinite(pv.Y))
                {
                    ThrowHelper.ThrowValidation($"Frame '{fDef.Id.Value}' pivot components must be finite numbers. Got: ({pv.X}, {pv.Y}).");
                }

                pivot = pv;
            }
            else
            {
                pivot = new Vec2(0.5f, 0.5f);
            }

            Border2D sliceBorder = Border2D.Zero;
            SpriteFrameAttributes flags = SpriteFrameAttributes.None;

            if (fDef.Rotated)
            {
                flags |= SpriteFrameAttributes.Rotated;
            }

            if (fDef.SliceBorder.HasValue)
            {
                Border2D sb = fDef.SliceBorder.Value;
                if (!float.IsFinite(sb.Left.Value) || sb.Left.Value < 0f ||
                    !float.IsFinite(sb.Right.Value) || sb.Right.Value < 0f ||
                    !float.IsFinite(sb.Top.Value) || sb.Top.Value < 0f ||
                    !float.IsFinite(sb.Bottom.Value) || sb.Bottom.Value < 0f)
                {
                    ThrowHelper.ThrowValidation($"Frame '{fDef.Id.Value}' sliceBorder must be finite and >= 0. Got: {sb}.");
                }

                sliceBorder = sb;
                flags |= SpriteFrameAttributes.HasBorder;
            }

            Milliseconds duration = Milliseconds.Zero;
            if (fDef.Duration.HasValue)
            {
                float dur = fDef.Duration.Value.Value;
                if (!float.IsFinite(dur) || dur <= 0f)
                {
                    ThrowHelper.ThrowValidation($"Frame '{fDef.Id.Value}' duration must be positive and finite. Got: {dur}.");
                }

                duration = fDef.Duration.Value;
                flags |= SpriteFrameAttributes.HasDuration;
            }

            Vec2 trimOffset = Vec2.Zero;
            if (fDef.TrimOffset.HasValue)
            {
                trimOffset = fDef.TrimOffset.Value;
                flags |= SpriteFrameAttributes.HasTrim;
            }

            UvRect uvRect = TCoordPolicy.ComputeUv(fDef.Region, state.TextureSize);

            resolvedFrames[i] = new SpriteAtlasFrame(
                fDef.Id,
                state.TextureId,
                fDef.Region,
                uvRect,
                sourceSize,
                pivot,
                trimOffset,
                sliceBorder,
                duration,
                flags);
        }

        int frameCapacity = Math.Max(16, (int)BitOperations.RoundUpToPowerOf2((uint)(resolvedFrames.Length * 2)));
        var frameSlots = new HashSlot[frameCapacity];
        Array.Fill(frameSlots, new HashSlot(0, -1));
        uint frameMask = (uint)frameCapacity - 1;

        for (int i = 0; i < resolvedFrames.Length; i++)
        {
            uint hash = Fnv1a32Algorithm.Hash(MemoryMarshal.AsBytes(resolvedFrames[i].Id.Value.AsSpan())).Value;
            uint idx = hash & frameMask;
            while (frameSlots[idx].TargetIndex >= 0)
            {
                idx = (idx + 1) & frameMask;
            }

            frameSlots[idx] = new HashSlot(hash, i);
        }

        SpriteAnimationClip[] resolvedAnimations;
        HashSlot[] animationSlots;

        if (state.Animations.Count > 0)
        {
            resolvedAnimations = new SpriteAnimationClip[state.Animations.Count];
            Span<SpriteClipDefinition> animSpan = CollectionsMarshal.AsSpan(state.Animations);

            for (int i = 0; i < animSpan.Length; i++)
            {
                ref readonly SpriteClipDefinition aDef = ref animSpan[i];
                float fps = aDef.Fps ?? 12.0f;
                if (!float.IsFinite(fps))
                {
                    fps = 12.0f;
                }

                Milliseconds fallbackDuration = new(1000.0f / Math.Max(1.0f, fps));
                var clipFrames = new SpriteAnimationFrame[aDef.Frames.Length];
                float totalClipDuration = 0f;

                for (int j = 0; j < aDef.Frames.Length; j++)
                {
                    SpriteClipRefDefinition aRef = aDef.Frames[j];

                    uint refHash = Fnv1a32Algorithm.Hash(MemoryMarshal.AsBytes(aRef.FrameId.Value.AsSpan())).Value;
                    uint sIdx = refHash & frameMask;
                    int foundIdx = -1;

                    while (true)
                    {
                        ref readonly HashSlot slot = ref frameSlots[sIdx];
                        if (slot.TargetIndex < 0)
                        {
                            break;
                        }

                        if (slot.Hash == refHash && resolvedFrames[slot.TargetIndex].Id.Equals(aRef.FrameId.Value.AsSpan()))
                        {
                            foundIdx = slot.TargetIndex;
                            break;
                        }

                        sIdx = (sIdx + 1) & frameMask;
                    }

                    if (foundIdx < 0)
                    {
                        ThrowHelper.ThrowValidation($"Animation '{aDef.Id.Value}' references non-existent frame '{aRef.FrameId.Value}'.");
                    }

                    ref readonly SpriteAtlasFrame targetFrame = ref resolvedFrames[foundIdx];

                    if (aRef.Duration.HasValue)
                    {
                        float refDur = aRef.Duration.Value.Value;
                        if (!float.IsFinite(refDur) || refDur <= 0f)
                        {
                            ThrowHelper.ThrowValidation($"Animation '{aDef.Id.Value}' frame ref '{aRef.FrameId.Value}' duration must be positive and finite. Got: {refDur}.");
                        }
                    }

                    Milliseconds resolvedDuration = aRef.Duration ?? (targetFrame.HasDuration ? targetFrame.Duration : fallbackDuration);
                    totalClipDuration += resolvedDuration.Value;
                    clipFrames[j] = new SpriteAnimationFrame(targetFrame, resolvedDuration);
                }

                resolvedAnimations[i] = new SpriteAnimationClip(
                    aDef.Id,
                    clipFrames,
                    new Milliseconds(totalClipDuration),
                    aDef.Loop ?? true);
            }

            int animCapacity = Math.Max(16, (int)BitOperations.RoundUpToPowerOf2((uint)(resolvedAnimations.Length * 2)));
            animationSlots = new HashSlot[animCapacity];
            Array.Fill(animationSlots, new HashSlot(0, -1));
            uint animMask = (uint)animCapacity - 1;

            for (int i = 0; i < resolvedAnimations.Length; i++)
            {
                uint hash = Fnv1a32Algorithm.Hash(MemoryMarshal.AsBytes(resolvedAnimations[i].Id.Value.AsSpan())).Value;
                uint idx = hash & animMask;
                while (animationSlots[idx].TargetIndex >= 0)
                {
                    idx = (idx + 1) & animMask;
                }

                animationSlots[idx] = new HashSlot(hash, i);
            }
        }
        else
        {
            resolvedAnimations = [];
            animationSlots = [];
        }

        return new SpriteAtlas<TCoordPolicy>(
            state.Id,
            state.TextureId,
            state.TextureSize,
            resolvedFrames,
            resolvedAnimations,
            frameSlots,
            animationSlots);
    }
}

