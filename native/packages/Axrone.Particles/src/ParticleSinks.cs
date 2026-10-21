namespace Axrone.Particles;

/// <summary>Particle event sink contract.</summary>
public interface IParticleEventSink
{
    /// <summary>Handles birth.</summary>
    void OnBirth(in ParticleBirthEvent evt);

    /// <summary>Handles death.</summary>
    void OnDeath(in ParticleDeathEvent evt);

    /// <summary>Handles collision.</summary>
    void OnCollision(in ParticleCollisionEvent evt);
}

/// <summary>Null event sink (drops all events).</summary>
public readonly struct NullEventSink : IParticleEventSink
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnBirth(in ParticleBirthEvent evt)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnDeath(in ParticleDeathEvent evt)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnCollision(in ParticleCollisionEvent evt)
    {
    }
}

/// <summary>Delegate event sink.</summary>
public sealed class DelegateEventSink : IParticleEventSink
{
    /// <summary>Birth handler.</summary>
    public Action<ParticleBirthEvent>? Birth { get; set; }

    /// <summary>Death handler.</summary>
    public Action<ParticleDeathEvent>? Death { get; set; }

    /// <summary>Collision handler.</summary>
    public Action<ParticleCollisionEvent>? Collision { get; set; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnBirth(in ParticleBirthEvent evt) => Birth?.Invoke(evt);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnDeath(in ParticleDeathEvent evt) => Death?.Invoke(evt);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnCollision(in ParticleCollisionEvent evt) => Collision?.Invoke(evt);
}
