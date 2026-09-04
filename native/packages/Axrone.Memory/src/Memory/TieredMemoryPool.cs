namespace Axrone.Memory;

public sealed class TieredMemoryPool<T> : MemoryPool<T>, IPoolBucketRegistry<T>
{
    private readonly int _minimumBlockSize;
    private readonly int _maximumBlockSize;
    private readonly int _baseShift;
    private readonly MemoryClearMode _clearMode;
    private readonly IBlockAllocator<T> _allocator;
    private readonly MpmcRingBuffer<PooledBufferSlot<T>>[] _tier2GlobalQueues;
    private readonly int[] _bucketCapacities;
    private readonly Timer? _trimTimer;
    private readonly int _autoTrimPercentage;
    private long _totalAllocatedBytes;
    private long _totalRentedBytes;
    private long _activeAllocations;
    private long _tier1Hits;
    private long _tier2Hits;
    private long _allocatorMisses;
    private int _isPoolDisposed;

    public TieredMemoryPool(BufferPoolOptions? options = null, IBlockAllocator<T>? customAllocator = null)
    {
        options ??= new BufferPoolOptions();

        if (options.MinimumBlockSize <= 0 || !BitOperations.IsPow2(options.MinimumBlockSize))
        {
            throw new ArgumentOutOfRangeException(nameof(options.MinimumBlockSize), "MinimumBlockSize must be a power of two.");
        }

        if (options.MaximumBlockSize < options.MinimumBlockSize || !BitOperations.IsPow2(options.MaximumBlockSize))
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaximumBlockSize), "MaximumBlockSize must be a power of two greater than or equal to MinimumBlockSize.");
        }

        _minimumBlockSize = options.MinimumBlockSize;
        _maximumBlockSize = options.MaximumBlockSize;
        _clearMode = options.ClearMode;
        _autoTrimPercentage = Math.Clamp(options.AutoTrimPercentage, 0, 100);
        _baseShift = BitOperations.Log2((uint)_minimumBlockSize);
        int maxShift = BitOperations.Log2((uint)_maximumBlockSize);
        int bucketCount = maxShift - _baseShift + 1;

        if (customAllocator != null)
        {
            _allocator = customAllocator;
        }
        else
        {
            _allocator = options.BackingStrategy switch
            {
                MemoryBackingStrategy.Managed => ManagedArrayBlockAllocator<T>.Instance,
                MemoryBackingStrategy.PinnedManaged => PinnedHeapBlockAllocator<T>.Instance,
                MemoryBackingStrategy.NativeAligned => throw new InvalidOperationException("NativeAligned strategy requires passing NativeAlignedBlockAllocator explicitly."),
                _ => ManagedArrayBlockAllocator<T>.Instance
            };
        }

        _tier2GlobalQueues = new MpmcRingBuffer<PooledBufferSlot<T>>[bucketCount];
        _bucketCapacities = new int[bucketCount];

        for (int i = 0; i < bucketCount; i++)
        {
            _tier2GlobalQueues[i] = new MpmcRingBuffer<PooledBufferSlot<T>>(options.GlobalQueueCapacity);
            _bucketCapacities[i] = _minimumBlockSize << i;
        }

        if (options.TrimInterval > TimeSpan.Zero && _autoTrimPercentage > 0)
        {
            _trimTimer = new Timer(
                static state => ((TieredMemoryPool<T>)state!).PerformPeriodicTrim(),
                this,
                options.TrimInterval,
                options.TrimInterval);
        }
    }

    public override int MaxBufferSize => _maximumBlockSize;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private int ComputeBucketIndex(int requestedSize)
    {
        uint normalized = (uint)Math.Max(_minimumBlockSize, requestedSize);
        uint powerOfTwo = BitOperations.RoundUpToPowerOf2(normalized);
        return BitOperations.Log2(powerOfTwo) - _baseShift;
    }

    public override IMemoryOwner<T> Rent(int minBufferSize = -1)
    {
        if (minBufferSize <= 0)
        {
            minBufferSize = _minimumBlockSize;
        }

        ThrowIfDisposed();

        if (minBufferSize > _maximumBlockSize)
        {
            return RentOversizedOwner(minBufferSize);
        }

        int bucketIndex = ComputeBucketIndex(minBufferSize);
        PooledBufferSlot<T> slot = RentInternalSlot(bucketIndex);
        slot.ActivateLease();
        return slot;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ValueMemoryLease<T> RentLease(int requestedLength)
    {
        if (requestedLength <= 0)
        {
            requestedLength = _minimumBlockSize;
        }

        ThrowIfDisposed();

        if (requestedLength > _maximumBlockSize)
        {
            return RentOversizedLease(requestedLength);
        }

        int bucketIndex = ComputeBucketIndex(requestedLength);
        PooledBufferSlot<T> slot = RentInternalSlot(bucketIndex);
        uint leaseId = slot.ActivateLease();
        return new ValueMemoryLease<T>(slot, leaseId, 0, requestedLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ValueMemoryLease<T> RentExact(int exactLength)
    {
        if (exactLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exactLength));
        }

        ThrowIfDisposed();

        if (exactLength > _maximumBlockSize)
        {
            return RentOversizedLease(exactLength);
        }

        int bucketIndex = ComputeBucketIndex(exactLength);
        PooledBufferSlot<T> slot = RentInternalSlot(bucketIndex);
        uint leaseId = slot.ActivateLease();
        return new ValueMemoryLease<T>(slot, leaseId, 0, exactLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryRentLease(int requestedLength, out ValueMemoryLease<T> lease)
    {
        if (Volatile.Read(ref _isPoolDisposed) != 0 || requestedLength <= 0)
        {
            lease = default;
            return false;
        }

        if (requestedLength > _maximumBlockSize)
        {
            lease = RentOversizedLease(requestedLength);
            return true;
        }

        int bucketIndex = ComputeBucketIndex(requestedLength);
        PooledBufferSlot<T> slot = RentInternalSlot(bucketIndex);
        uint leaseId = slot.ActivateLease();
        lease = new ValueMemoryLease<T>(slot, leaseId, 0, requestedLength);
        return true;
    }

    public int RentBatch(Span<ValueMemoryLease<T>> destination, int minimumLengthPerBlock)
    {
        ThrowIfDisposed();

        if (minimumLengthPerBlock <= 0)
        {
            minimumLengthPerBlock = _minimumBlockSize;
        }

        int rentedCount = 0;
        for (int i = 0; i < destination.Length; i++)
        {
            if (TryRentLease(minimumLengthPerBlock, out ValueMemoryLease<T> lease))
            {
                destination[i] = lease;
                rentedCount++;
            }
            else
            {
                for (int j = 0; j < rentedCount; j++)
                {
                    destination[j].Dispose();
                    destination[j] = default;
                }
                return 0;
            }
        }

        return rentedCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private PooledBufferSlot<T> RentInternalSlot(int bucketIndex)
    {
        PerThreadPartitionCache<T> threadCache = PerThreadPartitionCache<T>.Instance;

        if (threadCache.TryRetrieve(bucketIndex, out PooledBufferSlot<T>? cachedSlot))
        {
            Interlocked.Increment(ref _tier1Hits);
            Interlocked.Increment(ref _activeAllocations);
            Interlocked.Add(ref _totalRentedBytes, _allocator.ComputeByteSize(cachedSlot.Capacity));
            return cachedSlot;
        }

        if (_tier2GlobalQueues[bucketIndex].TryDequeue(out PooledBufferSlot<T>? globalSlot))
        {
            Interlocked.Increment(ref _tier2Hits);
            Interlocked.Increment(ref _activeAllocations);
            Interlocked.Add(ref _totalRentedBytes, _allocator.ComputeByteSize(globalSlot.Capacity));
            return globalSlot;
        }

        Interlocked.Increment(ref _allocatorMisses);
        int slotSize = _bucketCapacities[bucketIndex];
        Memory<T> allocatedMemory = _allocator.Allocate(slotSize, out object? token);
        long byteCount = _allocator.ComputeByteSize(slotSize);

        Interlocked.Add(ref _totalAllocatedBytes, byteCount);
        Interlocked.Add(ref _totalRentedBytes, byteCount);
        Interlocked.Increment(ref _activeAllocations);

        return new PooledBufferSlot<T>(
            allocatedMemory,
            token,
            bucketIndex,
            slotSize,
            _clearMode,
            this,
            _allocator);
    }

    private DynamicSingleBufferOwner<T> RentOversizedOwner(int minimumLength)
    {
        Interlocked.Increment(ref _allocatorMisses);
        Memory<T> raw = _allocator.Allocate(minimumLength, out object? token);
        long byteSize = _allocator.ComputeByteSize(minimumLength);

        Interlocked.Add(ref _totalAllocatedBytes, byteSize);
        Interlocked.Add(ref _totalRentedBytes, byteSize);
        Interlocked.Increment(ref _activeAllocations);

        return new DynamicSingleBufferOwner<T>(
            raw,
            token,
            _allocator,
            bytes =>
            {
                Interlocked.Add(ref _totalAllocatedBytes, -bytes);
                Interlocked.Add(ref _totalRentedBytes, -bytes);
                Interlocked.Decrement(ref _activeAllocations);
            },
            byteSize);
    }

    private ValueMemoryLease<T> RentOversizedLease(int minimumLength)
    {
        DynamicSingleBufferOwner<T> owner = RentOversizedOwner(minimumLength);
        return new ValueMemoryLease<T>(owner, 1, 0, minimumLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    void IPoolBucketRegistry<T>.Recycle(int bucketIndex, PooledBufferSlot<T> slot)
    {
        Interlocked.Decrement(ref _activeAllocations);
        Interlocked.Add(ref _totalRentedBytes, -_allocator.ComputeByteSize(slot.Capacity));

        if (Volatile.Read(ref _isPoolDisposed) != 0)
        {
            Interlocked.Add(ref _totalAllocatedBytes, -_allocator.ComputeByteSize(slot.Capacity));
            slot.FinalizeEviction();
            return;
        }

        PerThreadPartitionCache<T> threadCache = PerThreadPartitionCache<T>.Instance;

        if (threadCache.TryStore(bucketIndex, slot))
        {
            return;
        }

        if (_tier2GlobalQueues[bucketIndex].TryEnqueue(slot))
        {
            return;
        }

        Interlocked.Add(ref _totalAllocatedBytes, -_allocator.ComputeByteSize(slot.Capacity));
        slot.FinalizeEviction();
    }

    public PoolDiagnosticsSnapshot CreateDiagnosticsSnapshot() =>
        new(
            Volatile.Read(ref _totalAllocatedBytes),
            Volatile.Read(ref _totalRentedBytes),
            Volatile.Read(ref _activeAllocations),
            Volatile.Read(ref _tier1Hits),
            Volatile.Read(ref _tier2Hits),
            Volatile.Read(ref _allocatorMisses));

    public void Trim(float percentage = 0.5f)
    {
        ThrowIfDisposed();
        percentage = Math.Clamp(percentage, 0.0f, 1.0f);

        for (int i = 0; i < _tier2GlobalQueues.Length; i++)
        {
            MpmcRingBuffer<PooledBufferSlot<T>> queue = _tier2GlobalQueues[i];
            while (queue.TryDequeue(out PooledBufferSlot<T>? slot))
            {
                Interlocked.Add(ref _totalAllocatedBytes, -_allocator.ComputeByteSize(slot.Capacity));
                slot.FinalizeEviction();
            }
        }
    }

    private void PerformPeriodicTrim()
    {
        if (Volatile.Read(ref _isPoolDisposed) == 0)
        {
            Trim((float)_autoTrimPercentage / 100.0f);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _isPoolDisposed, 1) == 0)
        {
            _trimTimer?.Dispose();

            for (int i = 0; i < _tier2GlobalQueues.Length; i++)
            {
                MpmcRingBuffer<PooledBufferSlot<T>> queue = _tier2GlobalQueues[i];
                while (queue.TryDequeue(out PooledBufferSlot<T>? slot))
                {
                    Interlocked.Add(ref _totalAllocatedBytes, -_allocator.ComputeByteSize(slot.Capacity));
                    slot.FinalizeEviction();
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _isPoolDisposed) != 0)
        {
            ThrowPoolDisposed();
        }
    }

    [DoesNotReturn]
    private static void ThrowPoolDisposed() =>
        throw new ObjectDisposedException(nameof(TieredMemoryPool<T>));
}
