namespace Axrone.Scene.Runtime;

/// <summary>Profile composition helpers for 3D physics components.</summary>
public static class ScenePhysicsProfileExtensions
{
    /// <summary>Registers the 3D rigidbody and collider components.</summary>
    public static SceneProfileBuilder AddPhysics3D(this SceneProfileBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddData<SceneRigidbody3D>(
            "Rigidbody3D",
            SceneComponentTraits.Singleton | SceneComponentTraits.Serializable,
            priority: 200);

        builder.AddData<SceneCollider3D>(
            "Collider3D",
            SceneComponentTraits.Singleton | SceneComponentTraits.Serializable,
            priority: 150);

        return builder;
    }
}
