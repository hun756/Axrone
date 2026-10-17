namespace Axrone.Input;

using Axrone.Collections;
using Axrone.Utility.Backoff;

/// <summary>
/// Lock-free multi-producer multi-consumer ring buffer for raw input events,
/// backed by the shared <see cref="VyukovBoundedBatchQueue{T, TBackoff}"/>
/// transport. Producer threads (OS callbacks) never block the consumer
/// (frame loop). The input metrics sink observes enqueue, drop and drain.
/// </summary>
/// <typeparam name="TBackoff">Contention backoff policy.</typeparam>
/// <typeparam name="TMetrics">Queue telemetry sink.</typeparam>
public sealed class LockFreeInputRingBuffer<TBackoff, TMetrics> :
    IInputEventQueue, IAsyncDisposable
    where TBackoff : struct, ISpinBackoff
    where TMetrics : struct, IInputMetricsSink
{
    private readonly VyukovBoundedBatchQueue<RawInputEvent, TBackoff> _inner;
    private int _completed;
    private int _disposed;

    /// <summary>Queue capacity.</summary>
    public QueueCapacity Capacity { get; }

    /// <summary>Creates a queue with the given capacity.</summary>
    public LockFreeInputRingBuffer(QueueCapacity capacity)
    {
        Capacity = capacity;
        _inner = new VyukovBoundedBatchQueue<RawInputEvent, TBackoff>(
            new BufferCapacity(capacity.Value));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWrite(in RawInputEvent evt) => TryEnqueue(in evt);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead(out RawInputEvent evt) => TryDequeue(out evt);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryEnqueue(in RawInputEvent evt)
    {
        if (Volatile.Read(ref _completed) != 0)
        {
            TMetrics.OnEventDropped(evt.EventType);
            return false;
        }

        if (_inner.TryEnqueue(in evt))
        {
            TMetrics.OnEventEnqueued(evt.EventType);
            return true;
        }

        TMetrics.OnEventDropped(evt.EventType);
        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryDequeue(out RawInputEvent evt) => _inner.TryDequeue(out evt);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int Drain(Span<RawInputEvent> destination)
    {
        int count = _inner.TryDequeueBatch(destination);
        if (count > 0)
        {
            TMetrics.OnEventsDrained(count);
        }

        return count;
    }

    /// <inheritdoc/>
    public void Complete(Exception? failure = null) => Volatile.Write(ref _completed, 1);

    /// <inheritdoc/>
    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        SpinWait spinner = new();
        while (!_inner.IsEmpty)
        {
            cancellationToken.ThrowIfCancellationRequested();
            spinner.SpinOnce();
            if (spinner.NextSpinWillYield)
            {
                await Task.Yield();
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _inner.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        Complete();
        try
        {
            await DrainAsync().ConfigureAwait(false);
        }
        finally
        {
            Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>Finalizer releases the queue when Dispose was skipped.</summary>
    ~LockFreeInputRingBuffer() => Dispose();
}
