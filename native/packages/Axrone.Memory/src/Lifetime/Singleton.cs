namespace Axrone.Memory.Lifetime;

/// <summary>
/// Core lease-tracked singleton engine. The engine publishes exactly one instance of
/// <typeparamref name="T"/> produced by <typeparamref name="TFactory"/>, hands out RAII
/// leases that count outstanding consumers, and offers deterministic teardown through
/// <see cref="ILifecycleTerminable"/> and <see cref="ILifecycleDrainable"/>.
/// </summary>
/// <typeparam name="T">The singleton instance type.</typeparam>
/// <typeparam name="TFactory">The factory struct that produces instances.</typeparam>
/// <typeparam name="TBackoff">The backoff policy struct used to space drain waits.</typeparam>
/// <typeparam name="TDiagnostics">The diagnostics sink struct receiving lifecycle callbacks.</typeparam>
public class Singleton<T, TFactory, TBackoff, TDiagnostics> :
    ILifecycleTerminable,
    ILifecycleDrainable,
    ISingletonInspector,
    IScopedExecutor<T>,
    IInstanceProvider<T>,
    ILeaseProvider<T, SingletonLease<T, TFactory, TBackoff, TDiagnostics>>,
    IDisposable,
    IAsyncDisposable
    where T : class
    where TFactory : struct, ISingletonFactory<T>
    where TBackoff : struct, IDelayPolicy
    where TDiagnostics : struct, ISingletonDiagnostics
{
    private readonly TBackoff _backoff;
    private readonly TDiagnostics _diagnostics;
    private readonly SingletonOptions _options;
    private readonly MetricSequence _metrics;
    private readonly object _gate;
    private PaddedState _state;
    private T? _instance;
    private ExceptionDispatchInfo? _fault;

    /// <summary>
    /// Initializes a new instance of the <see cref="Singleton{T, TFactory, TBackoff, TDiagnostics}"/> class.
    /// </summary>
    /// <param name="options">The engine configuration; defaults are used when null.</param>
    public Singleton(SingletonOptions? options = null)
        : this(new TBackoff(), new TDiagnostics(), options ?? new SingletonOptions())
    {
    }

    internal Singleton(TBackoff backoff, TDiagnostics diagnostics, SingletonOptions options)
    {
        _backoff = backoff;
        _diagnostics = diagnostics;
        _options = options;
        _metrics = new MetricSequence(options.MetricCapacity);
        _gate = new object();
    }

    /// <summary>Gets the configuration options the engine was created with.</summary>
    public SingletonOptions Options => _options;

    /// <summary>Gets the registration name of the engine.</summary>
    public InstanceName Name => _options.InstanceName;

    /// <summary>Gets the number of leases currently held against the instance.</summary>
    public LeaseCount ActiveLeases => new((int)Volatile.Read(ref _state.LeaseCount));

    /// <summary>Gets the current lifecycle status of the engine.</summary>
    public LifecycleStatus Status => (LifecycleStatus)Volatile.Read(ref _state.Status);

    /// <summary>
    /// Gets the published instance, lazily initializing it on first access. When the
    /// engine is faulted the original factory exception is rethrown with its captured
    /// stack preserved; when disposed an <see cref="ObjectDisposedException"/> is thrown.
    /// </summary>
    /// <returns>The published instance.</returns>
    public T Instance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var instance = Volatile.Read(ref _instance);
            if (instance is not null)
            {
                return instance;
            }

            return Initialize();
        }
    }

    /// <summary>
    /// Acquires a lease, throwing when the engine cannot serve one in its current phase.
    /// </summary>
    /// <returns>The acquired lease.</returns>
    public SingletonLease<T, TFactory, TBackoff, TDiagnostics> AcquireLease()
    {
        var instance = Instance;
        lock (_gate)
        {
            var status = (LifecycleStatus)Volatile.Read(ref _state.Status);
            if (status != LifecycleStatus.Initialized)
            {
                return SingletonThrowHelper.ThrowLeaseAcquisitionFailed<SingletonLease<T, TFactory, TBackoff, TDiagnostics>>(ToAcquisitionStatus(status));
            }

            var iteration = NextIteration();
            Volatile.Write(ref _state.LeaseCount, Volatile.Read(ref _state.LeaseCount) + 1L);
            var leaseCount = new LeaseCount((int)Volatile.Read(ref _state.LeaseCount));
            _diagnostics.OnLeaseAcquired(_options.InstanceName, leaseCount);
            _metrics.Append(new MetricRecord(MetricKind.LeaseAcquired, EpochTimestamp.Now, iteration, leaseCount));
            return new SingletonLease<T, TFactory, TBackoff, TDiagnostics>(this, instance, iteration);
        }
    }

    /// <summary>
    /// Attempts to acquire a lease without throwing when the engine cannot serve one.
    /// </summary>
    /// <param name="result">The outcome of the acquisition attempt.</param>
    /// <returns><see langword="true"/> when a lease was acquired; otherwise <see langword="false"/>.</returns>
    public bool TryAcquireLease(out LeaseAcquisitionResult<SingletonLease<T, TFactory, TBackoff, TDiagnostics>> result)
    {
        var instance = Volatile.Read(ref _instance);
        if (instance is null)
        {
            if (!TryInitialize(out instance, out var initStatus))
            {
                result = new LeaseAcquisitionResult<SingletonLease<T, TFactory, TBackoff, TDiagnostics>>(initStatus, default);
                return false;
            }
        }

        lock (_gate)
        {
            var status = (LifecycleStatus)Volatile.Read(ref _state.Status);
            if (status != LifecycleStatus.Initialized)
            {
                result = new LeaseAcquisitionResult<SingletonLease<T, TFactory, TBackoff, TDiagnostics>>(ToAcquisitionStatus(status), default);
                return false;
            }

            var iteration = NextIteration();
            Volatile.Write(ref _state.LeaseCount, Volatile.Read(ref _state.LeaseCount) + 1L);
            var leaseCount = new LeaseCount((int)Volatile.Read(ref _state.LeaseCount));
            _diagnostics.OnLeaseAcquired(_options.InstanceName, leaseCount);
            _metrics.Append(new MetricRecord(MetricKind.LeaseAcquired, EpochTimestamp.Now, iteration, leaseCount));
            var lease = new SingletonLease<T, TFactory, TBackoff, TDiagnostics>(this, instance, iteration);
            result = new LeaseAcquisitionResult<SingletonLease<T, TFactory, TBackoff, TDiagnostics>>(LeaseAcquisitionStatus.Success, lease);
            return true;
        }
    }

    /// <summary>
    /// Acquires a lease, invokes the consumer with the instance and context, and
    /// releases the lease when the consumer returns.
    /// </summary>
    /// <typeparam name="TContext">The context value type; may be a ref struct.</typeparam>
    /// <param name="consumer">The consumer to invoke.</param>
    /// <param name="context">The context value to pass to the consumer.</param>
    public void Execute<TContext>(ISingletonConsumer<T, TContext> consumer, TContext context)
        where TContext : allows ref struct
    {
        var lease = AcquireLease();
        try
        {
            _metrics.Append(new MetricRecord(MetricKind.ExecuteStarted, EpochTimestamp.Now, NextIteration(), ActiveLeases));
            consumer.Consume(lease.Instance, context);
            _metrics.Append(new MetricRecord(MetricKind.ExecuteCompleted, EpochTimestamp.Now, NextIteration(), ActiveLeases));
        }
        finally
        {
            lease.Dispose();
        }
    }

    /// <summary>Transitions the engine to the closed phase in which no new leases are accepted.</summary>
    public void Complete()
    {
        lock (_gate)
        {
            if ((LifecycleStatus)Volatile.Read(ref _state.Status) is LifecycleStatus.Initialized)
            {
                Volatile.Write(ref _state.Status, (int)LifecycleStatus.Complete);
            }
        }
    }

    /// <summary>
    /// Gets the current metadata snapshot for the engine.
    /// </summary>
    /// <returns>The metadata snapshot.</returns>
    public SingletonMetadata GetMetadata()
    {
        lock (_gate)
        {
            return BuildMetadata();
        }
    }

    /// <summary>
    /// Attempts to get the current metadata snapshot for the engine.
    /// </summary>
    /// <param name="metadata">The metadata snapshot when available.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    public bool TryGetMetadata(out SingletonMetadata metadata)
    {
        metadata = GetMetadata();
        return true;
    }

    /// <summary>
    /// Copies the retained metric records into the supplied buffer.
    /// </summary>
    /// <param name="buffer">The destination buffer; must be at least as large as the retained record count.</param>
    /// <returns>The number of records copied.</returns>
    public int EnumerateMetrics(Span<MetricRecord> buffer)
    {
        var count = _metrics.Count;
        if (buffer.Length < count)
        {
            SingletonThrowHelper.ThrowBufferTooSmall(count, buffer.Length);
        }

        for (var i = 0; i < count; i++)
        {
            buffer[i] = _metrics[i];
        }

        return count;
    }

    /// <summary>
    /// Attempts to drain synchronously, returning <see langword="true"/> when all held
    /// leases released within the supplied timeout and the instance was torn down.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for held leases to release.</param>
    /// <param name="result">The outcome of the drain attempt.</param>
    /// <returns><see langword="true"/> when the drain completed; otherwise <see langword="false"/>.</returns>
    public bool TryDrain(TimeSpan timeout, out DrainResult result)
    {
        var started = EpochTimestamp.Now;
        if (!TryBeginDrain())
        {
            result = BuildNonApplicableDrainResult(started);
            return false;
        }

        var attempt = 0;
        while (Volatile.Read(ref _state.LeaseCount) > 0L)
        {
            if (EpochTimestamp.Now - started >= timeout)
            {
                result = CompleteDrainTimedOut(started);
                return false;
            }

            var backoff = _backoff.NextDelay(attempt++);
            if (backoff > TimeSpan.Zero)
            {
                Thread.Sleep(backoff);
            }
            else
            {
                Thread.SpinWait(64);
            }
        }

        result = CompleteDrainSuccessfully(started);
        return true;
    }

    /// <summary>
    /// Drains asynchronously, waiting until all held leases release or the engine's
    /// configured drain timeout elapses.
    /// </summary>
    /// <param name="cancellationToken">The token to cancel the wait.</param>
    /// <returns>The outcome of the drain attempt.</returns>
    public async ValueTask<DrainResult> DrainAsync(CancellationToken cancellationToken = default)
    {
        var started = EpochTimestamp.Now;
        if (!TryBeginDrain())
        {
            return BuildNonApplicableDrainResult(started);
        }

        var attempt = 0;
        while (Volatile.Read(ref _state.LeaseCount) > 0L)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (EpochTimestamp.Now - started >= _options.DrainTimeout)
            {
                return CompleteDrainTimedOut(started);
            }

            var backoff = _backoff.NextDelay(attempt++);
            if (backoff > TimeSpan.Zero)
            {
                await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await Task.Yield();
            }
        }

        return CompleteDrainSuccessfully(started);
    }

    /// <summary>
    /// Disposes the engine and its instance. The teardown is idempotent: subsequent
    /// calls return without effect.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the resources held by the engine. When <paramref name="disposing"/> is
    /// <see langword="true"/> the managed instance is also torn down. The teardown is
    /// idempotent: the first caller wins and later calls return without effect.
    /// </summary>
    /// <param name="disposing">Whether the call originates from a public teardown.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        if (Interlocked.Exchange(ref _state.Disposed, 1) != 0)
        {
            return;
        }

        T? instanceToDispose = null;
        lock (_gate)
        {
            instanceToDispose = Interlocked.Exchange(ref _instance, null);
            Volatile.Write(ref _state.Status, (int)LifecycleStatus.Disposed);
            Volatile.Write(ref _state.LeaseCount, 0L);
        }

        _metrics.Append(new MetricRecord(MetricKind.Disposed, EpochTimestamp.Now, NextIteration(), ActiveLeases));
        _diagnostics.OnDisposed(_options.InstanceName);
        DisposeInstance(instanceToDispose);
    }

    /// <summary>
    /// Disposes the engine and its instance asynchronously. The teardown is idempotent:
    /// subsequent calls return without effect.
    /// </summary>
    /// <returns>A task that completes when the teardown finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _state.Disposed, 1) != 0)
        {
            return;
        }

        T? instanceToDispose = null;
        lock (_gate)
        {
            instanceToDispose = Interlocked.Exchange(ref _instance, null);
            Volatile.Write(ref _state.Status, (int)LifecycleStatus.Disposed);
            Volatile.Write(ref _state.LeaseCount, 0L);
        }

        _metrics.Append(new MetricRecord(MetricKind.Disposed, EpochTimestamp.Now, NextIteration(), ActiveLeases));
        _diagnostics.OnDisposed(_options.InstanceName);
        await DisposeInstanceAsync(instanceToDispose).ConfigureAwait(false);
    }

    /// <summary>Releases a lease previously acquired from this engine.</summary>
    internal void ReleaseLease()
    {
        lock (_gate)
        {
            var current = Volatile.Read(ref _state.LeaseCount);
            if (current > 0L)
            {
                Volatile.Write(ref _state.LeaseCount, current - 1L);
            }

            var leaseCount = new LeaseCount((int)Volatile.Read(ref _state.LeaseCount));
            _diagnostics.OnLeaseReleased(_options.InstanceName, leaseCount);
            _metrics.Append(new MetricRecord(MetricKind.LeaseReleased, EpochTimestamp.Now, NextIteration(), leaseCount));
        }
    }

    private static LeaseAcquisitionStatus ToAcquisitionStatus(LifecycleStatus status) => status switch
    {
        LifecycleStatus.Uninitialized => LeaseAcquisitionStatus.NotInitialized,
        LifecycleStatus.Initializing => LeaseAcquisitionStatus.NotInitialized,
        LifecycleStatus.Faulted => LeaseAcquisitionStatus.Faulted,
        LifecycleStatus.Complete => LeaseAcquisitionStatus.Complete,
        LifecycleStatus.Draining => LeaseAcquisitionStatus.Draining,
        LifecycleStatus.Drained => LeaseAcquisitionStatus.NotInitialized,
        LifecycleStatus.Disposed => LeaseAcquisitionStatus.Disposed,
        _ => LeaseAcquisitionStatus.NotInitialized
    };

    private Iteration NextIteration() => new(Interlocked.Increment(ref _state.Iteration));

    private T Initialize()
    {
        if (!TryInitialize(out var instance, out var status))
        {
            if ((LifecycleStatus)Volatile.Read(ref _state.Status) == LifecycleStatus.Faulted && _fault is not null)
            {
                SingletonThrowHelper.ThrowFaulted(_fault);
            }

            if (status == LeaseAcquisitionStatus.Disposed)
            {
                return SingletonThrowHelper.ThrowDisposed<T>();
            }

            return SingletonThrowHelper.ThrowLeaseAcquisitionFailed<T>(status);
        }

        return instance;
    }

    private bool TryInitialize(out T instance, out LeaseAcquisitionStatus status)
    {
        lock (_gate)
        {
            var existing = Volatile.Read(ref _instance);
            if (existing is not null)
            {
                instance = existing;
                status = LeaseAcquisitionStatus.Success;
                return true;
            }

            if ((LifecycleStatus)Volatile.Read(ref _state.Status) == LifecycleStatus.Faulted)
            {
                instance = null!;
                status = LeaseAcquisitionStatus.Faulted;
                return false;
            }

            if (Volatile.Read(ref _state.Disposed) != 0 ||
                (LifecycleStatus)Volatile.Read(ref _state.Status) is LifecycleStatus.Draining or LifecycleStatus.Drained or LifecycleStatus.Disposed)
            {
                instance = null!;
                status = LeaseAcquisitionStatus.Disposed;
                return false;
            }

            Volatile.Write(ref _state.Status, (int)LifecycleStatus.Initializing);
        }

        T? created = null;
        Exception? error = null;
        try
        {
            created = TFactory.Create();
            if (created is null)
            {
                error = new InvalidOperationException($"Factory '{typeof(TFactory).FullName}' produced a null instance.");
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }

        lock (_gate)
        {
            if (error is not null)
            {
                Volatile.Write(ref _state.Status, (int)LifecycleStatus.Faulted);
                _fault = ExceptionDispatchInfo.Capture(error);
                _metrics.Append(new MetricRecord(MetricKind.InstanceFaulted, EpochTimestamp.Now, NextIteration(), ActiveLeases));
                _diagnostics.OnInstanceFaulted(_options.InstanceName, error);
                instance = null!;
                status = LeaseAcquisitionStatus.Faulted;
                return false;
            }

            var published = Interlocked.CompareExchange(ref _instance, created!, null);
            if (published is not null)
            {
                instance = published;
                status = LeaseAcquisitionStatus.Success;
                return true;
            }

            Volatile.Write(ref _state.Epoch, (long)EpochTimestamp.Now);
            Volatile.Write(ref _state.Status, (int)LifecycleStatus.Initialized);
            var iteration = new Iteration(Interlocked.Increment(ref _state.Iteration));
            _metrics.Append(new MetricRecord(MetricKind.InstanceCreated, EpochTimestamp.Now, iteration, ActiveLeases));
            _diagnostics.OnInstanceCreated(_options.InstanceName);
            instance = created!;
            status = LeaseAcquisitionStatus.Success;
            return true;
        }
    }

    private SingletonMetadata BuildMetadata() => new(
        _options.InstanceName,
        (LifecycleStatus)Volatile.Read(ref _state.Status),
        new LeaseCount((int)Volatile.Read(ref _state.LeaseCount)),
        new EpochTimestamp(Volatile.Read(ref _state.Epoch)),
        new Iteration(Volatile.Read(ref _state.Iteration)),
        _metrics.Count);

    private bool TryBeginDrain()
    {
        lock (_gate)
        {
            var status = (LifecycleStatus)Volatile.Read(ref _state.Status);
            if (status is LifecycleStatus.Disposed)
            {
                return false;
            }

            if (status is LifecycleStatus.Draining or LifecycleStatus.Drained)
            {
                return true;
            }

            if (status is not (LifecycleStatus.Initialized or LifecycleStatus.Complete))
            {
                return false;
            }

            Volatile.Write(ref _state.Status, (int)LifecycleStatus.Draining);
            var iteration = new Iteration(Interlocked.Increment(ref _state.Iteration));
            _metrics.Append(new MetricRecord(MetricKind.DrainStarted, EpochTimestamp.Now, iteration, ActiveLeases));
            _diagnostics.OnDrainStarted(_options.InstanceName);
            return true;
        }
    }

    private DrainResult BuildNonApplicableDrainResult(EpochTimestamp started)
    {
        var status = Status;
        var drainStatus = status switch
        {
            LifecycleStatus.Disposed => DrainStatus.Disposed,
            LifecycleStatus.Faulted => DrainStatus.Faulted,
            _ => DrainStatus.NotApplicable
        };

        return new DrainResult(drainStatus, ActiveLeases, EpochTimestamp.Now - started);
    }

    private DrainResult CompleteDrainTimedOut(EpochTimestamp started)
    {
        lock (_gate)
        {
            if ((LifecycleStatus)Volatile.Read(ref _state.Status) == LifecycleStatus.Draining)
            {
                Volatile.Write(ref _state.Status, (int)LifecycleStatus.Complete);
            }
        }

        var result = new DrainResult(DrainStatus.TimedOut, ActiveLeases, EpochTimestamp.Now - started);
        _metrics.Append(new MetricRecord(MetricKind.DrainCompleted, EpochTimestamp.Now, NextIteration(), ActiveLeases));
        _diagnostics.OnDrainCompleted(_options.InstanceName, result);
        return result;
    }

    private DrainResult CompleteDrainSuccessfully(EpochTimestamp started)
    {
        T? instanceToDispose = null;
        lock (_gate)
        {
            instanceToDispose = Interlocked.Exchange(ref _instance, null);
            Volatile.Write(ref _state.Status, (int)LifecycleStatus.Drained);
            Volatile.Write(ref _state.LeaseCount, 0L);
        }

        var result = new DrainResult(DrainStatus.Completed, new LeaseCount(0), EpochTimestamp.Now - started);
        _metrics.Append(new MetricRecord(MetricKind.DrainCompleted, EpochTimestamp.Now, NextIteration(), ActiveLeases));
        _diagnostics.OnDrainCompleted(_options.InstanceName, result);
        DisposeInstance(instanceToDispose);
        return result;
    }

    private static void DisposeInstance(T? instanceToDispose)
    {
        if (instanceToDispose is null)
        {
            return;
        }

        if (instanceToDispose is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        else if (instanceToDispose is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static async ValueTask DisposeInstanceAsync(T? instanceToDispose)
    {
        if (instanceToDispose is null)
        {
            return;
        }

        if (instanceToDispose is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (instanceToDispose is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    T IInstanceProvider<T>.Get() => Instance;

    bool IInstanceProvider<T>.IsAvailable => Volatile.Read(ref _instance) is not null;

    SingletonLease<T, TFactory, TBackoff, TDiagnostics> ILeaseProvider<T, SingletonLease<T, TFactory, TBackoff, TDiagnostics>>.Acquire(T instance) => AcquireLease();

    void ILeaseProvider<T, SingletonLease<T, TFactory, TBackoff, TDiagnostics>>.Release(SingletonLease<T, TFactory, TBackoff, TDiagnostics> lease) => lease.Dispose();
}
