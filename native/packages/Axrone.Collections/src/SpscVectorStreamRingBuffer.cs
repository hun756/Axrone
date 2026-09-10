namespace Axrone.Collections;

using System.Numerics;
using System.Runtime.CompilerServices;

public sealed unsafe class SpscVectorStreamRingBuffer<T> : IDisposable where T : unmanaged
{
    private readonly AlignedMemoryBlock<T> _storage;
    private readonly int _capacity;
    private readonly int _mask;
    private readonly long* _head;
    private readonly long* _tail;
    private int _disposed;

    public int Capacity => _capacity;

    public SpscVectorStreamRingBuffer(BatchCapacity capacity)
    {
        int cap = (int)BitOperations.RoundUpToPowerOf2((uint)capacity.Value);
        _capacity = cap;
        _mask = cap - 1;
        _storage = new AlignedMemoryBlock<T>(new BatchCapacity(cap), MemoryAlignment.CacheLine64);

        _head = (long*)NativeMemory.AlignedAlloc(128, 128);
        _tail = (long*)NativeMemory.AlignedAlloc(128, 128);
        NativeMemory.Clear(_head, 128);
        NativeMemory.Clear(_tail, 128);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryWrite(ReadOnlySpan<T> source)
    {
        int count = source.Length;
        if (count > _capacity) return false;

        long currentTail = Volatile.Read(ref *_tail);
        long currentHead = Volatile.Read(ref *_head);

        if ((currentTail - currentHead) + count > _capacity)
        {
            return false;
        }

        int offset = (int)(currentTail & _mask);
        int contiguous = _capacity - offset;
        Span<T> buffer = _storage.Span;

        if (count <= contiguous)
        {
            source.CopyTo(buffer.Slice(offset, count));
        }
        else
        {
            source[..contiguous].CopyTo(buffer[offset..]);
            source[contiguous..].CopyTo(buffer[..(count - contiguous)]);
        }

        Volatile.Write(ref *_tail, currentTail + count);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryRead(Span<T> destination, out int elementsRead)
    {
        long currentHead = Volatile.Read(ref *_head);
        long currentTail = Volatile.Read(ref *_tail);
        long available = currentTail - currentHead;

        if (available <= 0)
        {
            elementsRead = 0;
            return false;
        }

        int toRead = (int)Math.Min((long)destination.Length, available);
        int offset = (int)(currentHead & _mask);
        int contiguous = _capacity - offset;
        ReadOnlySpan<T> buffer = _storage.ReadOnlySpan;

        if (toRead <= contiguous)
        {
            buffer.Slice(offset, toRead).CopyTo(destination);
        }
        else
        {
            buffer.Slice(offset, contiguous).CopyTo(destination[..contiguous]);
            buffer[..(toRead - contiguous)].CopyTo(destination[contiguous..]);
        }

        Volatile.Write(ref *_head, currentHead + toRead);
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
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _storage.Dispose();
            if (_head != null) NativeMemory.AlignedFree(_head);
            if (_tail != null) NativeMemory.AlignedFree(_tail);
        }
    }
}
