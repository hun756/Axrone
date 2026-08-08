namespace Axrone.ObjectPool;

[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public sealed class ObjectPool<T> : IObjectPool<T> where T : class
{
    private readonly struct PooledItem
    {
        public readonly T Instance;
        public readonly DateTime Created;
        public readonly int Generation;
        public readonly long RentCount;
        public readonly Guid Id;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PooledItem(T instance, DateTime created, int generation, long rentCount, Guid id)
        {
            Instance = instance;
            Created = created;
            Generation = generation;
            RentCount = rentCount;
            Id = id;
        }
    }

    private readonly Func<T> _factory;
    private readonly PoolableHandler<T> _handler;
    private readonly object _syncLock;
    private readonly ConcurrentQueue<PooledItem> _items;
    private PoolConfiguration _configuration;
    private int _count;
    private int _rented;
    private int _generation;
    private volatile bool _isDisposed;

    public ObjectPool(PoolConfiguration configuration, PoolableHandler<T> handler)
    {
        if (handler.Factory is null)
            ThrowHelper.ThrowArgumentNullException(nameof(handler.Factory));

        _factory = handler.Factory;
        _handler = handler;
        _configuration = configuration;
        _syncLock = new object();
        _items = new ConcurrentQueue<PooledItem>();
        _isDisposed = false;

        if (configuration.InitialCapacity > 0)
        {
            PreAllocate(configuration.InitialCapacity);
        }
    }

    public ObjectPool(PoolableHandler<T> handler)
        : this(
            new PoolConfiguration
            {
                MaximumCapacity = Environment.ProcessorCount * 32,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
            },
            handler)
    {
    }

    public ObjectPool(
        Func<T> factory,
        Action<T>? reset = null,
        Action<T>? dispose = null,
        int initialCapacity = 0,
        int maxCapacity = 0)
        : this(
            new PoolConfiguration
            {
                InitialCapacity = initialCapacity,
                MaximumCapacity = maxCapacity > 0 ? maxCapacity : Environment.ProcessorCount * 32,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
            },
            new PoolableHandler<T>
            {
                Factory = factory,
                Reset = reset,
                OnDispose = dispose,
            })
    {
    }

    public int Count => Volatile.Read(ref _count);

    public int Capacity => _configuration.MaximumCapacity;

    public int Available => Count - Volatile.Read(ref _rented);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        ThrowIfDisposed();

        if (_items.TryDequeue(out var pooledItem))
        {
            Interlocked.Increment(ref _rented);
            _handler.OnRent?.Invoke(pooledItem.Instance);
            return pooledItem.Instance;
        }

        return CreateNewInstance();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        if (item is null)
            ThrowHelper.ThrowArgumentNullException(nameof(item));

        ThrowIfDisposed();

        try
        {
            if (_handler.Reset is not null)
            {
                _handler.Reset(item);
            }
            else if (item is IPoolable poolable)
            {
                poolable.Reset();
            }

            if (_configuration.ClearOnReturn && item is IClearable clearable)
            {
                clearable.Clear();
            }

            _handler.OnReturn?.Invoke(item);
        }
        catch
        {
            // Reset failure — dispose the item instead of returning it
            _handler.OnDispose?.Invoke(item);
            Interlocked.Decrement(ref _count);
            Interlocked.Decrement(ref _rented);
            return;
        }

        Interlocked.Decrement(ref _rented);

        if (Count < _configuration.MaximumCapacity || _configuration.AllowPoolExpansion)
        {
            _items.Enqueue(new PooledItem(
                item, DateTime.UtcNow, _generation, 0, Guid.Empty));
        }
        else
        {
            _handler.OnDispose?.Invoke(item);
            Interlocked.Decrement(ref _count);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T CreateNewInstance()
    {
        T instance = _factory();

        Interlocked.Increment(ref _count);
        Interlocked.Increment(ref _rented);

        _handler.OnRent?.Invoke(instance);
        return instance;
    }

    private void PreAllocate(int count)
    {
        for (int i = 0; i < count; i++)
        {
            T instance = _factory();
            var pooledItem = new PooledItem(
                instance, DateTime.UtcNow, _generation, 0, Guid.NewGuid());
            _items.Enqueue(pooledItem);
            Interlocked.Increment(ref _count);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            ThrowHelper.ThrowObjectDisposedException(nameof(ObjectPool<T>));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        while (_items.TryDequeue(out var item))
        {
            try
            {
                _handler.OnDispose?.Invoke(item.Instance);
            }
            catch
            {
                // Swallow dispose failures
            }

            Interlocked.Decrement(ref _count);
        }
    }

    // ─── IObjectPool<T> — not yet implemented ─────────────────────

    public ValueTask<T> RentAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public ValueTask ReturnAsync(T item, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public bool TryRent([NotNullWhen(true)] out T? item)
        => throw new NotImplementedException();

    public ValueTask<(bool Success, T? Item)> TryRentAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public IPoolable<T> GetPoolable()
        => throw new NotImplementedException();

    public ValueTask<IPoolable<T>> GetPoolableAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public void Clear()
        => throw new NotImplementedException();

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public bool Contains(T item)
        => throw new NotImplementedException();

    public PoolMetrics GetMetrics()
        => throw new NotImplementedException();

    public PoolDiagnostics GetDiagnostics()
        => throw new NotImplementedException();

    public void Reconfigure(PoolConfiguration configuration)
        => throw new NotImplementedException();

    public void Trim(int targetCapacity = -1)
        => throw new NotImplementedException();

    public ValueTask TrimAsync(int targetCapacity = -1, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public void ResetMetrics()
        => throw new NotImplementedException();

    public IPoolableFactory<T, TResult> CreateFactory<TResult>()
        => throw new NotImplementedException();

    public ValueTask DisposeAsync()
        => throw new NotImplementedException();
}
