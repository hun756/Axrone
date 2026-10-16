namespace Axrone.Audio.Ecs;

using Axrone.Audio;
using Axrone.Ecs;

/// <summary>
/// Links an entity to a registered audio source. Unmanaged, safe to store
/// in archetype columns. Command traffic lives in the binder, not here.
/// </summary>
public struct AudioSourceRef
{
    /// <summary>Target source.</summary>
    public AudioSourceId SourceId;

    /// <summary>Creates a link.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AudioSourceRef(AudioSourceId sourceId) => SourceId = sourceId;
}

/// <summary>
/// Links an entity to a registered audio listener. Unmanaged, safe to store
/// in archetype columns. Transform sync arrives with the Scene round.
/// </summary>
public struct AudioListenerRef
{
    /// <summary>Target listener.</summary>
    public AudioListenerId ListenerId;

    /// <summary>Creates a link.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AudioListenerRef(AudioListenerId listenerId) => ListenerId = listenerId;
}
