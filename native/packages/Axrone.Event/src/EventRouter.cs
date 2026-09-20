namespace Axrone.Event;

/// <summary>
/// Single-type fan-out router over a bounded MPMC queue. Publishers stamp at the edge, one
/// background loop dispatches batches to active subscribers, failures land in dead letters.
/// </summary>
/// <remarks>
/// The dispatch loop starts at construction and drains continuously, so a publish can never spin
/// forever waiting for a future subscriber: envelopes with no active subscriber are dequeued,
/// counted as dropped, and discarded. <see cref="Publish(in TEvent)"/> spins while running;
/// <see cref="TryPublish(in TEvent)"/> is the non-blocking game-thread path.
/// </remarks>
/// <typeparam name="TEvent">Payload type.</typeparam>
public sealed class EventRouter<TEvent> : IDisposable, IAsyncDisposable
{
    private const int StateRunning = 0;
    private const int StateCompleting = 1;
    private const int StateTerminated = 2;
    private const int StateFaulted = 3;

    private readonly VyukovBoundedBatchQueue<EventEnvelope<TEvent>> _queue;
    private readonly ConcurrentDictionary<Guid, EventSubscription<TEvent>> _subscriptions = new();
    private readonly ConcurrentQueue<DeadLetterEntry<TEvent>> _deadLetters = new();
    private readonly EventEnvelope<TEvent>[] _dispatchBuffer;
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _dispatchLoop;

    private volatile List<IEventFilter<TEvent>> _filters = new();
    private volatile List<IEventInterceptor<TEvent>> _interceptors = new();

    private int _state;
    private long _sequence;
    private long _droppedItems;
    private long _droppedDeadLetters;
    private readonly int _deadLetterCapacity;
    private readonly EventTelemetry _telemetry;
    private ExceptionDispatchInfo? _fault;
    private int _disposed;

    /// <summary>Items dequeued with no active subscriber.</summary>
    public long DroppedItems => Interlocked.Read(ref _droppedItems);

    /// <summary>Dead letters discarded because the bound was full (oldest first).</summary>
    public long DroppedDeadLetters => Interlocked.Read(ref _droppedDeadLetters);

    /// <summary>Envelopes waiting in transport.</summary>
    public int QueuedCount => _queue.Count;

    /// <summary>Registered subscriptions.</summary>
    public int SubscriberCount => _subscriptions.Count;

    /// <summary>Entries waiting in dead letters.</summary>
    public int DeadLetterCount => _deadLetters.Count;

    /// <summary>Creates a router with the given transport capacity (power of two).</summary>
    public EventRouter(int capacity = 65536)
        : this(RouterOptions.Default with { Capacity = capacity })
    {
    }

