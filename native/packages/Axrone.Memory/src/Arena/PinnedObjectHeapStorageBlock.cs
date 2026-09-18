using Axrone.Utility.Alignment;

namespace Axrone.Memory.Arena;

internal sealed unsafe class PinnedObjectHeapStorageBlock<T> : IArenaStorageBlock<T> where T : unmanaged
{
    private readonly T[] _pinnedArray;
    private T* _pointer;
    private readonly nuint _capacity;
    private int _disposed;

    public T* BasePointer => _pointer;
    public nuint Capacity => _capacity;

    public PinnedObjectHeapStorageBlock(nuint capacity)
    {
        _capacity = capacity;
        _pinnedArray = GC.AllocateArray<T>((int)capacity, pinned: true);
        _pointer = (T*)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(_pinnedArray));
        NativeMemory.Clear(_pointer, capacity * (nuint)sizeof(T));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        return _pinnedArray.AsSpan(0, (int)_capacity);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _pointer = null;
        }
    }
}
