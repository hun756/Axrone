namespace Axrone.Memory;

public interface IBlockAllocator<T>
{
    Memory<T> Allocate(int elementCount, out object? lifetimeToken);
    void Free(Memory<T> memory, object? lifetimeToken);
    long ComputeByteSize(int elementCount);
}

public interface IPooledBufferToken<T>
{
    Memory<T> Memory { get; }
    Span<T> Span { get; }
    bool IsLeaseValid(uint leaseId);
    void Return(uint leaseId);
}

public interface IPoolBucketRegistry<T>
{
    void Recycle(int bucketIndex, PooledBufferSlot<T> slot);
}
