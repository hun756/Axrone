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
    public Collection<float> KeyTimes { get; } = new();

    /// <summary>Key values.</summary>
    public Collection<float> KeyValues { get; } = new();
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
    public Collection<ChunkTrackDto> Tracks { get; } = new();
}

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
