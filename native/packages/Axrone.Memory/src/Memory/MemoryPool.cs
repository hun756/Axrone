namespace Axrone.Memory;

/// <summary>
/// Zero-allocation memory pool for buffer reuse via bucket-based pooling.
/// </summary>
/// <typeparam name="T">Unmanaged element type of the pooled buffers.</typeparam>
public sealed class MemoryPool<T> : IMemoryPool<T> where T : struct
{
    /// <summary>Shared singleton pool with default options.</summary>
    public static readonly MemoryPool<T> Shared = new();

    private readonly int[] _bucketSizes;
    private readonly int _defaultBufferSize;
    private readonly int _maxBufferSize;
    private readonly int _minBufferSize;
    private readonly ClearMode _clearMode;
    private readonly int _maxBuffersPerBucket;
    private readonly AllocationStrategy _strategy;
    private readonly int _chunkSize;
    private readonly MemoryPoolFlags _flags;
    private readonly bool _enableProfiling;
    private readonly long _maxTotalMemory;
    private readonly int _allocationQuota;
    private readonly float _overAllocationFactor;
    private readonly int _memoryGuardPadding;
    private readonly BufferBucket<T>?[] _bucketArray;
    private readonly ConcurrentDictionary<int, long>? _allocationFrequency;
    private readonly ConditionalWeakTable<Thread, ThreadLocalCache>? _threadLocalCaches;
    private readonly int _tlsCacheSize;
    private readonly Stopwatch _runtime = Stopwatch.StartNew();
    private readonly Timer? _trimTimer;
    private int _isDisposed;

    private long _totalRentedBuffers;
    private long _totalReturnedBuffers;
    private long _activeBuffers;
    private long _pooledBuffers;
    private long _hits;
    private long _misses;
    private long _totalAllocated;
    private long _maxMemory;
    private long _totalRentTimeNs;
    private long _totalReturnTimeNs;
    private long _totalRents;
    private long _totalReturns;
    private long _totalAllocations;
    private long _totalDeallocations;
    private long _totalAllocationSize;
    private int _largestAllocation;
    private int _smallestAllocation = int.MaxValue;
    private long _fragmentationBytes;

    /// <summary>Creates a pool with default options.</summary>
    public MemoryPool() : this(new MemoryPoolOptions<T>()) { }

    /// <summary>Creates a pool with the specified options.</summary>
    /// <param name="options">Configuration options for the pool.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public MemoryPool(MemoryPoolOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _maxBufferSize = Math.Max(16, options.MaxBufferSize);
        _minBufferSize = Math.Max(1, Math.Min(options.MinBufferSize, _maxBufferSize));
        _defaultBufferSize = Math.Max(_minBufferSize, Math.Min(options.DefaultBufferSize, _maxBufferSize));
        _clearMode = options.ClearMode;
        _maxBuffersPerBucket = Math.Max(1, options.MaxBuffersPerBucket);
        _strategy = options.AllocationStrategy;
        _chunkSize = Math.Max(16, options.ChunkSize);
        _flags = options.Flags;
        _enableProfiling = (_flags & MemoryPoolFlags.EnableProfiling) != 0;
        _maxTotalMemory = options.MaxTotalMemory;
        _allocationQuota = options.AllocationQuota;
        _overAllocationFactor = options.OverAllocationFactor;
        _memoryGuardPadding = options.MemoryGuardPadding;
        _bucketSizes = options.CustomBucketSizes.Count > 0
            ? options.CustomBucketSizes.Order().Distinct().ToArray()
            : GenerateBucketSizes();
        _bucketArray = new BufferBucket<T>?[_bucketSizes.Length];

        _tlsCacheSize = options.TlsCacheSize;
        if ((_flags & MemoryPoolFlags.UseTlsCache) != 0 && _tlsCacheSize > 0)
            _threadLocalCaches = new ConditionalWeakTable<Thread, ThreadLocalCache>();

        if ((_flags & (MemoryPoolFlags.EnableStatistics | MemoryPoolFlags.EnableProfiling)) != 0)
            _allocationFrequency = new ConcurrentDictionary<int, long>();

