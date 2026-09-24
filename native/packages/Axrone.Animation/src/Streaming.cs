namespace Axrone.Animation;

using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Merge policy for an ingested chunk.</summary>
public enum MergeMode
{
    /// <summary>Replace every channel.</summary>
    ReplaceAll = 0,

    /// <summary>Replace channels overlapping the time range.</summary>
    ReplaceRange = 1,
}

// CA1002/CA2227: DTO collection shape is dictated by the source-generated JSON
// serializer (mutable lists required both directions); not a domain API choice.
#pragma warning disable CA1002, CA2227

/// <summary>Serializable track payload.</summary>
public sealed class ChunkTrackDto
{
    /// <summary>Target bone.</summary>
    public int BoneIndex { get; set; }

    /// <summary>Channel target as byte.</summary>
    public byte Target { get; set; }

    /// <summary>Interpolation as byte.</summary>
    public byte Interpolation { get; set; }

    /// <summary>Key times.</summary>
    public List<float> KeyTimes { get; set; } = new();

    /// <summary>Key values.</summary>
    public List<float> KeyValues { get; set; } = new();
}

/// <summary>Serializable chunk payload.</summary>
public sealed class ChunkPayloadDto
{
    /// <summary>Schema version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Owning clip.</summary>
    public string ClipId { get; set; } = string.Empty;

    /// <summary>Merge policy as byte.</summary>
    public byte MergeMode { get; set; }

    /// <summary>Chunk start.</summary>
    public float StartTime { get; set; }

    /// <summary>Chunk end.</summary>
    public float EndTime { get; set; }

    /// <summary>Resulting clip duration.</summary>
    public float Duration { get; set; }

    /// <summary>Tracks.</summary>
    public List<ChunkTrackDto> Tracks { get; set; } = new();
}

#pragma warning restore CA1002, CA2227

/// <summary>Source-generated JSON context (AOT-safe, no reflection).</summary>
[JsonSerializable(typeof(ChunkPayloadDto))]
[JsonSerializable(typeof(ChunkTrackDto))]
public sealed partial class ChunkJsonSerializerContext : JsonSerializerContext
{
}

/// <summary>One chunk fetch request.</summary>
public readonly record struct ChunkRequest(string ChunkId, ClipId ClipId, float StartTime, float Weight, bool IsPreload);

/// <summary>Chunk fetch scheduler: active chunks first, preload window behind.</summary>
public sealed class StreamingScheduler
{
    private readonly HashSet<string> _loadedChunks = new(StringComparer.Ordinal);
    private readonly HashSet<string> _requestedChunks = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failedChunks = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Marks a chunk loaded.</summary>
    public void MarkLoaded(string chunkId)
    {
        ArgumentNullException.ThrowIfNull(chunkId);
        lock (_gate)
        {
            _requestedChunks.Remove(chunkId);
            _loadedChunks.Add(chunkId);
        }
    }

    /// <summary>Marks a chunk failed (eligible for retry after reset).</summary>
    public void MarkFailed(string chunkId)
    {
        ArgumentNullException.ThrowIfNull(chunkId);
        lock (_gate)
        {
            _requestedChunks.Remove(chunkId);
            _failedChunks.Add(chunkId);
        }
    }

    /// <summary>Forgets all chunk states.</summary>
    public void Reset(string chunkId)
    {
        ArgumentNullException.ThrowIfNull(chunkId);
        lock (_gate)
        {
            _loadedChunks.Remove(chunkId);
            _requestedChunks.Remove(chunkId);
            _failedChunks.Remove(chunkId);
        }
    }

