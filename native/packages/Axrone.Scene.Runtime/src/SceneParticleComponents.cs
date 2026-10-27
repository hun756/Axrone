namespace Axrone.Scene.Runtime;

/// <summary>
/// Particle system data component. The bridge owns the simulation state and
/// uses this payload for emission and per-particle start values.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct SceneParticleSystem : ISceneDataComponent
{
    /// <summary>Whether the system participates in simulation.</summary>
    public bool Enabled;

    /// <summary>Whether the system emits new particles.</summary>
    public bool Playing;

    /// <summary>Emission rate in particles per second.</summary>
    public float EmissionRate;

    /// <summary>Particle lifetime in seconds.</summary>
    public float StartLifetime;

    /// <summary>Initial speed along the emission cone.</summary>
    public float StartSpeed;

    /// <summary>Initial particle size in pixels.</summary>
    public float StartSize;

    /// <summary>Initial particle color.</summary>
    public Vec4 StartColor;

    /// <summary>Emission cone spread in the zero-to-one range.</summary>
    public float Spread;

    /// <summary>Maximum live particles.</summary>
    public int MaxParticles;

    /// <summary>Default: 100 particles/second, one-second lifetime, warm white.</summary>
    public static SceneParticleSystem Default
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new()
        {
            Enabled = true,
            Playing = true,
            EmissionRate = 100.0f,
            StartLifetime = 1.0f,
            StartSpeed = 2.0f,
            StartSize = 8.0f,
            StartColor = new Vec4(1.0f, 0.8f, 0.4f, 1.0f),
            Spread = 0.35f,
            MaxParticles = 512
        };
    }
}
