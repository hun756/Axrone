namespace Axrone.Animation;

/// <summary>Blend-tree motion kind.</summary>
public enum MotionKind
{
    /// <summary>Single clip.</summary>
    Clip = 0,

    /// <summary>One-parameter blend.</summary>
    Blend1D = 1,

    /// <summary>Two-parameter blend.</summary>
    Blend2D = 2,

    /// <summary>Per-child weight blend.</summary>
    Direct = 3,

    /// <summary>Rest-relative additive layer.</summary>
    Additive = 4,
}

/// <summary>Evaluatable motion node: clips and blends share one recursive contract.</summary>
public abstract class MotionNode
{
    /// <summary>Node kind.</summary>
    public abstract MotionKind Kind { get; }

    /// <summary>Playback rate multiplier.</summary>
    public float TimeScale { get; init; } = 1.0f;

    /// <summary>Cycle offset in normalized time.</summary>
    public float CycleOffset { get; init; }

    /// <summary>Wall-clock duration at unit speed.</summary>
    public abstract float GetDuration();

    /// <summary>Evaluates into a frame; depth guards recursion.</summary>
    public abstract void Evaluate(float normalizedTime, AnimationFrame outFrame, FrameArena arena, Rig rig, ParameterStore parameters, int depth);

    /// <summary>Root-joint delta between two normalized times.</summary>
    public abstract void ComputeRootDelta(float prevNormTime, float curNormTime, Rig rig, out Vector3 deltaPos, out Quaternion deltaRot);

    /// <summary>Collects clip events in the interval, scaled by layer weight.</summary>
    public abstract void CollectEvents(float prevNormTime, float curNormTime, float layerWeight, ICollection<ClipEvent> outEvents);
}

/// <summary>Leaf node: samples one clip with time scale and cycle offset honored.</summary>
public sealed class ClipMotionNode : MotionNode
{
    /// <inheritdoc/>
    public override MotionKind Kind => MotionKind.Clip;

    /// <summary>Sampled clip.</summary>
    public AnimationClip Clip { get; }

    /// <summary>Whether time wraps at the clip end.</summary>
    public bool IsLooping { get; init; } = true;

    /// <summary>Creates a leaf.</summary>
    public ClipMotionNode(AnimationClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        Clip = clip;
    }

    /// <inheritdoc/>
    public override float GetDuration()
    {
        float speed = MathF.Abs(TimeScale);
        return speed > AnimationConstants.SoaEpsilon ? Clip.Duration / speed : Clip.Duration;
    }

    /// <inheritdoc/>
    public override void Evaluate(float normalizedTime, AnimationFrame outFrame, FrameArena arena, Rig rig, ParameterStore parameters, int depth)
    {
        float scaled = (normalizedTime * TimeScale) + CycleOffset;
        float time = IsLooping
            ? FastMath.WrapTime(scaled * Clip.Duration, Clip.Duration)
            : Math.Clamp(scaled * Clip.Duration, 0.0f, Clip.Duration);
        Clip.Sample(time, outFrame, IsLooping);
    }