    private static void InsertSorted(Collection<ChunkRequest> requests, ChunkRequest request)
    {
        int index = 0;
        while (index < requests.Count && CompareRequests(requests[index], request) <= 0)
        {
            index++;
        }

        requests.Insert(index, request);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CompareRequests(ChunkRequest a, ChunkRequest b)
    {
        if (a.IsPreload != b.IsPreload)
        {
            return a.IsPreload ? 1 : -1;
        }

        int byWeight = b.Weight.CompareTo(a.Weight);
        if (byWeight != 0)
        {
            return byWeight;
        }

        return a.StartTime.CompareTo(b.StartTime);
    }

    /// <summary>
    /// Appends active and preload chunk requests to a caller-owned collection,
    /// kept sorted (active first, then weight, then start). No per-call allocation
    /// beyond the interpolated chunk ids.
    /// </summary>
    public void Schedule(
        ReadOnlySpan<(ClipId Clip, float Time, float Weight)> activities,
        float chunkDuration,
        float preloadWindow,
        Collection<ChunkRequest> outRequests)
    {
        ArgumentNullException.ThrowIfNull(outRequests);
        if (chunkDuration <= 0.0f)
        {
            AnimationThrowHelper.ThrowValidation(AnimationErrorCode.StreamingChunkIncompatible, "Chunk duration must be positive.");
        }

        lock (_gate)
        {
            for (int i = 0; i < activities.Length; i++)
            {
                (ClipId clip, float time, float weight) = activities[i];
                int currentIndex = (int)(time / chunkDuration);
                string activeId = $"{clip.Value}:v:{currentIndex}";

                if (!_loadedChunks.Contains(activeId) && !_requestedChunks.Contains(activeId) && !_failedChunks.Contains(activeId))
                {
                    InsertSorted(outRequests, new ChunkRequest(activeId, clip, currentIndex * chunkDuration, weight, false));
                    _requestedChunks.Add(activeId);
                }

                int preloadIndex = (int)((time + preloadWindow) / chunkDuration);
                if (preloadIndex != currentIndex)
                {
                    string preloadId = $"{clip.Value}:v:{preloadIndex}";
                    if (!_loadedChunks.Contains(preloadId) && !_requestedChunks.Contains(preloadId) && !_failedChunks.Contains(preloadId))
                    {
                        InsertSorted(outRequests, new ChunkRequest(preloadId, clip, preloadIndex * chunkDuration, weight * 0.5f, true));
                        _requestedChunks.Add(preloadId);
                    }
                }
            }
        }
    }
}

/// <summary>Chunk codec: bytes to payload with version and shape validation.</summary>
public static class ChunkCodec
{
    /// <summary>Decodes and validates a payload.</summary>
    public static ChunkPayloadDto Decode(ReadOnlySpan<byte> json)
    {
        ChunkPayloadDto? payload;
        try
        {
            payload = JsonSerializer.Deserialize(json, ChunkJsonSerializerContext.Default.ChunkPayloadDto);
        }
        catch (JsonException ex)
        {
            AnimationThrowHelper.ThrowSampling(AnimationErrorCode.StreamingChunkCorrupt, $"Chunk JSON corrupt: {ex.Message}");
            throw new UnreachableException();
        }

        if (payload == null)
        {
            AnimationThrowHelper.ThrowSampling(AnimationErrorCode.StreamingChunkCorrupt, "Chunk JSON decoded to null.");
        }

        if (payload.Version != 1)
        {
            AnimationThrowHelper.ThrowSampling(AnimationErrorCode.StreamingChunkIncompatible, $"Unsupported chunk version {payload.Version}.");
        }

        if (string.IsNullOrWhiteSpace(payload.ClipId))
        {
            AnimationThrowHelper.ThrowSampling(AnimationErrorCode.StreamingChunkCorrupt, "Chunk clip id missing.");
        }

        foreach (ChunkTrackDto track in payload.Tracks)
        {
            int components = track.Target switch
            {
                (byte)ChannelTarget.Translation => 3,
                (byte)ChannelTarget.Rotation => 4,
                (byte)ChannelTarget.Scale => 3,
                (byte)ChannelTarget.Curve => 1,
                _ => -1,
            };

            if (components < 0)
            {
                AnimationThrowHelper.ThrowSampling(AnimationErrorCode.StreamingChunkIncompatible, $"Unknown channel target {track.Target}.");
            }

            int stride = track.Interpolation == (byte)InterpolationMode.CubicSpline ? components * 3 : components;
            if (track.KeyTimes.Count * stride != track.KeyValues.Count)
            {
                AnimationThrowHelper.ThrowSampling(AnimationErrorCode.StreamingChunkIncompatible, "Chunk track stride mismatch.");
            }
        }

        return payload;
    }

