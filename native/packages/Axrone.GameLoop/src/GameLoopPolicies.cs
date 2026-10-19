namespace Axrone.GameLoop;

/// <summary>
/// Clock policy: monotonic game time.
/// </summary>
public interface IClockPolicy
{
    /// <summary>Samples now.</summary>
    static abstract GameTime Now();
}

/// <summary>
/// Stopwatch clock with a process-epoch zero point.
/// </summary>
public readonly struct HighResolutionStopwatchClock : IClockPolicy
{
    private static readonly double MillisecondsPerTick = 1000.0 / Stopwatch.Frequency;
    private static readonly long EpochTimestamp = Stopwatch.GetTimestamp();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameTime Now()
    {
        long ticks = Stopwatch.GetTimestamp() - EpochTimestamp;
        return GameTime.FromMilliseconds(ticks * MillisecondsPerTick);
    }
}

/// <summary>
/// Spiral-of-death mitigation: caps steps per frame, reports dropped time.
/// </summary>
public interface ISpiralMitigationPolicy
{
    /// <summary>Computes steps and dropped time.</summary>
    static abstract void Mitigate(
        ref double accumulatorMs,
        double fixedStepMs,
        uint maxSubSteps,
        out uint stepsToExecute,
        out double droppedMs);
}

/// <summary>
/// Drops the remainder modulo one step.
/// </summary>
public readonly struct ModuloSpiralMitigation : ISpiralMitigationPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Mitigate(
        ref double accumulatorMs,
        double fixedStepMs,
        uint maxSubSteps,
        out uint stepsToExecute,
        out double droppedMs)
    {
        double available = accumulatorMs / fixedStepMs;
        uint totalAvailableSteps = (uint)available;

        if (totalAvailableSteps <= maxSubSteps)
        {
            stepsToExecute = totalAvailableSteps;
            droppedMs = 0.0;
            accumulatorMs -= totalAvailableSteps * fixedStepMs;
        }
        else
        {
            stepsToExecute = maxSubSteps;
            double consumedMs = maxSubSteps * fixedStepMs;
            droppedMs = accumulatorMs - consumedMs;
            accumulatorMs %= fixedStepMs;
        }
    }
}

/// <summary>
/// Drops the whole backlog past the budget.
/// </summary>
public readonly struct StrictDropSpiralMitigation : ISpiralMitigationPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Mitigate(
        ref double accumulatorMs,
        double fixedStepMs,
        uint maxSubSteps,
        out uint stepsToExecute,
        out double droppedMs)
    {
        double available = accumulatorMs / fixedStepMs;
        uint totalAvailableSteps = (uint)available;

        if (totalAvailableSteps <= maxSubSteps)
        {
            stepsToExecute = totalAvailableSteps;
            droppedMs = 0.0;
            accumulatorMs -= totalAvailableSteps * fixedStepMs;
        }
        else
        {
            stepsToExecute = maxSubSteps;
            droppedMs = accumulatorMs - (maxSubSteps * fixedStepMs);
            accumulatorMs = 0.0;
        }
    }
}

/// <summary>
/// Error recovery policy: maps a system exception to a disposition.
/// </summary>
/// <typeparam name="TState">Frame state type.</typeparam>
public interface IErrorRecoveryPolicy<TState>
{
    /// <summary>Handles a system error.</summary>
    static abstract ErrorDisposition OnError(
        Exception exception,
        SystemId systemId,
        GameLoopPhase phase,
        int attempt,
        ref TState state);
}

/// <summary>
/// Escalates every error.
/// </summary>
/// <typeparam name="TState">Frame state type.</typeparam>
public readonly struct FailFastErrorPolicy<TState> : IErrorRecoveryPolicy<TState>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ErrorDisposition OnError(
        Exception exception,
        SystemId systemId,
        GameLoopPhase phase,
        int attempt,
        ref TState state) => ErrorDisposition.Escalate;
}

/// <summary>
/// Swallows errors and continues.
/// </summary>
/// <typeparam name="TState">Frame state type.</typeparam>
public readonly struct SuppressAndContinueErrorPolicy<TState> : IErrorRecoveryPolicy<TState>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ErrorDisposition OnError(
        Exception exception,
        SystemId systemId,
        GameLoopPhase phase,
        int attempt,
        ref TState state) => ErrorDisposition.Continue;
}

/// <summary>
/// Stops the loop on error.
/// </summary>
/// <typeparam name="TState">Frame state type.</typeparam>
public readonly struct HaltOnErrorPolicy<TState> : IErrorRecoveryPolicy<TState>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ErrorDisposition OnError(
        Exception exception,
        SystemId systemId,
        GameLoopPhase phase,
        int attempt,
        ref TState state) => ErrorDisposition.Stop;
}
