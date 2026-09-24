namespace Axrone.Animation;

/// <summary>Keyframe interpolation mode.</summary>
public enum InterpolationMode
{
    /// <summary>Linear blend (slerp for rotations).</summary>
    Linear = 0,

    /// <summary>Hold previous key.</summary>
    Step = 1,

    /// <summary>Hermite spline (in-tangent/value/out-tangent triples).</summary>
    CubicSpline = 2,
}

/// <summary>What a channel drives.</summary>
public enum ChannelTarget
{
    /// <summary>Bone translation (3 lanes).</summary>
    Translation = 0,

    /// <summary>Bone rotation (4 lanes).</summary>
    Rotation = 1,

    /// <summary>Bone scale (3 lanes).</summary>
    Scale = 2,

    /// <summary>Named curve (1 lane).</summary>
    Curve = 3,
}

/// <summary>One animated track: key times plus packed key values.</summary>
public sealed class AnimationChannel
{
    private readonly float[] _keyTimes;
    private readonly float[] _keyValues;

    /// <summary>Target bone index.</summary>
    public int BoneIndex { get; }

    /// <summary>What the channel drives.</summary>
    public ChannelTarget Target { get; }

    /// <summary>Interpolation mode.</summary>
    public InterpolationMode Interpolation { get; }

    /// <summary>Key times, ascending.</summary>
    public ReadOnlySpan<float> KeyTimes => _keyTimes;

    /// <summary>Packed key values.</summary>
    public ReadOnlySpan<float> KeyValues => _keyValues;

    /// <summary>Floats per key (×3 for splines holding tangents).</summary>
    public int Stride { get; }

    /// <summary>Target curve for curve channels.</summary>
    public CurveId? TargetCurveId { get; }

    /// <summary>Creates a channel; validates the time/value packing.</summary>
    public AnimationChannel(int boneIndex, ChannelTarget target, InterpolationMode interpolation, float[] times, float[] values, CurveId? curveId = null)
    {
        ArgumentNullException.ThrowIfNull(times);
        ArgumentNullException.ThrowIfNull(values);

        BoneIndex = boneIndex;
        Target = target;
        Interpolation = interpolation;
        _keyTimes = times;
        _keyValues = values;
        TargetCurveId = curveId;

        int componentCount = target switch
        {
            ChannelTarget.Translation => 3,
            ChannelTarget.Rotation => 4,
            ChannelTarget.Scale => 3,
            ChannelTarget.Curve => 1,
            _ => 3,
        };

        Stride = interpolation == InterpolationMode.CubicSpline ? componentCount * 3 : componentCount;

        if (times.Length * Stride != values.Length)
        {
            AnimationThrowHelper.ThrowValidation(AnimationErrorCode.ValidationClipMismatch, $"Channel stride mismatch: times({times.Length}) * stride({Stride}) != values({values.Length}).");
        }
    }
}
