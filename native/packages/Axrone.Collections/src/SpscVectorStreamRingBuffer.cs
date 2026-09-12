namespace Axrone.Collections;

using System.Numerics;
using System.Runtime.CompilerServices;
using Axrone.Utility.Alignment;
using Axrone.Utility.Disposable;

public sealed unsafe class SpscVectorStreamRingBuffer<T> : IDisposable where T : unmanaged
{
    private readonly NativeAlignedBlock _storage;
    private readonly int _capacity;
    private readonly int _mask;
    private AlignedAtomicCounter128 _head;
    private AlignedAtomicCounter128 _tail;
    private DisposalTracker _tracker;

    public int Capacity => _capacity;

    public SpscVectorStreamRingBuffer(BatchCapacity capacity)
    {
        int cap = (int)BitOperations.RoundUpToPowerOf2((uint)capacity.Value);
        _capacity = cap;
        _mask = cap - 1;
        _storage = NativeAlignedMemoryAllocator.Shared.Allocate(
            new ByteSize((nuint)cap * (nuint)sizeof(T)), Alignment.CacheLine64, zeroInitialize: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryWrite(ReadOnlySpan<T> source)
    {
        int count = source.Length;
        if (count > _capacity) return false;

        long currentTail = _tail.Value;
        long currentHead = _head.Value;

        if ((currentTail - currentHead) + count > _capacity)
        {
            return false;
        }

        int offset = (int)(currentTail & _mask);
        int contiguous = _capacity - offset;
        Span<T> buffer = _storage.AsSpan<T>();

        if (count <= contiguous)
        {
            source.CopyTo(buffer.Slice(offset, count));
        }
        else
        {
            source[..contiguous].CopyTo(buffer[offset..]);
            source[contiguous..].CopyTo(buffer[..(count - contiguous)]);
        }

        _tail.Add(count);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryRead(Span<T> destination, out int elementsRead)
    {
        long currentHead = _head.Value;
        long currentTail = _tail.Value;
        long available = currentTail - currentHead;

        if (available <= 0)
        {
            elementsRead = 0;
            return false;
        }

        int toRead = (int)Math.Min((long)destination.Length, available);
        int offset = (int)(currentHead & _mask);
        int contiguous = _capacity - offset;
        ReadOnlySpan<T> buffer = _storage.AsReadOnlySpan<T>();

        if (toRead <= contiguous)
        {
            buffer.Slice(offset, toRead).CopyTo(destination);
        }
        else
        {
            buffer.Slice(offset, contiguous).CopyTo(destination[..contiguous]);
            buffer[..(toRead - contiguous)].CopyTo(destination[contiguous..]);
        }

        _head.Add(toRead);
        elementsRead = toRead;
        return true;
    }

    ~SpscVectorStreamRingBuffer()
    {
        Release();
    }

    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    private void Release()
    {
        if (_tracker.TryDispose())
        {
            _storage.Dispose();
        }
    }
}
