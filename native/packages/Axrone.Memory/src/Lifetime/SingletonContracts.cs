namespace Axrone.Memory.Lifetime;

/// <summary>
/// Factory contract for producing singleton instances. Implemented as a struct with a
/// static abstract member so the engine can invoke the factory without virtual dispatch
/// and without allocating a delegate.
/// </summary>
/// <typeparam name="T">The instance type produced by the factory.</typeparam>
public interface ISingletonFactory<out T>
{
    /// <summary>Produces a new singleton instance. Implementations must never return null.</summary>
    /// <returns>The produced instance.</returns>
    static abstract T Create();
}

/// <summary>
/// Policy controlling how the engine spaces retry and drain-wait attempts. Implementations
/// must be mutable-free structs so the engine can store them without aliasing hazards.
/// </summary>
public interface IBackoffPolicy
{
    /// <summary>Gets the maximum number of attempts the policy will space before giving up.</summary>
    int MaxAttempts { get; }

    /// <summary>
    /// Computes the delay to observe before the specified attempt number.
    /// </summary>
    /// <param name="attempt">The zero-based attempt number whose delay is requested.</param>
    /// <returns>The delay to observe before the next attempt.</returns>
    TimeSpan NextDelay(int attempt);
}

/// <summary>
/// Receives diagnostic callbacks from the singleton engine. Implementations must be
/// thread-safe because the engine raises callbacks from arbitrary threads.
/// </summary>
public interface ISingletonDiagnostics
{
    /// <summary>Raised when the instance has been successfully published.</summary>
    void OnInstanceCreated(InstanceName name);

    /// <summary>Raised when the factory fails while producing the instance.</summary>
    void OnInstanceFaulted(InstanceName name, Exception error);

    /// <summary>Raised when a lease is acquired.</summary>
    void OnLeaseAcquired(InstanceName name, LeaseCount activeLeases);

    /// <summary>Raised when a lease is released.</summary>
    void OnLeaseReleased(InstanceName name, LeaseCount activeLeases);

    /// <summary>Raised when a drain attempt begins.</summary>
    void OnDrainStarted(InstanceName name);

    /// <summary>Raised when a drain attempt ends, regardless of outcome.</summary>
    void OnDrainCompleted(InstanceName name, DrainResult result);

    /// <summary>Raised when the engine is permanently disposed.</summary>
    void OnDisposed(InstanceName name);
}

/// <summary>
/// Consumes a singleton instance together with a caller-provided context value. The
/// context parameter accepts ref structs so high-performance callers can pass
/// stack-only state without boxing.
/// </summary>
/// <typeparam name="T">The singleton instance type.</typeparam>
/// <typeparam name="TContext">The context value type; may be a ref struct.</typeparam>
public interface ISingletonConsumer<in T, TContext>
    where TContext : allows ref struct
{
    /// <summary>Consumes the singleton instance with the supplied context.</summary>
    /// <param name="instance">The instance being consumed.</param>
    /// <param name="context">The caller-provided context value.</param>
    void Consume(T instance, TContext context);
}

/// <summary>
/// Provides access to a singleton instance without exposing lease semantics.
/// </summary>
/// <typeparam name="T">The instance type provided.</typeparam>
public interface IInstanceProvider<out T>
{
    /// <summary>Gets the provided instance.</summary>
    T Get();

    /// <summary>Determines whether an instance is currently available.</summary>
    bool IsAvailable { get; }
}

/// <summary>
/// Acquires and releases leases against a singleton instance.
/// </summary>
/// <typeparam name="T">The instance type.</typeparam>
/// <typeparam name="TLease">The concrete lease token type.</typeparam>
public interface ILeaseProvider<T, TLease>
    where TLease : struct
{
    /// <summary>Acquires a lease against the specified instance.</summary>
    TLease Acquire(T instance);

    /// <summary>Releases a previously acquired lease.</summary>
    void Release(TLease lease);
}

/// <summary>
/// Executes a consumer under the guarantee that a lease is held for the duration
/// of the execution.
/// </summary>
/// <typeparam name="T">The singleton instance type.</typeparam>
public interface IScopedExecutor<T>
{
    /// <summary>
    /// Acquires a lease, invokes the consumer with the instance and context, and
    /// releases the lease when the consumer returns.
    /// </summary>
    /// <typeparam name="TContext">The context value type; may be a ref struct.</typeparam>
    /// <param name="consumer">The consumer to invoke.</param>
    /// <param name="context">The context value to pass to the consumer.</param>
    void Execute<TContext>(ISingletonConsumer<T, TContext> consumer, TContext context)
        where TContext : allows ref struct;
}

/// <summary>
/// Marks a lifecycle owner that can transition from an open phase (accepting leases)
/// to a closed phase (rejecting new leases).
/// </summary>
public interface ILifecycleTerminable
{
    /// <summary>Transitions the owner to the closed phase in which no new leases are accepted.</summary>
    void Complete();
}

/// <summary>
/// Marks a lifecycle owner that can drain held leases and release its resources.
/// </summary>
public interface ILifecycleDrainable
{
    /// <summary>
    /// Attempts to drain synchronously, returning <see langword="true"/> when the drain
    /// completed within the supplied timeout.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for held leases to release.</param>
    /// <param name="result">The outcome of the drain attempt.</param>
    /// <returns><see langword="true"/> when the drain completed; otherwise <see langword="false"/>.</returns>
    bool TryDrain(TimeSpan timeout, out DrainResult result);

    /// <summary>
    /// Drains asynchronously, waiting until all held leases release or the engine's
    /// configured drain timeout elapses.
    /// </summary>
    /// <param name="cancellationToken">The token to cancel the wait.</param>
    /// <returns>The outcome of the drain attempt.</returns>
    ValueTask<DrainResult> DrainAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Inspects a singleton engine's registration metadata and metric history.
/// </summary>
public interface ISingletonInspector
{
    /// <summary>Gets the current metadata snapshot for the engine.</summary>
    SingletonMetadata GetMetadata();

    /// <summary>Attempts to get the current metadata snapshot for the engine.</summary>
    /// <param name="metadata">The metadata snapshot when available.</param>
    /// <returns><see langword="true"/> when metadata was captured; otherwise <see langword="false"/>.</returns>
    bool TryGetMetadata(out SingletonMetadata metadata);

    /// <summary>
    /// Copies the retained metric records into the supplied buffer.
    /// </summary>
    /// <param name="buffer">The destination buffer; must be at least as large as the retained record count.</param>
    /// <returns>The number of records copied.</returns>
    int EnumerateMetrics(Span<MetricRecord> buffer);
}
