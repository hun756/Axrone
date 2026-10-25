namespace Axrone.Scene.Rendering;

/// <summary>Profile composition helpers for rendering components.</summary>
public static class SceneRenderingProfileExtensions
{
    /// <summary>Registers the 3D camera and mesh renderer components.</summary>
    public static SceneProfileBuilder AddRendering3D(this SceneProfileBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddData<SceneCamera>(
            "Camera",
            SceneComponentTraits.Singleton | SceneComponentTraits.Serializable,
            priority: 900);

        builder.AddData<SceneMeshRenderer>(
            "MeshRenderer",
            SceneComponentTraits.Singleton | SceneComponentTraits.Serializable,
            priority: 100);

        builder.AddData<SceneFog>(
            "Fog",
            SceneComponentTraits.Singleton | SceneComponentTraits.Serializable,
            priority: 950);

        return builder;
    }

    /// <summary>Registers the 3D light components.</summary>
    public static SceneProfileBuilder AddLighting3D(this SceneProfileBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddData<SceneDirectionalLight>(
            "DirectionalLight",
            SceneComponentTraits.Singleton | SceneComponentTraits.Serializable,
            priority: 800);

        builder.AddData<ScenePointLight>(
            "PointLight",
            SceneComponentTraits.Singleton | SceneComponentTraits.Serializable,
            priority: 800);

        builder.AddData<SceneSpotLight>(
            "SpotLight",
            SceneComponentTraits.Singleton | SceneComponentTraits.Serializable,
            priority: 800);

        return builder;
    }
}
