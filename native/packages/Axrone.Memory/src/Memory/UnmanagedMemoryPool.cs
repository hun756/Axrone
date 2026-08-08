namespace Axrone.Memory;

/// <summary>
/// Memory pool that allocates unmanaged (native) memory via <see cref="System.Runtime.InteropServices.Marshal.AllocHGlobal(int)"/>.
/// Useful for interop scenarios, pinned buffers, and avoiding GC pressure for large allocations.
/// </summary>
public sealed class UnmanagedMemoryPool : IDisposable
{
    /// <summary>Shared singleton instance with default settings.</summary>
    public static readonly UnmanagedMemoryPool Shared = new();

    private readonly ConcurrentDictionary<int, ConcurrentQueue<IntPtr>> _buffersBySize;
    private readonly ConcurrentDictionary<IntPtr, int> _bufferSizes;
    private readonly ConcurrentDictionary<int, int> _bucketCounts;
    private readonly HashSet<int> _bucketSizes;
    private readonly int _maxBufferSize;
    private readonly int _defaultBufferSize;
    private readonly int _minBufferSize;
    private readonly bool _clearOnReturn;
    private readonly bool _clearOnRent;
    private readonly AllocationStrategy _allocationStrategy;
    private readonly int _maxBuffersPerBucket;
    private readonly bool _enableStatistics;
    private readonly int _chunkSize;

    private long _totalAllocated;
    private long _totalRented;
    private long _totalReturned;
    private long _totalBytes;

    private int _isDisposed;

    /// <summary>Creates an unmanaged pool with default settings.</summary>
    public UnmanagedMemoryPool()
        : this(
            defaultBufferSize: 4096,
            maxBufferSize: 1024 * 1024,
            clearOnReturn: true,
            clearOnRent: true,
            strategy: AllocationStrategy.PowerOfTwo,
            maxBuffersPerBucket: 32,
            enableStatistics: false,
            minBufferSize: 16,
            chunkSize: 128
        )
    { }

    /// <summary>Creates an unmanaged pool with the specified configuration.</summary>
    /// <param name="defaultBufferSize">Default buffer size in bytes.</param>
    /// <param name="maxBufferSize">Maximum buffer size in bytes.</param>
    /// <param name="clearOnReturn">Clear buffer contents when returning to pool.</param>
    /// <param name="clearOnRent">Clear buffer contents when renting from pool.</param>
    /// <param name="strategy">Bucket size allocation strategy.</param>
    /// <param name="maxBuffersPerBucket">Maximum idle buffers per bucket.</param>
    /// <param name="enableStatistics">Track allocation statistics.</param>
    /// <param name="minBufferSize">Minimum buffer size in bytes.</param>
    /// <param name="chunkSize">Chunk increment for chunked strategy.</param>
    public UnmanagedMemoryPool(
        int defaultBufferSize = 4096,
        int maxBufferSize = 1024 * 1024,
        bool clearOnReturn = true,
        bool clearOnRent = true,
        AllocationStrategy strategy = AllocationStrategy.PowerOfTwo,
        int maxBuffersPerBucket = 32,
        bool enableStatistics = false,
        int minBufferSize = 16,
        int chunkSize = 128)
    {
        _defaultBufferSize = Math.Max(16, defaultBufferSize);
        _maxBufferSize = Math.Max(_defaultBufferSize, maxBufferSize);
        _minBufferSize = Math.Max(16, minBufferSize);
        _clearOnReturn = clearOnReturn;
        _clearOnRent = clearOnRent;
        _allocationStrategy = strategy;
        _maxBuffersPerBucket = Math.Max(1, maxBuffersPerBucket);
        _enableStatistics = enableStatistics;
        _chunkSize = Math.Max(16, chunkSize);

        _buffersBySize = new ConcurrentDictionary<int, ConcurrentQueue<IntPtr>>();
        _bufferSizes = new ConcurrentDictionary<IntPtr, int>();
        _bucketCounts = new ConcurrentDictionary<int, int>();
        _bucketSizes = [];

        GenerateBucketSizes();
    }

