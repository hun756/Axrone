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
/// Mirrors the web AudioListenerComponent tuning surface; managed metadata
/// stays out and arrives with the asset round.
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

    /// <summary>Listener position.</summary>
    [Property(Label = "Position", Type = PropertyTypes.Vec3)]
    public Vector3 Position;

    /// <summary>Listener forward.</summary>
    [Property(Label = "Forward", Type = PropertyTypes.Vec3)]
    public Vector3 Forward;

    /// <summary>Listener up.</summary>
    [Property(Label = "Up", Type = PropertyTypes.Vec3)]
    public Vector3 Up;

    /// <summary>Master gain in [0, 1].</summary>
    [Property(Label = "Global Volume", Type = PropertyTypes.Number, Min = 0.0, Max = 1.0, Step = 0.01)]
    public float GlobalVolume;

    /// <summary>Doppler factor in [0, 5].</summary>
    [Property(Label = "Doppler", Type = PropertyTypes.Number, Min = 0.0, Max = 5.0, Step = 0.01)]
    public float DopplerFactor;

    /// <summary>Output channel layout.</summary>
    [Property(Label = "Speaker Mode", Type = "audio.speakerMode")]
    public AudioSpeakerMode SpeakerMode;

    /// <summary>Mix sample rate.</summary>
    [Property(Label = "Sample Rate", Type = PropertyTypes.Number, Min = 8000.0, Max = 192000.0, Step = 1.0)]
    public int SampleRate;

    /// <summary>DSP buffer size.</summary>
    [Property(Label = "DSP Buffer", Type = PropertyTypes.Number, Min = 64.0, Max = 8192.0, Step = 1.0)]
    public int DspBufferSize;

    /// <summary>Real voice budget.</summary>
    [Property(Label = "Real Voices", Type = PropertyTypes.Number, Min = 1.0, Max = 256.0, Step = 1.0)]
    public int RealVoices;

    /// <summary>Virtual voice budget.</summary>
    [Property(Label = "Virtual Voices", Type = PropertyTypes.Number, Min = 1.0, Max = 1024.0, Step = 1.0)]
    public int VirtualVoices;

    /// <summary>Over-budget voice policy.</summary>
    [Property(Label = "Virtual Behavior", Type = "audio.virtualVoiceBehavior")]
    public AudioVirtualVoiceBehavior VirtualVoiceBehavior;

    /// <summary>HRTF provider.</summary>
    [Property(Label = "HRTF", Type = "audio.hrtfPlugin")]
    public AudioHrtfPlugin HrtfPlugin;

    /// <summary>Occlusion tracing mode.</summary>
    [Property(Label = "Occlusion Mode", Type = "audio.occlusionMode")]
    public AudioOcclusionMode OcclusionMode;

    /// <summary>Occlusion layer bitmask (web number[] collapsed).</summary>
    [Property(Label = "Occlusion Mask", Type = PropertyTypes.Number)]
    public uint OcclusionMask;

    /// <summary>Reverb preset.</summary>
    [Property(Label = "Reverb Preset", Type = "audio.reverbPreset")]
    public AudioReverbPreset ReverbPreset;

    /// <summary>Reverb mix in [0, 1].</summary>
    [Property(Label = "Reverb Level", Type = PropertyTypes.Number, Min = 0.0, Max = 1.0, Step = 0.01)]
    public float ReverbLevel;

    /// <summary>Ambient clip.</summary>
    public AudioClipId AmbientClip;

    /// <summary>Ambient gain in [0, 1].</summary>
    [Property(Label = "Ambient Volume", Type = PropertyTypes.Number, Min = 0.0, Max = 1.0, Step = 0.01)]
    public float AmbientVolume;

    /// <summary>Whether the listener follows the camera.</summary>
    [Property(Label = "Follow Camera", Type = PropertyTypes.Boolean)]
    public bool FollowCamera;

    /// <summary>Creates a link with web-default tuning.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AudioListenerRef(AudioListenerId listenerId, bool active = false)
    {
        ListenerId = listenerId;
        Active = active;
        UseTransform = true;
        Position = Vector3.Zero;
        Forward = -Vector3.UnitZ;
        Up = Vector3.UnitY;
        GlobalVolume = 1.0f;
        DopplerFactor = 1.0f;
        SpeakerMode = AudioSpeakerMode.Stereo;
        SampleRate = 48000;
        DspBufferSize = 1024;
        RealVoices = 32;
        VirtualVoices = 128;
        VirtualVoiceBehavior = AudioVirtualVoiceBehavior.PlaySilent;
        HrtfPlugin = AudioHrtfPlugin.None;
        OcclusionMode = AudioOcclusionMode.RaycastDiffraction;
        OcclusionMask = 0xC9U;
        ReverbPreset = AudioReverbPreset.Off;
        ReverbLevel = 0.0f;
        AmbientClip = default;
        AmbientVolume = 0.5f;
        FollowCamera = true;
    }

    /// <summary>Builds the registration/update descriptor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly AudioListenerDescriptor ToDescriptor() => new(
        Position,
        Forward,
        Up,
        GlobalVolume,
        DopplerFactor,
        SpeakerMode,
        SampleRate,
        DspBufferSize,
        RealVoices,
        VirtualVoices,
        VirtualVoiceBehavior,
        HrtfPlugin,
        OcclusionMode,
        OcclusionMask,
        ReverbPreset,
        ReverbLevel,
        AmbientClip,
        AmbientVolume,
        FollowCamera);
}
