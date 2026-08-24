namespace Axrone.ObjectPool;

/// <summary>
/// Defines a contract for objects that can clear their internal state.
/// </summary>
public interface IClearable
{
    /// <summary>
    /// Clears the internal state of the object.
    /// </summary>
    void Clear();
}

/// <summary>
/// Defines a contract for objects that can be reset for reuse from a pool.
/// </summary>
public interface IPoolable
{
    /// <summary>
    /// Resets the object to a clean state for reuse.
    /// </summary>
    void Reset();

    /// <summary>
    /// Asynchronously resets the object to a clean state for reuse.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous reset operation.</returns>
    ValueTask ResetAsync();
}

/// <summary>
/// Validates whether a pooled item is still in a usable state.
/// </summary>
/// <typeparam name="T">The type of object being validated.</typeparam>
public interface IPoolValidator<T> where T : class
{
    /// <summary>
    /// Synchronously validates whether the specified item is still usable.
    /// </summary>
    /// <param name="item">The item to validate.</param>
    /// <returns><c>true</c> if the item is valid and can be rented; otherwise, <c>false</c>.</returns>
    bool Validate(T item);

    /// <summary>
    /// Asynchronously validates whether the specified item is still usable.
    /// </summary>
    /// <param name="item">The item to validate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A tuple indicating whether the item is valid.</returns>
    ValueTask<bool> ValidateAsync(T item, CancellationToken cancellationToken);
}

/// <summary>
/// Creates a derived result from a pooled item, typically used for factory patterns.
/// </summary>
/// <typeparam name="T">The source pooled item type.</typeparam>
/// <typeparam name="TResult">The type of result produced.</typeparam>
public interface IPoolableFactory<T, TResult> where T : class
{
    /// <summary>
    /// Creates a result from the specified pooled item.
    /// </summary>
    /// <param name="item">The source pooled item.</param>
    /// <returns>The created result.</returns>
    TResult Create(T item);
}

/// <summary>
/// Factory for creating object pools with various configurations.
/// </summary>
/// <typeparam name="T">The type of objects managed by the pool.</typeparam>
public interface IObjectPoolFactory<T> where T : class
{
    /// <summary>
    /// Creates a pool using the default factory for <typeparamref name="T"/>.
    /// </summary>
    /// <param name="configuration">The pool configuration.</param>
    /// <returns>A new object pool instance.</returns>
    IObjectPool<T> CreatePool(PoolConfiguration configuration);

    /// <summary>
    /// Creates a pool with a custom factory delegate.
    /// </summary>
    /// <param name="factory">A delegate that creates new instances of <typeparamref name="T"/>.</param>
    /// <param name="configuration">The pool configuration.</param>
    /// <returns>A new object pool instance.</returns>
    IObjectPool<T> CreatePool(Func<T> factory, PoolConfiguration configuration);

    /// <summary>
    /// Creates a pool with a custom factory, optional reset action, and capacity settings.
    /// </summary>
    /// <param name="factory">A delegate that creates new instances of <typeparamref name="T"/>.</param>
    /// <param name="reset">An optional action to reset items when returned to the pool.</param>
    /// <param name="initialCapacity">The initial number of items to pre-allocate.</param>
    /// <param name="maxCapacity">The maximum number of items the pool can hold.</param>
    /// <returns>A new object pool instance.</returns>
    IObjectPool<T> CreatePool(Func<T> factory, Action<T>? reset = null, int initialCapacity = 0, int maxCapacity = 0);
}

/// <summary>
/// A thread-safe object pool that manages the lifecycle of reusable instances of <typeparamref name="T"/>.
/// Supports synchronous and asynchronous rent/return operations, metrics, diagnostics, and reconfiguration.
/// </summary>
/// <typeparam name="T">The type of objects managed by the pool.</typeparam>
public interface IObjectPool<T> : IDisposable, IAsyncDisposable where T : class
{
    /// <summary>
    /// Rents an item from the pool. Creates a new instance if none are available.
    /// </summary>
    /// <returns>A pooled item instance.</returns>
    T Rent();

    /// <summary>
    /// Asynchronously rents an item from the pool. Creates a new instance if none are available.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A pooled item instance.</returns>
    ValueTask<T> RentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an item to the pool for future reuse.
    /// </summary>
    /// <param name="item">The item to return.</param>
    void Return(T item);

    /// <summary>
    /// Asynchronously returns an item to the pool for future reuse.
    /// </summary>
    /// <param name="item">The item to return.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous return operation.</returns>
    ValueTask ReturnAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a poolable item to the pool by its poolable identifier.
    /// </summary>
    /// <param name="poolableId">The unique identifier of the poolable wrapper.</param>
    /// <param name="item">The item to return.</param>
    void ReturnPoolable(int poolableId, T item);