    /// <summary>Gets the maximum buffer size this pool will allocate.</summary>
    public int MaxBufferSize => _maxBufferSize;

    /// <summary>Rents an unmanaged memory handle with at least <paramref name="minimumLength"/> bytes.</summary>
    /// <param name="minimumLength">Minimum byte count. Use -1 for the default buffer size.</param>
    /// <returns>An <see cref="UnmanagedMemoryHandle"/> wrapping the native memory.</returns>
    /// <exception cref="ObjectDisposedException">The pool has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Requested size exceeds maximum.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedMemoryHandle Rent(int minimumLength = -1)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        int requestedSize = minimumLength <= 0 ? _defaultBufferSize : minimumLength;
        int bufferSize = GetOptimalBufferSize(requestedSize);

        if (bufferSize > _maxBufferSize)
            throw new ArgumentOutOfRangeException(nameof(minimumLength));

        if (TryRentExisting(bufferSize, out var ptr))
        {
            if (_clearOnRent)
                ZeroMemory(ptr, bufferSize);

            if (_enableStatistics)
                Interlocked.Increment(ref _totalRented);

            return new UnmanagedMemoryHandle(this, ptr, bufferSize);
        }

        ptr = Marshal.AllocHGlobal(bufferSize);
        _bufferSizes[ptr] = bufferSize;

        if (_enableStatistics)
        {
            Interlocked.Increment(ref _totalRented);
            Interlocked.Add(ref _totalBytes, bufferSize);
            Interlocked.Add(ref _totalAllocated, 1);
        }

        ZeroMemory(ptr, bufferSize);

        return new UnmanagedMemoryHandle(this, ptr, bufferSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryRentExisting(int size, out IntPtr ptr)
    {
        if (_buffersBySize.TryGetValue(size, out var queue))
        {
            if (queue.TryDequeue(out ptr))
            {
                if (_bucketCounts.TryGetValue(size, out var count))
                    _bucketCounts.TryUpdate(size, count - 1, count);
                return true;
            }
        }

        ptr = IntPtr.Zero;
        return false;
    }

    /// <summary>Returns an unmanaged buffer to the pool for reuse.</summary>
    /// <param name="buffer">Pointer to the buffer.</param>
    /// <param name="size">Size of the buffer in bytes.</param>
    internal void Return(IntPtr buffer, int size)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            FreeBuffer(buffer);
            return;
        }

        if (size > _maxBufferSize)
        {
            FreeBuffer(buffer);
            return;
        }

        if (_clearOnReturn)
            ZeroMemory(buffer, size);

        if (_enableStatistics)
            Interlocked.Increment(ref _totalReturned);

        var queue = _buffersBySize.GetOrAdd(size, static _ => new ConcurrentQueue<IntPtr>());

