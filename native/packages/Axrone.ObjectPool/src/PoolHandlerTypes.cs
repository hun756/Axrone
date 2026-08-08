namespace Axrone.ObjectPool;

/// <summary>
/// Contains metadata and tracking information for a single object managed by the pool.
/// </summary>
/// <typeparam name="T">The type of the pooled object.</typeparam>
public readonly struct PoolObjectInfo<T> where T : class
{
    /// <summary>
    /// Gets the pooled object instance.
    /// </summary>
    public T Instance { get; init; }

    /// <summary>
    /// Gets the UTC time at which this object was created by the pool.
    /// </summary>
    public DateTime CreationTime { get; init; }

    /// <summary>
    /// Gets the generation number of this object, incremented each time it is recycled.
    /// </summary>
    public int Generation { get; init; }

    /// <summary>
    /// Gets the total number of times this object has been rented from the pool.
    /// </summary>
    public long RentCount { get; init; }

    /// <summary>
    /// Gets the cumulative time this object has spent rented across all rent/return cycles.
    /// </summary>
    public TimeSpan TotalRentTime { get; init; }

    /// <summary>
    /// Gets a value indicating whether this object is currently wrapped in a poolable handler.
    /// </summary>
    public bool IsPoolable { get; init; }

    /// <summary>
    /// Gets a value indicating whether this object is currently rented (active).
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Gets the unique identifier assigned to this object by the pool.
    /// </summary>
    public long Id { get; init; }
}

/// <summary>
/// Holds delegate-based handlers that customize pool behavior for creation, reset,
/// lifecycle events, and validation of pooled objects.
/// </summary>
/// <typeparam name="T">The type of objects managed by the pool.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct PoolableHandler<T> where T : class
{
    /// <summary>
    /// Gets the factory delegate used to create new instances of <typeparamref name="T"/>.
    /// </summary>
    public readonly Func<T> Factory { get; init; }

    /// <summary>
    /// Gets the optional synchronous reset action invoked when an item is returned to the pool.
    /// </summary>
    public readonly Action<T>? Reset { get; init; }

    /// <summary>
    /// Gets the optional asynchronous reset action invoked when an item is returned to the pool.
    /// </summary>
    public readonly Func<T, ValueTask>? ResetAsync { get; init; }

    /// <summary>
    /// Gets the optional callback invoked when an item is rented from the pool.
    /// </summary>
    public readonly Action<T>? OnRent { get; init; }

    /// <summary>
    /// Gets the optional callback invoked when an item is returned to the pool.
    /// </summary>
    public readonly Action<T>? OnReturn { get; init; }

    /// <summary>
    /// Gets the optional synchronous callback invoked when an item is permanently disposed by the pool.
    /// </summary>
    public readonly Action<T>? OnDispose { get; init; }

    /// <summary>
    /// Gets the optional asynchronous callback invoked when an item is permanently disposed by the pool.
    /// </summary>
    public readonly Func<T, ValueTask>? OnDisposeAsync { get; init; }

    /// <summary>
    /// Gets the optional synchronous validator that determines whether a returned item is still usable.
    /// </summary>
    public readonly Func<T, bool>? Validator { get; init; }

    /// <summary>
    /// Gets the optional asynchronous validator that determines whether a returned item is still usable.
    /// </summary>
    public readonly Func<T, ValueTask<bool>>? ValidatorAsync { get; init; }

    /// <summary>
    /// Gets the optional callback invoked when a new object is created by the pool.
    /// Receives the <see cref="PoolObjectInfo{T}"/> metadata for the newly created object.
    /// </summary>
    public readonly Action<PoolObjectInfo<T>>? OnCreate { get; init; }

    /// <summary>
    /// Gets the optional callback invoked when an object is permanently destroyed by the pool.
    /// Receives the <see cref="PoolObjectInfo{T}"/> metadata for the object being destroyed.
    /// </summary>
    public readonly Action<PoolObjectInfo<T>>? OnDestroy { get; init; }
}
