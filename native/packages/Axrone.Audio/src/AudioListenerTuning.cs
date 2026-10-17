namespace Axrone.Audio;

/// <summary>
/// Output channel layout.
/// </summary>
public enum AudioSpeakerMode : byte
{
    /// <summary>Mono.</summary>
    Mono = 0,

    /// <summary>Stereo.</summary>
    Stereo = 1,

    /// <summary>Quad.</summary>
    Quad = 2,

    /// <summary>5.1 surround.</summary>
    Surround51 = 3,

    /// <summary>7.1 surround.</summary>
    Surround71 = 4,
}

/// <summary>
/// HRTF provider.
/// </summary>
public enum AudioHrtfPlugin : byte
{
    /// <summary>No HRTF.</summary>
    None = 0,

    /// <summary>Oculus.</summary>
    Oculus = 1,

    /// <summary>Steam.</summary>
    Steam = 2,

    /// <summary>Resonance.</summary>
    Resonance = 3,

    /// <summary>Windows Sonic.</summary>
    WindowsSonic = 4,
}

/// <summary>
/// Occlusion tracing mode.
/// </summary>
public enum AudioOcclusionMode : byte
{
    /// <summary>Raycast.</summary>
    Raycast = 0,

    /// <summary>Raycast with diffraction.</summary>
    RaycastDiffraction = 1,

    /// <summary>Disabled.</summary>
    None = 2,
}

/// <summary>
/// Reverb preset.
/// </summary>
public enum AudioReverbPreset : byte
{
    /// <summary>Off.</summary>
    Off = 0,

    /// <summary>Generic.</summary>
    Generic = 1,

    /// <summary>Room.</summary>
    Room = 2,

    /// <summary>Bathroom.</summary>
    Bathroom = 3,

    /// <summary>Stone room.</summary>
    StoneRoom = 4,

    /// <summary>Auditorium.</summary>
    Auditorium = 5,

    /// <summary>Concert hall.</summary>
    ConcertHall = 6,

    /// <summary>Cave.</summary>
    Cave = 7,

    /// <summary>Forest.</summary>
    Forest = 8,

    /// <summary>City.</summary>
    City = 9,

    /// <summary>Underwater.</summary>
    Underwater = 10,
}

/// <summary>
/// Virtual voice policy when over budget.
/// </summary>
public enum AudioVirtualVoiceBehavior : byte
{
    /// <summary>Keep silent.</summary>
    PlaySilent = 0,

    /// <summary>Keep inaudible.</summary>
    PlayInaudible = 1,

    /// <summary>Stop immediately.</summary>
    StopImmediately = 2,

    /// <summary>Swap with a real voice.</summary>
    SwapWithReal = 3,
}