    /// <summary>
    /// Asynchronously returns a poolable item to the pool by its poolable identifier.
    /// </summary>
    /// <param name="poolableId">The unique identifier of the poolable wrapper.</param>
    /// <param name="item">The item to return.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous return operation.</returns>
    ValueTask ReturnPoolableAsync(int poolableId, T item);

    /// <summary>
    /// Attempts to rent an item from the pool without creating a new one.
    /// </summary>
    /// <param name="item">When this method returns <c>true</c>, contains the rented item; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if an item was available and rented; otherwise, <c>false</c>.</returns>
    bool TryRent([NotNullWhen(true)] out T? item);

    /// <summary>
    /// Asynchronously attempts to rent an item from the pool without creating a new one.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A tuple indicating success and the rented item (or <c>null</c> if unavailable).</returns>
    ValueTask<(bool Success, T? Item)> TryRentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a poolable wrapper that tracks the rented item's lifetime and metadata.
    /// </summary>
    /// <returns>A poolable wrapper around the rented item.</returns>
    IPoolable<T> GetPoolable();

    /// <summary>
    /// Asynchronously gets a poolable wrapper that tracks the rented item's lifetime and metadata.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A poolable wrapper around the rented item.</returns>
    ValueTask<IPoolable<T>> GetPoolableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all items from the pool, releasing references to allow garbage collection.
    /// </summary>
    void Clear();

    /// <summary>
    /// Asynchronously clears all items from the pool.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous clear operation.</returns>
    ValueTask ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the specified item is currently held by the pool.
    /// </summary>
    /// <param name="item">The item to locate.</param>
    /// <returns><c>true</c> if the item is in the pool; otherwise, <c>false</c>.</returns>
    bool Contains(T item);

    /// <summary>
    /// Gets the total number of items currently managed by the pool (both available and rented).
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the current capacity of the pool.
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// Gets the number of items currently available for rent.
    /// </summary>
    int Available { get; }

    /// <summary>
    /// Gets a snapshot of the pool's performance metrics.
    /// </summary>
    /// <returns>A <see cref="PoolMetrics"/> instance with current metric values.</returns>
    PoolMetrics GetMetrics();

    /// <summary>
    /// Gets a snapshot of the pool's diagnostic information.
    /// </summary>
    /// <returns>A <see cref="PoolDiagnostics"/> instance with current diagnostic data.</returns>
    PoolDiagnostics GetDiagnostics();

    /// <summary>
    /// Reconfigures the pool with new settings at runtime.
    /// </summary>
    /// <param name="configuration">The new pool configuration.</param>
    void Reconfigure(PoolConfiguration configuration);

    /// <summary>
    /// Trims the pool to reduce its capacity, releasing excess items.
    /// </summary>
    /// <param name="targetCapacity">The desired capacity after trimming. Use -1 for the pool's default trim target.</param>
    void Trim(int targetCapacity = -1);

    /// <summary>
    /// Asynchronously trims the pool to reduce its capacity, releasing excess items.
    /// </summary>
    /// <param name="targetCapacity">The desired capacity after trimming. Use -1 for the pool's default trim target.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous trim operation.</returns>
    ValueTask TrimAsync(int targetCapacity = -1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all accumulated metrics counters to their initial values.
    /// </summary>
    void ResetMetrics();

    /// <summary>
    /// Creates a factory that produces derived results from pooled items.
    /// </summary>
    /// <typeparam name="TResult">The type of result the factory produces.</typeparam>
    /// <returns>A factory instance bound to this pool.</returns>
    IPoolableFactory<T, TResult> CreateFactory<TResult>();
}

/// <summary>
/// A disposable wrapper around a rented pooled item that tracks its lifetime and metadata.
/// Disposing or calling <see cref="Detach"/> releases the wrapper without returning the item to the pool.
/// </summary>
/// <typeparam name="T">The type of the pooled item.</typeparam>
public interface IPoolable<T> : IDisposable, IAsyncDisposable where T : class
{
    /// <summary>
    /// Gets the underlying pooled item instance.
    /// </summary>
    T Instance { get; }

    /// <summary>
    /// Gets the time at which this item was rented from the pool.
    /// </summary>
    DateTime RentTime { get; }

    /// <summary>
    /// Gets a value indicating whether this poolable wrapper has been disposed.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// Gets the elapsed time since the item was rented.
    /// </summary>
    TimeSpan Age { get; }

    /// <summary>
    /// Detaches the poolable wrapper from the pool, preventing automatic return on dispose.
    /// The caller assumes full ownership of the underlying item.
    /// </summary>
    void Detach();
}