    /// <inheritdoc/>
    [SkipLocalsInit]
    public override void ComputeRootDelta(float prevNormTime, float curNormTime, Rig rig, out Vector3 deltaPos, out Quaternion deltaRot)
    {
        float duration = Clip.Duration;
        if (duration <= AnimationConstants.SoaEpsilon || Clip.Channels.Length == 0)
        {
            deltaPos = Vector3.Zero;
            deltaRot = Quaternion.Identity;
            return;
        }

        float t0 = MapTime(prevNormTime, duration);
        float t1 = MapTime(curNormTime, duration);

        Span<float> buf0 = stackalloc float[4];
        Span<float> buf1 = stackalloc float[4];
        Span<float> bufEnd = stackalloc float[4];
        Span<float> bufStart = stackalloc float[4];

        Vector3 p0 = Vector3.Zero;
        Vector3 p1 = Vector3.Zero;
        Quaternion r0 = Quaternion.Identity;
        Quaternion r1 = Quaternion.Identity;

        foreach (AnimationChannel channel in Clip.Channels)
        {
            if (channel.BoneIndex != 0)
            {
                continue;
            }

            if (channel.Target == ChannelTarget.Translation)
            {
                channel.Sample(t0, buf0);
                p0 = new Vector3(buf0[0], buf0[1], buf0[2]);
                channel.Sample(t1, buf1);
                p1 = new Vector3(buf1[0], buf1[1], buf1[2]);
            }
            else if (channel.Target == ChannelTarget.Rotation)
            {
                channel.Sample(t0, buf0);
                r0 = new Quaternion(buf0[0], buf0[1], buf0[2], buf0[3]);
                channel.Sample(t1, buf1);
                r1 = new Quaternion(buf1[0], buf1[1], buf1[2], buf1[3]);
            }
        }

        deltaPos = p1 - p0;
        deltaRot = Quaternion.Normalize(r1 * Quaternion.Inverse(r0));
        if (t1 >= t0)
        {
            return;
        }

        foreach (AnimationChannel channel in Clip.Channels)
        {
            if (channel.BoneIndex != 0)
            {
                continue;
            }

            if (channel.Target == ChannelTarget.Translation)
            {
                channel.Sample(duration, bufEnd);
                channel.Sample(0.0f, bufStart);
                Vector3 pEnd = new(bufEnd[0], bufEnd[1], bufEnd[2]);
                Vector3 pStart = new(bufStart[0], bufStart[1], bufStart[2]);
                deltaPos = (pEnd - p0) + (p1 - pStart);
            }
            else if (channel.Target == ChannelTarget.Rotation)
            {
                channel.Sample(duration, bufEnd);
                channel.Sample(0.0f, bufStart);
                Quaternion rEnd = new(bufEnd[0], bufEnd[1], bufEnd[2], bufEnd[3]);
                Quaternion rStart = new(bufStart[0], bufStart[1], bufStart[2], bufStart[3]);
                deltaRot = Quaternion.Normalize((rEnd * Quaternion.Inverse(r0)) * (r1 * Quaternion.Inverse(rStart)));
            }
        }
    }

    /// <inheritdoc/>
    public override void CollectEvents(float prevNormTime, float curNormTime, float layerWeight, ICollection<ClipEvent> outEvents)
    {
        ArgumentNullException.ThrowIfNull(outEvents);
        if (layerWeight <= 0.0f)
        {
            return;
        }

        float duration = Clip.Duration;
        float t0 = MapTime(prevNormTime, duration);
        float t1 = MapTime(curNormTime, duration);
        Clip.CollectEvents(t0, t1, outEvents);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float MapTime(float normalizedTime, float duration)
    {
        float scaled = (normalizedTime * TimeScale) + CycleOffset;
        float time = scaled * duration;
        return IsLooping ? FastMath.WrapTime(time, duration) : Math.Clamp(time, 0.0f, duration);
    }
}

/// <summary>Segment bracketing a Blend1D parameter value.</summary>
public readonly record struct Blend1DSegment(int Index0, int Index1, float Weight);

/// <summary>One-parameter blend over sorted threshold children.</summary>
public sealed class Blend1DMotionNode : MotionNode
{
    /// <inheritdoc/>
    public override MotionKind Kind => MotionKind.Blend1D;

    private readonly float[] _thresholds;
    private readonly MotionNode[] _children;

    /// <summary>Driving parameter name.</summary>
    public string ParameterName { get; }

    /// <summary>Sorted thresholds.</summary>
    public ReadOnlySpan<float> Thresholds => _thresholds;

    /// <summary>Children aligned with thresholds.</summary>
    public ReadOnlySpan<MotionNode> Children => _children;

    /// <summary>Creates a blend; entries sort by threshold.</summary>
    public Blend1DMotionNode(string parameterName, (float Threshold, MotionNode Child)[] entries)
    {
        ArgumentNullException.ThrowIfNull(parameterName);
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Length == 0)
        {
            AnimationThrowHelper.ThrowCompilation(AnimationErrorCode.CompilationEmptyChildren, "Blend1D requires at least one child.");
        }

        Array.Sort(entries, static (a, b) => a.Threshold.CompareTo(b.Threshold));
        ParameterName = parameterName;
        _thresholds = new float[entries.Length];
        _children = new MotionNode[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            _thresholds[i] = entries[i].Threshold;
            _children[i] = entries[i].Child;
        }
    }

