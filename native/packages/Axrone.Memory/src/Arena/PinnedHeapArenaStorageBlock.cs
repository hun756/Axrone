using Axrone.Utility.Alignment;

namespace Axrone.Memory.Arena;

public sealed unsafe class PinnedHeapArenaStorageBlock<T> : IArenaStorageBlock<T> where T : unmanaged
{
    private readonly PinnedAlignedBlock _block;
    private readonly int _elementCount;

    public PinnedHeapArenaStorageBlock(int elementCount, Alignment alignment)
    {
        if (elementCount <= 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(elementCount));

        nuint byteSize = checked((nuint)elementCount * (nuint)sizeof(T));
        _block = PinnedAlignedMemoryAllocator.Shared.Allocate(new ByteSize(byteSize), alignment, zeroInitialize: true);
        _elementCount = elementCount;
    }

    public MemoryTopology Topology => MemoryTopology.PinnedHeap;

    public int ElementCount => _elementCount;

    public ByteSize ByteSize => _block.ByteSize;

    public Alignment BlockAlignment => _block.Alignment;

    public Span<T> Span => MemoryMarshal.Cast<byte, T>(_block.Span);

    public T* Pointer => (T*)_block.BasePointer;

    public bool IsDisposed => _block.IsDisposed;

    public void Dispose() => _block.Dispose();
}
