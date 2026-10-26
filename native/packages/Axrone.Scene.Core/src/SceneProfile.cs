namespace Axrone.Scene;

/// <summary>
/// Immutable scene composition: a profile identity plus the sealed component
/// registry it owns. A profile can create any number of worlds; every world
/// shares the same descriptor ids and name table.
/// </summary>
public sealed class SceneProfile
{
    internal SceneProfile(SceneProfileId id, SceneComponentRegistry registry)
    {
        if (!id.IsValid)
        {
            throw new SceneValidationException(
                SceneErrorCodes.ProfileInvalid,
                "Scene profiles require a non-empty identity.");
        }

        ArgumentNullException.ThrowIfNull(registry);
        registry.Seal();

        Id = id;
        Registry = registry;
        HasTransform3D = registry.TryGetByName(SceneBuiltInNames.Transform3D, out _);
        HasTransform2D = registry.TryGetByName(SceneBuiltInNames.Transform2D, out _);
    }

    /// <summary>Profile identity.</summary>
    public SceneProfileId Id { get; }

    /// <summary>Sealed component registry.</summary>
    public SceneComponentRegistry Registry { get; }

    /// <summary>Whether the profile registers a 3D transform column.</summary>
    public bool HasTransform3D { get; }

    /// <summary>Whether the profile registers a 2D transform column.</summary>
    public bool HasTransform2D { get; }

    /// <summary>Creates a world with default options.</summary>
    public SceneWorld CreateWorld(ISceneDiagnosticsSink? diagnostics = null) =>
        new(Registry, diagnostics);

    /// <summary>Creates a world with explicit options.</summary>
    public SceneWorld CreateWorld(SceneWorldOptions options, ISceneDiagnosticsSink? diagnostics = null) =>
        new(Registry, diagnostics, options);
}