    /// <summary>Finds the bracketing segment: linear scan for few children, binary beyond.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Blend1DSegment FindSegment(float paramValue)
    {
        int length = _thresholds.Length;
        if (length == 1 || paramValue <= _thresholds[0])
        {
            return new Blend1DSegment(0, 0, 1.0f);
        }

        if (paramValue >= _thresholds[length - 1])
        {
            return new Blend1DSegment(length - 1, length - 1, 1.0f);
        }

        int index0 = 0;
        if (length <= AnimationConstants.Blend1DLinearScanLimit)
        {
            for (int i = 0; i < length - 1; i++)
            {
                if (paramValue <= _thresholds[i + 1])
                {
                    index0 = i;
                    break;
                }
            }
        }
        else
        {
            int probe = Array.BinarySearch(_thresholds, paramValue);
            index0 = probe >= 0 ? probe : ~probe - 1;
        }

        int index1 = index0 + 1;
        float span = _thresholds[index1] - _thresholds[index0];
        float weight = span > AnimationConstants.SoaEpsilon ? (paramValue - _thresholds[index0]) / span : 0.0f;
        return new Blend1DSegment(index0, index1, FastMath.Clamp01(weight));
    }

    /// <inheritdoc/>
    public override float GetDuration()
    {
        if (Children.Length == 0)
        {
            return 0.0f;
        }

        float sum = 0.0f;
        for (int i = 0; i < Children.Length; i++)
        {
            sum += Children[i].GetDuration();
        }

        return sum / Children.Length;
    }

    /// <inheritdoc/>
    public override void Evaluate(float normalizedTime, AnimationFrame outFrame, FrameArena arena, Rig rig, ParameterStore parameters, int depth)
    {
        ArgumentNullException.ThrowIfNull(outFrame);
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(parameters);
        if (depth >= AnimationConstants.MaxBlendDepth)
        {
            AnimationThrowHelper.ThrowEvaluation(AnimationErrorCode.EvaluationDepthOverflow, "Maximum blend recursion depth exceeded.");
        }

        float paramValue = parameters.GetFloat(ParameterName);
        Blend1DSegment segment = FindSegment(paramValue);

        if (segment.Index0 == segment.Index1 || segment.Weight <= 0.0f)
        {
            Children[segment.Index0].Evaluate(normalizedTime, outFrame, arena, rig, parameters, depth + 1);
            return;
        }

        if (segment.Weight >= 1.0f)
        {
            Children[segment.Index1].Evaluate(normalizedTime, outFrame, arena, rig, parameters, depth + 1);
            return;
        }

        AnimationFrame frame0 = arena.Alloc();
        AnimationFrame frame1 = arena.Alloc();

        Children[segment.Index0].Evaluate(normalizedTime, frame0, arena, rig, parameters, depth + 1);
        Children[segment.Index1].Evaluate(normalizedTime, frame1, arena, rig, parameters, depth + 1);

        BlendingKernels.BlendFrame(outFrame, frame0, frame1, segment.Weight);

        arena.Free();
        arena.Free();
    }

    /// <inheritdoc/>
    public override void ComputeRootDelta(float prevNormTime, float curNormTime, Rig rig, out Vector3 deltaPos, out Quaternion deltaRot)
    {
        if (Children.Length == 0)
        {
            deltaPos = Vector3.Zero;
            deltaRot = Quaternion.Identity;
            return;
        }

        Children[0].ComputeRootDelta(prevNormTime, curNormTime, rig, out deltaPos, out deltaRot);
    }

    /// <inheritdoc/>
    public override void CollectEvents(float prevNormTime, float curNormTime, float layerWeight, ICollection<ClipEvent> outEvents)
    {
        ArgumentNullException.ThrowIfNull(outEvents);
        if (layerWeight <= 0.0f)
        {
            return;
        }

        for (int i = 0; i < Children.Length; i++)
        {
            Children[i].CollectEvents(prevNormTime, curNormTime, layerWeight, outEvents);
        }
    }
}

/// <summary>Two-parameter inverse-distance blend over positioned children.</summary>
public sealed class Blend2DMotionNode : MotionNode
{
    private readonly Vector2[] _positions;
    private readonly MotionNode[] _children;

