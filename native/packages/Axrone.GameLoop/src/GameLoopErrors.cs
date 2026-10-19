namespace Axrone.GameLoop;

/// <summary>
/// Coded game-loop error.
/// </summary>
public class GameLoopException : Exception
{
    /// <summary>Machine-readable code.</summary>
    public string Code { get; }

    /// <summary>Creates an error.</summary>
    public GameLoopException(string code, string message)
        : base(message) => Code = code;

    /// <summary>Creates an error with an inner exception.</summary>
    protected GameLoopException(string code, string message, Exception? inner)
        : base(message, inner) => Code = code;
}

/// <summary>
/// Invalid game-loop configuration.
/// </summary>
public sealed class GameLoopConfigurationException(string code, string message) : GameLoopException(code, message);

/// <summary>
/// Use after game-loop disposal.
/// </summary>
public sealed class GameLoopDisposedException(string message) : GameLoopException("loop.disposed", message);

/// <summary>
/// Invalid loop snapshot.
/// </summary>
public sealed class GameLoopSnapshotException(string message) : GameLoopException("loop.snapshot.invalid", message);

/// <summary>
/// System failure during a phase.
/// </summary>
public sealed class GameLoopSystemException(string message, SystemId systemId, GameLoopPhase phase, int attempt, Exception? inner = null)
    : GameLoopException("loop.system.failed", message, inner)
{
    /// <summary>Failing system.</summary>
    public SystemId SystemId { get; } = systemId;

    /// <summary>Failing phase.</summary>
    public GameLoopPhase Phase { get; } = phase;

    /// <summary>Attempt number.</summary>
    public int Attempt { get; } = attempt;
}

/// <summary>
/// Game-loop status.
/// </summary>
public enum GameLoopStatus : byte
{
    /// <summary>Constructed, not running.</summary>
    Idle = 0,

    /// <summary>Rendering.</summary>
    Running = 1,

    /// <summary>Suspended.</summary>
    Paused = 2,

    /// <summary>Stopped.</summary>
    Stopped = 3,

    /// <summary>Disposed.</summary>
    Disposed = 4,
}

/// <summary>
/// Frame phase.
/// </summary>
public enum GameLoopPhase : byte
{
    /// <summary>Before fixed steps.</summary>
    BeforeUpdate = 0,

    /// <summary>Fixed simulation.</summary>
    FixedUpdate = 1,

    /// <summary>Variable update.</summary>
    Update = 2,

    /// <summary>Render.</summary>
    Render = 3,

    /// <summary>After render.</summary>
    AfterFrame = 4,
}

/// <summary>
/// Error disposition from a recovery policy.
/// </summary>
public enum ErrorDisposition : byte
{
    /// <summary>Skip and continue.</summary>
    Continue = 0,

    /// <summary>Retry the system.</summary>
    Retry = 1,

    /// <summary>Pause the loop.</summary>
    Pause = 2,

    /// <summary>Stop the loop.</summary>
    Stop = 3,

    /// <summary>Throw to the host.</summary>
    Escalate = 4,
}

/// <summary>
/// Frame outcome.
/// </summary>
public enum FrameOutcomeKind : byte
{
    /// <summary>Executed normally.</summary>
    Executed = 0,

    /// <summary>Spiral mitigated.</summary>
    SpiralMitigated = 1,

    /// <summary>Zero delta throttled.</summary>
    ThrottledZeroDelta = 2,

    /// <summary>Halted while not running.</summary>
    Halted = 3,
}

/// <summary>
/// Centralized game-loop throw helpers.
/// </summary>
internal static class GameLoopThrowHelper
{
    /// <summary>Throws for an invalid fixed step.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidFixedStep(double value) =>
        throw new GameLoopConfigurationException("loop.invalid-fixed-delta", $"Fixed step {value} must be finite and positive.");

    /// <summary>Throws for an invalid max delta.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidMaxDelta(double value) =>
        throw new GameLoopConfigurationException("loop.invalid-max-delta", $"Max delta {value} must be finite and positive.");

    /// <summary>Throws for an invalid sub-step budget.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidMaxSubSteps(uint value) =>
        throw new GameLoopConfigurationException("loop.invalid-max-sub-steps", $"Max sub-steps {value} must be positive.");

    /// <summary>Throws for an invalid time scale.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidTimeScale(double value) =>
        throw new GameLoopConfigurationException("loop.invalid-time-scale", $"Time scale {value} must be finite and non-negative.");

    /// <summary>Throws for an invalid capacity.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidCapacity(int value) =>
        throw new GameLoopConfigurationException("loop.invalid-capacity", $"Capacity {value} must be positive.");

    /// <summary>Throws when the loop is used after disposal.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowDisposed(string entity) => throw new GameLoopDisposedException($"'{entity}' has already been disposed.");

    /// <summary>Throws for a duplicate system.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowDuplicateSystem(SystemId id) =>
        throw new GameLoopConfigurationException("loop.invalid-system", $"System {id} is already registered.");

    /// <summary>Throws for an escalated system fault.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowSystemFault(SystemId id, GameLoopPhase phase, int attempt, Exception inner) =>
        throw new GameLoopSystemException($"System {id} failed during {phase} on attempt {attempt}.", id, phase, attempt, inner);
}