    /// <summary>Merges a payload into live channels.</summary>
    public static AnimationChannel[] Merge(AnimationChannel[] baseChannels, ChunkPayloadDto payload, MergeMode mode)
    {
        ArgumentNullException.ThrowIfNull(baseChannels);
        ArgumentNullException.ThrowIfNull(payload);

        var incoming = new List<AnimationChannel>(payload.Tracks.Count);
        foreach (ChunkTrackDto track in payload.Tracks)
        {
            incoming.Add(new AnimationChannel(
                track.BoneIndex,
                (ChannelTarget)track.Target,
                (InterpolationMode)track.Interpolation,
                [.. track.KeyTimes],
                [.. track.KeyValues]));
        }

        if (mode == MergeMode.ReplaceAll)
        {
            return incoming.ToArray();
        }

        var merged = new List<AnimationChannel>(baseChannels);
        foreach (AnimationChannel channel in incoming)
        {
            bool replaced = false;
            for (int i = 0; i < merged.Count; i++)
            {
                if (merged[i].BoneIndex == channel.BoneIndex && merged[i].Target == channel.Target)
                {
                    merged[i] = channel;
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                merged.Add(channel);
            }
        }

        return merged.ToArray();
    }
}

/// <summary>Linear keyframe reduction: drops interior keys a straight line predicts.</summary>
public static class KeyframeOptimizer
{
    /// <summary>Optimizes a clip with per-lane tolerances.</summary>
    public static AnimationClip Optimize(
        AnimationClip source,
        float positionTolerance = AnimationConstants.KeyframePosTol,
        float rotationTolerance = AnimationConstants.KeyframeRotTol,
        float scaleTolerance = AnimationConstants.KeyframeScaleTol)
    {
        ArgumentNullException.ThrowIfNull(source);

        var translations = new List<AnimationChannel>();
        var rotations = new List<AnimationChannel>();
        var scales = new List<AnimationChannel>();
        var curves = new List<AnimationChannel>();

        foreach (AnimationChannel channel in source.Channels)
        {
            float tolerance = channel.Target switch
            {
                ChannelTarget.Translation => positionTolerance,
                ChannelTarget.Rotation => rotationTolerance,
                ChannelTarget.Scale => scaleTolerance,
                ChannelTarget.Curve => AnimationConstants.KeyframeCurveTol,
                _ => positionTolerance,
            };

            AnimationChannel reduced = Reduce(channel, tolerance);
            switch (channel.Target)
            {
                case ChannelTarget.Translation: translations.Add(reduced); break;
                case ChannelTarget.Rotation: rotations.Add(reduced); break;
                case ChannelTarget.Scale: scales.Add(reduced); break;
                default: curves.Add(reduced); break;
            }
        }

        var merged = new List<AnimationChannel>(translations.Count + rotations.Count + scales.Count + curves.Count);
        merged.AddRange(translations);
        merged.AddRange(rotations);
        merged.AddRange(scales);
        merged.AddRange(curves);

        return new AnimationClip(
            source.Id,
            source.Duration,
            merged.ToArray(),
            [.. source.Events],
            [.. source.FootContacts],
            source.Tags);
    }

    private static AnimationChannel Reduce(AnimationChannel channel, float tolerance)
    {
        if (channel.Interpolation != InterpolationMode.Linear)
        {
            return channel;
        }

        ReadOnlySpan<float> times = channel.KeyTimes;
        ReadOnlySpan<float> values = channel.KeyValues;
        if (times.Length <= 2)
        {
            return channel;
        }

        int stride = channel.Stride;
        var newTimes = new List<float>(times.Length) { times[0] };
        var newValues = new List<float>(values.Length);
        for (int c = 0; c < stride; c++)
        {
            newValues.Add(values[c]);
        }

        int anchor = 0;
        for (int i = 1; i < times.Length - 1; i++)
        {
            float t0 = times[anchor];
            float t1 = times[i];
            float t2 = times[i + 1];

            if (MathF.Abs(t2 - t0) <= AnimationConstants.SoaEpsilon)
            {
                PushKey(times, values, stride, i, newTimes, newValues);
                anchor = i;
                continue;
            }

            float factor = (t1 - t0) / (t2 - t0);
            bool deviate = false;
            for (int s = 0; s < stride; s++)
            {
                float v0 = values[(anchor * stride) + s];
                float actual = values[(i * stride) + s];
                float v2 = values[((i + 1) * stride) + s];
                float predicted = v0 + (factor * (v2 - v0));
                if (MathF.Abs(actual - predicted) > tolerance)
                {
                    deviate = true;
                    break;
                }
            }

            if (deviate)
            {
                PushKey(times, values, stride, i, newTimes, newValues);
                anchor = i;
            }
        }

        PushKey(times, values, stride, times.Length - 1, newTimes, newValues);
        return new AnimationChannel(channel.BoneIndex, channel.Target, channel.Interpolation, newTimes.ToArray(), newValues.ToArray(), channel.TargetCurveId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PushKey(ReadOnlySpan<float> times, ReadOnlySpan<float> values, int stride, int index, List<float> outTimes, List<float> outValues)
    {
        outTimes.Add(times[index]);
        int baseOffset = index * stride;
        for (int s = 0; s < stride; s++)
        {
            outValues.Add(values[baseOffset + s]);
        }
    }
}
