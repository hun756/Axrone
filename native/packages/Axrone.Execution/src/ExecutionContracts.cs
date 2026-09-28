namespace Axrone.Execution;

/// <summary>
/// Outcome of a single enqueue attempt. The sequence number names the claimed
/// slot; it is -1 when nothing was claimed.
/// </summary>
public enum EnqueueStatus : byte
{
    /// <summary>Command claimed a slot and is visible to the consumer.</summary>
    Enqueued = 0,

    /// <summary>Ring is full; nothing was claimed. Back off and retry.</summary>
    QueueFull = 1,

    /// <summary>
    /// Pump is draining, stopped, faulted, or disposed; the command was refused.
    /// This is the single refusal status for every non-running lifecycle: a
    /// producer retries on <see cref="QueueFull"/> and gives up on
    /// <see cref="Closed"/> without ever reading <see cref="PumpState"/>, so the
    /// faulted state is observable through <c>CommandPump.State</c> alone.
    /// </summary>
    Closed = 2,

    /// <summary>
    /// The pump is faulted. Reserved for callers that reclassify a refusal using
    /// <c>CommandPump.State</c> (queue wrappers that surface
    /// <see cref="PumpState.Faulted"/> as an enqueue outcome);
    /// <c>CommandPump.TryEnqueue</c> itself never returns it, because a faulted
    /// pump refuses exactly like any other non-running state, through
    /// <see cref="Closed"/>.
    /// </summary>
    Faulted = 3,
}

/// <summary>
/// Names a claimed ring slot. Returned by value; never stored on the hot path.
/// </summary>
/// <param name="SequenceNumber">Claimed slot sequence, or -1 when unclaimed.</param>
/// <param name="Status">The enqueue outcome.</param>
public readonly record struct EnqueueResult(long SequenceNumber, EnqueueStatus Status)
{
    /// <summary>Whether the command was accepted by the pump.</summary>
    public bool IsEnqueued
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => Status == EnqueueStatus.Enqueued;
    }
}

/// <summary>
/// Lifecycle of a command pump. Producers may enqueue only while
/// <see cref="Running"/>; the consumer drains through <see cref="Draining"/>
/// into <see cref="Stopped"/>, and every other state refuses enqueues with
/// <see cref="EnqueueStatus.Closed"/>.
/// </summary>
public enum PumpState : int
{
    /// <summary>Accepting commands.</summary>
    Running = 0,

    /// <summary>Refusing new commands; previously queued work still pumps.</summary>
    Draining = 1,

    /// <summary>Drained and idle.</summary>
    Stopped = 2,

    /// <summary>Native memory released.</summary>
    Disposed = 3,

    /// <summary>
    /// A processor threw. The pump refuses further enqueues, reports the fault
    /// through <see cref="IExecutorTelemetry.FaultEncountered"/>, and leaves the
    /// faulting command at the head of the ring so a retry sees the same head of
    /// queue. The fault itself is rethrown to the pumping caller.
    /// </summary>
    /// <remarks>
    /// Appended after <see cref="Disposed"/> on purpose: hosts persist these
    /// values into logs and telemetry sinks, so existing values must never move.
    /// A faulted pump that is then disposed reports <see cref="Disposed"/>,
    /// because the ring's native memory is gone and disposal is the state callers
    /// must honor. Pump state therefore reads as a terminal failure that
    /// <see cref="Disposed"/> supersedes.
    /// </remarks>
    Faulted = 4,
}

/// <summary>
/// Static command processor: the unit of work a pump executes. Struct
/// implementations devirtualize at the JIT call site (same idiom as
/// <c>Axrone.Batching.IBatchKernel</c> and the Simd static strategies);
/// the context may be a <c>ref struct</c>, so pumps can thread caller-owned
/// spans and scratch buffers with zero allocation.
/// </summary>
/// <typeparam name="TCommand">Command type. Unmanaged so the ring backs onto native memory.</typeparam>
/// <typeparam name="TContext">Consumer context. May be a ref struct.</typeparam>
public interface ICommandProcessor<TCommand, TContext>
    where TCommand : unmanaged
    where TContext : allows ref struct
{
    /// <summary>Processes one command.</summary>
    /// <param name="command">The command to process. The callee may not retain this reference.</param>
    /// <param name="context">The consumer-owned context.</param>
    static abstract void Process(ref TCommand command, ref TContext context);
}

/// <summary>
/// Cold-path telemetry for a command pump. Struct sinks with empty bodies
/// (see <see cref="NullExecutorTelemetry"/>) are eliminated by the JIT.
/// </summary>
public interface IExecutorTelemetry
{
    /// <summary>Called when a command is accepted.</summary>
    static abstract void ItemEnqueued();

    /// <summary>Called when a command finishes processing.</summary>
    static abstract void ItemDequeued();

    /// <summary>Called when an enqueue attempt finds the ring full.</summary>
    static abstract void QueueSaturated();

    /// <summary>
    /// Called once per blocking-enqueue retry round that lost the race for a
    /// slot, immediately before the backoff step. A sustained count means the
    /// ring is full while the consumer is not draining fast enough.
    /// </summary>
    static abstract void ContentionDetected();

    /// <summary>
    /// Called when a processor faulted the pump and the lifecycle moved to
    /// <see cref="PumpState.Faulted"/>. The pump still rethrows the fault to the
    /// pumping caller; this is the observing channel, not a recovery channel.
    /// </summary>
    /// <param name="exception">The faulting processor's exception.</param>
    static abstract void FaultEncountered(Exception exception);
}

/// <summary>
/// No-op telemetry sink, eliminated by dead-code removal. The default for
/// production pumps where per-item counting would cost hot-path cycles.
/// </summary>
public readonly struct NullExecutorTelemetry : IExecutorTelemetry, IEquatable<NullExecutorTelemetry>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ItemEnqueued() { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ItemDequeued() { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void QueueSaturated() { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ContentionDetected() { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FaultEncountered(Exception exception) { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(NullExecutorTelemetry other) => true;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is NullExecutorTelemetry;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => 0;

    /// <summary>Equality operator.</summary>
    public static bool operator ==(NullExecutorTelemetry left, NullExecutorTelemetry right) => true;

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(NullExecutorTelemetry left, NullExecutorTelemetry right) => false;
}

/// <summary>
/// Pump sizing. Capacity must be a power of two greater than or equal to 2.
/// </summary>
public sealed class ExecutorOptions
{
    private int _capacity = 1024;

    /// <summary>Ring slot capacity.</summary>
    public int Capacity
    {
        get => _capacity;
        init
        {
            if (value < 2 || (value & (value - 1)) != 0)
            {
                ThrowHelper.ThrowInvalidCapacity(value);
            }

            _capacity = value;
        }
    }
}
