namespace Axrone.Scene;

/// <summary>How the world reacts to a managed behaviour fault.</summary>
public enum SceneWorldErrorPolicy : byte
{
    /// <summary>Report the fault and continue with the remaining behaviours.</summary>
    IsolateBehaviours = 0,

    /// <summary>Report the fault and rethrow it, aborting the phase.</summary>
    FailFast = 1
}

/// <summary>Construction settings for a scene world.</summary>
public readonly record struct SceneWorldOptions(
    int InitialCapacity = 64,
    SceneWorldErrorPolicy ErrorPolicy = SceneWorldErrorPolicy.IsolateBehaviours);

/// <summary>Creation settings for one scene actor.</summary>
public readonly record struct SceneActorConfig(
    string? Name = null,
    string? Tag = null,
    ushort Layer = 0,
    bool Active = true,
    bool Persistent = false,
    bool Pooled = false);
