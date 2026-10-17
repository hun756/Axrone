namespace Axrone.Audio.Ecs;

using Axrone.Audio;
using Axrone.Ecs;

/// <summary>
/// Links an entity to a registered audio source and carries its full
/// tuning: bus, clip, gain chain, loop, playback rate, pan, spatialization
/// and start offset. Unmanaged, safe to store in archetype columns.
/// Mirrors the web AudioSourceComponent tuning surface; asset metadata and
/// transform coupling arrive with the asset and Scene rounds.
/// </summary>
public struct AudioSourceRef
{
    /// <summary>Target source.</summary>
    public AudioSourceId SourceId;

    /// <summary>Target bus.</summary>
    public AudioBusId BusId;

    /// <summary>Target clip.</summary>
    public AudioClipId ClipId;

    /// <summary>Gain multiplier.</summary>
    public float Volume;

    /// <summary>Whether muted.</summary>
    public bool Muted;

    /// <summary>Whether looping.</summary>
    public bool Loop;

    /// <summary>Whether playback starts on attach.</summary>
    public bool Autoplay;

    /// <summary>Playback rate multiplier.</summary>
    public float PlaybackRate;

    /// <summary>Detune in cents.</summary>
    public float DetuneCents;

    /// <summary>Stereo pan in [-1, 1].</summary>
    public float Pan;

    /// <summary>Spatialization.</summary>
    public AudioSpatialization Spatial;

    /// <summary>Start offset in seconds.</summary>
    public float StartOffsetSeconds;

    /// <summary>Last known playback state, synced back by the binder.</summary>
    public AudioPlaybackState PlaybackState;

    /// <summary>Creates a link with sensible tuning defaults.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AudioSourceRef(AudioSourceId sourceId)
    {
        SourceId = sourceId;
        BusId = default;
        ClipId = default;
        Volume = 1.0f;
        Muted = false;
        Loop = false;
        Autoplay = false;
        PlaybackRate = 1.0f;
        DetuneCents = 0.0f;
        Pan = 0.0f;
        Spatial = default;
        StartOffsetSeconds = 0.0f;
        PlaybackState = AudioPlaybackState.Stopped;
    }

    /// <summary>Builds the registration/update definition.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly AudioSourceDefinition ToDefinition() => new(
        BusId,
        ClipId,
        Volume,
        Muted,
        Loop,
        Autoplay,
        PlaybackRate,
        DetuneCents,
        Pan,
        Spatial,
        StartOffsetSeconds);
}

/// <summary>
/// Links an entity to a registered audio listener. Unmanaged, safe to store
/// in archetype columns. Transform sync arrives with the Scene round.
/// </summary>
public struct AudioListenerRef
{
    /// <summary>Target listener.</summary>
    public AudioListenerId ListenerId;

    /// <summary>Whether this listener is the active one.</summary>
    public bool Active;

    /// <summary>Creates a link.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AudioListenerRef(AudioListenerId listenerId, bool active = false)
    {
        ListenerId = listenerId;
        Active = active;
    }
}
