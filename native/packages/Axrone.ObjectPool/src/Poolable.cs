namespace Axrone.ObjectPool;

/// <summary>
/// A lightweight reference-type wrapper that tracks a rented object and returns it to the
/// originating <see cref="IObjectPool{T}"/> when disposed. Supports <see cref="IDisposable"/>
/// and <see cref="IAsyncDisposable"/> for use in <c>using</c> / <c>await using</c> blocks.
/// </summary>
/// <typeparam name="T">The pooled reference type.</typeparam>
[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay("Poolable<{typeof(T).Name}> Id = {_poolableId}, Disposed = {IsDisposed}")]
public sealed class Poolable<T> : IPoolable<T> where T : class
{
    private const int STATE_ACTIVE = 0;
    private const int STATE_DISPOSED = 1;
    private const int STATE_DETACHED = 2;

    private static int s_nextPoolableId;

    private readonly T _instance;
    private readonly IObjectPool<T>? _pool;
    private readonly DateTime _rentTime;
    private readonly bool _trackRentTime;
    private readonly Action<T, TimeSpan>? _onDispose;
    private readonly int _poolableId;
    private int _state;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Poolable(
        T instance,
        IObjectPool<T>? pool,
        bool trackRentTime = false,
        Action<T, TimeSpan>? onDispose = null)
    {
        _instance = instance;
        _pool = pool;
        _rentTime = DateTime.UtcNow;
        _trackRentTime = trackRentTime;
        _onDispose = onDispose;
        _poolableId = Interlocked.Increment(ref s_nextPoolableId);
        _state = STATE_ACTIVE;
    }

    internal int PoolableId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _poolableId;
    }

    /// <summary>
    /// Gets the pooled object instance.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if this poolable has already been disposed.</exception>
    public T Instance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return _instance;
        }
    }

    /// <summary>
    /// Gets the UTC time at which this poolable was rented from the pool.
    /// </summary>
    public DateTime RentTime => _rentTime;

    /// <summary>
    /// Gets a value indicating whether this poolable has been disposed or detached.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _state) != STATE_ACTIVE;

    /// <summary>
    /// Gets the elapsed time since this poolable was rented.
    /// </summary>
    public TimeSpan Age => DateTime.UtcNow - _rentTime;

    /// <summary>
    /// Detaches this poolable from the underlying pool, preventing the object from being
    /// returned when <see cref="Dispose"/> or <see cref="DisposeAsync"/> is called.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Detach()
    {
        Interlocked.Exchange(ref _state, STATE_DETACHED);
    }

    /// <summary>
    /// Returns the pooled object to the originating pool. If rent-time tracking is enabled,
    /// the configured <c>onDispose</c> callback is invoked with the rent duration.
    /// No-op if already disposed or detached.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _state, STATE_DISPOSED) != STATE_ACTIVE)
            return;

        if (_pool == null)
            return;

        if (_trackRentTime && _onDispose != null)
        {
            TimeSpan rentDuration = DateTime.UtcNow - _rentTime;
            _onDispose(_instance, rentDuration);
        }

        _pool.ReturnPoolable(_poolableId, _instance);
    }

    /// <summary>
    /// Asynchronously returns the pooled object to the originating pool. If rent-time tracking
    /// is enabled, the configured <c>onDispose</c> callback is invoked with the rent duration.
    /// No-op if already disposed or detached.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous return operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _state, STATE_DISPOSED) != STATE_ACTIVE)
            return ValueTask.CompletedTask;

        if (_pool == null)
            return ValueTask.CompletedTask;

        if (_trackRentTime && _onDispose != null)
        {
            TimeSpan rentDuration = DateTime.UtcNow - _rentTime;
            _onDispose(_instance, rentDuration);
        }

        return _pool.ReturnPoolableAsync(_poolableId, _instance);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _state) == STATE_DISPOSED)
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(Poolable<T>));
        }
    }
}
