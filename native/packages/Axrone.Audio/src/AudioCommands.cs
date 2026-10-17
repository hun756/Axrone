namespace Axrone.Audio;

/// <summary>
/// Source command discriminant.
/// </summary>
public enum AudioSourceCommandType : byte
{
    /// <summary>Play.</summary>
    Play = 0,

    /// <summary>Pause.</summary>
    Pause = 1,

    /// <summary>Resume.</summary>
    Resume = 2,

    /// <summary>Stop.</summary>
    Stop = 3,
}

/// <summary>
/// One deferred source command: discriminant plus play request.
/// Unmanaged, safe to store in queues and pass by value.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AudioSourceCommand(
    AudioSourceCommandType Type,
    AudioSourcePlayRequest Request);