        if (options.TrimInterval > TimeSpan.Zero && options.AutoTrimPercentage > 0)
        {
            double trimPressure = options.AutoTrimPercentage / 100.0;
            _trimTimer = new Timer(
                _ =>
                {
                    if (Volatile.Read(ref _isDisposed) != 0) return;
                    Trim(trimPressure);
                },
                null,
                options.TrimInterval,
                options.TrimInterval);
        }

        if (options.PreallocationCount > 0)
            Preallocate(options.PreallocationCount);
    }

    /// <summary>Gets the maximum buffer size this pool will allocate.</summary>
    public int MaxBufferSize => _maxBufferSize;

    /// <summary>Gets the total number of bytes currently allocated by the pool (active + pooled).</summary>
    public long TotalAllocated => Interlocked.Read(ref _totalAllocated);

    /// <summary>Rents a buffer with at least <paramref name="minimumLength"/> elements.</summary>
    /// <param name="minimumLength">Minimum number of elements. Use -1 for the default buffer size.</param>
    /// <returns>An <see cref="IMemoryOwner{T}"/> wrapping the rented buffer.</returns>
    /// <exception cref="OutOfMemoryException">The requested size exceeds <see cref="MaxBufferSize"/>.</exception>
    /// <exception cref="ObjectDisposedException">The pool has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IMemoryOwner<T> Rent(int minimumLength = -1)
    {
        if (!TryRent(minimumLength, out var owner))
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
            MemoryPoolThrowHelper.ThrowRequestedSizeExceedsCapacity();
        }

        return owner;
    }

    /// <summary>Tries to rent a buffer with at least <paramref name="minimumLength"/> elements.</summary>
    /// <param name="minimumLength">Minimum number of elements. Use -1 for the default buffer size.</param>
    /// <param name="memoryOwner">When this method returns <c>true</c>, the rented buffer owner; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> if a buffer was successfully rented; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRent(int minimumLength, [NotNullWhen(true)] out IMemoryOwner<T>? memoryOwner)
    {
        long startTimestamp = 0;
        if (_enableProfiling)
            startTimestamp = Stopwatch.GetTimestamp();

        if (Volatile.Read(ref _isDisposed) != 0)
        {
            memoryOwner = null;
            return false;
        }

        int requestedSize = minimumLength <= 0 ? _defaultBufferSize : minimumLength;

        if (_overAllocationFactor > 1.0f)
        {
            long oversized = (long)(requestedSize * (double)_overAllocationFactor);
            requestedSize = oversized > int.MaxValue ? int.MaxValue : (int)oversized;
        }

        if (_allocationQuota > 0 && Interlocked.Read(ref _activeBuffers) >= _allocationQuota)
        {
            memoryOwner = null;
            return false;
        }

        if ((_flags & MemoryPoolFlags.UseTlsCache) != 0
            && TryRentFromTlsCache(requestedSize, out memoryOwner))
        {
            Interlocked.Increment(ref _totalRentedBuffers);
            Interlocked.Increment(ref _activeBuffers);
            Interlocked.Increment(ref _totalRents);
            if (_enableProfiling)
                Interlocked.Add(ref _totalRentTimeNs, Stopwatch.GetTimestamp() - startTimestamp);
            return true;
        }

        TrackAllocationSize(requestedSize);

        int bufferSize = GetOptimalBufferSize(requestedSize);

        if (bufferSize > _maxBufferSize)
        {
            memoryOwner = null;
            return false;
        }

        var bucket = GetOrCreateBucket(GetBucketIndex(bufferSize));

        if (bucket.TryTake(out var owner))
        {
            var buffer = (MemoryPoolBuffer)owner;
            Interlocked.Increment(ref _hits);
            Interlocked.Increment(ref _totalRentedBuffers);
            Interlocked.Increment(ref _activeBuffers);
            Interlocked.Decrement(ref _pooledBuffers);
            Interlocked.Increment(ref _totalRents);
            if (_enableProfiling)
                Interlocked.Add(ref _totalRentTimeNs, Stopwatch.GetTimestamp() - startTimestamp);

            if (_clearMode == ClearMode.OnRent || _clearMode == ClearMode.Always)
                buffer.Clear();

            buffer.Activate();
            memoryOwner = buffer;
            return true;
        }

        long allocBytes = (long)bufferSize * Unsafe.SizeOf<T>();
        {
            long currentTotal;
            do
            {
                currentTotal = Volatile.Read(ref _totalAllocated);
                if (_maxTotalMemory < long.MaxValue && currentTotal + allocBytes > _maxTotalMemory)
                {
                    memoryOwner = null;
                    return false;
                }
            } while (Interlocked.CompareExchange(ref _totalAllocated, currentTotal + allocBytes, currentTotal) != currentTotal);
        }

        Interlocked.Increment(ref _misses);
        Interlocked.Increment(ref _totalRentedBuffers);
        Interlocked.Increment(ref _activeBuffers);
        Interlocked.Increment(ref _totalAllocations);
        Interlocked.Add(ref _totalAllocationSize, bufferSize);

        T[] array;
        try
        {
            array = (_flags & MemoryPoolFlags.PinBuffers) != 0
                ? GC.AllocateUninitializedArray<T>(bufferSize, pinned: true)
                : GC.AllocateUninitializedArray<T>(bufferSize);
        }
        catch
        {
            Interlocked.Decrement(ref _activeBuffers);
            Interlocked.Decrement(ref _totalRentedBuffers);
            Interlocked.Decrement(ref _totalAllocations);
            Interlocked.Add(ref _totalAllocationSize, -bufferSize);
            Interlocked.Add(ref _totalAllocated, -allocBytes);
            throw;
        }

        var newBuffer = new MemoryPoolBuffer(this, array, bufferSize);
        UpdateMaxMemory(Interlocked.Read(ref _totalAllocated));

        if ((_flags & MemoryPoolFlags.ZeroMemory) != 0 || _clearMode == ClearMode.OnRent || _clearMode == ClearMode.Always)
            newBuffer.Clear();

        newBuffer.Activate();
        memoryOwner = newBuffer;
        Interlocked.Increment(ref _totalRents);
        if (_enableProfiling)
            Interlocked.Add(ref _totalRentTimeNs, Stopwatch.GetTimestamp() - startTimestamp);
        return true;
    }

    /// <summary>Disposes the pool and releases all pooled buffers.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        _trimTimer?.Dispose();

        long pooledBytes = 0;
        for (int i = 0; i < _bucketArray.Length; i++)
        {
            var bucket = _bucketArray[i];
            if (bucket is null) continue;
            pooledBytes += (long)bucket.Count * bucket.BufferSize * Unsafe.SizeOf<T>();
            bucket.Dispose();
        }

        Interlocked.Add(ref _totalAllocated, -pooledBytes);
        Array.Clear(_bucketArray);

        if (_threadLocalCaches != null)
        {
            _threadLocalCaches.Clear();
        }
    }

    private void Return(MemoryPoolBuffer buffer)
    {
        long startTimestamp = 0;
        if (_enableProfiling)
            startTimestamp = Stopwatch.GetTimestamp();

        if (_clearMode == ClearMode.OnReturn || _clearMode == ClearMode.Always)
            buffer.Clear();

        if (Volatile.Read(ref _isDisposed) != 0)
            return;

        int size = buffer.Length;

        if ((_flags & MemoryPoolFlags.UseTlsCache) != 0 && TryReturnToTlsCache(buffer))
        {
            Interlocked.Increment(ref _totalReturnedBuffers);
            Interlocked.Increment(ref _totalReturns);
            Interlocked.Decrement(ref _activeBuffers);
            if (_enableProfiling)
                Interlocked.Add(ref _totalReturnTimeNs, Stopwatch.GetTimestamp() - startTimestamp);
            return;
        }

        var bucket = GetOrCreateBucket(GetBucketIndex(size));

        if (bucket.TryAdd(buffer))
            Interlocked.Increment(ref _pooledBuffers);

        Interlocked.Increment(ref _totalReturnedBuffers);
        Interlocked.Increment(ref _totalReturns);
        Interlocked.Decrement(ref _activeBuffers);
        if (_enableProfiling)
            Interlocked.Add(ref _totalReturnTimeNs, Stopwatch.GetTimestamp() - startTimestamp);
    }

    private bool TryRentFromTlsCache(int requestedSize, [NotNullWhen(true)] out IMemoryOwner<T>? memoryOwner)
    {
        if (_threadLocalCaches == null || _tlsCacheSize <= 0)
        {
            memoryOwner = null;
            return false;
        }

        var currentThread = Thread.CurrentThread;

        var cache = _threadLocalCaches!.GetValue(currentThread, t =>
            new ThreadLocalCache(_tlsCacheSize, t));

        lock (cache.Lock)
        {
            for (int i = 0; i < cache.Sizes.Length; i++)
            {
                int size = cache.Sizes[i];
                if (size >= requestedSize && size <= requestedSize * 2 && cache.Buffers[i] != null)
                {
                    var buffer = cache.Buffers[i];
                    cache.Buffers[i] = null!;
                    cache.Sizes[i] = -1;

                    Interlocked.Increment(ref _hits);
                    memoryOwner = buffer;
                    return true;
                }
            }
        }

        memoryOwner = null;
        return false;
    }

    private bool TryReturnToTlsCache(MemoryPoolBuffer buffer)
    {
        if (_threadLocalCaches == null || _tlsCacheSize <= 0)
            return false;

        var currentThread = Thread.CurrentThread;

        if (!_threadLocalCaches!.TryGetValue(currentThread, out var cache))
            return false;

        lock (cache.Lock)
        {
            for (int i = 0; i < cache.Sizes.Length; i++)
            {
                if (cache.Sizes[i] == -1)
                {
                    cache.Buffers[i] = buffer;
                    cache.Sizes[i] = buffer.Length;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tries to rent a buffer with exactly <paramref name="exactLength"/> elements.</summary>
    /// <param name="exactLength">Exact number of elements required.</param>
    /// <param name="memoryOwner">When this method returns <c>true</c>, the rented buffer owner; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> if a buffer of the exact size was successfully rented; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRentExact(int exactLength, [NotNullWhen(true)] out IMemoryOwner<T>? memoryOwner)
    {
        if (Volatile.Read(ref _isDisposed) != 0 || exactLength <= 0)
        {
            memoryOwner = null;
            return false;
        }

        int bufferSize = GetOptimalBufferSize(exactLength);

        if (bufferSize != exactLength || bufferSize > _maxBufferSize)
        {
            memoryOwner = null;
            return false;
        }

        return TryRent(exactLength, out memoryOwner);
    }

    /// <summary>Rents multiple buffers at once.</summary>
    /// <param name="count">Number of buffers to rent.</param>
    /// <param name="minimumLength">Minimum number of elements per buffer.</param>
    /// <returns>An array of rented buffer owners.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than or equal to zero.</exception>
    public IMemoryOwner<T>[] RentMultiple(int count, int minimumLength = -1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        var owners = new IMemoryOwner<T>[count];
        int rented = 0;

        try
        {
            for (int i = 0; i < count; i++)
            {
                owners[i] = Rent(minimumLength);
                rented++;
            }
        }
        catch
        {
            for (int i = 0; i < rented; i++)
                owners[i].Dispose();
            throw;
        }

        return owners;
    }

    /// <summary>Returns multiple buffers to the pool at once.</summary>
    /// <param name="buffers">The buffers to return.</param>
    /// <returns><c>true</c> if all buffers were successfully returned; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="buffers"/> is null.</exception>
    public bool ReturnMultiple(IMemoryOwner<T>[] buffers)
    {
        ArgumentNullException.ThrowIfNull(buffers);

        bool allReturned = true;
        for (int i = 0; i < buffers.Length; i++)
        {
            try
            {
                buffers[i]?.Dispose();
            }
            catch
            {
                allReturned = false;
            }
        }

        return allReturned;
    }

    /// <summary>Gets a snapshot of the pool's performance metrics.</summary>
    /// <returns>A <see cref="MemoryPoolMetrics"/> snapshot with current counter values.</returns>
    public MemoryPoolMetrics GetMetrics()
    {
        long hits = Interlocked.Read(ref _hits);
        long misses = Interlocked.Read(ref _misses);
        long total = hits + misses;
        long totalRents = Interlocked.Read(ref _totalRents);
        long totalReturns = Interlocked.Read(ref _totalReturns);
        long totalAllocations = Interlocked.Read(ref _totalAllocations);
        long totalAllocationSize = Interlocked.Read(ref _totalAllocationSize);

        return new MemoryPoolMetrics(
            totalAllocatedBytes: Interlocked.Read(ref _totalAllocated),
            totalRentedBuffers: Interlocked.Read(ref _totalRentedBuffers),
            totalReturnedBuffers: Interlocked.Read(ref _totalReturnedBuffers),
            activeBuffers: Interlocked.Read(ref _activeBuffers),
            pooledBuffers: Interlocked.Read(ref _pooledBuffers),
            misses: misses,
            hits: hits,
            bucketCount: CountActiveBuckets(),
            fragmentationBytes: Interlocked.Read(ref _fragmentationBytes),
            elapsedTime: _runtime.Elapsed,
            averageRentTime: totalRents > 0 ? (double)Interlocked.Read(ref _totalRentTimeNs) / totalRents : 0,
            averageReturnTime: totalReturns > 0 ? (double)Interlocked.Read(ref _totalReturnTimeNs) / totalReturns : 0,
            totalAllocations: totalAllocations,
            totalDeallocations: Interlocked.Read(ref _totalDeallocations),
            hitRatio: total > 0 ? (float)hits / total : 0f,
            bytesPerAllocation: totalAllocations > 0 ? (int)(totalAllocationSize / totalAllocations) : 0);
    }

    /// <summary>Resets all statistics counters to zero.</summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _totalRentedBuffers, 0);
        Interlocked.Exchange(ref _totalReturnedBuffers, 0);
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
        Interlocked.Exchange(ref _totalRentTimeNs, 0);
        Interlocked.Exchange(ref _totalReturnTimeNs, 0);
        Interlocked.Exchange(ref _totalRents, 0);
        Interlocked.Exchange(ref _totalReturns, 0);
        Interlocked.Exchange(ref _totalAllocations, 0);
        Interlocked.Exchange(ref _totalDeallocations, 0);
        Interlocked.Exchange(ref _totalAllocationSize, 0);
        Interlocked.Exchange(ref _largestAllocation, 0);
        Interlocked.Exchange(ref _smallestAllocation, int.MaxValue);
        Interlocked.Exchange(ref _fragmentationBytes, 0);
        _allocationFrequency?.Clear();

        for (int i = 0; i < _bucketArray.Length; i++)
            _bucketArray[i]?.ResetStatistics();
    }

    /// <summary>Removes a fraction of idle buffers from the pool.</summary>
    /// <param name="pressure">Fraction to remove: 0.0 removes none, 1.0 removes all.</param>
    /// <exception cref="ObjectDisposedException">The pool has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pressure"/> is outside [0.0, 1.0].</exception>
    public void Trim(double pressure = 0.5)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        if (pressure < 0.0 || pressure > 1.0)
            MemoryPoolThrowHelper.ThrowPressureOutOfRange(pressure);

        if (pressure == 0.0)
            return;

        long removedBytes = 0;
        long removedCount = 0;

        foreach (var bucket in _bucketArray)
        {
            if (bucket is null) continue;
            int countBefore = bucket.Count;
            bucket.Trim((float)pressure);
            int countAfter = bucket.Count;
            int removed = countBefore - countAfter;
            removedCount += removed;
            removedBytes += (long)removed * bucket.BufferSize * Unsafe.SizeOf<T>();
        }

        Interlocked.Add(ref _pooledBuffers, -removedCount);
        Interlocked.Add(ref _totalAllocated, -removedBytes);
        Interlocked.Add(ref _totalDeallocations, removedCount);
    }

    /// <summary>Gets a comprehensive diagnostic snapshot of the pool's internal state.</summary>
    /// <returns>A <see cref="MemoryPoolDiagnostics"/> snapshot.</returns>
    public MemoryPoolDiagnostics GetDiagnostics()
    {
        long totalMemory = Interlocked.Read(ref _totalAllocated);
        long maxMemory = Interlocked.Read(ref _maxMemory);

        long wastedMemory = 0;
        var bucketInfo = new Dictionary<int, BucketDiagnostics>(CountActiveBuckets());
        for (int i = 0; i < _bucketArray.Length; i++)
        {
            var bucket = _bucketArray[i];
            if (bucket is null) continue;
            int count = bucket.Count;
            if (count > 0)
                wastedMemory += (long)count * bucket.BufferSize * Unsafe.SizeOf<T>();
            bucketInfo[_bucketSizes[i]] = bucket.GetDiagnostics();
        }

        long totalAllocations = Interlocked.Read(ref _totalAllocations);
        long totalAllocationSize = Interlocked.Read(ref _totalAllocationSize);
        int largest = Volatile.Read(ref _largestAllocation);
        int smallest = Volatile.Read(ref _smallestAllocation);
        float avgSize = totalAllocations > 0 ? (float)((double)totalAllocationSize / totalAllocations) : 0;
        int mostFrequent = CalculateMostFrequentAllocation();
        float variance = CalculateAllocationVariance(avgSize);

        return new MemoryPoolDiagnostics(
            bucketInfo: bucketInfo,
            totalMemory: totalMemory,
            maxMemory: maxMemory,
            wastedMemory: Math.Max(0, wastedMemory),
            runtime: _runtime.Elapsed,
            largestAllocation: largest,
            smallestAllocation: smallest == int.MaxValue ? 0 : smallest,
            mostFrequentAllocation: mostFrequent,
            averageAllocationSize: avgSize,
            allocationVariance: variance);
    }

    /// <summary>Gets per-bucket diagnostic information.</summary>
    /// <returns>A collection of <see cref="BucketDiagnostics"/> keyed by buffer size.</returns>
    public IReadOnlyDictionary<int, BucketDiagnostics> GetBucketDiagnostics()
    {
        var result = new Dictionary<int, BucketDiagnostics>(CountActiveBuckets());

        for (int i = 0; i < _bucketArray.Length; i++)
        {
            var bucket = _bucketArray[i];
            if (bucket is null) continue;
            result[_bucketSizes[i]] = bucket.GetDiagnostics();
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrackAllocationSize(int size)
    {
        if (_allocationFrequency != null)
            _allocationFrequency.AddOrUpdate(size, 1, (_, count) => count + 1);

        int currentLargest = Volatile.Read(ref _largestAllocation);
        while (size > currentLargest)
        {
            if (Interlocked.CompareExchange(ref _largestAllocation, size, currentLargest) == currentLargest)
                break;
            currentLargest = Volatile.Read(ref _largestAllocation);
        }

        int currentSmallest = Volatile.Read(ref _smallestAllocation);
        while (size < currentSmallest)
        {
            if (Interlocked.CompareExchange(ref _smallestAllocation, size, currentSmallest) == currentSmallest)
                break;
            currentSmallest = Volatile.Read(ref _smallestAllocation);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetBucketIndex(int bufferSize)
    {
        int index = Array.BinarySearch(_bucketSizes, bufferSize);
        return index >= 0 ? index : ~index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BufferBucket<T> GetOrCreateBucket(int index)
    {
        var bucket = Volatile.Read(ref _bucketArray[index]);
        if (bucket is not null)
            return bucket;

        int size = _bucketSizes[index];
        var newBucket = new BufferBucket<T>(size, _maxBuffersPerBucket, _enableProfiling, false);
        bucket = Interlocked.CompareExchange(ref _bucketArray[index], newBucket, null);
        return bucket ?? newBucket;
    }

    private int CountActiveBuckets()
    {
        int count = 0;
        for (int i = 0; i < _bucketArray.Length; i++)
            if (_bucketArray[i] is not null) count++;
        return count;
    }

    private int CalculateMostFrequentAllocation()
    {
        if (_allocationFrequency == null || _allocationFrequency.IsEmpty)
            return 0;

        int maxSize = 0;
        long maxCount = 0;
        foreach (var kvp in _allocationFrequency)
        {
            if (kvp.Value > maxCount)
            {
                maxCount = kvp.Value;
                maxSize = kvp.Key;
            }
        }

        return maxSize;
    }

    private float CalculateAllocationVariance(float mean)
    {
        if (_allocationFrequency == null || _allocationFrequency.IsEmpty || mean == 0)
            return 0;

        double sumSquaredDiff = 0;
        long totalCount = 0;
        foreach (var kvp in _allocationFrequency)
        {
            double diff = kvp.Key - mean;
            sumSquaredDiff += diff * diff * kvp.Value;
            totalCount += kvp.Value;
        }

        return totalCount > 0 ? (float)(sumSquaredDiff / totalCount) : 0;
    }

    private void Preallocate(int count)
    {
        int bufferSize = _minBufferSize;

        for (int i = 0; i < count; i++)
        {
            var array = GC.AllocateUninitializedArray<T>(bufferSize);
            var buffer = new MemoryPoolBuffer(this, array, bufferSize);
            Interlocked.Add(ref _totalAllocated, (long)bufferSize * Unsafe.SizeOf<T>());

            var bucket = GetOrCreateBucket(GetBucketIndex(bufferSize));
            if (bucket.TryAdd(buffer))
                Interlocked.Increment(ref _pooledBuffers);
        }

        UpdateMaxMemory(Interlocked.Read(ref _totalAllocated));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateMaxMemory(long currentTotal)
    {
        long currentMax;
        do
        {
            currentMax = Volatile.Read(ref _maxMemory);
            if (currentTotal <= currentMax)
                return;
        } while (Interlocked.CompareExchange(ref _maxMemory, currentTotal, currentMax) != currentMax);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetOptimalBufferSize(int requestedSize)
    {
        if (requestedSize <= _minBufferSize)
            return _minBufferSize;

        int index = Array.BinarySearch(_bucketSizes, requestedSize);
        if (index >= 0)
            return _bucketSizes[index];

        int insertionPoint = ~index;
        return insertionPoint < _bucketSizes.Length
            ? _bucketSizes[insertionPoint]
            : GetNextPowerOfTwo(requestedSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetNextPowerOfTwo(int value)
    {
        if (value <= 0) return 1;
        if (BitOperations.IsPow2(value)) return value;
        uint result = BitOperations.RoundUpToPowerOf2((uint)value);
        return result > (uint)int.MaxValue ? int.MaxValue : (int)result;
    }

    private int[] GenerateBucketSizes()
    {
        var sizes = new List<int>();

        switch (_strategy)
        {
            case AllocationStrategy.PowerOfTwo:
                for (int size = _minBufferSize; size <= _maxBufferSize; size *= 2)
                {
                    sizes.Add(size);
                    if (size > _maxBufferSize / 2) break;
                }
                break;

            case AllocationStrategy.Fibonacci:
                int a = _minBufferSize, b = _minBufferSize;
                sizes.Add(a);
                while (b <= _maxBufferSize)
                {
                    long nextLong = (long)a + b;
                    if (nextLong > int.MaxValue) break;
                    int next = (int)nextLong;
                    a = b;
                    b = next;
                    if (b > _maxBufferSize) break;
                    sizes.Add(b);
                }
                break;

            case AllocationStrategy.Chunked:
                int chunkStart = Math.Max(_minBufferSize, _chunkSize);
                for (int size = chunkStart; size <= _maxBufferSize; size += _chunkSize)
                {
                    sizes.Add(size);
                }
                if (sizes.Count == 0 || sizes[0] != _minBufferSize)
                    sizes.Insert(0, _minBufferSize);
                break;

            case AllocationStrategy.Exact:
                sizes.Add(_minBufferSize);
                if (_defaultBufferSize != _minBufferSize && _defaultBufferSize != _maxBufferSize)
                    sizes.Add(_defaultBufferSize);
                if (_maxBufferSize != _minBufferSize)
                    sizes.Add(_maxBufferSize);
                break;
        }

        sizes.Sort();
        return sizes.Distinct().ToArray();
    }

    private sealed class ThreadLocalCache
    {
        public readonly MemoryPoolBuffer[] Buffers;
        public readonly int[] Sizes;
        public readonly Thread Thread;
        public readonly object Lock;

        public ThreadLocalCache(int capacity, Thread thread)
        {
            Buffers = new MemoryPoolBuffer[capacity];
            Sizes = new int[capacity];
            Thread = thread;
            Lock = new object();
            Array.Fill(Sizes, -1);
        }
    }

    private sealed class MemoryPoolBuffer : IMemoryOwner<T>
    {
        private readonly MemoryPool<T> _pool;
        private readonly T[] _array;
        private readonly int _length;
        private int _active;
        private int _referenceCount;

        internal MemoryPoolBuffer(MemoryPool<T> pool, T[] array, int length)
        {
            _pool = pool;
            _array = array;
            _length = length;
            _referenceCount = 1;
        }

        public Memory<T> Memory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _active) != 1, nameof(IMemoryOwner<T>));
                return _array.AsMemory(0, _length);
            }
        }

        public Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _active) != 1, nameof(IMemoryOwner<T>));
                return _array.AsSpan(0, _length);
            }
        }

        public ReadOnlyMemory<T> ReadOnlyMemory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _active) != 1, nameof(IMemoryOwner<T>));
                return _array.AsMemory(0, _length);
            }
        }

        public ReadOnlySpan<T> ReadOnlySpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _active) != 1, nameof(IMemoryOwner<T>));
                return _array.AsSpan(0, _length);
            }
        }

        public int ReferenceCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _referenceCount);
        }

        public int Length => _length;

        public MemoryHandle Pin(int elementIndex = 0)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _active) != 1, nameof(IMemoryOwner<T>));
            if ((uint)elementIndex >= (uint)_length)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));
            return _array.AsMemory(elementIndex, _length - elementIndex).Pin();
        }

        public bool TryGetArray(out ArraySegment<T> segment)
        {
            if (Volatile.Read(ref _active) != 1)
            {
                segment = default;
                return false;
            }
            segment = new ArraySegment<T>(_array, 0, _length);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Clear() => Array.Clear(_array, 0, _length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Activate()
        {
            Interlocked.Exchange(ref _referenceCount, 1);
            Interlocked.Exchange(ref _active, 1);
        }

        public IMemoryOwner<T> Slice(int start, int length)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _active) != 1, nameof(IMemoryOwner<T>));
            if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
                throw new ArgumentOutOfRangeException(nameof(start));
            Interlocked.Increment(ref _referenceCount);
            return new SlicedMemoryOwner(this, start, length);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _active, 0) != 1)
                return;

            if (Interlocked.Decrement(ref _referenceCount) > 0)
                return;

            _pool.Return(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReleaseSlice()
        {
            if (Interlocked.Decrement(ref _referenceCount) > 0)
                return;

            if (Volatile.Read(ref _active) == 0)
                _pool.Return(this);
        }

        private sealed class SlicedMemoryOwner : IMemoryOwner<T>
        {
            private readonly MemoryPoolBuffer _owner;
            private readonly int _start;
            private readonly int _length;
            private int _disposed;

            internal SlicedMemoryOwner(MemoryPoolBuffer owner, int start, int length)
            {
                _owner = owner;
                _start = start;
                _length = length;
            }

            public Memory<T> Memory
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _owner.Memory.Slice(_start, _length);
            }

            public Span<T> Span
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _owner.Span.Slice(_start, _length);
            }

            public ReadOnlyMemory<T> ReadOnlyMemory
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _owner.ReadOnlyMemory.Slice(_start, _length);
            }

            public ReadOnlySpan<T> ReadOnlySpan
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _owner.ReadOnlySpan.Slice(_start, _length);
            }

            public int ReferenceCount
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _owner.ReferenceCount;
            }

            public int Length => _length;

            public MemoryHandle Pin(int elementIndex = 0)
            {
                if ((uint)elementIndex >= (uint)_length)
                    throw new ArgumentOutOfRangeException(nameof(elementIndex));
                return _owner.Memory.Slice(_start + elementIndex, _length - elementIndex).Pin();
            }

            public bool TryGetArray(out ArraySegment<T> segment)
            {
                if (_owner.TryGetArray(out var parentSegment))
                {
                    segment = new ArraySegment<T>(parentSegment.Array!, parentSegment.Offset + _start, _length);
                    return true;
                }
                segment = default;
                return false;
            }

            public IMemoryOwner<T> Slice(int start, int length)
            {
                if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
                    throw new ArgumentOutOfRangeException(nameof(start));
                Interlocked.Increment(ref _owner._referenceCount);
                return new SlicedMemoryOwner(_owner, _start + start, length);
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    return;
                _owner.ReleaseSlice();
            }
        }
    }
}

internal static class MemoryPoolThrowHelper
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowRequestedSizeExceedsCapacity() =>
        throw new InvalidOperationException("Requested size exceeds pool capacity.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowPressureOutOfRange(double pressure) =>
        throw new ArgumentOutOfRangeException(nameof(pressure), pressure, "Pressure must be between 0.0 and 1.0.");
}
