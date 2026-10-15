namespace Axrone.Input;

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
/// Contention backoff for lock-free loops. Monomorphized, no vtable.
/// </summary>
public interface IBackoffPolicy
{
    /// <summary>Backs off once, advancing the iteration cursor.</summary>
    static abstract void SpinOnce(ref int iteration);
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
    QueueCapacity Capacity { get; }

    /// <summary>Enqueues an event; <see langword="false"/> when full or completed.</summary>
    bool TryEnqueue(in RawInputEvent evt);

    /// <summary>Dequeues an event; <see langword="false"/> when empty.</summary>
    bool TryDequeue(out RawInputEvent evt);

    /// <summary>Rejects further writes; readers drain what remains.</summary>
    /// <param name="failure">Optional terminal error.</param>
    void Complete(Exception? failure = null);
}

/// <summary>
/// Power-of-two queue capacity with its index mask.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct QueueCapacity :
    IComparable<QueueCapacity>,
    IComparisonOperators<QueueCapacity, QueueCapacity, bool>
{
    /// <summary>Slot count.</summary>
    public uint Value { get; }

    /// <summary>Index mask (capacity minus one).</summary>
    public uint Mask { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private QueueCapacity(uint value)
    {
        Value = value;
        Mask = value - 1;
    }

    /// <summary>Creates a capacity; must be a power of two, at least two.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QueueCapacity Create(uint powerOfTwoCapacity)
    {
        if (powerOfTwoCapacity < 2 || (powerOfTwoCapacity & (powerOfTwoCapacity - 1)) != 0)
        {
            ThrowHelper.ThrowMustBePowerOfTwo();
        }
        return new QueueCapacity(powerOfTwoCapacity);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(QueueCapacity other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left capacity is smaller.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(QueueCapacity left, QueueCapacity right) => left.Value < right.Value;

    /// <summary>Determines whether the left capacity is larger.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(QueueCapacity left, QueueCapacity right) => left.Value > right.Value;

    /// <summary>Determines whether the left capacity is smaller or equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(QueueCapacity left, QueueCapacity right) => left.Value <= right.Value;

    /// <summary>Determines whether the left capacity is larger or equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(QueueCapacity left, QueueCapacity right) => left.Value >= right.Value;

    /// <summary>Unwraps the slot count.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(QueueCapacity capacity) => capacity.Value;
}

/// <summary>
/// Adaptive backoff: spins with growth, then yields.
/// </summary>
public readonly struct AdaptiveBackoff : IBackoffPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SpinOnce(ref int iteration)
    {
        iteration++;
        if ((uint)iteration <= 10)
        {
            Thread.SpinWait(1 << iteration);
        }
        else if ((uint)iteration <= 20)
        {
            Thread.SpinWait(1024);
        }
        else
        {
            Thread.Yield();
        }
    }
}

/// <summary>
/// Minimal fixed-spin backoff for short critical sections.
/// </summary>
public readonly struct SpinWaitBackoff : IBackoffPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SpinOnce(ref int iteration)
    {
        iteration++;
        Thread.SpinWait(4);
    }
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
