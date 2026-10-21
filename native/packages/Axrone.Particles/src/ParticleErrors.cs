namespace Axrone.Particles;

/// <summary>Particle system failure with a stable error code.</summary>
public sealed class ParticleSystemException : Exception
{
    /// <summary>Error code.</summary>
    public ParticleSystemErrorCode ErrorCode { get; }

    /// <summary>Creates an error.</summary>
    public ParticleSystemException(ParticleSystemErrorCode errorCode, string message)
        : base($"[ParticleSystem Error 0x{(uint)errorCode:X4}] {message}")
    {
        ErrorCode = errorCode;
    }
}

/// <summary>Centralized throw paths (keeps hot paths inline).</summary>
internal static class ParticleThrowHelper
{
    /// <summary>Throws a coded error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw(ParticleSystemErrorCode code, string message) =>
        throw new ParticleSystemException(code, message);

    /// <summary>Throws system-not-initialized.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void SystemNotInitialized() =>
        Throw(ParticleSystemErrorCode.SystemNotInitialized, "Particle System has not been initialized.");

    /// <summary>Throws particle-not-found.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ParticleNotFound(ParticleId id) =>
        Throw(ParticleSystemErrorCode.ParticleNotFound, $"Particle with {id} was not found.");

    /// <summary>Throws module-not-found.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ModuleNotFound(string name) =>
        Throw(ParticleSystemErrorCode.ModuleNotFound, $"Module '{name}' was not found in pipeline.");

    /// <summary>Throws capacity-exceeded.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void CapacityExceeded(int cap) =>
        Throw(ParticleSystemErrorCode.CapacityExceeded, $"Buffer capacity of {cap} elements exceeded.");

    /// <summary>Throws invalid-configuration.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidConfiguration(string detail) =>
        Throw(ParticleSystemErrorCode.InvalidConfiguration, $"Configuration invalid: {detail}");

    /// <summary>Throws memory-allocation-failed.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void MemoryAllocationFailed(nuint bytes) =>
        Throw(ParticleSystemErrorCode.MemoryAllocationFailed, $"Failed to allocate {bytes} aligned bytes.");

    /// <summary>Throws invalid-state.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidState(string detail) =>
        Throw(ParticleSystemErrorCode.InvalidState, $"Operation failed due to state: {detail}");

    /// <summary>Throws thread-safety-violation.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThreadSafetyViolation() =>
        Throw(ParticleSystemErrorCode.ThreadSafetyViolation, "Concurrent state corruption detected.");
}
