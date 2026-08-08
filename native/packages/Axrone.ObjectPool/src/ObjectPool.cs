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
    private Timer? _scavengeTimer;

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

        if (configuration.ScavengeIntervalMs > 0
            && configuration.EvictionStrategy != EvictionStrategy.None)
        {
            _scavengeTimer = new Timer(
                ScavengeCallback,
                null,
                configuration.ScavengeIntervalMs,
                configuration.ScavengeIntervalMs);
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

        _scavengeTimer?.Dispose();

        ClearCore();

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

    // ─── IObjectPool<T> — remaining methods ──────────────────────

    public async ValueTask<T> RentAsync(CancellationToken cancellationToken = default)
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

        return await RentSlowAsync(sw, cancellationToken).ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask<T> RentSlowAsync(Stopwatch? sw, CancellationToken cancellationToken)
    {
        if (_configuration.Strategy == PoolingStrategy.BoundedChannel && _channel is not null)
        {
            try
            {
                if (await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (_channel.Reader.TryRead(out var channelItem))
                    {
                        if (_handler.ValidatorAsync is not null)
                        {
                            bool isValid = await _handler.ValidatorAsync(channelItem.Instance).ConfigureAwait(false);
                            if (!isValid)
                            {
                                Interlocked.Increment(ref _metrics.FailedValidationCount);
                                Interlocked.Decrement(ref _count);
                                return await RentAsync(cancellationToken).ConfigureAwait(false);
                            }
                        }
                        else if (_handler.Validator is not null && !_handler.Validator(channelItem.Instance))
                        {
                            Interlocked.Increment(ref _metrics.FailedValidationCount);
                            Interlocked.Decrement(ref _count);
                            return await RentAsync(cancellationToken).ConfigureAwait(false);
                        }

                        Interlocked.Increment(ref _rented);
                        Interlocked.Increment(ref _metrics.TotalRented);

                        if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full && sw is not null)
                        {
                            sw.Stop();
                            RecordRentTime(sw.Elapsed);
                        }

                        _handler.OnRent?.Invoke(channelItem.Instance);
                        return channelItem.Instance;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _metrics.LastException = ex;
                if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Basic)
                {
                    RecordEvent($"Channel operation failed: {ex.Message}");
                }
            }
        }

        if (_configuration.UseSlimSynchronization)
        {
            using (await _asyncLock.ReadLockAsync(cancellationToken).ConfigureAwait(false))
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

                using (await _asyncLock.WriteLockAsync(cancellationToken).ConfigureAwait(false))
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

    public async ValueTask ReturnAsync(T item, CancellationToken cancellationToken = default)
    {
        if (item is null)
            ThrowHelper.ThrowArgumentNullException(nameof(item));

        ThrowIfDisposed();

        try
        {
            if (_handler.ResetAsync is not null)
            {
                await _handler.ResetAsync(item).ConfigureAwait(false);
            }
            else if (_handler.Reset is not null)
            {
                _handler.Reset(item);
            }
            else if (item is IPoolable poolable)
            {
                await poolable.ResetAsync().ConfigureAwait(false);
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

        DateTime now = DateTime.UtcNow;
        var pooledItem = new PooledItem(item, now, _generation, 0, Guid.Empty);

        if (_configuration.Strategy == PoolingStrategy.BoundedChannel && _channel is not null)
        {
            try
            {
                await _channel.Writer.WriteAsync(pooledItem, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch
            {
                // Fall through to central queue
            }
        }

        _items.Enqueue(pooledItem);
    }

    public bool TryRent([NotNullWhen(true)] out T? item)
    {
        ThrowIfDisposed();
        item = null;

        if (TryGetFromPool(out var pooledItem))
        {
            item = pooledItem.Instance;
            return true;
        }

        // Non-blocking: try to create if under capacity
        if (Volatile.Read(ref _rented) >= _configuration.MaximumCapacity
            && !_configuration.AllowPoolExpansion)
        {
            return false;
        }

        bool lockTaken = false;
        try
        {
            _spinLock.Enter(ref lockTaken);

            if (Volatile.Read(ref _rented) >= _configuration.MaximumCapacity
                && !_configuration.AllowPoolExpansion)
            {
                return false;
            }

            item = CreateNewInstance(null);
            return true;
        }
        finally
        {
            if (lockTaken)
                _spinLock.Exit();
        }
    }

    public async ValueTask<(bool Success, T? Item)> TryRentAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (TryGetFromPool(out var pooledItem))
        {
            return (true, pooledItem.Instance);
        }

        if (Volatile.Read(ref _rented) >= _configuration.MaximumCapacity
            && !_configuration.AllowPoolExpansion)
        {
            return (false, null);
        }

        if (_configuration.UseSlimSynchronization)
        {
            using (await _asyncLock.WriteLockAsync(cancellationToken).ConfigureAwait(false))
            {
                if (Volatile.Read(ref _rented) >= _configuration.MaximumCapacity
                    && !_configuration.AllowPoolExpansion)
                {
                    return (false, null);
                }

                var item = CreateNewInstance(null);
                return (true, item);
            }
        }
        else
        {
            lock (_syncLock)
            {
                if (Volatile.Read(ref _rented) >= _configuration.MaximumCapacity
                    && !_configuration.AllowPoolExpansion)
                {
                    return (false, null);
                }

                var item = CreateNewInstance(null);
                return (true, item);
            }
        }
    }

    public IPoolable<T> GetPoolable()
    {
        var instance = Rent();
        return new Poolable<T>(instance, this);
    }

    public async ValueTask<IPoolable<T>> GetPoolableAsync(CancellationToken cancellationToken = default)
    {
        var instance = await RentAsync(cancellationToken).ConfigureAwait(false);
        return new Poolable<T>(instance, this);
    }

    public void Clear()
    {
        ThrowIfDisposed();

        if (_configuration.UseSlimSynchronization)
        {
            using (_asyncLock.WriteLock())
            {
                ClearCore();
            }
        }
        else
        {
            lock (_syncLock)
            {
                ClearCore();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearCore()
    {
        while (_items.TryDequeue(out var item))
        {
            Interlocked.Decrement(ref _count);
            try
            {
                _handler.OnDispose?.Invoke(item.Instance);
                Interlocked.Increment(ref _metrics.TotalDestroyed);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _metrics.DisposeFailureCount);
                _metrics.LastException = ex;
            }
        }

        if (_threadLocalStorage is not null)
        {
            foreach (var localQueue in _threadLocalStorage.Values)
            {
                while (localQueue.TryDequeue(out var item))
                {
                    Interlocked.Decrement(ref _count);
                    try
                    {
                        _handler.OnDispose?.Invoke(item.Instance);
                        Interlocked.Increment(ref _metrics.TotalDestroyed);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref _metrics.DisposeFailureCount);
                        _metrics.LastException = ex;
                    }
                }
            }
        }

        for (int i = 0; i < _shards.Length; i++)
        {
            while (_shards[i].TryDequeue(out var item))
            {
                Interlocked.Decrement(ref _count);
                try
                {
                    _handler.OnDispose?.Invoke(item.Instance);
                    Interlocked.Increment(ref _metrics.TotalDestroyed);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _metrics.DisposeFailureCount);
                    _metrics.LastException = ex;
                }
            }
        }

        RecordEvent("Pool cleared");
    }

    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_configuration.UseSlimSynchronization)
        {
            using (await _asyncLock.WriteLockAsync(cancellationToken).ConfigureAwait(false))
            {
                await ClearAsyncCore(cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            lock (_syncLock)
            {
                ClearCore();
            }
        }
    }

    private async ValueTask ClearAsyncCore(CancellationToken cancellationToken)
    {
        while (_items.TryDequeue(out var item))
        {
            Interlocked.Decrement(ref _count);
            try
            {
                if (_handler.OnDisposeAsync is not null)
                {
                    await _handler.OnDisposeAsync(item.Instance).ConfigureAwait(false);
                }
                else
                {
                    _handler.OnDispose?.Invoke(item.Instance);
                }
                Interlocked.Increment(ref _metrics.TotalDestroyed);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _metrics.DisposeFailureCount);
                _metrics.LastException = ex;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        RecordEvent("Pool cleared asynchronously");
    }

    public bool Contains(T item)
    {
        if (item is null)
            ThrowHelper.ThrowArgumentNullException(nameof(item));

        ThrowIfDisposed();

        if (_configuration.TrackLeaks)
        {
            foreach (var kvp in _rentTracking)
            {
                if (ReferenceEquals(kvp.Value.Instance, item))
                    return true;
            }
        }

        foreach (var pooledItem in _items)
        {
            if (ReferenceEquals(pooledItem.Instance, item))
                return true;
        }

        if (_threadLocalStorage is not null)
        {
            foreach (var localQueue in _threadLocalStorage.Values)
            {
                foreach (var pooledItem in localQueue)
                {
                    if (ReferenceEquals(pooledItem.Instance, item))
                        return true;
                }
            }
        }

        return false;
    }

    public PoolDiagnostics GetDiagnostics()
    {
        ThrowIfDisposed();

        if (_configuration.DiagnosticsLevel == DiagnosticsLevel.None)
        {
            return new PoolDiagnostics
            {
                Events = new List<KeyValuePair<DateTime, string>>(0),
                CustomMetrics = new Dictionary<string, double>(0),
            };
        }

        var events = _configuration.DiagnosticsLevel >= DiagnosticsLevel.Basic
            ? _metrics.Events.ToList()
            : new List<KeyValuePair<DateTime, string>>(0);

        var customMetrics = _configuration.DiagnosticsLevel >= DiagnosticsLevel.Basic
            ? new Dictionary<string, double>(_metrics.CustomMetrics)
            : new Dictionary<string, double>(0);

        return new PoolDiagnostics
        {
            Uptime = _uptime.Elapsed,
            ConfigurationChanges = _metrics.ConfigurationChanges,
            ScavengeCount = _metrics.ScavengeCount,
            ExpansionCount = _metrics.ExpansionCount,
            ShrinkCount = _metrics.ShrinkCount,
            FailedValidationCount = _metrics.FailedValidationCount,
            ExhaustionCount = _metrics.ExhaustionCount,
            InitialCapacity = _configuration.InitialCapacity,
            MaximumCapacity = _configuration.MaximumCapacity,
            AllocFailureCount = _metrics.AllocFailureCount,
            DisposeFailureCount = _metrics.DisposeFailureCount,
            RecycleFailureCount = _metrics.RecycleFailureCount,
            Events = events,
            CustomMetrics = customMetrics,
            LastException = _metrics.LastException,
        };
    }

    public void Reconfigure(PoolConfiguration configuration)
    {
        ThrowIfDisposed();

        if (_configuration.UseSlimSynchronization)
        {
            using (_asyncLock.WriteLock())
            {
                ReconfigureCore(configuration);
            }
        }
        else
        {
            lock (_syncLock)
            {
                ReconfigureCore(configuration);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReconfigureCore(PoolConfiguration configuration)
    {
        Interlocked.Increment(ref _metrics.ConfigurationChanges);
        var oldConfiguration = _configuration;
        _configuration = configuration;

        if (configuration.MaximumCapacity < oldConfiguration.MaximumCapacity)
        {
            TrimCore(configuration.MaximumCapacity);
        }
        else if (configuration.MaximumCapacity > oldConfiguration.MaximumCapacity
            && configuration.AllocationPolicy == AllocationPolicy.PreallocateAll)
        {
            int toAdd = configuration.MaximumCapacity - Count;
            if (toAdd > 0)
            {
                PreAllocate(toAdd);
            }
        }

        if (_scavengeTimer is not null
            && configuration.ScavengeIntervalMs != oldConfiguration.ScavengeIntervalMs
            && configuration.ScavengeIntervalMs > 0)
        {
            _scavengeTimer.Change(
                configuration.ScavengeIntervalMs,
                configuration.ScavengeIntervalMs);
        }

        RecordEvent($"Pool reconfigured: MaxCapacity {oldConfiguration.MaximumCapacity} -> {configuration.MaximumCapacity}");
    }

    public void Trim(int targetCapacity = -1)
    {
        ThrowIfDisposed();

        if (_configuration.UseSlimSynchronization)
        {
            using (_asyncLock.WriteLock())
            {
                TrimCore(targetCapacity);
            }
        }
        else
        {
            lock (_syncLock)
            {
                TrimCore(targetCapacity);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrimCore(int targetCapacity)
    {
        int currentCount = Count;
        int targetCount = targetCapacity > 0
            ? Math.Min(targetCapacity, _configuration.MaximumCapacity)
            : (int)(currentCount * (_configuration.TargetUtilizationPercentage / 100.0));

        if (targetCount >= currentCount)
            return;

        int toRemove = currentCount - targetCount;
        if (toRemove <= 0)
            return;

        Interlocked.Increment(ref _metrics.ShrinkCount);

        int removed = 0;
        while (removed < toRemove && _items.TryDequeue(out var item))
        {
            Interlocked.Decrement(ref _count);
            removed++;
            DisposeTrimmedItem(item);
        }

        if (removed < toRemove && _threadLocalStorage is not null)
        {
            foreach (var localQueue in _threadLocalStorage.Values)
            {
                while (removed < toRemove && localQueue.TryDequeue(out var item))
                {
                    Interlocked.Decrement(ref _count);
                    removed++;
                    DisposeTrimmedItem(item);
                }
                if (removed >= toRemove) break;
            }
        }

        if (removed < toRemove)
        {
            for (int i = 0; i < _shards.Length; i++)
            {
                while (removed < toRemove && _shards[i].TryDequeue(out var item))
                {
                    Interlocked.Decrement(ref _count);
                    removed++;
                    DisposeTrimmedItem(item);
                }
                if (removed >= toRemove) break;
            }
        }

        RecordEvent($"Pool trimmed: removed {removed} items");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DisposeTrimmedItem(PooledItem item)
    {
        try
        {
            _handler.OnDispose?.Invoke(item.Instance);
            Interlocked.Increment(ref _metrics.TotalDestroyed);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _metrics.DisposeFailureCount);
            _metrics.LastException = ex;
        }
    }

    public async ValueTask TrimAsync(int targetCapacity = -1, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_configuration.UseSlimSynchronization)
        {
            using (await _asyncLock.WriteLockAsync(cancellationToken).ConfigureAwait(false))
            {
                TrimCore(targetCapacity);
            }
        }
        else
        {
            await Task.Run(() =>
            {
                lock (_syncLock)
                {
                    TrimCore(targetCapacity);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    public void ResetMetrics()
    {
        ThrowIfDisposed();

        if (_configuration.DiagnosticsLevel == DiagnosticsLevel.None)
            return;

        DateTime now = DateTime.UtcNow;

        lock (_syncLock)
        {
            _metrics.TotalRentDurationTicks = 0;
            _metrics.TotalCreateDurationTicks = 0;
            _metrics.TotalResetDurationTicks = 0;
            _metrics.RentDurationsMs.Clear();
            _metrics.Events.Clear();
            _metrics.LastResetTime = now;
            _metrics.PeakUtilization = Volatile.Read(ref _rented);
            _metrics.PeakTime = now;
        }

        RecordEvent("Metrics reset");
    }

    public IPoolableFactory<T, TResult> CreateFactory<TResult>()
    {
        return new PooledObjectFactory<T, TResult>(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    // ─── Scavenge ────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ScavengeCallback(object? state)
    {
        if (_isDisposed)
            return;

        if (Available <= 0
            || DateTime.UtcNow - _metrics.LastScavengeTime
                < TimeSpan.FromMilliseconds(_configuration.ScavengeIntervalMs / 2))
            return;

        if (_configuration.UseSlimSynchronization)
        {
            if (!_asyncLock.TryEnterWriteLock())
                return;

            try
            {
                ScavengeCore();
            }
            finally
            {
                _asyncLock.ExitWriteLock();
            }
        }
        else
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_syncLock, 0, ref lockTaken);
                if (!lockTaken)
                    return;

                ScavengeCore();
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(_syncLock);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ScavengeCore()
    {
        if (_configuration.EvictionStrategy == EvictionStrategy.None
            || _configuration.IdleTimeout <= TimeSpan.Zero)
        {
            return;
        }

        Interlocked.Increment(ref _generation);
        Interlocked.Increment(ref _metrics.ScavengeCount);
        _metrics.LastScavengeTime = DateTime.UtcNow;

        DateTime cutoffTime = DateTime.UtcNow - _configuration.IdleTimeout;
        int removed = 0;

        var tempItems = new ConcurrentQueue<PooledItem>();

        while (_items.TryDequeue(out var item))
        {
            if (item.Created < cutoffTime && item.Generation < _generation)
            {
                Interlocked.Decrement(ref _count);
                removed++;
                try
                {
                    _handler.OnDispose?.Invoke(item.Instance);
                    Interlocked.Increment(ref _metrics.TotalDestroyed);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _metrics.DisposeFailureCount);
                    _metrics.LastException = ex;
                }
            }
            else
            {
                tempItems.Enqueue(item);
            }
        }

        while (tempItems.TryDequeue(out var item))
        {
            _items.Enqueue(item);
        }

        if (removed > 0)
        {
            RecordEvent($"Scavenged {removed} idle items");
        }
    }

    // ─── Metrics calculation helpers ─────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double avgRentDuration, double p95RentDuration, double p99RentDuration) CalculateRentStatistics()
    {
        double avgRentDuration = 0, p95RentDuration = 0, p99RentDuration = 0;

        if (_configuration.MetricsSamplingRate == MetricsSamplingRate.None)
        {
            return (0, 0, 0);
        }

        long totalRentTicks = Interlocked.Read(ref _metrics.TotalRentDurationTicks);
        long totalReturned = Math.Max(1, Interlocked.Read(ref _metrics.TotalReturned));

        avgRentDuration = TimeSpan.FromTicks(totalRentTicks / totalReturned).TotalMilliseconds;

        if (_configuration.MetricsSamplingRate >= MetricsSamplingRate.Medium)
        {
            lock (_syncLock)
            {
                if (_metrics.RentDurationsMs.Count > 0)
                {
                    var durations = _metrics.RentDurationsMs.ToArray();
                    Array.Sort(durations);

                    int p95Index = (int)Math.Ceiling(durations.Length * 0.95) - 1;
                    int p99Index = (int)Math.Ceiling(durations.Length * 0.99) - 1;

                    if (p95Index >= 0 && p95Index < durations.Length)
                        p95RentDuration = durations[p95Index];
                    if (p99Index >= 0 && p99Index < durations.Length)
                        p99RentDuration = durations[p99Index];
                }
            }
        }

        return (avgRentDuration, p95RentDuration, p99RentDuration);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateAverageCreateDuration()
    {
        if (_configuration.MetricsSamplingRate == MetricsSamplingRate.None)
            return 0;

        long totalCreateTicks = Interlocked.Read(ref _metrics.TotalCreateDurationTicks);
        long totalCreated = Math.Max(1, Interlocked.Read(ref _metrics.TotalCreated));
        return TimeSpan.FromTicks(totalCreateTicks / totalCreated).TotalMilliseconds;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateAverageResetDuration()
    {
        if (_configuration.MetricsSamplingRate == MetricsSamplingRate.None)
            return 0;

        long totalResetTicks = Interlocked.Read(ref _metrics.TotalResetDurationTicks);
        long totalReturned = Math.Max(1, Interlocked.Read(ref _metrics.TotalReturned));
        return TimeSpan.FromTicks(totalResetTicks / totalReturned).TotalMilliseconds;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateTurnoverRate()
    {
        long totalRented = Interlocked.Read(ref _metrics.TotalRented);
        long totalReturned = Interlocked.Read(ref _metrics.TotalReturned);
        if (totalRented == 0) return 0;
        return (double)totalReturned / totalRented;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateGrowthRate()
    {
        long totalCreated = Interlocked.Read(ref _metrics.TotalCreated);
        long totalDestroyed = Interlocked.Read(ref _metrics.TotalDestroyed);
        if (totalCreated == 0) return 0;
        return 1.0 - (double)totalDestroyed / totalCreated;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateMemoryUsage()
    {
        if (_configuration.DiagnosticsLevel <= DiagnosticsLevel.Minimal)
            return 0;

        double estimatedSizePerItem = _metrics.MemoryUsageBytes / Math.Max(1, Count);
        return estimatedSizePerItem * Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateMemoryPressure(double memoryUsage)
    {
        if (_configuration.DiagnosticsLevel <= DiagnosticsLevel.Minimal || memoryUsage <= 0)
            return 0;

        long availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return memoryUsage / availableMemory;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordCreateTime(TimeSpan elapsed)
    {
        if (_configuration.DiagnosticsLevel < DiagnosticsLevel.Full)
            return;
        Interlocked.Add(ref _metrics.TotalCreateDurationTicks, elapsed.Ticks);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordResetTime(TimeSpan elapsed)
    {
        if (_configuration.DiagnosticsLevel < DiagnosticsLevel.Full)
            return;
        Interlocked.Add(ref _metrics.TotalResetDurationTicks, elapsed.Ticks);
    }
}

// ─── PooledObjectFactory ────────────────────────────────────────

[StructLayout(LayoutKind.Auto)]
public sealed class PooledObjectFactory<T, TResult> : IPoolableFactory<T, TResult> where T : class
{
    private readonly IObjectPool<T> _pool;

    public PooledObjectFactory(IObjectPool<T> pool)
    {
        _pool = pool ?? ThrowHelper.ThrowArgumentNullException<IObjectPool<T>>(nameof(pool));
    }

    public TResult Create(T item)
    {
        if (item is null)
            ThrowHelper.ThrowArgumentNullException(nameof(item));

        if (item is TResult result)
            return result;

        if (typeof(TResult) == typeof(IPoolable<T>))
            return (TResult)(object)new Poolable<T>(item, _pool);

        throw new InvalidOperationException(
            $"Cannot convert {typeof(T).Name} to {typeof(TResult).Name}");
    }
}
