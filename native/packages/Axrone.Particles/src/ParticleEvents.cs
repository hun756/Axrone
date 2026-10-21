namespace Axrone.Particles;

/// <summary>Axis-aligned particle bounds.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ParticleBounds(Vec3 Min, Vec3 Max) : IEquatable<ParticleBounds>
{
    /// <summary>Whether a point is inside.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Contains(in Vec3 point) =>
        point.X >= Min.X && point.X <= Max.X &&
        point.Y >= Min.Y && point.Y <= Max.Y &&
        point.Z >= Min.Z && point.Z <= Max.Z;

    /// <summary>Whether two bounds overlap.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Intersects(in ParticleBounds other) =>
        Min.X <= other.Max.X && Max.X >= other.Min.X &&
        Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
        Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
}

/// <summary>Emission burst definition.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ParticleBurst(
    float Time,
    int MinCount,
    int MaxCount,
    int Cycles,
    float Interval,
    float Probability);

/// <summary>Particle birth event.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ParticleBirthEvent(ParticleId ParticleId, uint EmitterId, float InitialLifetime);

/// <summary>Particle death event.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ParticleDeathEvent(ParticleId ParticleId, ParticleDeathReason Reason, float Age, float Lifetime);

/// <summary>Particle collision event.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ParticleCollisionEvent(ParticleId ParticleId, uint ColliderId, Vec3 Normal, float Impulse);
