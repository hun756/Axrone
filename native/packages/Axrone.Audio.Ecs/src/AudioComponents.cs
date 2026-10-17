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
[Script(ScriptName = "AudioSource", Priority = 810, ExecuteInEditMode = true)]
public struct AudioSourceRef
{
    /// <summary>Target source.</summary>
    public AudioSourceId SourceId;

    /// <summary>Target bus.</summary>
    public AudioBusId BusId;

    /// <summary>Target clip.</summary>
    public AudioClipId ClipId;

    /// <summary>Gain multiplier.</summary>
    [Property(Label = "Volume", Type = PropertyTypes.Number, Min = 0.0, Max = 4.0, Step = 0.01)]
    public float Volume;

    /// <summary>Whether muted.</summary>
    [Property(Label = "Muted", Type = PropertyTypes.Boolean)]
    public bool Muted;

    /// <summary>Whether looping.</summary>
    [Property(Label = "Loop", Type = PropertyTypes.Boolean)]
    public bool Loop;

    /// <summary>Whether playback starts on attach.</summary>
    [Property(Label = "Autoplay", Type = PropertyTypes.Boolean)]
    public bool Autoplay;

    /// <summary>Playback rate multiplier.</summary>
    [Property(Label = "Playback Rate", Type = PropertyTypes.Number, Min = 0.25, Max = 4.0, Step = 0.01)]
    public float PlaybackRate;

    /// <summary>Detune in cents.</summary>
    [Property(Label = "Detune", Type = PropertyTypes.Number, Min = -1200.0, Max = 1200.0, Step = 1.0)]
    public float DetuneCents;

    /// <summary>Stereo pan in [-1, 1].</summary>
    [Property(Label = "Pan", Type = PropertyTypes.Number, Min = -1.0, Max = 1.0, Step = 0.01)]
    public float Pan;

    /// <summary>Spatialization.</summary>
    public AudioSpatialization Spatial;

    /// <summary>Start offset in seconds.</summary>
    [Property(Label = "Start Offset", Type = PropertyTypes.Number, Min = 0.0, Max = 3600.0, Step = 0.01)]
    public float StartOffsetSeconds;

    /// <summary>Whether the Scene transform drives spatialization.</summary>
    [Property(Label = "Use Transform", Type = PropertyTypes.Boolean)]
    public bool UseTransform;

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
        UseTransform = true;
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
[Script(ScriptName = "AudioListener", Priority = 800, ExecuteInEditMode = true)]
public struct AudioListenerRef
{
    /// <summary>Target listener.</summary>
    public AudioListenerId ListenerId;

    /// <summary>Whether this listener is the active one.</summary>
    [Property(Label = "Active", Type = PropertyTypes.Boolean)]
    public bool Active;

    /// <summary>Whether the Scene transform drives the listener pose.</summary>
    [Property(Label = "Use Transform", Type = PropertyTypes.Boolean)]
    public bool UseTransform;

    /// <summary>Creates a link.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AudioListenerRef(AudioListenerId listenerId, bool active = false)
    {
        ListenerId = listenerId;
        Active = active;
        UseTransform = true;
    }
}
