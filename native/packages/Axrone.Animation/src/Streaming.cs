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
