namespace Axrone.Memory;

/// <summary>
/// Zero-allocation memory pool for buffer reuse via bucket-based pooling.
/// </summary>
/// <typeparam name="T">Element type of the pooled buffers.</typeparam>
public sealed class MemoryPool<T> : IDisposable
{
    private readonly int[] _bucketSizes;
    private readonly int _defaultBufferSize;
    private readonly int _maxBufferSize;
    private readonly int _minBufferSize;
    private readonly ClearMode _clearMode;
    private readonly int _maxBuffersPerBucket;
    private readonly AllocationStrategy _strategy;
    private readonly int _chunkSize;
    private readonly ConcurrentDictionary<int, ConcurrentQueue<MemoryPoolBuffer>> _buckets;
    private int _isDisposed;

    private long _totalRentedBuffers;
    private long _totalReturnedBuffers;
    private long _activeBuffers;
    private long _pooledBuffers;
    private long _hits;
    private long _misses;

    /// <summary>Creates a pool with default options.</summary>
    public MemoryPool() : this(new MemoryPoolOptions<T>()) { }

    /// <summary>Creates a pool with the specified options.</summary>
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
        _buckets = new ConcurrentDictionary<int, ConcurrentQueue<MemoryPoolBuffer>>();
        _bucketSizes = GenerateBucketSizes();
    }

    /// <summary>Gets the maximum buffer size this pool will allocate.</summary>
    public int MaxBufferSize => _maxBufferSize;

    /// <summary>Rents a buffer with at least <paramref name="minimumLength"/> elements.</summary>
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRent(int minimumLength, [NotNullWhen(true)] out IMemoryOwner<T>? memoryOwner)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            memoryOwner = null;
            return false;
        }

        int requestedSize = minimumLength <= 0 ? _defaultBufferSize : minimumLength;
        int bufferSize = GetOptimalBufferSize(requestedSize);

        if (bufferSize > _maxBufferSize)
        {
            memoryOwner = null;
            return false;
        }

        var bucket = _buckets.GetOrAdd(bufferSize, static _ => new ConcurrentQueue<MemoryPoolBuffer>());

        if (bucket.TryDequeue(out var buffer))
        {
            Interlocked.Increment(ref _hits);
            Interlocked.Increment(ref _totalRentedBuffers);
            Interlocked.Increment(ref _activeBuffers);
            Interlocked.Decrement(ref _pooledBuffers);

            if (_clearMode == ClearMode.OnRent || _clearMode == ClearMode.Always)
                buffer.Clear();

            buffer.Activate();
            memoryOwner = buffer;
            return true;
        }

        Interlocked.Increment(ref _misses);
        Interlocked.Increment(ref _totalRentedBuffers);
        Interlocked.Increment(ref _activeBuffers);

        var array = GC.AllocateUninitializedArray<T>(bufferSize);
        var newBuffer = new MemoryPoolBuffer(this, array, bufferSize);

        if (_clearMode == ClearMode.OnRent || _clearMode == ClearMode.Always)
            newBuffer.Clear();

        newBuffer.Activate();
        memoryOwner = newBuffer;
        return true;
    }

    /// <summary>Disposes the pool and releases all pooled buffers.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        foreach (var bucket in _buckets.Values)
        {
            while (bucket.TryDequeue(out _)) { }
        }

        _buckets.Clear();
    }

    private void Return(MemoryPoolBuffer buffer)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
            return;

        if (_clearMode == ClearMode.OnReturn || _clearMode == ClearMode.Always)
            buffer.Clear();

        int size = buffer.Length;
        var bucket = _buckets.GetOrAdd(size, static _ => new ConcurrentQueue<MemoryPoolBuffer>());

        if (bucket.Count < _maxBuffersPerBucket)
        {
            bucket.Enqueue(buffer);
            Interlocked.Increment(ref _pooledBuffers);
        }

        Interlocked.Increment(ref _totalReturnedBuffers);
        Interlocked.Decrement(ref _activeBuffers);
    }

    /// <summary>Gets a snapshot of the pool's performance metrics.</summary>
    public MemoryPoolMetrics GetMetrics()
    {
        return new MemoryPoolMetrics(
            Interlocked.Read(ref _totalRentedBuffers),
            Interlocked.Read(ref _totalReturnedBuffers),
            Interlocked.Read(ref _activeBuffers),
            Interlocked.Read(ref _pooledBuffers),
            Interlocked.Read(ref _hits),
            Interlocked.Read(ref _misses));
    }

    /// <summary>Resets all statistics counters to zero.</summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _totalRentedBuffers, 0);
        Interlocked.Exchange(ref _totalReturnedBuffers, 0);
        Interlocked.Exchange(ref _activeBuffers, 0);
        Interlocked.Exchange(ref _pooledBuffers, 0);
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
    }

    /// <summary>Removes all idle buffers from the pool, releasing them for garbage collection.</summary>
    /// <exception cref="ObjectDisposedException">The pool has been disposed.</exception>
    public void Trim()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        long removed = 0;
        foreach (var bucket in _buckets.Values)
        {
            while (bucket.TryDequeue(out _))
                removed++;
        }

        Interlocked.Add(ref _pooledBuffers, -removed);
    }

    /// <summary>Removes a fraction of idle buffers from the pool.</summary>
    /// <param name="pressure">Fraction to remove: 0.0 removes none, 1.0 removes all.</param>
    /// <exception cref="ObjectDisposedException">The pool has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pressure"/> is outside [0.0, 1.0].</exception>
    public void Trim(double pressure)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        if (pressure < 0.0 || pressure > 1.0)
            MemoryPoolThrowHelper.ThrowPressureOutOfRange(pressure);

        if (pressure == 0.0)
            return;

        if (pressure == 1.0)
        {
            Trim();
            return;
        }

        long removed = 0;
        foreach (var bucket in _buckets.Values)
        {
            int count = bucket.Count;
            int toRemove = (int)(count * pressure);
            for (int i = 0; i < toRemove; i++)
            {
                if (bucket.TryDequeue(out _))
                    removed++;
            }
        }

        Interlocked.Add(ref _pooledBuffers, -removed);
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
        return (int)BitOperations.RoundUpToPowerOf2((uint)value);
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
                    int next = a + b;
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

        public int Length => _length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Clear() => Array.Clear(_array, 0, _length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Activate() => Interlocked.Exchange(ref _active, 1);

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

            public int Length => _length;

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
        throw new OutOfMemoryException("Requested size exceeds pool capacity.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowPressureOutOfRange(double pressure) =>
        throw new ArgumentOutOfRangeException(nameof(pressure), pressure, "Pressure must be between 0.0 and 1.0.");
}
