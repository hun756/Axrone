namespace Axrone.Particles;

/// <summary>Shape-driven particle emitter.</summary>
public struct ParticleEmitter
{
    /// <summary>Emitter identity (0 = manual emission).</summary>
    public uint EmitterId;

    /// <summary>Emission rate module.</summary>
    public EmissionModule Emission;

    /// <summary>Emission shape module.</summary>
    public ShapeModule Shape;

    /// <summary>Emitter origin.</summary>
    public Vec3 Position;

    /// <summary>Enabled flag.</summary>
    public bool Enabled;

    /// <summary>Creates an emitter.</summary>
    public ParticleEmitter(uint emitterId)
    {
        EmitterId = emitterId;
        Emission = new EmissionModule();
        Shape = new ShapeModule();
        Position = Vec3.Zero;
        Enabled = true;
    }
}
