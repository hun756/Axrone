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

    private readonly struct RentOperation
    {
        public readonly T Instance;
        public readonly DateTime RentTime;
        public readonly long OperationId;
        public readonly int ThreadId;
        public readonly Guid InstanceId;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RentOperation(T instance, DateTime rentTime, long operationId, int threadId, Guid instanceId)
        {
            Instance = instance;
            RentTime = rentTime;
            OperationId = operationId;
            ThreadId = threadId;
            InstanceId = instanceId;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private sealed class PoolMetricsStorage
    {
        public long TotalCreated;
        public long TotalRented;
        public long TotalReturned;
        public long TotalDestroyed;
        public long TotalRentDurationTicks;
        public long TotalCreateDurationTicks;
        public long TotalResetDurationTicks;
        public int PeakUtilization;
        public DateTime PeakTime;
        public DateTime LastResetTime;
        public DateTime LastScavengeTime;
        public double MemoryUsageBytes;
        public List<double> RentDurationsMs;
        public int FailedValidationCount;
        public int ExhaustionCount;
        public int ScavengeCount;
        public int ExpansionCount;
        public int ShrinkCount;
        public int ConfigurationChanges;
        public int AllocFailureCount;
        public int DisposeFailureCount;
        public int RecycleFailureCount;
        public List<KeyValuePair<DateTime, string>> Events;
        public Dictionary<string, double> CustomMetrics;
        public Exception? LastException;

        public PoolMetricsStorage()
        {
            RentDurationsMs = new List<double>(100);
            Events = new List<KeyValuePair<DateTime, string>>(50);
            CustomMetrics = new Dictionary<string, double>(10);
            LastResetTime = DateTime.UtcNow;
            LastScavengeTime = DateTime.UtcNow;
            PeakTime = DateTime.UtcNow;
        }
    }

    private struct ShardedBucket
    {
        private readonly ConcurrentQueue<PooledItem> _items;
        private readonly int _shardIndex;
        private readonly int _partitionsCount;
        private int _localCount;

        public ShardedBucket(int shardIndex, int partitionsCount)
        {
            _items = new ConcurrentQueue<PooledItem>();
            _shardIndex = shardIndex;
            _partitionsCount = partitionsCount;
            _localCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(PooledItem item)
        {
            _items.Enqueue(item);
            Interlocked.Increment(ref _localCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out PooledItem item)
        {
            if (_items.TryDequeue(out item))
            {
                Interlocked.Decrement(ref _localCount);
                return true;
            }
            return false;
        }

        public int Count => _localCount;
    }

    // ─── Fields ──────────────────────────────────────────────────

    private readonly Func<T> _factory;
    private readonly PoolableHandler<T> _handler;
    private readonly object _syncLock;
    private readonly AsyncReaderWriterLock _asyncLock;
    private readonly SpinLock _spinLock;
    private readonly SemaphoreSlim _semaphore;
    private readonly Channel<PooledItem>? _channel;
    private readonly ConcurrentDictionary<long, RentOperation> _rentTracking;
    private readonly Stopwatch _uptime;
    private readonly PoolMetricsStorage _metrics;
    private readonly ThreadLocal<ConcurrentQueue<PooledItem>>? _threadLocalStorage;
    private readonly ShardedBucket[] _shards;
    private readonly ConcurrentDictionary<Guid, PoolObjectInfo<T>> _objectInfo;

    private PoolConfiguration _configuration;
    private int _count;
    private int _rented;
    private int _generation;
    private long _lastRentId;
    private volatile bool _isDisposed;
    private ConcurrentQueue<PooledItem> _items;

    // ─── Constructors ────────────────────────────────────────────

    public ObjectPool(PoolConfiguration configuration, PoolableHandler<T> handler)
    {
        if (handler.Factory is null)
            ThrowHelper.ThrowArgumentNullException(nameof(handler.Factory));

        _factory = handler.Factory;
        _handler = handler;
        _configuration = configuration;
        _syncLock = new object();
        _asyncLock = new AsyncReaderWriterLock();
        _spinLock = new SpinLock(enableThreadOwnerTracking: false);
        _semaphore = new SemaphoreSlim(
            configuration.MaximumCapacity > 0
                ? configuration.MaximumCapacity
                : Environment.ProcessorCount * 32,
            configuration.MaximumCapacity > 0 ? configuration.MaximumCapacity : int.MaxValue);
        _items = new ConcurrentQueue<PooledItem>();
        int concurrency = (int)configuration.ConcurrencyLevel;
        if (concurrency <= 0) concurrency = (int)ConcurrencyLevel.Default;

        _rentTracking = new ConcurrentDictionary<long, RentOperation>(
            concurrency,
            configuration.MaximumCapacity > 0 ? configuration.MaximumCapacity : 32);
        _uptime = Stopwatch.StartNew();
        _metrics = new PoolMetricsStorage();
        _isDisposed = false;
        _objectInfo = new ConcurrentDictionary<Guid, PoolObjectInfo<T>>();

        int shardCount = concurrency * 2;
        _shards = new ShardedBucket[shardCount];
        for (int i = 0; i < shardCount; i++)
        {
            _shards[i] = new ShardedBucket(i, shardCount);
        }

        if (configuration.Strategy == PoolingStrategy.ThreadLocal
            || configuration.Strategy == PoolingStrategy.ThreadLocalWithPartitioning)
        {
            _threadLocalStorage = new ThreadLocal<ConcurrentQueue<PooledItem>>(
                () => new ConcurrentQueue<PooledItem>(), true);
        }

        if (configuration.Strategy == PoolingStrategy.BoundedChannel)
        {
            var options = new BoundedChannelOptions(
                configuration.MaximumCapacity > 0 ? configuration.MaximumCapacity : Environment.ProcessorCount * 32)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = true,
            };
            _channel = Channel.CreateBounded<PooledItem>(options);
        }

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

    // ─── Properties ──────────────────────────────────────────────

    public int Count => Volatile.Read(ref _count);

    public int Capacity => _configuration.MaximumCapacity;

    public int Available => Count - Volatile.Read(ref _rented);

    // ─── Rent ────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        ThrowIfDisposed();

        Stopwatch? sw = null;
        if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full)
        {
            sw = Stopwatch.StartNew();
        }

        if (TryGetFromPool(out var pooledItem))
        {
            if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full && sw is not null)
            {
                sw.Stop();
                RecordRentTime(sw.Elapsed);
            }
            return pooledItem.Instance;
        }

        return RentSlow(sw);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private T RentSlow(Stopwatch? sw)
    {
        if (_configuration.Strategy == PoolingStrategy.LockFree)
        {
            return RentLockFree(sw);
        }

        if (_configuration.Strategy == PoolingStrategy.ThreadLocalWithPartitioning
            || _configuration.Strategy == PoolingStrategy.ShardedByThread)
        {
            return RentSharded(sw);
        }

        if (_configuration.UseSlimSynchronization)
        {
            using (_asyncLock.ReadLock())
            {
                if (TryGetFromPool(out var pooledItem))
                {
                    if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full && sw is not null)
                    {
                        sw.Stop();
                        RecordRentTime(sw.Elapsed);
                    }
                    return pooledItem.Instance;
                }

                using (_asyncLock.WriteLock())
                {
                    if (Volatile.Read(ref _rented) >= _configuration.MaximumCapacity
                        && !_configuration.AllowPoolExpansion)
                    {
                        if (_configuration.ThrowOnExhaustion)
                        {
                            RecordExhaustion();
                            ThrowHelper.ThrowInvalidOperationException(
                                "The pool is exhausted and cannot allocate more items.");
                        }
                    }
                    return CreateNewInstance(sw);
                }
            }
        }
        else
        {
            lock (_syncLock)
            {
                if (TryGetFromPool(out var pooledItem))
                {
                    if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full && sw is not null)
                    {
                        sw.Stop();
                        RecordRentTime(sw.Elapsed);
                    }
                    return pooledItem.Instance;
                }

                if (Volatile.Read(ref _rented) >= _configuration.MaximumCapacity
                    && !_configuration.AllowPoolExpansion)
                {
                    if (_configuration.ThrowOnExhaustion)
                    {
                        RecordExhaustion();
                        ThrowHelper.ThrowInvalidOperationException(
                            "The pool is exhausted and cannot allocate more items.");
                    }
                }
                return CreateNewInstance(sw);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private T RentLockFree(Stopwatch? sw)
    {
        if (Interlocked.Increment(ref _rented) > _configuration.MaximumCapacity
            && !_configuration.AllowPoolExpansion)
        {
            Interlocked.Decrement(ref _rented);
            if (_configuration.ThrowOnExhaustion)
            {
                RecordExhaustion();
                ThrowHelper.ThrowInvalidOperationException(
                    "The pool is exhausted and cannot allocate more items.");
            }

            bool lockTaken = false;
            _spinLock.Enter(ref lockTaken);
            try
            {
                if (TryGetFromPool(out var pooledItem))
                {
                    if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full && sw is not null)
                    {
                        sw.Stop();
                        RecordRentTime(sw.Elapsed);
                    }
                    return pooledItem.Instance;
                }

                return CreateNewInstance(sw);
            }
            finally
            {
                if (lockTaken)
                    _spinLock.Exit();
            }
        }

        Interlocked.Increment(ref _metrics.TotalRented);

        T instance = _factory();
        DateTime now = DateTime.UtcNow;
        Guid id = Guid.NewGuid();

        TrackInstanceInfo(instance, now, id);
        TrackRentOperation(instance, now, id);

        if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full && sw is not null)
        {
            sw.Stop();
            RecordRentTime(sw.Elapsed);
        }

        Interlocked.Increment(ref _count);
        _handler.OnRent?.Invoke(instance);
        return instance;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private T RentSharded(Stopwatch? sw)
    {
        if (_threadLocalStorage is not null)
        {
            var localQueue = _threadLocalStorage.Value!;
            if (localQueue.TryDequeue(out var localItem))
            {
                Interlocked.Increment(ref _rented);
                Interlocked.Increment(ref _metrics.TotalRented);

                if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full && sw is not null)
                {
                    sw.Stop();
                    RecordRentTime(sw.Elapsed);
                }

                _handler.OnRent?.Invoke(localItem.Instance);
                return localItem.Instance;
            }
        }

        int threadId = Environment.CurrentManagedThreadId;
        int shardIndex = Math.Abs(threadId % _shards.Length);
        ref var shard = ref _shards[shardIndex];

        if (shard.TryDequeue(out var shardItem))
        {
            Interlocked.Increment(ref _rented);
            Interlocked.Increment(ref _metrics.TotalRented);

            if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full && sw is not null)
            {
                sw.Stop();
                RecordRentTime(sw.Elapsed);
            }

            _handler.OnRent?.Invoke(shardItem.Instance);
            return shardItem.Instance;
        }

        if (TryGetFromPool(out var pooledItem))
        {
            if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full && sw is not null)
            {
                sw.Stop();
                RecordRentTime(sw.Elapsed);
            }
            return pooledItem.Instance;
        }

        if (Volatile.Read(ref _rented) >= _configuration.MaximumCapacity
            && !_configuration.AllowPoolExpansion)
        {
            if (_configuration.ThrowOnExhaustion)
            {
                RecordExhaustion();
                ThrowHelper.ThrowInvalidOperationException(
                    "The pool is exhausted and cannot allocate more items.");
            }

            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);
                if (TryGetFromPool(out var fallbackItem))
                {
                    Interlocked.Increment(ref _rented);
                    if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full && sw is not null)
                    {
                        sw.Stop();
                        RecordRentTime(sw.Elapsed);
                    }
                    return fallbackItem.Instance;
                }

                return CreateNewInstance(sw);
            }
            finally
            {
                if (lockTaken)
                    _spinLock.Exit();
            }
        }

        return CreateNewInstance(sw);
    }

    // ─── Return ──────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        if (item is null)
            ThrowHelper.ThrowArgumentNullException(nameof(item));

        ThrowIfDisposed();

        DateTime now = DateTime.UtcNow;
        Guid instanceId = Guid.Empty;

        if (_configuration.TrackLeaks)
        {
            foreach (var kvp in _rentTracking)
            {
                if (ReferenceEquals(kvp.Value.Instance, item))
                {
                    instanceId = kvp.Value.InstanceId;
                    _rentTracking.TryRemove(kvp.Key, out _);
                    break;
                }
            }
        }

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
            _handler.OnDispose?.Invoke(item);
            Interlocked.Decrement(ref _count);
            Interlocked.Decrement(ref _rented);
            return;
        }

        Interlocked.Decrement(ref _rented);
        Interlocked.Increment(ref _metrics.TotalReturned);

        // Validate before returning to pool
        if (_handler.Validator is not null && !_handler.Validator(item))
        {
            Interlocked.Increment(ref _metrics.FailedValidationCount);
            try { _handler.OnDispose?.Invoke(item); } catch { Interlocked.Increment(ref _metrics.DisposeFailureCount); }
            Interlocked.Increment(ref _metrics.TotalDestroyed);
            Interlocked.Decrement(ref _count);
            return;
        }

        var pooledItem = new PooledItem(item, now, _generation, 0, instanceId);

        if (_configuration.Strategy == PoolingStrategy.ThreadLocal && _threadLocalStorage is not null)
        {
            var localQueue = _threadLocalStorage.Value!;
            localQueue.Enqueue(pooledItem);
            return;
        }

        if (_configuration.Strategy == PoolingStrategy.ThreadLocalWithPartitioning
            || _configuration.Strategy == PoolingStrategy.ShardedByThread)
        {
            int threadId = Environment.CurrentManagedThreadId;
            int shardIndex = Math.Abs(threadId % _shards.Length);
            ref var shard = ref _shards[shardIndex];
            shard.Enqueue(pooledItem);
            return;
        }

        if (_configuration.Strategy == PoolingStrategy.BoundedChannel && _channel is not null)
        {
            if (_channel.Writer.TryWrite(pooledItem))
                return;
        }

        if (Count < _configuration.MaximumCapacity || _configuration.AllowPoolExpansion)
        {
            _items.Enqueue(pooledItem);
        }
        else
        {
            try
            {
                _handler.OnDispose?.Invoke(item);
            }
            catch
            {
                Interlocked.Increment(ref _metrics.DisposeFailureCount);
            }
            Interlocked.Increment(ref _metrics.TotalDestroyed);
            Interlocked.Decrement(ref _count);
        }
    }

    // ─── Internal helpers ────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetFromPool(out PooledItem item)
    {
        item = default;

        if (_configuration.Strategy == PoolingStrategy.ThreadLocal && _threadLocalStorage is not null)
        {
            var localQueue = _threadLocalStorage.Value!;
            if (localQueue.TryDequeue(out var localItem))
            {
                Interlocked.Increment(ref _rented);
                Interlocked.Increment(ref _metrics.TotalRented);
                item = localItem;
                _handler.OnRent?.Invoke(item.Instance);
                return true;
            }
        }

        if (_configuration.Strategy == PoolingStrategy.BoundedChannel && _channel is not null)
        {
            if (_channel.Reader.TryRead(out var channelItem))
            {
                if (_handler.Validator is not null && !_handler.Validator(channelItem.Instance))
                {
                    Interlocked.Increment(ref _metrics.FailedValidationCount);
                    Interlocked.Decrement(ref _count);
                    return false;
                }

                Interlocked.Increment(ref _rented);
                Interlocked.Increment(ref _metrics.TotalRented);
                item = channelItem;
                _handler.OnRent?.Invoke(item.Instance);
                return true;
            }
        }

        while (_items.TryDequeue(out item))
        {
            if (_handler.Validator is not null && !_handler.Validator(item.Instance))
            {
                Interlocked.Increment(ref _metrics.FailedValidationCount);
                Interlocked.Decrement(ref _count);
                continue;
            }

            Interlocked.Increment(ref _rented);
            Interlocked.Increment(ref _metrics.TotalRented);
            _handler.OnRent?.Invoke(item.Instance);
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T CreateNewInstance(Stopwatch? sw)
    {
        Interlocked.Increment(ref _metrics.TotalCreated);

        T instance;
        try
        {
            instance = _factory();
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _metrics.AllocFailureCount);
            _metrics.LastException = ex;
            throw;
        }

        DateTime now = DateTime.UtcNow;
        Guid id = Guid.NewGuid();

        TrackInstanceInfo(instance, now, id);
        TrackRentOperation(instance, now, id);

        Interlocked.Increment(ref _count);
        Interlocked.Increment(ref _rented);

        if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full && sw is not null)
        {
            sw.Stop();
            RecordRentTime(sw.Elapsed);
        }

        _handler.OnRent?.Invoke(instance);
        return instance;
    }

    private void TrackInstanceInfo(T instance, DateTime now, Guid id)
    {
        if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full)
        {
            _objectInfo[id] = new PoolObjectInfo<T>
            {
                Instance = instance,
                CreationTime = now,
                Generation = _generation,
                RentCount = 0,
                TotalRentTime = TimeSpan.Zero,
                IsPoolable = instance is IPoolable,
                IsActive = true,
                Id = id,
            };
        }
    }

    private void TrackRentOperation(T instance, DateTime now, Guid id)
    {
        if (_configuration.TrackLeaks)
        {
            long rentId = Interlocked.Increment(ref _lastRentId);
            _rentTracking[rentId] = new RentOperation(
                instance, now, rentId, Environment.CurrentManagedThreadId, id);
        }
    }

    private void PreAllocate(int count)
    {
        for (int i = 0; i < count; i++)
        {
            T instance = _factory();
            DateTime now = DateTime.UtcNow;
            Guid id = Guid.NewGuid();

            Interlocked.Increment(ref _metrics.TotalCreated);
            TrackInstanceInfo(instance, now, id);

            var pooledItem = new PooledItem(instance, now, _generation, 0, id);

            if (_configuration.Strategy == PoolingStrategy.ThreadLocal && _threadLocalStorage is not null)
            {
                _threadLocalStorage.Value!.Enqueue(pooledItem);
            }
            else if (_configuration.Strategy == PoolingStrategy.ThreadLocalWithPartitioning
                || _configuration.Strategy == PoolingStrategy.ShardedByThread)
            {
                int shardIndex = i % _shards.Length;
                _shards[shardIndex].Enqueue(pooledItem);
            }
            else if (_configuration.Strategy == PoolingStrategy.BoundedChannel && _channel is not null)
            {
                _channel.Writer.TryWrite(pooledItem);
            }
            else
            {
                _items.Enqueue(pooledItem);
            }

            Interlocked.Increment(ref _count);
        }
    }

    // ─── Metrics helpers ─────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordRentTime(TimeSpan elapsed)
    {
        lock (_syncLock)
        {
            _metrics.TotalRentDurationTicks += elapsed.Ticks;
            if (_configuration.MetricsSamplingRate >= MetricsSamplingRate.Medium)
            {
                _metrics.RentDurationsMs.Add(elapsed.TotalMilliseconds);
                if (_metrics.RentDurationsMs.Count > 1000)
                    _metrics.RentDurationsMs.RemoveRange(0, 500);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordExhaustion()
    {
        Interlocked.Increment(ref _metrics.ExhaustionCount);
    }

    private void RecordEvent(string message)
    {
        lock (_syncLock)
        {
            _metrics.Events.Add(new KeyValuePair<DateTime, string>(DateTime.UtcNow, message));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            ThrowHelper.ThrowObjectDisposedException(nameof(ObjectPool<T>));
    }

    // ─── Dispose ─────────────────────────────────────────────────

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

        _semaphore.Dispose();
        _asyncLock.Dispose();
        _threadLocalStorage?.Dispose();

        RecordEvent("Pool disposed");
    }

    // ─── GetMetrics ──────────────────────────────────────────────

    public PoolMetrics GetMetrics()
    {
        return new PoolMetrics
        {
            TotalCreated = Volatile.Read(ref _metrics.TotalCreated),
            TotalRented = Volatile.Read(ref _metrics.TotalRented),
            TotalReturned = Volatile.Read(ref _metrics.TotalReturned),
            TotalDestroyed = Volatile.Read(ref _metrics.TotalDestroyed),
            CurrentCount = Count,
            AvailableCount = Available,
            RentedCount = Volatile.Read(ref _rented),
            MaximumCapacity = _configuration.MaximumCapacity,
            PeakUtilization = _metrics.PeakUtilization,
            PeakTime = _metrics.PeakTime,
            LastResetTime = _metrics.LastResetTime,
            LastScavengeTime = _metrics.LastScavengeTime,
        };
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
