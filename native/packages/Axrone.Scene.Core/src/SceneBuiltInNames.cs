namespace Axrone.Scene;

/// <summary>
/// Stable symbols of components owned by the scene core itself. Profiles
/// register the same names, so symbol lookup is independent of registration
/// order.
/// </summary>
public static class SceneBuiltInNames
{
    /// <summary>Three-dimensional local transform.</summary>
    public static readonly SceneStringId Transform3D = SceneStringId.FromName("Transform3D");

    /// <summary>Two-dimensional local transform.</summary>
    public static readonly SceneStringId Transform2D = SceneStringId.FromName("Transform2D");
}
