namespace Axrone.Scene.Runtime;

/// <summary>Profile composition helpers for particle systems.</summary>
public static class SceneParticleProfileExtensions
{
    /// <summary>Registers the particle system component.</summary>
    public static SceneProfileBuilder AddParticles3D(this SceneProfileBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddData<SceneParticleSystem>(
            "ParticleSystem",
            SceneComponentTraits.Singleton | SceneComponentTraits.Serializable,
            priority: 120);

        return builder;
    }
}
