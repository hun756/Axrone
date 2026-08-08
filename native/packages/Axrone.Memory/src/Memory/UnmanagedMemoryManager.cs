namespace Axrone.Memory;

/// <summary>
/// Internal <see cref="MemoryManager{T}"/> that wraps a native pointer and provides
/// <see cref="Memory{T}"/> access to unmanaged memory. Used by <see cref="UnmanagedMemoryWriter"/>
/// to implement <see cref="System.Buffers.IBufferWriter{T}"/>GetMemory.
/// </summary>
/// <remarks>
/// SAFETY: The caller must ensure the native pointer remains valid for the lifetime
/// of any Memory&lt;byte&gt; instances obtained from this manager. The pointer is not
/// owned by this manager — lifetime is managed by <see cref="UnmanagedMemoryPool"/>.
/// This manager is designed to be reused — call <see cref="Update"/> to rebind the pointer
/// and length without allocating a new instance.
/// </remarks>
internal sealed class UnmanagedMemoryManager : MemoryManager<byte>
{
    private unsafe void* _pointer;
    private int _length;

    /// <summary>Creates a new manager wrapping the specified native pointer.</summary>
    /// <param name="pointer">Native memory pointer.</param>
    /// <param name="length">Length in bytes.</param>
    internal unsafe UnmanagedMemoryManager(void* pointer, int length)
    {
        _pointer = pointer;
        _length = length;
    }

    /// <summary>Rebinds the manager to a new pointer and length without allocating.</summary>
    /// <param name="pointer">New native memory pointer.</param>
    /// <param name="length">New length in bytes.</param>
    internal unsafe void Update(void* pointer, int length)
    {
        _pointer = pointer;
        _length = length;
    }

    /// <inheritdoc/>
    public override Span<byte> GetSpan()
    {
        unsafe
        {
            return new Span<byte>(_pointer, _length);
        }
    }

    /// <inheritdoc/>
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        unsafe
        {
            if ((uint)elementIndex > (uint)_length)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));

            return new MemoryHandle((byte*)_pointer + elementIndex);
        }
    }

    /// <inheritdoc/>
    public override void Unpin()
    {
        // No-op: unmanaged memory doesn't need pinning/unpinning.
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // No-op: lifetime managed by UnmanagedMemoryPool.
    }
}
