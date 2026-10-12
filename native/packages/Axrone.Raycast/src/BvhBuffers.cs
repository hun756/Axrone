namespace Axrone.Raycast;

/// <summary>Fixed traversal stack storage for BVH walks.</summary>
[InlineArray(64)]
internal struct TraversalStack64
{
    private int _element;
}

/// <summary>
/// One 64-byte aligned native block with deterministic release: allocation
/// zeroes, accessors fail closed once disposed, and double disposal is a
/// no-op. Backs the node, index and primitive pools of a built hierarchy.
/// </summary>
/// <remarks>
/// Uses aligned allocation throughout: the runtime frees aligned blocks only
/// through the aligned free, so the plain free is never used here.
/// </remarks>
/// <typeparam name="T">Element type; must be unmanaged.</typeparam>
internal sealed unsafe class AlignedMemoryBlock<T> : IDisposable where T : unmanaged
{
    private T* _pointer;
    private readonly int _length;
    private int _disposed;

    /// <summary>Allocates and zeroes <paramref name="length"/> elements.</summary>
    public AlignedMemoryBlock(int length)
    {
        if (length <= 0)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(length));
        }

        _length = length;
        nuint byteSize = (nuint)length * (nuint)sizeof(T);
        _pointer = (T*)NativeMemory.AlignedAlloc(byteSize, 64);
        NativeMemory.Clear(_pointer, byteSize);
    }

    /// <summary>Base pointer; fails closed once disposed.</summary>
    public T* Pointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_disposed != 0)
            {
                ThrowHelper.ThrowObjectDisposed(nameof(AlignedMemoryBlock<T>));
            }
            return _pointer;
        }
    }

    /// <summary>Element count.</summary>
    public int Length => _length;

    /// <summary>Span view; fails closed once disposed.</summary>
    public Span<T> AsSpan()
    {
        if (_disposed != 0)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(AlignedMemoryBlock<T>));
        }
        return new Span<T>(_pointer, _length);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            T* pointer = _pointer;
            _pointer = null;
            if (pointer != null)
            {
                NativeMemory.AlignedFree(pointer);
            }
        }
    }
}
