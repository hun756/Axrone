namespace Axrone.Scene.Rendering;

/// <summary>
/// Selects the frame camera: the first active primary camera, otherwise the
/// first active enabled camera in stable actor order.
/// </summary>
public static class SceneCameraSelector
{
    /// <summary>Selects a camera actor.</summary>
    public static bool TrySelect(SceneWorld world, out SceneActor cameraActor)
    {
        ArgumentNullException.ThrowIfNull(world);

        SceneActor fallback = SceneActor.Null;
        foreach (SceneActor actor in world.Actors)
        {
            // Actors carrying the 2D marker are owned by the 2D selector.
            if (!world.IsActiveInHierarchy(actor) ||
                !world.HasData<SceneCamera>(actor) ||
                world.HasData<SceneCamera2D>(actor))
            {
                continue;
            }

            ref SceneCamera camera = ref world.GetData<SceneCamera>(actor);
            if (!camera.Enabled)
            {
                continue;
            }

            if (camera.Primary)
            {
                cameraActor = actor;
                return true;
            }

            if (fallback.IsNull)
            {
                fallback = actor;
            }
        }

        cameraActor = fallback;
        return cameraActor.IsValid;
    }
}
