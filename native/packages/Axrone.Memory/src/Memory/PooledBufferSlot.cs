namespace Axrone.Memory;

public sealed class PooledBufferSlot<T> : IMemoryOwner<T>, IPooledBufferToken<T>
{
    private readonly IPoolBucketRegistry<T> _registry;
    private readonly IBlockAllocator<T> _allocator;
    private readonly object? _lifetimeToken;
    private readonly int _bucketIndex;
    private readonly int _slotCapacity;
    private readonly MemoryClearMode _clearMode;
    private Memory<T> _allocatedMemory;
    private uint _leaseGeneration;
    private int _state;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PooledBufferSlot(
        Memory<T> memory,
        object? lifetimeToken,
        int bucketIndex,
        int slotCapacity,
        MemoryClearMode clearMode,
        IPoolBucketRegistry<T> registry,
        IBlockAllocator<T> allocator)
    {
        _allocatedMemory = memory;
        _lifetimeToken = lifetimeToken;
        _bucketIndex = bucketIndex;
        _slotCapacity = slotCapacity;
        _clearMode = clearMode;
        _registry = registry;
        _allocator = allocator;
        _leaseGeneration = 1;
        _state = 0;
    }

    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _slotCapacity;
    }

    public Memory<T> Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (Volatile.Read(ref _state) != 1)
            {
                ThrowNotRented();
            }
            return _allocatedMemory;
        }
    }

    public Span<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (Volatile.Read(ref _state) != 1)
            {
                ThrowNotRented();
            }
            return _allocatedMemory.Span;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ActivateLease()
    {
        uint currentId = Interlocked.Increment(ref _leaseGeneration);
        Volatile.Write(ref _state, 1);
        if (_clearMode == MemoryClearMode.OnRent)
        {
            _allocatedMemory.Span.Clear();
        }
        return currentId;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsLeaseValid(uint leaseId) =>
        Volatile.Read(ref _state) == 1 && Volatile.Read(ref _leaseGeneration) == leaseId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(uint leaseId)
    {
        if (Volatile.Read(ref _leaseGeneration) != leaseId)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _state, 0, 1) == 1)
        {
            if (_clearMode == MemoryClearMode.OnReturn)
            {
                _allocatedMemory.Span.Clear();
            }
            _registry.Recycle(_bucketIndex, this);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _state, 0, 1) == 1)
        {
            Interlocked.Increment(ref _leaseGeneration);
            if (_clearMode == MemoryClearMode.OnReturn)
            {
                _allocatedMemory.Span.Clear();
            }
            _registry.Recycle(_bucketIndex, this);
        }
    }

    public void FinalizeEviction()
    {
        if (Interlocked.Exchange(ref _state, 2) != 2)
        {
            _allocator.Free(_allocatedMemory, _lifetimeToken);
            _allocatedMemory = Memory<T>.Empty;
        }
    }

    [DoesNotReturn]
    private static void ThrowNotRented() =>
        throw new InvalidOperationException("Attempted to access memory that is not actively leased.");
}

internal sealed class DynamicSingleBufferOwner<T> : IMemoryOwner<T>, IPooledBufferToken<T>
{
    private readonly Memory<T> _memory;
    private readonly object? _lifetimeToken;
    private readonly IBlockAllocator<T> _allocator;
    private readonly Action<long> _decrementCallback;
    private readonly long _byteSize;
    private uint _generation;
    private int _isDisposed;

    public DynamicSingleBufferOwner(
        Memory<T> memory,
        object? lifetimeToken,
        IBlockAllocator<T> allocator,
        Action<long> decrementCallback,
        long byteSize)
    {
        _memory = memory;
        _lifetimeToken = lifetimeToken;
        _allocator = allocator;
        _decrementCallback = decrementCallback;
        _byteSize = byteSize;
        _generation = 1;
        _isDisposed = 0;
    }

    public Memory<T> Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (Volatile.Read(ref _isDisposed) != 0)
            {
                ThrowDisposed();
            }
            return _memory;
        }
    }

    public Span<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (Volatile.Read(ref _isDisposed) != 0)
            {
                ThrowDisposed();
            }
            return _memory.Span;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsLeaseValid(uint leaseId) =>
        Volatile.Read(ref _isDisposed) == 0 && Volatile.Read(ref _generation) == leaseId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(uint leaseId) => Dispose();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            Interlocked.Increment(ref _generation);
            _decrementCallback(_byteSize);
            _allocator.Free(_memory, _lifetimeToken);
        }
    }

    [DoesNotReturn]
    private static void ThrowDisposed() =>
        throw new ObjectDisposedException(nameof(DynamicSingleBufferOwner<T>));
}

internal sealed class PerThreadPartitionCache<T>
{
    private const int PartitionCapacity = 32;
    private const int ItemsPerPartition = 4;

    [ThreadStatic]
    private static PerThreadPartitionCache<T>? t_threadLocal;

    private readonly PooledBufferSlot<T>?[][] _partitions;
    private readonly int[] _partitionCounts;

    public PerThreadPartitionCache()
    {
        _partitions = new PooledBufferSlot<T>?[PartitionCapacity][];
        for (int i = 0; i < PartitionCapacity; i++)
        {
            _partitions[i] = new PooledBufferSlot<T>?[ItemsPerPartition];
        }
        _partitionCounts = new int[PartitionCapacity];
    }

    public static PerThreadPartitionCache<T> Instance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => t_threadLocal ??= new PerThreadPartitionCache<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryStore(int partitionIndex, PooledBufferSlot<T> slot)
    {
        if ((uint)partitionIndex >= PartitionCapacity)
        {
            return false;
        }

        ref int count = ref _partitionCounts[partitionIndex];
        if (count < ItemsPerPartition)
        {
            _partitions[partitionIndex][count] = slot;
            count++;
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryRetrieve(int partitionIndex, [MaybeNullWhen(false)] out PooledBufferSlot<T> slot)
    {
        if ((uint)partitionIndex >= PartitionCapacity)
        {
            slot = default;
            return false;
        }

        ref int count = ref _partitionCounts[partitionIndex];
        if (count > 0)
        {
            count--;
            slot = _partitions[partitionIndex][count]!;
            _partitions[partitionIndex][count] = default;
            return true;
        }

        slot = default;
        return false;
    }
}
