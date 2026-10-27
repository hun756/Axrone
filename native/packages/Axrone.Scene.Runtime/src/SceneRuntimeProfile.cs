namespace Axrone.Scene.Runtime;

/// <summary>Built-in runtime profile families.</summary>
public enum SceneRuntimeProfileKind
{
    /// <summary>Transforms and hierarchy only.</summary>
    Core = 0,

    /// <summary>2D cameras and sprites.</summary>
    TwoD = 1,

    /// <summary>3D cameras, meshes, lights and fog.</summary>
    ThreeD = 2,

    /// <summary>2D and 3D paths together.</summary>
    Mixed = 3,

    /// <summary>Default full engine composition.</summary>
    Full = 4,

    /// <summary>Caller-composed profile outside the built-in families.</summary>
    Custom = 5
}

/// <summary>Physics bridges a runtime profile participates in.</summary>
public readonly record struct ScenePhysicsBridgeSelection(bool Bridge3D, bool Bridge2D);

/// <summary>Stable built-in runtime profile ids.</summary>
public static class SceneRuntimeProfileIds
{
    /// <summary>Transforms and hierarchy only.</summary>
    public const string Core = "scene/core-default";

    /// <summary>2D cameras and sprites.</summary>
    public const string TwoD = "scene/2d-default";

    /// <summary>3D cameras, meshes, lights and fog.</summary>
    public const string ThreeD = "scene/3d-default";

    /// <summary>2D and 3D paths together.</summary>
    public const string Mixed = "scene/mixed-default";

    /// <summary>Default full engine composition (3D today).</summary>
    public const string Full = "scene/full-3d-default";

    /// <summary>Id assigned to caller-composed profiles.</summary>
    public const string Custom = "scene/custom";
}

/// <summary>
/// Immutable runtime profile: a stable id, the composition family and the
/// sealed scene profile it resolves to. Profiles own no mutable state and can
/// create any number of worlds.
/// </summary>
public sealed class SceneRuntimeProfile
{
    /// <summary>Creates a runtime profile wrapper.</summary>
    public SceneRuntimeProfile(
        string id,
        SceneRuntimeProfileKind kind,
        SceneProfile profile,
        ScenePhysicsBridgeSelection physicsBridges)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(profile);

        Id = id;
        Symbol = SceneStringId.FromName(id);
        Kind = kind;
        Profile = profile;
        PhysicsBridges = physicsBridges;
    }

    /// <summary>Stable profile id.</summary>
    public string Id { get; }

    /// <summary>Profile id symbol.</summary>
    public SceneStringId Symbol { get; }

    /// <summary>Composition family.</summary>
    public SceneRuntimeProfileKind Kind { get; }

    /// <summary>Sealed scene composition.</summary>
    public SceneProfile Profile { get; }

    /// <summary>Physics bridges this profile participates in.</summary>
    public ScenePhysicsBridgeSelection PhysicsBridges { get; }
}