    /// <summary>Creates a router from options.</summary>
    public EventRouter(RouterOptions options)
    {
        if (options.DispatchBatchSize < 1)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(options), "DispatchBatchSize must be positive.");
        }

        if (options.DeadLetterCapacity < 1)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(options), "DeadLetterCapacity must be positive.");
        }

        _deadLetterCapacity = options.DeadLetterCapacity;
        _telemetry = new EventTelemetry(options.MeterName ?? "Axrone.Event");
        _dispatchBuffer = new EventEnvelope<TEvent>[options.DispatchBatchSize];
        _queue = new VyukovBoundedBatchQueue<EventEnvelope<TEvent>>(new BufferCapacity((uint)options.Capacity));
        CancellationToken token = _cts.Token;
        _dispatchLoop = Task.Factory.StartNew(
            () => DispatchLoopAsync(token),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    /// <summary>Registers a publish-gate filter (copy-on-write; publishers never block).</summary>
    public void AddFilter(IEventFilter<TEvent> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        lock (_gate)
        {
            var next = new List<IEventFilter<TEvent>>(_filters) { filter };
            _filters = next;
        }
    }

    /// <summary>Registers a publish/consumption observer (copy-on-write; publishers never block).</summary>
    public void AddInterceptor(IEventInterceptor<TEvent> interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        lock (_gate)
        {
            var next = new List<IEventInterceptor<TEvent>>(_interceptors) { interceptor };
            _interceptors = next;
        }
    }

    /// <summary>Subscribes a synchronous handler; starts delivery immediately.</summary>
    public IEventSubscription Subscribe(Action<EventEnvelope<TEvent>, CancellationToken> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var subscription = new EventSubscription<TEvent>(handler, RemoveSubscription);
        _subscriptions.TryAdd(subscription.SubscriptionId, subscription);
        return subscription;
    }

    /// <summary>Subscribes an asynchronous handler; starts delivery immediately.</summary>
    public IEventSubscription SubscribeAsync(Func<EventEnvelope<TEvent>, CancellationToken, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var subscription = new EventSubscription<TEvent>(handler, RemoveSubscription);
        _subscriptions.TryAdd(subscription.SubscriptionId, subscription);
        return subscription;
    }

    /// <summary>
    /// Publishes a payload, spinning while the transport is full. Requires a running router;
    /// throws once completed. For subscriber-less emission use <see cref="TryPublish(in TEvent)"/>.
    /// </summary>
    public void Publish(in TEvent message)
    {
        var envelope = Stamp(message);
        PublishEnvelope(in envelope);
    }

    /// <summary>
    /// Publishes a pre-built envelope, preserving an existing stamp (replay) or stamping when
    /// unstamped. Spins while the transport is full.
    /// </summary>
    public void PublishEnvelope(in EventEnvelope<TEvent> envelope)
    {
        ProgressiveSpinBackoff.Initialize(out int backoff);
        while (!TryPublishCore(in envelope))
        {
            ThrowIfFaulted();
            if (Volatile.Read(ref _state) != StateRunning)
            {
                ThrowHelper.ThrowEngineTerminated();
            }

            ProgressiveSpinBackoff.Advance(ref backoff);
        }
    }

    /// <summary>
    /// Non-blocking publish; false when full or not running (backpressure, not an error).
    /// </summary>
    public bool TryPublish(in TEvent message)
    {
        var envelope = Stamp(message);
        return TryPublishCore(in envelope);
    }

    /// <summary>Non-blocking envelope publish; preserves an existing stamp.</summary>
    public bool TryPublishEnvelope(in EventEnvelope<TEvent> envelope) => TryPublishCore(in envelope);

    /// <summary>
    /// Publishes a batch, spinning while the transport is full. Filters apply per item; accepted
    /// items share one sequence-range reservation. Returns accepted items; throws once completed.
    /// </summary>
    public int PublishBatch(ReadOnlySpan<TEvent> items)
    {
        if (items.IsEmpty)
        {
            return 0;
        }

        ThrowIfFaulted();
        if (Volatile.Read(ref _state) != StateRunning)
        {
            ThrowHelper.ThrowEngineTerminated();
        }

        EventEnvelope<TEvent>[] buffer = ArrayPool<EventEnvelope<TEvent>>.Shared.Rent(items.Length);
        try
        {
            int accepted = FillBatch(items, buffer);
            int offset = 0;
            ProgressiveSpinBackoff.Initialize(out int backoff);
            while (offset < accepted)
            {
                int enqueued = _queue.TryEnqueueBatch(buffer.AsSpan(offset, accepted - offset));
                if (enqueued > 0)
                {
                    _telemetry.RecordPublished(enqueued);
                    NotifyPublished(buffer.AsSpan(offset, enqueued));
                    offset += enqueued;
                    continue;
                }

                ThrowIfFaulted();
                if (Volatile.Read(ref _state) != StateRunning)
                {
                    ThrowHelper.ThrowEngineTerminated();
                }

                ProgressiveSpinBackoff.Advance(ref backoff);
            }

            return accepted;
        }
        finally
        {
            ArrayPool<EventEnvelope<TEvent>>.Shared.Return(buffer, clearArray: true);
        }
    }

    /// <summary>
    /// Non-blocking batch publish; single transport reservation. Returns enqueued items
    /// (possibly partial when full); filtered items never consume slots.
    /// </summary>
    public int TryPublishBatch(ReadOnlySpan<TEvent> items)
    {
        if (items.IsEmpty)
        {
            return 0;
        }

        ThrowIfFaulted();
        if (Volatile.Read(ref _state) != StateRunning)
        {
            return 0;
        }

        EventEnvelope<TEvent>[] buffer = ArrayPool<EventEnvelope<TEvent>>.Shared.Rent(items.Length);
        try
        {
            int accepted = FillBatch(items, buffer);
            int enqueued = _queue.TryEnqueueBatch(buffer.AsSpan(0, accepted));
            if (enqueued > 0)
            {
                _telemetry.RecordPublished(enqueued);
                NotifyPublished(buffer.AsSpan(0, enqueued));
            }

            return enqueued;
        }
        finally
        {
            ArrayPool<EventEnvelope<TEvent>>.Shared.Return(buffer, clearArray: true);
        }
    }

    /// <summary>Filters, stamps, and observes publish intent; returns accepted items.</summary>
    private int FillBatch(ReadOnlySpan<TEvent> items, EventEnvelope<TEvent>[] buffer)
    {
        int accepted = 0;
        for (int i = 0; i < items.Length; i++)
        {
            EventEnvelope<TEvent> stamped = Stamp(in items[i]);

            bool filtered = false;
            foreach (IEventFilter<TEvent> filter in _filters)
            {
                if (!filter.ShouldProcess(in stamped))
                {
                    filtered = true;
                    break;
                }
            }

            if (filtered)
            {
                continue;
            }

            foreach (IEventInterceptor<TEvent> interceptor in _interceptors)
            {
                interceptor.OnPublishing(in stamped);
            }

            buffer[accepted++] = stamped;
        }

        return accepted;
    }

    private void NotifyPublished(ReadOnlySpan<EventEnvelope<TEvent>> enqueued)
    {
        foreach (IEventInterceptor<TEvent> interceptor in _interceptors)
        {
            for (int i = 0; i < enqueued.Length; i++)
            {
                interceptor.OnPublished(in enqueued[i]);
            }
        }
    }

    /// <summary>Drains dead letters; the returned list is a cold-path allocation.</summary>
    public List<DeadLetterEntry<TEvent>> DrainDeadLetters()
    {
        var drained = new List<DeadLetterEntry<TEvent>>();
        while (_deadLetters.TryDequeue(out DeadLetterEntry<TEvent> entry))
        {
            drained.Add(entry);
        }

        return drained;
    }

    /// <summary>
    /// Stops accepting publishes. A clean completion lets the loop drain remaining items, then
    /// terminates; a fault stops dispatch immediately without draining.
    /// </summary>
    public void Complete(Exception? error = null)
    {
        if (error is not null)
        {
            int previous = Volatile.Read(ref _state);
            _fault = ExceptionDispatchInfo.Capture(error);
            Volatile.Write(ref _state, StateFaulted);
            EventEventSource.Log.FaultOccurred(error.GetType().Name, error.Message);
            EventEventSource.Log.StateTransition(previous, StateFaulted);
        }
        else if (Interlocked.CompareExchange(ref _state, StateCompleting, StateRunning) == StateRunning)
        {
            EventEventSource.Log.StateTransition(StateRunning, StateCompleting);
        }

        _cts.Cancel();
    }

    /// <summary>Rethrows the terminal fault captured by <see cref="Complete(Exception?)"/>.</summary>
    public void ThrowIfFaulted() => _fault?.Throw();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Complete();
        try
        {
            _dispatchLoop.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Loop observed cancellation mid-batch; shutdown is still clean.
        }

        _queue.Dispose();
        _telemetry.Dispose();
        _cts.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Complete();
        try
        {
            await _dispatchLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Loop observed cancellation mid-batch; shutdown is still clean.
        }

        _queue.Dispose();
        _telemetry.Dispose();
        _cts.Dispose();
    }

    private bool TryPublishCore(in EventEnvelope<TEvent> envelope)
    {
        ThrowIfFaulted();
        if (Volatile.Read(ref _state) != StateRunning)
        {
            return false;
        }

        EventEnvelope<TEvent> stamped = envelope.IsStamped ? envelope : StampEnvelope(in envelope);
        foreach (IEventFilter<TEvent> filter in _filters)
        {
            if (!filter.ShouldProcess(in stamped))
            {
                return true;
            }
        }

        foreach (IEventInterceptor<TEvent> interceptor in _interceptors)
        {
            interceptor.OnPublishing(in stamped);
        }

        if (!_queue.TryEnqueue(in stamped))
        {
            return false;
        }

        _telemetry.RecordPublished(1);

        foreach (IEventInterceptor<TEvent> interceptor in _interceptors)
        {
            interceptor.OnPublished(in stamped);
        }

        return true;
    }

    private EventEnvelope<TEvent> Stamp(in TEvent message)
    {
        Guid id = Guid.NewGuid();
        return new EventEnvelope<TEvent>(
            new EventMetadata(
                id,
                id,
                Guid.Empty,
                Interlocked.Increment(ref _sequence),
                1,
                0,
                Stopwatch.GetTimestamp()),
            message);
    }

    private EventEnvelope<TEvent> StampEnvelope(in EventEnvelope<TEvent> envelope)
    {
        Guid id = Guid.NewGuid();
        EventMetadata metadata = envelope.Metadata.WithIdentity(id, id);
        metadata = metadata.WithSequence(Interlocked.Increment(ref _sequence));
        return new EventEnvelope<TEvent>(metadata, envelope.Payload);
    }

    private void RemoveSubscription(Guid subscriptionId) => _subscriptions.TryRemove(subscriptionId, out _);

    private async Task DispatchLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                int received;
                try
                {
                    received = await _queue.DequeueBatchAsync(_dispatchBuffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await DispatchBatchAsync(_dispatchBuffer.AsMemory(0, received), cancellationToken).ConfigureAwait(false);

                if (Volatile.Read(ref _state) != StateRunning && _queue.IsEmpty)
                {
                    break;
                }
            }
        }
        finally
        {
            if (Volatile.Read(ref _state) != StateFaulted)
            {
                await DrainRemainingAsync().ConfigureAwait(false);
                int previous = Interlocked.Exchange(ref _state, StateTerminated);
                EventEventSource.Log.StateTransition(previous, StateTerminated);
            }
        }
    }

    private async ValueTask DrainRemainingAsync()
    {
        while (true)
        {
            int received = _queue.TryDequeueBatch(_dispatchBuffer.AsSpan());
            if (received == 0)
            {
                break;
            }

            await DispatchBatchAsync(_dispatchBuffer.AsMemory(0, received), CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async ValueTask DispatchBatchAsync(ReadOnlyMemory<EventEnvelope<TEvent>> batch, CancellationToken cancellationToken)
    {
        long start = Stopwatch.GetTimestamp();
        int dispatched = 0;
        int dropped = 0;

        for (int i = 0; i < batch.Length; i++)
        {
            EventEnvelope<TEvent> envelope = batch.Span[i];
            bool delivered = false;

            foreach (EventSubscription<TEvent> subscription in _subscriptions.Values)
            {
                if (!subscription.IsActive)
                {
                    continue;
                }

                delivered = true;
                try
                {
                    await subscription.InvokeAsync(in envelope, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    AddDeadLetter(DeadLetterEntry<TEvent>.Capture(in envelope, DeadLetterReason.HandlerException, ex));
                    NotifyConsumptionError(in envelope, ex);
                }
            }

            if (!delivered)
            {
                dropped++;
                Interlocked.Increment(ref _droppedItems);
            }
            else
            {
                dispatched++;
            }
        }

        _telemetry.RecordDispatch(dispatched, (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency);
        if (dropped > 0)
        {
            _telemetry.RecordDropped(dropped);
        }
    }

    private void NotifyConsumptionError(in EventEnvelope<TEvent> envelope, Exception exception)
    {
        try
        {
            foreach (IEventInterceptor<TEvent> interceptor in _interceptors)
            {
                interceptor.OnConsumptionError(in envelope, exception);
            }
        }
        catch (Exception interceptorFault)
        {
            AddDeadLetter(DeadLetterEntry<TEvent>.Capture(in envelope, DeadLetterReason.DispatchFailure, interceptorFault));
        }
    }

    /// <summary>
    /// Retains a dead letter; when the bound is full the oldest entry is discarded first and
    /// counted. Count checks are approximate under concurrency; retention never exceeds the
    /// bound by more than the number of racing producers.
    /// </summary>
    private void AddDeadLetter(in DeadLetterEntry<TEvent> entry)
    {
        if (_deadLetters.Count >= _deadLetterCapacity && _deadLetters.TryDequeue(out _))
        {
            Interlocked.Increment(ref _droppedDeadLetters);
            _telemetry.RecordDeadLetterDropped();
        }

        _deadLetters.Enqueue(entry);
        _telemetry.RecordDeadLetter();
    }
}
