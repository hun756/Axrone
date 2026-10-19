namespace Axrone.Input;

using Axrone.Collections;

/// <summary>
/// Writes one event without blocking.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IEventWriter<TEvent> where TEvent : allows ref struct
{
    /// <summary>Writes an event; <see langword="false"/> when the queue is full or completed.</summary>
    bool TryWrite(in TEvent evt);
}

/// <summary>
/// Reads one event without blocking.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IEventReader<TEvent> where TEvent : allows ref struct
{
    /// <summary>Reads an event; <see langword="false"/> when the queue is empty.</summary>
    bool TryRead(out TEvent evt);
}

/// <summary>
/// Bulk-drains events into caller memory.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IBatchDrainable<TEvent> where TEvent : struct
{
    /// <summary>Drains up to the destination length; returns events written.</summary>
    int Drain(Span<TEvent> destination);
}

/// <summary>
/// Waits until queued work settles.
/// </summary>
public interface IAsyncDrainable
{
    /// <summary>Drains asynchronously; honors cancellation.</summary>
    ValueTask DrainAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Zero-cost queue telemetry. Monomorphized, no vtable.
/// </summary>
public interface IInputMetricsSink
{
    /// <summary>Observes an enqueued event.</summary>
    static abstract void OnEventEnqueued(RawInputType type);

    /// <summary>Observes a dropped event.</summary>
    static abstract void OnEventDropped(RawInputType type);

    /// <summary>Observes a drain batch.</summary>
    static abstract void OnEventsDrained(int count);
}

/// <summary>
/// Input event queue: write, read, bulk drain and completion.
/// </summary>
public interface IInputEventQueue :
    IEventWriter<RawInputEvent>,
    IEventReader<RawInputEvent>,
    IBatchDrainable<RawInputEvent>,
    IAsyncDrainable,
    IDisposable
{
    /// <summary>Queue capacity.</summary>
    BufferCapacity Capacity { get; }

    /// <summary>Enqueues an event; <see langword="false"/> when full or completed.</summary>
    bool TryEnqueue(in RawInputEvent evt);

    /// <summary>Dequeues an event; <see langword="false"/> when empty.</summary>
    bool TryDequeue(out RawInputEvent evt);

    /// <summary>Rejects further writes; readers drain what remains.</summary>
    /// <param name="failure">Optional terminal error.</param>
    void Complete(Exception? failure = null);
}

/// <summary>
/// Adaptive backoff: spins with growth, then yields.
/// </summary>
public readonly struct AdaptiveBackoff : ISpinBackoff
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Initialize(out int state) => state = 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Advance(ref int state)
    {
        state++;
        if ((uint)state <= 10)
        {
            Thread.SpinWait(1 << state);
        }
        else if ((uint)state <= 20)
        {
            Thread.SpinWait(1024);
        }
        else
        {
            Thread.Yield();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Reset(ref int state) => state = 0;
}

/// <summary>
/// Minimal fixed-spin backoff for short critical sections.
/// </summary>
public readonly struct SpinWaitBackoff : ISpinBackoff
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Initialize(out int state) => state = 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Advance(ref int state)
    {
        state++;
        Thread.SpinWait(4);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Reset(ref int state) => state = 0;
}

/// <summary>
/// No-op metrics sink, elided by the JIT.
/// </summary>
public readonly struct NullMetricsPolicy : IInputMetricsSink
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnEventEnqueued(RawInputType type)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnEventDropped(RawInputType type)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnEventsDrained(int count)
    {
    }
}