    /// <inheritdoc/>
    public override MotionKind Kind => MotionKind.Blend2D;

    /// <summary>X driving parameter.</summary>
    public string ParameterX { get; }

    /// <summary>Y driving parameter.</summary>
    public string ParameterY { get; }

    /// <summary>Child positions in blend space.</summary>
    public ReadOnlySpan<Vector2> Positions => _positions;

    /// <summary>Children aligned with positions.</summary>
    public ReadOnlySpan<MotionNode> Children => _children;

    /// <summary>Creates a blend.</summary>
    public Blend2DMotionNode(string parameterX, string parameterY, (Vector2 Position, MotionNode Child)[] entries)
    {
        ArgumentNullException.ThrowIfNull(parameterX);
        ArgumentNullException.ThrowIfNull(parameterY);
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Length == 0)
        {
            AnimationThrowHelper.ThrowCompilation(AnimationErrorCode.CompilationEmptyChildren, "Blend2D requires at least one child.");
        }

        ParameterX = parameterX;
        ParameterY = parameterY;
        _positions = new Vector2[entries.Length];
        _children = new MotionNode[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            _positions[i] = entries[i].Position;
            _children[i] = entries[i].Child;
        }
    }

    /// <inheritdoc/>
    public override float GetDuration()
    {
        if (_children.Length == 0)
        {
            return 0.0f;
        }

        float sum = 0.0f;
        for (int i = 0; i < _children.Length; i++)
        {
            sum += _children[i].GetDuration();
        }

        return sum / _children.Length;
    }

    /// <inheritdoc/>
    [SkipLocalsInit]
    public override void Evaluate(float normalizedTime, AnimationFrame outFrame, FrameArena arena, Rig rig, ParameterStore parameters, int depth)
    {
        ArgumentNullException.ThrowIfNull(outFrame);
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(parameters);
        if (depth >= AnimationConstants.MaxBlendDepth)
        {
            AnimationThrowHelper.ThrowEvaluation(AnimationErrorCode.EvaluationDepthOverflow, "Maximum blend recursion depth exceeded.");
        }

        Vector2 input = new(parameters.GetFloat(ParameterX), parameters.GetFloat(ParameterY));
        Span<float> weights = stackalloc float[_positions.Length];

        for (int i = 0; i < _positions.Length; i++)
        {
            float distanceSq = Vector2.DistanceSquared(input, _positions[i]);
            if (distanceSq <= AnimationConstants.BlendDistanceEpsilonSq)
            {
                _children[i].Evaluate(normalizedTime, outFrame, arena, rig, parameters, depth + 1);
                return;
            }

            weights[i] = 1.0f / MathF.Sqrt(distanceSq);
        }

        AnimationFrame[] scratchFrames = ArrayPool<AnimationFrame>.Shared.Rent(_children.Length);
        try
        {
            for (int i = 0; i < _children.Length; i++)
            {
                scratchFrames[i] = arena.Alloc();
                _children[i].Evaluate(normalizedTime, scratchFrames[i], arena, rig, parameters, depth + 1);
            }

            BlendingKernels.BlendWeightedFrames(outFrame, scratchFrames.AsSpan(0, _children.Length), weights, rig);

            for (int i = 0; i < _children.Length; i++)
            {
                arena.Free();
            }
        }
        finally
        {
            ArrayPool<AnimationFrame>.Shared.Return(scratchFrames);
        }
    }

    /// <inheritdoc/>
    public override void ComputeRootDelta(float prevNormTime, float curNormTime, Rig rig, out Vector3 deltaPos, out Quaternion deltaRot)
    {
        if (_children.Length == 0)
        {
            deltaPos = Vector3.Zero;
            deltaRot = Quaternion.Identity;
            return;
        }

        _children[0].ComputeRootDelta(prevNormTime, curNormTime, rig, out deltaPos, out deltaRot);
    }

    /// <inheritdoc/>
    public override void CollectEvents(float prevNormTime, float curNormTime, float layerWeight, ICollection<ClipEvent> outEvents)
    {
        ArgumentNullException.ThrowIfNull(outEvents);
        if (layerWeight <= 0.0f)
        {
            return;
        }

        for (int i = 0; i < _children.Length; i++)
        {
            _children[i].CollectEvents(prevNormTime, curNormTime, layerWeight, outEvents);
        }
    }
}

