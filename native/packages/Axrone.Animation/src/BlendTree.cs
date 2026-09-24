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
