namespace Axrone.Physics2D;

using System.Runtime.CompilerServices;

/// <summary>
/// Compile-time behavioral trait for quiet thresholds, kinematic interaction
/// and sleep transitions. Specializes statically with zero virtual overhead.
/// </summary>
/// <typeparam name="TSelf">The policy type itself.</typeparam>
public interface ISleepPolicy<TSelf>
    where TSelf : struct, ISleepPolicy<TSelf>
{
    /// <summary>Whether velocities read as quiet.</summary>
    static abstract bool IsQuiet(
        in Vec2 linearVelocity,
        float angularVelocity,
        float linTolSq,
        float angTolSq);

    /// <summary>Whether a kinematic body reads as moving.</summary>
    static abstract bool IsKinematicMoving(
        in Vec2 linearVelocity,
        float angularVelocity,
        float linTolSq,
        float angTolSq);

    /// <summary>Advances or resets a quiet timer.</summary>
    static abstract float UpdateSleepTimer(
        float currentSleepTime,
        float dt,
        bool isQuiet);

    /// <summary>Whether an island qualifies for sleep.</summary>
    static abstract bool ShouldSleep(
        float minSleepTime,
        float timeToSleep,
        bool isBlocked);
}

/// <summary>Canonical Box2D-style sleep evaluation policy.</summary>
public readonly struct DefaultSleepPolicy : ISleepPolicy<DefaultSleepPolicy>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool IsQuiet(
        in Vec2 linearVelocity,
        float angularVelocity,
        float linTolSq,
        float angTolSq) =>
        linearVelocity.LengthSquared() < linTolSq &&
        (angularVelocity * angularVelocity) < angTolSq;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool IsKinematicMoving(
        in Vec2 linearVelocity,
        float angularVelocity,
        float linTolSq,
        float angTolSq) =>
        linearVelocity.LengthSquared() >= linTolSq ||
        (angularVelocity * angularVelocity) >= angTolSq;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float UpdateSleepTimer(float currentSleepTime, float dt, bool isQuiet) =>
        isQuiet ? currentSleepTime + dt : 0.0f;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldSleep(float minSleepTime, float timeToSleep, bool isBlocked) =>
        !isBlocked && minSleepTime >= timeToSleep;
}

/// <summary>Sleep policy with a looser quiet band, faster timer and earlier threshold.</summary>
public readonly struct AggressiveSleepPolicy : ISleepPolicy<AggressiveSleepPolicy>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool IsQuiet(
        in Vec2 linearVelocity,
        float angularVelocity,
        float linTolSq,
        float angTolSq) =>
        linearVelocity.LengthSquared() < (linTolSq * 1.5f) &&
        (angularVelocity * angularVelocity) < (angTolSq * 1.5f);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool IsKinematicMoving(
        in Vec2 linearVelocity,
        float angularVelocity,
        float linTolSq,
        float angTolSq) =>
        linearVelocity.LengthSquared() >= linTolSq ||
        (angularVelocity * angularVelocity) >= angTolSq;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float UpdateSleepTimer(float currentSleepTime, float dt, bool isQuiet) =>
        isQuiet ? currentSleepTime + (dt * 1.25f) : 0.0f;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldSleep(float minSleepTime, float timeToSleep, bool isBlocked) =>
        !isBlocked && minSleepTime >= (timeToSleep * 0.8f);
}
