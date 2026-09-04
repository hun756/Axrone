namespace Axrone.Memory;

public sealed unsafe class ContiguousSlabPool<T> : MemoryPool<T>, IPoolBucketRegistry<T> where T : unmanaged
{
    private readonly int _blockSize;
    private readonly int _blockCount;
    private readonly MemoryClearMode _clearMode;
    private readonly void* _backingPointer;
    private readonly NativeBlockMemoryManager<T> _rootManager;
    private readonly PooledBufferSlot<T>[] _slotArray;
    private readonly MpmcRingBuffer<PooledBufferSlot<T>> _freeSlotQueue;
    private long _activeRentals;
    private long _hits;
    private long _misses;
    private int _isDisposed;

    public ContiguousSlabPool(int blockSize, int blockCount, MemoryClearMode clearMode = MemoryClearMode.Never)
    {
        if (blockSize <= 0) throw new ArgumentOutOfRangeException(nameof(blockSize));
        if (blockCount <= 0) throw new ArgumentOutOfRangeException(nameof(blockCount));

        _blockSize = blockSize;
        _blockCount = blockCount;
        _clearMode = clearMode;

        nuint totalBytes = checked((nuint)blockSize * (nuint)blockCount * (nuint)sizeof(T));
        _backingPointer = NativeMemory.AlignedAlloc(totalBytes, 64);
        if (_backingPointer == null)
        {
            throw new InsufficientMemoryException($"Failed to allocate {totalBytes} contiguous slab memory bytes.");
        }

        _rootManager = new NativeBlockMemoryManager<T>((T*)_backingPointer, blockSize * blockCount, 64);
        _slotArray = new PooledBufferSlot<T>[blockCount];
        _freeSlotQueue = new MpmcRingBuffer<PooledBufferSlot<T>>(blockCount);

        for (int i = 0; i < blockCount; i++)
        {
            Memory<T> slice = _rootManager.Memory.Slice(i * blockSize, blockSize);
            PooledBufferSlot<T> slot = new(slice, null, 0, blockSize, _clearMode, this, NativeAlignedBlockAllocator<T>.Instance);
            _slotArray[i] = slot;
            _freeSlotQueue.TryEnqueue(slot);
        }
    }

    public override int MaxBufferSize => _blockSize;

    public override IMemoryOwner<T> Rent(int minBufferSize = -1)
    {
        if (minBufferSize <= 0) minBufferSize = _blockSize;
        ThrowIfDisposed();
        if (minBufferSize > _blockSize) throw new ArgumentOutOfRangeException(nameof(minBufferSize), "Requested length exceeds fixed slab size.");

        if (_freeSlotQueue.TryDequeue(out PooledBufferSlot<T>? slot))
        {
            Interlocked.Increment(ref _hits);
            Interlocked.Increment(ref _activeRentals);
            slot.ActivateLease();
            return slot;
        }

        Interlocked.Increment(ref _misses);
        throw new InsufficientMemoryException("Contiguous slab pool is fully exhausted.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ValueMemoryLease<T> RentLease(int length)
    {
        if (length <= 0) length = _blockSize;
        ThrowIfDisposed();
        if ((uint)length > (uint)_blockSize) throw new ArgumentOutOfRangeException(nameof(length));

        if (_freeSlotQueue.TryDequeue(out PooledBufferSlot<T>? slot))
        {
            Interlocked.Increment(ref _hits);
            Interlocked.Increment(ref _activeRentals);
            uint leaseId = slot.ActivateLease();
            return new ValueMemoryLease<T>(slot, leaseId, 0, length);
        }

        Interlocked.Increment(ref _misses);
        throw new InsufficientMemoryException("Contiguous slab pool is fully exhausted.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryRentLease(int length, out ValueMemoryLease<T> lease)
    {
        if (Volatile.Read(ref _isDisposed) != 0 || (uint)length > (uint)_blockSize || length <= 0)
        {
            lease = default;
            return false;
        }

        if (_freeSlotQueue.TryDequeue(out PooledBufferSlot<T>? slot))
        {
            Interlocked.Increment(ref _hits);
            Interlocked.Increment(ref _activeRentals);
            uint leaseId = slot.ActivateLease();
            lease = new ValueMemoryLease<T>(slot, leaseId, 0, length);
            return true;
        }

        Interlocked.Increment(ref _misses);
        lease = default;
        return false;
    }

    public int RentBatch(Span<ValueMemoryLease<T>> destination, int blockLength)
    {
        ThrowIfDisposed();
        if (blockLength <= 0) blockLength = _blockSize;

        int rentedCount = 0;
        for (int i = 0; i < destination.Length; i++)
        {
            if (TryRentLease(blockLength, out ValueMemoryLease<T> lease))
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

    void IPoolBucketRegistry<T>.Recycle(int bucketIndex, PooledBufferSlot<T> slot)
    {
        Interlocked.Decrement(ref _activeRentals);
        if (Volatile.Read(ref _isDisposed) == 0)
        {
            _freeSlotQueue.TryEnqueue(slot);
        }
    }

    public PoolDiagnosticsSnapshot CreateDiagnosticsSnapshot()
    {
        long totalBytes = checked((long)_blockSize * (long)_blockCount * (long)sizeof(T));
        long active = Volatile.Read(ref _activeRentals);
        long rentedBytes = checked(active * (long)_blockSize * (long)sizeof(T));
        return new PoolDiagnosticsSnapshot(totalBytes, rentedBytes, active, 0, Volatile.Read(ref _hits), Volatile.Read(ref _misses));
    }

    protected override void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            for (int i = 0; i < _slotArray.Length; i++) _slotArray[i].FinalizeEviction();
            ((IDisposable)_rootManager).Dispose();
            NativeMemory.AlignedFree(_backingPointer);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _isDisposed) != 0) ThrowDisposed();
    }

    [DoesNotReturn]
    private static void ThrowDisposed() => throw new ObjectDisposedException(nameof(ContiguousSlabPool<T>));
}
