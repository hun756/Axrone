namespace Axrone.Memory;

using Axrone.Utility.Internal;

/// <summary>
/// Lock-free Treiber stack of native nodes with an integrated ABA counter: the stack top packs
/// pointer and tag in one word, so pop/push retire through a single compare-exchange.
/// </summary>
/// <remarks>
/// Rented nodes are zeroed. The free-list link occupies the first pointer-sized bytes, so
/// element types must be at least pointer-sized. Disposing with outstanding rents leaks those
/// nodes by design — drain first.
/// </remarks>
/// <typeparam name="T">Node type.</typeparam>
public sealed unsafe class AlignedNodePool<T> : IDisposable
    where T : unmanaged
{
    private void* _block;
    private readonly int _capacity;
    private readonly nuint _elementSize;
    private nuint _stackTopRaw;
    private int _disposed;

    /// <summary>Node slots.</summary>
    public int Capacity => _capacity;

    /// <summary>Creates a pool.</summary>
    /// <param name="capacity">Node slots; must be positive.</param>
    /// <param name="alignment">Block alignment; must be a power of two.</param>
    public AlignedNodePool(int capacity, nuint alignment = 16)
    {
        if (capacity <= 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(capacity));
        }

        if (alignment == 0 || !BitOperations.IsPow2((uint)alignment))
        {
            ThrowHelper.ThrowInvalidAlignment((uint)alignment);
        }

        if (sizeof(T) < sizeof(void*))
        {
            ThrowHelper.ThrowInvalidOperationException(
                $"Node type {typeof(T).Name} ({sizeof(T)} bytes) cannot hold a pointer-sized free-list link.");
        }

        _capacity = capacity;
        nuint stride = (nuint)sizeof(T);
        nuint remainder = stride % alignment;
        if (remainder != 0)
        {
            stride += alignment - remainder;
        }

        _elementSize = stride;
        _block = NativeMemory.AlignedAlloc(stride * (nuint)capacity, alignment);
        NativeMemory.Clear(_block, stride * (nuint)capacity);

        byte* basePointer = (byte*)_block;
        for (int i = 0; i < capacity; i++)
        {
            T* current = (T*)(basePointer + ((nuint)i * stride));
            T* next = i < capacity - 1 ? (T*)(basePointer + ((nuint)(i + 1) * stride)) : null;
            *(void**)current = next;
        }

        _stackTopRaw = new TaggedPtr<T, ushort, HighBit16TagPolicy>((T*)basePointer, 0).Raw;
    }

    /// <summary>Rents a node; null when exhausted.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T* Rent()
    {
        return TryRent(out T* node) ? node : null;
    }

    /// <summary>Rents a node without throwing.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRent(out T* node)
    {
        while (true)
        {
            nuint currentRaw = Volatile.Read(ref _stackTopRaw);
            var top = new TaggedPtr<T, ushort, HighBit16TagPolicy>(currentRaw);
            if (top.IsNull)
            {
                node = null;
                return false;
            }

            T* claimed = top.Pointer;
            var next = new TaggedPtr<T, ushort, HighBit16TagPolicy>(
                *(T**)claimed, (ushort)(top.Tag + 1));

            if (Interlocked.CompareExchange(ref _stackTopRaw, next.Raw, currentRaw) == currentRaw)
            {
                Unsafe.InitBlock(claimed, 0, (uint)sizeof(T));
                node = claimed;
                return true;
            }
        }
    }

    /// <summary>Returns a node; null is rejected fail-fast.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T* node)
    {
        if (node == null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(node));
        }

        while (true)
        {
            nuint currentRaw = Volatile.Read(ref _stackTopRaw);
            var top = new TaggedPtr<T, ushort, HighBit16TagPolicy>(currentRaw);
            *(void**)node = top.Pointer;
            var restacked = new TaggedPtr<T, ushort, HighBit16TagPolicy>(
                node, (ushort)(top.Tag + 1));

            if (Interlocked.CompareExchange(ref _stackTopRaw, restacked.Raw, currentRaw) == currentRaw)
            {
                return;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            if (_block != null)
            {
                NativeMemory.AlignedFree(_block);
                _block = null;
            }
        }
    }
}
