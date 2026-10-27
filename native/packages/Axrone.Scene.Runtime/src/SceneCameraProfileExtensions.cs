namespace Axrone.Scene.Runtime;

/// <summary>Profile composition helpers for scene camera controllers.</summary>
public static class SceneCameraProfileExtensions
{
    /// <summary>Registers the orbit and follow camera controllers.</summary>
    public static SceneProfileBuilder AddCameraControllers(this SceneProfileBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddBehaviour<SceneOrbitCameraControllerBehaviour>(
            "OrbitCameraController",
            priority: 800);

        builder.AddBehaviour<SceneFollowCameraControllerBehaviour>(
            "FollowCameraController",
            priority: 790);

        return builder;
    }
}
