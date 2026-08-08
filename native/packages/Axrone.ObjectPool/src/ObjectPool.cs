namespace Axrone.ObjectPool;

/// <summary>
/// A high-performance, thread-safe generic object pool that supports multiple pooling strategies,
/// diagnostics, metrics tracking, leak detection, and automatic scavenging of idle items.
/// </summary>
/// <typeparam name="T">The reference type of objects managed by the pool.</typeparam>
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
        public readonly long Id;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PooledItem(T instance, DateTime created, int generation, long rentCount, long id)
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
        public readonly long InstanceId;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RentOperation(T instance, DateTime rentTime, long operationId, int threadId, long instanceId)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsInstance(T item)
        {
            foreach (var pooledItem in _items)
            {
                if (ReferenceEquals(pooledItem.Instance, item))
                    return true;
            }
            return false;
        }
    }

    // ─── Fields ──────────────────────────────────────────────────

    private readonly Func<T> _factory;
    private readonly PoolableHandler<T> _handler;
    private readonly object _syncLock;
    private readonly AsyncReaderWriterLock _asyncLock;
    private readonly SpinLock _spinLock;
    private readonly Channel<PooledItem>? _channel;
    private readonly ConcurrentDictionary<long, RentOperation> _rentTracking;
    private readonly Stopwatch _uptime;
    private readonly PoolMetricsStorage _metrics;
    private readonly ThreadLocal<ConcurrentQueue<PooledItem>>? _threadLocalStorage;
    private readonly ShardedBucket[] _shards;
    private readonly ConcurrentDictionary<long, PoolObjectInfo<T>> _objectInfo;
    private readonly ConcurrentDictionary<int, byte> _activePoolables;
    private readonly ConditionalWeakTable<T, RentTrackingInfo> _rentReverseLookup;

    private sealed class RentTrackingInfo
    {
        public long RentId;
        public DateTime RentTime;
        public long InstanceId;
    }

    private PoolConfiguration _configuration;
    private int _count;
    private int _rented;
    private int _generation;
    private long _lastRentId;
    private long _nextInstanceId;
    private volatile bool _isDisposed;
    private ConcurrentQueue<PooledItem> _items;
    private Timer? _scavengeTimer;

    // ─── Constructors ────────────────────────────────────────────

    /// <summary>
    /// Initializes a new instance of <see cref="ObjectPool{T}"/> with the specified configuration and handler.
    /// </summary>
    /// <param name="configuration">The pool configuration controlling capacity, strategy, and diagnostics.</param>
    /// <param name="handler">The handler providing factory, reset, dispose, and optional validation callbacks.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> or its <see cref="PoolableHandler{T}.Factory"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="configuration"/> contains invalid values.</exception>
    public ObjectPool(PoolConfiguration configuration, PoolableHandler<T> handler)
    {
        if (handler.Factory is null)
            ThrowHelper.ThrowArgumentNullException(nameof(handler.Factory));

        _factory = handler.Factory;
        _handler = handler;
        _configuration = configuration;
        ValidateConfiguration(configuration);
        ValidateStrategy(configuration.Strategy);
        _syncLock = new object();
        _asyncLock = new AsyncReaderWriterLock();
        _spinLock = new SpinLock(enableThreadOwnerTracking: false);
        _items = new ConcurrentQueue<PooledItem>();
        int concurrency = (int)configuration.ConcurrencyLevel;
        if (concurrency <= 0) concurrency = (int)ConcurrencyLevel.Default;

        _rentTracking = new ConcurrentDictionary<long, RentOperation>(
            concurrency,
            configuration.MaximumCapacity > 0 ? configuration.MaximumCapacity : 32);
        _uptime = Stopwatch.StartNew();
        _metrics = new PoolMetricsStorage();
        _isDisposed = false;
        _objectInfo = new ConcurrentDictionary<long, PoolObjectInfo<T>>();
        _activePoolables = new ConcurrentDictionary<int, byte>();
        _rentReverseLookup = new ConditionalWeakTable<T, RentTrackingInfo>();

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

    /// <summary>
    /// Initializes a new instance of <see cref="ObjectPool{T}"/> with default configuration and the specified handler.
    /// </summary>
    /// <param name="handler">The handler providing factory, reset, dispose, and optional validation callbacks.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> or its <see cref="PoolableHandler{T}.Factory"/> is <c>null</c>.</exception>
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

    /// <summary>
    /// Initializes a new instance of <see cref="ObjectPool{T}"/> with a factory delegate and optional reset/dispose callbacks.
    /// </summary>
    /// <param name="factory">The delegate used to create new instances of <typeparamref name="T"/>.</param>
    /// <param name="reset">An optional action invoked to reset an item when it is returned to the pool.</param>
    /// <param name="dispose">An optional action invoked to dispose an item when it is evicted or the pool is cleared.</param>
    /// <param name="initialCapacity">The number of items to pre-allocate; 0 means no pre-allocation.</param>
    /// <param name="maxCapacity">The maximum number of items the pool may hold; 0 defaults to <c>ProcessorCount * 32</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is <c>null</c>.</exception>
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

    /// <summary>
    /// Gets the total number of items currently managed by the pool (both available and rented).
    /// </summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>
    /// Gets the maximum number of items the pool is configured to hold.
    /// </summary>
    public int Capacity => _configuration.MaximumCapacity;

    /// <summary>
    /// Gets the number of items currently available in the pool (total count minus rented count).
    /// </summary>
    public int Available => Count - Volatile.Read(ref _rented);

    // ─── Rent ────────────────────────────────────────────────────

    /// <summary>
    /// Rents an item from the pool, reusing an available item or creating a new one if the pool is empty.
    /// </summary>
    /// <returns>A pooled instance of <typeparamref name="T"/>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the pool is exhausted and <see cref="PoolConfiguration.ThrowOnExhaustion"/> is enabled.</exception>
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
        long id = Interlocked.Increment(ref _nextInstanceId);
        DateTime now = (_configuration.TrackLeaks
            || _configuration.DiagnosticsLevel >= DiagnosticsLevel.Full)
            ? DateTime.UtcNow : default;

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

    /// <summary>
    /// Returns a previously rented item to the pool, invoking reset and validation callbacks before making it available.
    /// </summary>
    /// <param name="item">The item to return to the pool.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        if (item is null)
            ThrowHelper.ThrowArgumentNullException(nameof(item));

        ThrowIfDisposed();

        DateTime now = DateTime.UtcNow;
        long instanceId = 0L;
        TimeSpan rentDuration = TimeSpan.Zero;

        if (_configuration.TrackLeaks)
        {
            if (_rentReverseLookup.TryGetValue(item, out var trackingInfo))
            {
                instanceId = trackingInfo.InstanceId;
                rentDuration = now - trackingInfo.RentTime;
                _rentTracking.TryRemove(trackingInfo.RentId, out _);
                _rentReverseLookup.Remove(item);
            }
        }

        Stopwatch? resetSw = null;
        if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full)
        {
            resetSw = Stopwatch.StartNew();
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
        catch (Exception ex)
        {
            Interlocked.Increment(ref _metrics.RecycleFailureCount);
            _metrics.LastException = ex;
            _handler.OnDispose?.Invoke(item);
            Interlocked.Decrement(ref _count);
            Interlocked.Decrement(ref _rented);
            return;
        }
        finally
        {
            if (resetSw is not null)
            {
                resetSw.Stop();
                RecordResetTime(resetSw.Elapsed);
            }
        }

        Interlocked.Decrement(ref _rented);
        Interlocked.Increment(ref _metrics.TotalReturned);

        // Update objectInfo rent count
        if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full && instanceId != 0L)
        {
            if (_objectInfo.TryGetValue(instanceId, out var info))
            {
                _objectInfo[instanceId] = new PoolObjectInfo<T>
                {
                    Instance = info.Instance,
                    CreationTime = info.CreationTime,
                    Generation = info.Generation,
                    RentCount = info.RentCount + 1,
                    TotalRentTime = info.TotalRentTime + rentDuration,
                    IsPoolable = info.IsPoolable,
                    IsActive = false,
                    Id = info.Id,
                };
            }
        }

        // Validate before returning to pool
        if (_handler.Validator is not null && !_handler.Validator(item))
        {
            Interlocked.Increment(ref _metrics.FailedValidationCount);
            try
            {
                _handler.OnDispose?.Invoke(item);
                if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full)
                {
                    _handler.OnDestroy?.Invoke(new PoolObjectInfo<T>
                    {
                        Instance = item, CreationTime = now, Generation = _generation,
                        RentCount = 0, TotalRentTime = TimeSpan.Zero,
                        IsPoolable = item is IPoolable, IsActive = false, Id = instanceId,
                    });
                    _objectInfo.TryRemove(instanceId, out _);
                }
            }
            catch { Interlocked.Increment(ref _metrics.DisposeFailureCount); }
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
            {
                return;
            }
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
                if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full)
                {
                    _handler.OnDestroy?.Invoke(new PoolObjectInfo<T>
                    {
                        Instance = item, CreationTime = now, Generation = _generation,
                        RentCount = 0, TotalRentTime = TimeSpan.Zero,
                        IsPoolable = item is IPoolable, IsActive = false, Id = instanceId,
                    });
                    _objectInfo.TryRemove(instanceId, out _);
                }
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
        var creationSw = _configuration.DiagnosticsLevel >= DiagnosticsLevel.Full
            ? Stopwatch.StartNew() : null;

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
            if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Basic)
                RecordEvent($"Failed to create instance: {ex.Message}");
            throw;
        }

        if (creationSw is not null)
        {
            creationSw.Stop();
            RecordCreateTime(creationSw.Elapsed);
        }

        long id = Interlocked.Increment(ref _nextInstanceId);
        DateTime now = (_configuration.TrackLeaks
            || _configuration.DiagnosticsLevel >= DiagnosticsLevel.Full)
            ? DateTime.UtcNow : default;

        TrackInstanceInfo(instance, now, id);

        if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full)
        {
            if (_objectInfo.TryGetValue(id, out var info))
                _handler.OnCreate?.Invoke(info);
        }

        TrackRentOperation(instance, now, id);

        Interlocked.Increment(ref _count);
        Interlocked.Increment(ref _rented);

        if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Basic)
        {
            RecordPeakUtilization();
            if (Volatile.Read(ref _rented) > _configuration.MaximumCapacity)
                Interlocked.Increment(ref _metrics.ExpansionCount);
        }

        if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full && sw is not null)
        {
            sw.Stop();
            RecordRentTime(sw.Elapsed);
        }

        _handler.OnRent?.Invoke(instance);
        return instance;
    }

    private void TrackInstanceInfo(T instance, DateTime now, long id)
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

    private void TrackRentOperation(T instance, DateTime now, long id)
    {
        if (_configuration.TrackLeaks)
        {
            long rentId = Interlocked.Increment(ref _lastRentId);
            _rentTracking[rentId] = new RentOperation(
                instance, now, rentId, Environment.CurrentManagedThreadId, id);
            _rentReverseLookup.AddOrUpdate(instance, new RentTrackingInfo
            {
                RentId = rentId,
                RentTime = now,
                InstanceId = id,
            });
        }
    }

    private void PreAllocate(int count)
    {
        for (int i = 0; i < count; i++)
        {
            T instance = _factory();
            long id = Interlocked.Increment(ref _nextInstanceId);
            DateTime now = DateTime.UtcNow;

            Interlocked.Increment(ref _metrics.TotalCreated);
            TrackInstanceInfo(instance, now, id);

            if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full)
            {
                if (_objectInfo.TryGetValue(id, out var info))
                    _handler.OnCreate?.Invoke(info);
            }

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
        if (_configuration.DiagnosticsLevel < DiagnosticsLevel.Full)
            return;

        Interlocked.Add(ref _metrics.TotalRentDurationTicks, elapsed.Ticks);

        lock (_syncLock)
        {
            if (_configuration.MetricsSamplingRate >= MetricsSamplingRate.Medium)
            {
                _metrics.RentDurationsMs.Add(elapsed.TotalMilliseconds);
                if (_metrics.RentDurationsMs.Count > 1000)
                    _metrics.RentDurationsMs.RemoveRange(0, 500);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordRentDuration(T item, TimeSpan duration)
    {
        if (_configuration.DiagnosticsLevel < DiagnosticsLevel.Full)
            return;

        Interlocked.Add(ref _metrics.TotalRentDurationTicks, duration.Ticks);

        lock (_syncLock)
        {
            if (_configuration.MetricsSamplingRate >= MetricsSamplingRate.Medium)
            {
                _metrics.RentDurationsMs.Add(duration.TotalMilliseconds);
                if (_metrics.RentDurationsMs.Count > 1000)
                    _metrics.RentDurationsMs.RemoveRange(0, 500);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordExhaustion()
    {
        Interlocked.Increment(ref _metrics.ExhaustionCount);
        RecordEvent("Pool exhaustion occurred");
    }

    private void RecordEvent(string message)
    {
        if (_configuration.DiagnosticsLevel < DiagnosticsLevel.Basic)
            return;

        lock (_syncLock)
        {
            _metrics.Events.Add(new KeyValuePair<DateTime, string>(DateTime.UtcNow, message));
            if (_metrics.Events.Count > 100)
                _metrics.Events.RemoveRange(0, 50);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateConfiguration(in PoolConfiguration configuration)
    {
        if (configuration.InitialCapacity < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(configuration.InitialCapacity),
                "InitialCapacity must be non-negative.");

        if (configuration.MaximumCapacity < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(configuration.MaximumCapacity),
                "MaximumCapacity must be non-negative.");

        if (configuration.MaximumCapacity > 0 && configuration.InitialCapacity > configuration.MaximumCapacity)
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(configuration.InitialCapacity),
                "InitialCapacity must not exceed MaximumCapacity.");

        if (configuration.TargetUtilizationPercentage < 0 || configuration.TargetUtilizationPercentage > 100)
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(configuration.TargetUtilizationPercentage),
                "TargetUtilizationPercentage must be between 0 and 100.");

        if (configuration.ScavengeIntervalMs < 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(configuration.ScavengeIntervalMs),
                "ScavengeIntervalMs must be non-negative.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateStrategy(PoolingStrategy strategy)
    {
        switch (strategy)
        {
            case PoolingStrategy.CentralizedQueue:
            case PoolingStrategy.ThreadLocal:
            case PoolingStrategy.ThreadLocalWithPartitioning:
            case PoolingStrategy.ShardedByThread:
            case PoolingStrategy.BoundedChannel:
            case PoolingStrategy.LockFree:
                return;
            default:
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(strategy),
                    $"Unsupported pooling strategy: {strategy}. Supported strategies are: " +
                    "CentralizedQueue, ThreadLocal, ThreadLocalWithPartitioning, ShardedByThread, BoundedChannel, LockFree.");
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            ThrowHelper.ThrowObjectDisposedException(nameof(ObjectPool<T>));
    }

    // ─── Dispose ─────────────────────────────────────────────────

    /// <summary>
    /// Releases all resources used by the pool, disposing all available items and stopping background timers.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        _scavengeTimer?.Dispose();

        ClearCore();

        _asyncLock.Dispose();
        _threadLocalStorage?.Dispose();

        RecordEvent("Pool disposed");
    }

    // ─── GetMetrics ──────────────────────────────────────────────

    /// <summary>
    /// Returns a snapshot of the current pool performance metrics including throughput, utilization, and latency percentiles.
    /// </summary>
    /// <returns>A <see cref="PoolMetrics"/> struct containing the current metrics snapshot.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
    public PoolMetrics GetMetrics()
    {
        ThrowIfDisposed();

        (double avgRentDuration, double p95RentDuration, double p99RentDuration) =
            CalculateRentStatistics();

        int rented = Volatile.Read(ref _rented);
        double utilization = rented > 0 ? (double)rented / Math.Max(1, Count) * 100 : 0;
        double memoryUsage = CalculateMemoryUsage();

        return new PoolMetrics
        {
            TotalCreated = Interlocked.Read(ref _metrics.TotalCreated),
            TotalRented = Interlocked.Read(ref _metrics.TotalRented),
            TotalReturned = Interlocked.Read(ref _metrics.TotalReturned),
            TotalDestroyed = Interlocked.Read(ref _metrics.TotalDestroyed),
            CurrentCount = Count,
            AvailableCount = Available,
            RentedCount = rented,
            MaximumCapacity = _configuration.MaximumCapacity,
            AverageRentDurationMs = avgRentDuration,
            AverageCreateDurationMs = CalculateAverageCreateDuration(),
            AverageResetDurationMs = CalculateAverageResetDuration(),
            P95RentDurationMs = p95RentDuration,
            P99RentDurationMs = p99RentDuration,
            HitRatio = Interlocked.Read(ref _metrics.TotalRented) > 0
                ? 1.0 - (double)Interlocked.Read(ref _metrics.TotalCreated) / Interlocked.Read(ref _metrics.TotalRented)
                : 0,
            Utilization = utilization,
            TurnoverRate = CalculateTurnoverRate(),
            GrowthRate = CalculateGrowthRate(),
            MemoryUsageBytes = memoryUsage,
            MemoryPressureLevel = CalculateMemoryPressure(memoryUsage),
            AverageThroughputPerSecond = _uptime.Elapsed.TotalSeconds > 0
                ? Interlocked.Read(ref _metrics.TotalRented) / _uptime.Elapsed.TotalSeconds
                : 0,
            CurrentThroughputPerSecond = _uptime.Elapsed.TotalSeconds > 0
                ? Interlocked.Read(ref _metrics.TotalReturned) / _uptime.Elapsed.TotalSeconds
                : 0,
            PeakUtilization = _metrics.PeakUtilization,
            PeakTime = _metrics.PeakTime,
            LastResetTime = _metrics.LastResetTime,
            LastScavengeTime = _metrics.LastScavengeTime,
        };
    }

    // ─── IObjectPool<T> — remaining methods ──────────────────────

    /// <summary>
    /// Asynchronously rents an item from the pool, awaiting availability when using bounded-channel strategy.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask{T}"/> representing the asynchronous rent operation, yielding the pooled instance.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the pool is exhausted and <see cref="PoolConfiguration.ThrowOnExhaustion"/> is enabled.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
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

    /// <summary>
    /// Asynchronously returns a previously rented item to the pool, invoking async reset and validation callbacks.
    /// </summary>
    /// <param name="item">The item to return to the pool.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous return operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
    public async ValueTask ReturnAsync(T item, CancellationToken cancellationToken = default)
    {
        if (item is null)
            ThrowHelper.ThrowArgumentNullException(nameof(item));

        ThrowIfDisposed();

        DateTime now = DateTime.UtcNow;
        long instanceId = 0L;
        TimeSpan rentDuration = TimeSpan.Zero;

        if (_configuration.TrackLeaks)
        {
            if (_rentReverseLookup.TryGetValue(item, out var trackingInfo))
            {
                instanceId = trackingInfo.InstanceId;
                rentDuration = now - trackingInfo.RentTime;
                _rentTracking.TryRemove(trackingInfo.RentId, out _);
                _rentReverseLookup.Remove(item);
            }
        }

        Stopwatch? resetSw = null;
        if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full)
        {
            resetSw = Stopwatch.StartNew();
        }

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
        catch (Exception ex)
        {
            Interlocked.Increment(ref _metrics.RecycleFailureCount);
            _metrics.LastException = ex;
            _handler.OnDispose?.Invoke(item);
            Interlocked.Decrement(ref _count);
            Interlocked.Decrement(ref _rented);
            return;
        }
        finally
        {
            if (resetSw is not null)
            {
                resetSw.Stop();
                RecordResetTime(resetSw.Elapsed);
            }
        }

        Interlocked.Decrement(ref _rented);
        Interlocked.Increment(ref _metrics.TotalReturned);

        if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full && instanceId != 0L)
        {
            if (_objectInfo.TryGetValue(instanceId, out var info))
            {
                _objectInfo[instanceId] = new PoolObjectInfo<T>
                {
                    Instance = info.Instance,
                    CreationTime = info.CreationTime,
                    Generation = info.Generation,
                    RentCount = info.RentCount + 1,
                    TotalRentTime = info.TotalRentTime + rentDuration,
                    IsPoolable = info.IsPoolable,
                    IsActive = false,
                    Id = info.Id,
                };
            }
        }

        if (_handler.Validator is not null && !_handler.Validator(item))
        {
            Interlocked.Increment(ref _metrics.FailedValidationCount);
            try
            {
                _handler.OnDispose?.Invoke(item);
                if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full)
                {
                    _handler.OnDestroy?.Invoke(new PoolObjectInfo<T>
                    {
                        Instance = item, CreationTime = now, Generation = _generation,
                        RentCount = 0, TotalRentTime = TimeSpan.Zero,
                        IsPoolable = item is IPoolable, IsActive = false, Id = instanceId,
                    });
                    _objectInfo.TryRemove(instanceId, out _);
                }
            }
            catch { Interlocked.Increment(ref _metrics.DisposeFailureCount); }
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
            try
            {
                if (_channel.Writer.TryWrite(pooledItem))
                    return;

                await _channel.Writer.WriteAsync(pooledItem, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch
            {
                // Fall through to central queue
            }
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
                if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full)
                {
                    _handler.OnDestroy?.Invoke(new PoolObjectInfo<T>
                    {
                        Instance = item, CreationTime = now, Generation = _generation,
                        RentCount = 0, TotalRentTime = TimeSpan.Zero,
                        IsPoolable = item is IPoolable, IsActive = false, Id = instanceId,
                    });
                    _objectInfo.TryRemove(instanceId, out _);
                }
            }
            catch
            {
                Interlocked.Increment(ref _metrics.DisposeFailureCount);
            }
            Interlocked.Increment(ref _metrics.TotalDestroyed);
            Interlocked.Decrement(ref _count);
        }
    }

    /// <summary>
    /// Attempts to rent an item from the pool without blocking; returns <c>false</c> if the pool is exhausted and expansion is disabled.
    /// </summary>
    /// <param name="item">When this method returns <c>true</c>, contains the rented instance; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if an item was successfully rented; otherwise, <c>false</c>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
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

    /// <summary>
    /// Asynchronously attempts to rent an item from the pool without blocking; returns a failure tuple if the pool is exhausted.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask{T}"/> yielding a tuple indicating success and the rented item (or <c>null</c> on failure).</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
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

    /// <summary>
    /// Rents an item and wraps it in an <see cref="IPoolable{T}"/> RAII wrapper that automatically returns the item on disposal.
    /// </summary>
    /// <returns>An <see cref="IPoolable{T}"/> wrapper around the rented instance.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the pool is exhausted and <see cref="PoolConfiguration.ThrowOnExhaustion"/> is enabled.</exception>
    public IPoolable<T> GetPoolable()
    {
        var instance = Rent();
        var poolable = new Poolable<T>(instance, this);
        _activePoolables.TryAdd(poolable.PoolableId, 0);
        return poolable;
    }

    /// <summary>
    /// Asynchronously rents an item and wraps it in an <see cref="IPoolable{T}"/> RAII wrapper that automatically returns the item on disposal.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask{T}"/> yielding an <see cref="IPoolable{T}"/> wrapper around the rented instance.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public async ValueTask<IPoolable<T>> GetPoolableAsync(CancellationToken cancellationToken = default)
    {
        var instance = await RentAsync(cancellationToken).ConfigureAwait(false);
        var poolable = new Poolable<T>(instance, this);
        _activePoolables.TryAdd(poolable.PoolableId, 0);
        return poolable;
    }

    /// <summary>
    /// Returns a poolable wrapper's item back to the pool, identified by the poolable's unique ID.
    /// </summary>
    /// <param name="poolableId">The unique identifier of the poolable wrapper to return.</param>
    /// <param name="item">The item to return to the pool.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReturnPoolable(int poolableId, T item)
    {
        if (!_activePoolables.TryRemove(poolableId, out _))
            return;

        Return(item);
    }

    /// <summary>
    /// Asynchronously returns a poolable wrapper's item back to the pool, identified by the poolable's unique ID.
    /// </summary>
    /// <param name="poolableId">The unique identifier of the poolable wrapper to return.</param>
    /// <param name="item">The item to return to the pool.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous return operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask ReturnPoolableAsync(int poolableId, T item)
    {
        if (!_activePoolables.TryRemove(poolableId, out _))
            return ValueTask.CompletedTask;

        return ReturnAsync(item);
    }

    /// <summary>
    /// Removes all available items from the pool, invoking dispose callbacks for each evicted item.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
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
            DisposeWithCallbacks(item);
        }

        if (_threadLocalStorage is not null)
        {
            foreach (var localQueue in _threadLocalStorage.Values)
            {
                while (localQueue.TryDequeue(out var item))
                {
                    Interlocked.Decrement(ref _count);
                    DisposeWithCallbacks(item);
                }
            }
        }

        for (int i = 0; i < _shards.Length; i++)
        {
            while (_shards[i].TryDequeue(out var item))
            {
                Interlocked.Decrement(ref _count);
                DisposeWithCallbacks(item);
            }
        }

        RecordEvent("Pool cleared");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DisposeWithCallbacks(PooledItem item)
    {
        try
        {
            _handler.OnDispose?.Invoke(item.Instance);
            if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full)
            {
                _handler.OnDestroy?.Invoke(new PoolObjectInfo<T>
                {
                    Instance = item.Instance, CreationTime = item.Created,
                    Generation = item.Generation, RentCount = item.RentCount,
                    TotalRentTime = TimeSpan.Zero, IsPoolable = item.Instance is IPoolable,
                    IsActive = false, Id = item.Id,
                });
                _objectInfo.TryRemove(item.Id, out _);
            }
            Interlocked.Increment(ref _metrics.TotalDestroyed);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _metrics.DisposeFailureCount);
            _metrics.LastException = ex;
        }
    }

    /// <summary>
    /// Asynchronously removes all available items from the pool, invoking async dispose callbacks for each evicted item.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous clear operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
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

    /// <summary>
    /// Determines whether the specified item is currently managed by the pool (either available or rented).
    /// </summary>
    /// <param name="item">The item to locate in the pool.</param>
    /// <returns><c>true</c> if the item is found in the pool; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
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

        for (int i = 0; i < _shards.Length; i++)
        {
            if (_shards[i].ContainsInstance(item))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns a snapshot of the pool's diagnostic information including events, failure counts, and configuration changes.
    /// </summary>
    /// <returns>A <see cref="PoolDiagnostics"/> struct containing the current diagnostic snapshot.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
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

    /// <summary>
    /// Applies a new pool configuration at runtime, trimming or pre-allocating items as needed to match the new capacity.
    /// </summary>
    /// <param name="configuration">The new configuration to apply.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="configuration"/> contains invalid values.</exception>
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
        Thread.MemoryBarrier();

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

    /// <summary>
    /// Reduces the pool size by disposing idle items down to the specified target capacity or the configured utilization percentage.
    /// </summary>
    /// <param name="targetCapacity">The desired number of items to retain; -1 uses <see cref="PoolConfiguration.TargetUtilizationPercentage"/> to compute the target.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
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
            if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full)
            {
                _handler.OnDestroy?.Invoke(new PoolObjectInfo<T>
                {
                    Instance = item.Instance, CreationTime = item.Created,
                    Generation = item.Generation, RentCount = item.RentCount,
                    TotalRentTime = TimeSpan.Zero, IsPoolable = item.Instance is IPoolable,
                    IsActive = false, Id = item.Id,
                });
                _objectInfo.TryRemove(item.Id, out _);
            }
            Interlocked.Increment(ref _metrics.TotalDestroyed);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _metrics.DisposeFailureCount);
            _metrics.LastException = ex;
        }
    }

    /// <summary>
    /// Asynchronously reduces the pool size by disposing idle items down to the specified target capacity.
    /// </summary>
    /// <param name="targetCapacity">The desired number of items to retain; -1 uses <see cref="PoolConfiguration.TargetUtilizationPercentage"/> to compute the target.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous trim operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
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

    /// <summary>
    /// Resets all accumulated performance metrics, duration histograms, and custom metrics to their initial state.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
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
            _metrics.CustomMetrics.Clear();
            _metrics.LastResetTime = now;
            _metrics.PeakUtilization = Volatile.Read(ref _rented);
            _metrics.PeakTime = now;
        }

        RecordEvent("Metrics reset");
    }

    /// <summary>
    /// Records or updates a named custom metric value that will be included in diagnostic snapshots.
    /// </summary>
    /// <param name="name">The name of the custom metric.</param>
    /// <param name="value">The numeric value to record.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <c>null</c>.</exception>
    public void RecordCustomMetric(string name, double value)
    {
        ThrowIfDisposed();

        if (name is null)
            ThrowHelper.ThrowArgumentNullException(nameof(name));

        if (_configuration.DiagnosticsLevel < DiagnosticsLevel.Basic)
            return;

        lock (_syncLock)
        {
            _metrics.CustomMetrics[name] = value;
        }
    }

    /// <summary>
    /// Creates a <see cref="IPoolableFactory{T, TResult}"/> that wraps this pool and produces <typeparamref name="TResult"/> instances from pooled items.
    /// </summary>
    /// <typeparam name="TResult">The result type to create from pooled items (e.g., <see cref="IPoolable{T}"/> or <typeparamref name="T"/> itself).</typeparam>
    /// <returns>A new <see cref="IPoolableFactory{T, TResult}"/> bound to this pool.</returns>
    public IPoolableFactory<T, TResult> CreateFactory<TResult>()
    {
        return new PooledObjectFactory<T, TResult>(this);
    }

    /// <summary>
    /// Asynchronously releases all resources used by the pool, disposing all available items and stopping background timers.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        _scavengeTimer?.Dispose();

        await ClearAsyncCore(CancellationToken.None).ConfigureAwait(false);

        await _asyncLock.DisposeAsync().ConfigureAwait(false);

        _threadLocalStorage?.Dispose();

        RecordEvent("Pool disposed asynchronously");
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

        // Process central queue
        var tempItems = new ConcurrentQueue<PooledItem>();
        while (_items.TryDequeue(out var item))
        {
            if (item.Created < cutoffTime && item.Generation < _generation)
            {
                Interlocked.Decrement(ref _count);
                removed++;
                DisposeScavengedItem(item);
            }
            else
            {
                tempItems.Enqueue(item);
            }
        }
        while (tempItems.TryDequeue(out var item))
            _items.Enqueue(item);

        // Process thread-local storage
        if (_threadLocalStorage is not null)
        {
            foreach (var localQueue in _threadLocalStorage.Values)
            {
                var localTemp = new ConcurrentQueue<PooledItem>();
                while (localQueue.TryDequeue(out var item))
                {
                    if (item.Created < cutoffTime && item.Generation < _generation)
                    {
                        Interlocked.Decrement(ref _count);
                        removed++;
                        DisposeScavengedItem(item);
                    }
                    else
                    {
                        localTemp.Enqueue(item);
                    }
                }
                while (localTemp.TryDequeue(out var item))
                    localQueue.Enqueue(item);
            }
        }

        // Process shards
        for (int i = 0; i < _shards.Length; i++)
        {
            var shardTemp = new ConcurrentQueue<PooledItem>();
            while (_shards[i].TryDequeue(out var item))
            {
                if (item.Created < cutoffTime && item.Generation < _generation)
                {
                    Interlocked.Decrement(ref _count);
                    removed++;
                    DisposeScavengedItem(item);
                }
                else
                {
                    shardTemp.Enqueue(item);
                }
            }
            while (shardTemp.TryDequeue(out var item))
                _shards[i].Enqueue(item);
        }

        if (removed > 0)
            RecordEvent($"Scavenged {removed} idle items");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DisposeScavengedItem(PooledItem item)
    {
        try
        {
            _handler.OnDispose?.Invoke(item.Instance);
            if (_configuration.DiagnosticsLevel >= DiagnosticsLevel.Full)
            {
                _handler.OnDestroy?.Invoke(new PoolObjectInfo<T>
                {
                    Instance = item.Instance, CreationTime = item.Created,
                    Generation = item.Generation, RentCount = item.RentCount,
                    TotalRentTime = TimeSpan.Zero, IsPoolable = item.Instance is IPoolable,
                    IsActive = false, Id = item.Id,
                });
                _objectInfo.TryRemove(item.Id, out _);
            }
            Interlocked.Increment(ref _metrics.TotalDestroyed);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _metrics.DisposeFailureCount);
            _metrics.LastException = ex;
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

        int count = Count;
        if (count == 0) return 0;

        double estimatedPerItem = IntPtr.Size * 4 + 64;
        return estimatedPerItem * count;
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
    private void RecordPeakUtilization()
    {
        int currentRented = Volatile.Read(ref _rented);
        if (currentRented > _metrics.PeakUtilization)
        {
            lock (_syncLock)
            {
                if (currentRented > _metrics.PeakUtilization)
                {
                    _metrics.PeakUtilization = currentRented;
                    _metrics.PeakTime = DateTime.UtcNow;
                }
            }
        }
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

/// <summary>
/// A factory that creates <typeparamref name="TResult"/> wrappers from pooled items managed by an <see cref="IObjectPool{T}"/>.
/// </summary>
/// <typeparam name="T">The reference type of objects managed by the underlying pool.</typeparam>
/// <typeparam name="TResult">The result type produced by the factory (e.g., <see cref="IPoolable{T}"/> or <typeparamref name="T"/>).</typeparam>
[StructLayout(LayoutKind.Auto)]
public sealed class PooledObjectFactory<T, TResult> : IPoolableFactory<T, TResult> where T : class
{
    private readonly IObjectPool<T> _pool;

    /// <summary>
    /// Initializes a new instance of <see cref="PooledObjectFactory{T, TResult}"/> bound to the specified pool.
    /// </summary>
    /// <param name="pool">The object pool to rent items from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pool"/> is <c>null</c>.</exception>
    public PooledObjectFactory(IObjectPool<T> pool)
    {
        _pool = pool ?? ThrowHelper.ThrowArgumentNullException<IObjectPool<T>>(nameof(pool));
    }

    /// <summary>
    /// Creates a <typeparamref name="TResult"/> instance from the specified pooled item, wrapping it in an <see cref="IPoolable{T}"/> when applicable.
    /// </summary>
    /// <param name="item">The pooled item to wrap or cast.</param>
    /// <returns>A <typeparamref name="TResult"/> instance derived from <paramref name="item"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="item"/> cannot be converted to <typeparamref name="TResult"/>.</exception>
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
