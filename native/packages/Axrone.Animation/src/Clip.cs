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

    /// <summary>Samples the channel at a time inside its range.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sample(float time, Span<float> output)
    {
        ReadOnlySpan<float> times = _keyTimes;
        ReadOnlySpan<float> values = _keyValues;
        int count = times.Length;
        if (count == 0)
        {
            return;
        }

        if (count == 1 || time <= times[0])
        {
            values.Slice(0, Stride).CopyTo(output);
            return;
        }

        if (time >= times[count - 1])
        {
            values.Slice((count - 1) * Stride, Stride).CopyTo(output);
            return;
        }

        int segment = FindSegment(times, time);
        float t0 = times[segment];
        float t1 = times[segment + 1];
        float factor = (time - t0) / MathF.Max(t1 - t0, AnimationConstants.SoaEpsilon);

        if (Interpolation == InterpolationMode.Step)
        {
            values.Slice(segment * Stride, Stride).CopyTo(output);
            return;
        }

        if (Interpolation == InterpolationMode.Linear)
        {
            int off0 = segment * Stride;
            int off1 = (segment + 1) * Stride;
            if (Target == ChannelTarget.Rotation && Stride == 4)
            {
                Quaternion q0 = new(values[off0], values[off0 + 1], values[off0 + 2], values[off0 + 3]);
                Quaternion q1 = new(values[off1], values[off1 + 1], values[off1 + 2], values[off1 + 3]);
                Quaternion result = FastMath.Slerp(q0, q1, factor);
                output[0] = result.X;
                output[1] = result.Y;
                output[2] = result.Z;
                output[3] = result.W;
            }
            else
            {
                for (int c = 0; c < Stride; c++)
                {
                    output[c] = (values[off0 + c] * (1.0f - factor)) + (values[off1 + c] * factor);
                }
            }

            return;
        }

        SampleCubic(values, segment, factor, t1 - t0, output);
    }

    /// <summary>
    /// Hermite spline over glTF-layout triples (in-tangent/value/out-tangent per key).
    /// Rotations renormalize after blending.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SampleCubic(ReadOnlySpan<float> values, int segment, float factor, float dt, Span<float> output)
    {
        int compCount = Stride / 3;
        float t2 = factor * factor;
        float t3 = t2 * factor;
        float h00 = (2.0f * t3) - (3.0f * t2) + 1.0f;
        float h10 = t3 - (2.0f * t2) + factor;
        float h01 = (-2.0f * t3) + (3.0f * t2);
        float h11 = t3 - t2;

        int off0 = segment * Stride;
        int off1 = (segment + 1) * Stride;

        for (int c = 0; c < compCount; c++)
        {
            float p0 = values[off0 + compCount + c];
            float m0 = values[off0 + (2 * compCount) + c] * dt;
            float p1 = values[off1 + compCount + c];
            float m1 = values[off1 + c] * dt;
            output[c] = (h00 * p0) + (h10 * m0) + (h01 * p1) + (h11 * m1);
        }

        if (Target == ChannelTarget.Rotation && compCount == 4)
        {
            Quaternion q = FastMath.Normalize(new Quaternion(output[0], output[1], output[2], output[3]));
            output[0] = q.X;
            output[1] = q.Y;
            output[2] = q.Z;
            output[3] = q.W;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindSegment(ReadOnlySpan<float> times, float time)
    {
        int low = 0;
        int high = times.Length - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            if (times[mid] <= time)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return Math.Clamp(low - 1, 0, times.Length - 2);
    }
}

/// <summary>Timed named marker fired during playback.</summary>
public readonly record struct ClipEvent(float Time, string Name, string Payload);

/// <summary>Foot-ground contact window with a ramped weight.</summary>
public readonly record struct FootContact(float StartTime, float EndTime, int BoneIndex)
{
    /// <summary>Contact weight at a time: ramps at edges, floored inside.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float EvaluateWeight(float time)
    {
        if (time < StartTime || time > EndTime)
        {
            return 0.0f;
        }

        float duration = EndTime - StartTime;
        if (duration <= AnimationConstants.SoaEpsilon)
        {
            return 1.0f;
        }

        float n = (time - StartTime) / duration;
        float ramp = MathF.Min(n, 1.0f - n);
        return MathF.Min(1.0f, MathF.Max(AnimationConstants.FootWeightFloor, ramp * 4.0f));
    }
}

/// <summary>Named set of channels with events, foot contacts, and tags.</summary>
public sealed class AnimationClip
{
    private AnimationChannel[] _channels;
    private ClipEvent[] _events;
    private FootContact[] _footContacts;

    /// <summary>Clip identity.</summary>
    public ClipId Id { get; }

    /// <summary>Playback length; at least the last key time.</summary>
    public float Duration { get; }

    /// <summary>Channels.</summary>
    public ReadOnlySpan<AnimationChannel> Channels => _channels;

    /// <summary>Sorted events.</summary>
    public ReadOnlySpan<ClipEvent> Events => _events;

    /// <summary>Foot contacts.</summary>
    public ReadOnlySpan<FootContact> FootContacts => _footContacts;

    /// <summary>Tags for motion matching.</summary>
    public HashSet<string> Tags { get; }

    /// <summary>Creates a clip, sanitizing events, contacts, and tags.</summary>
    public AnimationClip(ClipId id, float duration, AnimationChannel[] channels, ClipEvent[]? events = null, FootContact[]? contacts = null, IEnumerable<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(channels);
        Id = id;
        _channels = channels;

        float maxKeyTime = 0.0f;
        for (int i = 0; i < channels.Length; i++)
        {
            ReadOnlySpan<float> times = channels[i].KeyTimes;
            if (times.Length > 0)
            {
                maxKeyTime = MathF.Max(maxKeyTime, times[times.Length - 1]);
            }
        }

        Duration = MathF.Max(MathF.Max(duration, maxKeyTime), 0.0f);

        var cleanEvents = new List<ClipEvent>();
        if (events != null)
        {
            foreach (ClipEvent evt in events)
            {
                if (!float.IsNaN(evt.Time) && !string.IsNullOrWhiteSpace(evt.Name))
                {
                    cleanEvents.Add(evt);
                }
            }

            cleanEvents.Sort(static (a, b) => a.Time.CompareTo(b.Time));
        }

        _events = cleanEvents.ToArray();

        var cleanContacts = new List<FootContact>();
        if (contacts != null)
        {
            foreach (FootContact contact in contacts)
            {
                float start = MathF.Min(contact.StartTime, contact.EndTime);
                float end = MathF.Max(contact.StartTime, contact.EndTime);
                cleanContacts.Add(new FootContact(start, end, contact.BoneIndex));
            }
        }

        _footContacts = cleanContacts.ToArray();

        Tags = new HashSet<string>(StringComparer.Ordinal);
        if (tags != null)
        {
            foreach (string tag in tags)
            {
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    Tags.Add(tag);
                }
            }
        }
    }

    /// <summary>Replaces channels (streaming merge).</summary>
    public void RebuildChannels(AnimationChannel[] newChannels)
    {
        ArgumentNullException.ThrowIfNull(newChannels);
        _channels = newChannels;
    }
}