        int count = _bucketCounts.GetOrAdd(size, 0);
        if (count < _maxBuffersPerBucket)
        {
            queue.Enqueue(buffer);
            _bucketCounts.TryUpdate(size, count + 1, count);
        }
        else
        {
            FreeBuffer(buffer);
        }
    }

    private void FreeBuffer(IntPtr buffer)
    {
        if (_bufferSizes.TryRemove(buffer, out var size) && _enableStatistics)
            Interlocked.Add(ref _totalBytes, -size);

        Marshal.FreeHGlobal(buffer);
    }

    /// <summary>Removes a fraction of idle buffers from the pool.</summary>
    /// <param name="percentage">Fraction to remove: 0.0 removes none, 1.0 removes all.</param>
    public void Trim(float percentage = 0.5f)
    {
        if (percentage <= 0 || percentage > 1)
            return;

        foreach (var pair in _buffersBySize)
        {
            int size = pair.Key;
            var queue = pair.Value;

            if (_bucketCounts.TryGetValue(size, out var count))
            {
                int trimCount = (int)(count * percentage);
                if (trimCount <= 0)
                    continue;

                for (int i = 0; i < trimCount; i++)
                {
                    if (queue.TryDequeue(out var ptr))
                        FreeBuffer(ptr);
                    else
                        break;
                }

                _bucketCounts.TryUpdate(size, count - trimCount, count);
            }
        }
    }

    /// <summary>Gets a snapshot of pool statistics.</summary>
    /// <returns>A tuple of (TotalAllocated, TotalRented, TotalReturned, TotalBytes).</returns>
    public (long TotalAllocated, long TotalRented, long TotalReturned, long TotalBytes) GetStatistics()
    {
        if (!_enableStatistics)
            return (0, 0, 0, 0);

        return (
            Interlocked.Read(ref _totalAllocated),
            Interlocked.Read(ref _totalRented),
            Interlocked.Read(ref _totalReturned),
            Interlocked.Read(ref _totalBytes)
        );
    }

    /// <summary>Disposes the pool and frees all pooled native memory.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        foreach (var pair in _buffersBySize)
        {
            var queue = pair.Value;
            while (queue.TryDequeue(out var ptr))
                Marshal.FreeHGlobal(ptr);
        }

        _buffersBySize.Clear();
        _bufferSizes.Clear();
        _bucketCounts.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ZeroMemory(IntPtr ptr, int size)
    {
        new Span<byte>((void*)ptr, size).Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetOptimalBufferSize(int requestedSize)
    {
        if (requestedSize <= _minBufferSize)
            return _minBufferSize;

        if (_bucketSizes.Contains(requestedSize))
            return requestedSize;

        return _allocationStrategy switch
        {
            AllocationStrategy.PowerOfTwo => GetNextPowerOfTwo(requestedSize),
            AllocationStrategy.Fibonacci => GetNextFibonacci(requestedSize),
            AllocationStrategy.Chunked => ((requestedSize + _chunkSize - 1) / _chunkSize) * _chunkSize,
            AllocationStrategy.Exact => GetNextExact(requestedSize),
            _ => GetNextPowerOfTwo(requestedSize)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetNextPowerOfTwo(int value)
    {
        if (value <= _minBufferSize)
            return _minBufferSize;
        if (BitOperations.IsPow2(value))
            return value;
        return (int)BitOperations.RoundUpToPowerOf2((uint)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetNextFibonacci(int requestedSize)
    {
        foreach (var size in _bucketSizes)
        {
            if (size >= requestedSize)
                return size;
        }
        return Math.Min(requestedSize, _maxBufferSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetNextExact(int requestedSize)
    {
        foreach (var size in _bucketSizes)
        {
            if (size >= requestedSize)
                return size;
        }
        return Math.Min(requestedSize, _maxBufferSize);
    }

    private void GenerateBucketSizes()
    {
        switch (_allocationStrategy)
        {
            case AllocationStrategy.PowerOfTwo:
                for (int size = _minBufferSize; size <= _maxBufferSize; size *= 2)
                    _bucketSizes.Add(size);
                break;

            case AllocationStrategy.Fibonacci:
                int a = _minBufferSize, b = a;
                _bucketSizes.Add(a);
                while (b <= _maxBufferSize)
                {
                    int next = a + b;
                    a = b;
                    b = next;
                    if (b > _maxBufferSize) break;
                    _bucketSizes.Add(b);
                }
                break;

            case AllocationStrategy.Chunked:
                for (int size = _minBufferSize; size <= _maxBufferSize; size += _chunkSize)
                    _bucketSizes.Add(size);
                break;

            case AllocationStrategy.Exact:
                _bucketSizes.Add(_minBufferSize);
                _bucketSizes.Add(_defaultBufferSize);
                _bucketSizes.Add(_maxBufferSize);
                break;
        }
    }
}
