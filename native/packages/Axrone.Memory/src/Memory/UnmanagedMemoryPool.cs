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
    private readonly int[] _bucketSizesArray;
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

        var bucketSizesSet = new SortedSet<int>();
        GenerateBucketSizes(bucketSizesSet);
        _bucketSizesArray = [.. bucketSizesSet];
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
                _bucketCounts.AddOrUpdate(size, 0, static (_, count) => Math.Max(0, count - 1));
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

        int currentCount;
        do
        {
            currentCount = _bucketCounts.GetOrAdd(size, 0);
            if (currentCount >= _maxBuffersPerBucket)
            {
                FreeBuffer(buffer);
                return;
            }
        } while (!_bucketCounts.TryUpdate(size, currentCount + 1, currentCount));

        if (Volatile.Read(ref _isDisposed) != 0)
        {
            _bucketCounts.TryUpdate(size, currentCount, currentCount + 1);
            FreeBuffer(buffer);
            return;
        }

        queue.Enqueue(buffer);
    }

    private void FreeBuffer(IntPtr buffer)
    {
        if (_bufferSizes.TryRemove(buffer, out var size) && _enableStatistics)
            Interlocked.Add(ref _totalBytes, -size);

        Marshal.FreeHGlobal(buffer);
    }

    /// <summary>Removes a fraction of idle buffers from the pool.</summary>
    /// <param name="percentage">Fraction to remove: 0.0 removes none, 1.0 removes all.</param>
    /// <exception cref="ObjectDisposedException">The pool has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="percentage"/> is outside (0.0, 1.0].</exception>
    public void Trim(double percentage = 0.5)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        if (percentage < 0.0 || percentage > 1.0)
            throw new ArgumentOutOfRangeException(nameof(percentage), percentage, "Percentage must be between 0.0 and 1.0.");

        if (percentage == 0.0)
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

                int actualRemoved = 0;
                for (int i = 0; i < trimCount; i++)
                {
                    if (queue.TryDequeue(out var ptr))
                    {
                        FreeBuffer(ptr);
                        actualRemoved++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (actualRemoved > 0)
                    _bucketCounts.TryUpdate(size, Math.Max(0, count - actualRemoved), count);
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
        if (ReferenceEquals(this, Shared))
            return;

        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        foreach (var pair in _buffersBySize)
        {
            var queue = pair.Value;
            while (queue.TryDequeue(out var ptr))
                FreeBuffer(ptr);
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

        int index = Array.BinarySearch(_bucketSizesArray, requestedSize);
        if (index >= 0)
            return _bucketSizesArray[index];

        int insertionPoint = ~index;
        return insertionPoint < _bucketSizesArray.Length
            ? _bucketSizesArray[insertionPoint]
            : _bucketSizesArray[^1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetNextPowerOfTwo(int value)
    {
        if (value <= _minBufferSize)
            return _minBufferSize;
        if (BitOperations.IsPow2(value))
            return value;
        uint result = BitOperations.RoundUpToPowerOf2((uint)value);
        return result > (uint)int.MaxValue ? int.MaxValue : (int)result;
    }

    private void GenerateBucketSizes(SortedSet<int> set)
    {
        switch (_allocationStrategy)
        {
            case AllocationStrategy.PowerOfTwo:
                for (int size = _minBufferSize; size <= _maxBufferSize; size = size > _maxBufferSize / 2 ? _maxBufferSize + 1 : size * 2)
                    set.Add(size);
                break;

            case AllocationStrategy.Fibonacci:
                int fa = _minBufferSize, fb = fa;
                set.Add(fa);
                while (fb <= _maxBufferSize)
                {
                    long nextLong = (long)fa + fb;
                    if (nextLong > int.MaxValue) break;
                    int next = (int)nextLong;
                    fa = fb;
                    fb = next;
                    if (fb > _maxBufferSize) break;
                    set.Add(fb);
                }
                break;

            case AllocationStrategy.Chunked:
                for (int size = _minBufferSize; size <= _maxBufferSize; size = size > _maxBufferSize - _chunkSize ? _maxBufferSize + 1 : size + _chunkSize)
                    set.Add(size);
                break;

            case AllocationStrategy.Exact:
                set.Add(_minBufferSize);
                set.Add(_defaultBufferSize);
                set.Add(_maxBufferSize);
                break;
        }
    }
}
