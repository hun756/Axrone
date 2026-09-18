namespace Axrone.Memory;

/// <summary>Allocates and frees managed memory blocks with optional lifetime tracking.</summary>
public interface IBlockAllocator<T>
{
    Memory<T> Allocate(int elementCount, out object? lifetimeToken);
    void Free(Memory<T> memory, object? lifetimeToken);
    long ComputeByteSize(int elementCount);
}

/// <summary>Token representing a leased buffer segment from a pooled buffer.</summary>
public interface IPooledBufferToken<T>
{
    Memory<T> Memory { get; }
    Span<T> Span { get; }
    bool IsLeaseValid(uint leaseId);
    void Return(uint leaseId);
}

/// <summary>Recycles pooled buffer slots back to their owning bucket.</summary>
public interface IPoolBucketRegistry<T>
{
    void Recycle(int bucketIndex, PooledBufferSlot<T> slot);
}