/// <summary>Per-child parameter-weighted blend with single-active fast path.</summary>
public sealed class DirectMotionNode : MotionNode
{
    private readonly string[] _parameterNames;
    private readonly MotionNode[] _children;

    /// <inheritdoc/>
    public override MotionKind Kind => MotionKind.Direct;

    /// <summary>Driving parameter per child.</summary>
    public ReadOnlySpan<string> ParameterNames => _parameterNames;

    /// <summary>Children aligned with parameters.</summary>
    public ReadOnlySpan<MotionNode> Children => _children;

    /// <summary>Creates a blend.</summary>
    public DirectMotionNode((string Parameter, MotionNode Child)[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Length == 0)
        {
            AnimationThrowHelper.ThrowCompilation(AnimationErrorCode.CompilationEmptyChildren, "Direct blend requires at least one child.");
        }

        _parameterNames = new string[entries.Length];
        _children = new MotionNode[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            _parameterNames[i] = entries[i].Parameter;
            _children[i] = entries[i].Child;
        }
    }

    /// <inheritdoc/>
    public override float GetDuration()
    {
        float max = 0.0f;
        for (int i = 0; i < _children.Length; i++)
        {
            max = MathF.Max(max, _children[i].GetDuration());
        }

        return max;
    }

    /// <inheritdoc/>
    [SkipLocalsInit]
    public override void Evaluate(float normalizedTime, AnimationFrame outFrame, FrameArena arena, Rig rig, ParameterStore parameters, int depth)
    {
        ArgumentNullException.ThrowIfNull(outFrame);
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(parameters);
        if (depth >= AnimationConstants.MaxBlendDepth)
        {
            AnimationThrowHelper.ThrowEvaluation(AnimationErrorCode.EvaluationDepthOverflow, "Maximum blend recursion depth exceeded.");
        }

        Span<float> weights = stackalloc float[_children.Length];
        int activeCount = 0;
        int singleIndex = -1;

        for (int i = 0; i < _children.Length; i++)
        {
            float w = MathF.Max(0.0f, parameters.GetFloat(_parameterNames[i]));
            weights[i] = w;
            if (w > 0.0f)
            {
                activeCount++;
                singleIndex = i;
            }
        }

        if (activeCount == 0)
        {
            outFrame.ResetToRest(rig);
            return;
        }

        if (activeCount == 1)
        {
            _children[singleIndex].Evaluate(normalizedTime, outFrame, arena, rig, parameters, depth + 1);
            return;
        }

        AnimationFrame[] scratchFrames = ArrayPool<AnimationFrame>.Shared.Rent(_children.Length);
        try
        {
            for (int i = 0; i < _children.Length; i++)
            {
                scratchFrames[i] = arena.Alloc();
                _children[i].Evaluate(normalizedTime, scratchFrames[i], arena, rig, parameters, depth + 1);
            }

            BlendingKernels.BlendWeightedFrames(outFrame, scratchFrames.AsSpan(0, _children.Length), weights, rig);

            for (int i = 0; i < _children.Length; i++)
            {
                arena.Free();
            }
        }
        finally
        {
            ArrayPool<AnimationFrame>.Shared.Return(scratchFrames);
        }
    }

    /// <inheritdoc/>
    public override void ComputeRootDelta(float prevNormTime, float curNormTime, Rig rig, out Vector3 deltaPos, out Quaternion deltaRot)
    {
        if (_children.Length == 0)
        {
            deltaPos = Vector3.Zero;
            deltaRot = Quaternion.Identity;
            return;
        }

        _children[0].ComputeRootDelta(prevNormTime, curNormTime, rig, out deltaPos, out deltaRot);
    }

    /// <inheritdoc/>
    public override void CollectEvents(float prevNormTime, float curNormTime, float layerWeight, ICollection<ClipEvent> outEvents)
    {
        ArgumentNullException.ThrowIfNull(outEvents);
        if (layerWeight <= 0.0f)
        {
            return;
        }

        for (int i = 0; i < _children.Length; i++)
        {
            _children[i].CollectEvents(prevNormTime, curNormTime, layerWeight, outEvents);
        }
    }
}
