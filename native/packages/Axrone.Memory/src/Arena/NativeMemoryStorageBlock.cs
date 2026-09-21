using Axrone.Utility.Alignment;

namespace Axrone.Memory.Arena;

internal sealed unsafe class NativeMemoryStorageBlock<T> : IArenaStorageBlock<T> where T : unmanaged
{
    private T* _pointer;
    private readonly nuint _capacity;
    private readonly nuint _byteSize;
    private int _disposed;

    public T* BasePointer => _pointer;
    public nuint Capacity => _capacity;

    public NativeMemoryStorageBlock(nuint capacity, Alignment alignment)
    {
        _capacity = capacity;
        _byteSize = capacity * (nuint)sizeof(T);
        nuint allocAlign = Math.Max(alignment.Value, (nuint)sizeof(nint));
        _pointer = (T*)NativeMemory.AlignedAlloc(_byteSize, allocAlign);
        NativeMemory.Clear(_pointer, _byteSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        return new Span<T>(_pointer, (int)_capacity);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            if (_pointer != null)
            {
                NativeMemory.AlignedFree(_pointer);
                _pointer = null;
            }
        }
    }
}
