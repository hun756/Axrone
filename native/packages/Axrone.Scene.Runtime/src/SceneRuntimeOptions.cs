namespace Axrone.Scene.Runtime;

/// <summary>Runtime composition settings.</summary>
public sealed record SceneRuntimeOptions
{
    /// <summary>
    /// Built-in or custom runtime profile. When null, <see cref="Profile"/> is
    /// used if provided, otherwise <see cref="SceneRuntimeProfiles.Full"/>.
    /// </summary>
    public SceneRuntimeProfile? RuntimeProfile { get; init; }

    /// <summary>Custom scene composition used when no runtime profile is set.</summary>
    public SceneProfile? Profile { get; init; }

    /// <summary>Optional world sizing and capacity settings.</summary>
    public SceneWorldOptions? WorldOptions { get; init; }

    /// <summary>Viewport width in pixels.</summary>
    public int ViewportWidth { get; init; } = 1280;

    /// <summary>Viewport height in pixels.</summary>
    public int ViewportHeight { get; init; } = 720;

    /// <summary>Ambient light applied when the scene has no ambient component.</summary>
    public Vec3 AmbientLight { get; init; } = new(0.2f, 0.2f, 0.25f);

    /// <summary>Sky light contribution.</summary>
    public Vec3 SkyLight { get; init; } = new(0.4f, 0.45f, 0.55f);

    /// <summary>Ground light contribution.</summary>
    public Vec3 GroundLight { get; init; } = new(0.15f, 0.13f, 0.1f);

    /// <summary>Maximum point and spot lights forwarded to the lighting rig.</summary>
    public int MaxLocalLights { get; init; } = 4;

    /// <summary>Optional frame profiler settings; recording is disabled by default.</summary>
    public SceneRuntimeProfilerOptions? Profiler { get; init; }
}
