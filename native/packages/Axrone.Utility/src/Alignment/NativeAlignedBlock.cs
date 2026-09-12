namespace Axrone.Utility.Alignment;

public sealed unsafe class NativeAlignedBlock : IAlignedBlock
{
    private void* _pointer;
    private readonly ByteSize _byteSize;
    private readonly Alignment _alignment;
    private int _disposed;

    public void* BasePointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return _pointer;
        }
    }

    public ByteSize ByteSize => _byteSize;
    public Alignment Alignment => _alignment;
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public Span<byte> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return new Span<byte>(_pointer, checked((int)_byteSize.Value));
        }
    }

    public ReadOnlySpan<byte> ReadOnlySpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return new ReadOnlySpan<byte>(_pointer, checked((int)_byteSize.Value));
        }
    }

    internal NativeAlignedBlock(ByteSize byteSize, Alignment alignment, bool zeroInitialize)
    {
        nuint bytes = byteSize.Value;
        nuint align = alignment.Value;

        void* ptr = NativeMemory.AlignedAlloc(bytes, align);
        if (ptr == null)
        {
            ThrowHelper.ThrowOutOfMemory(bytes, align);
        }

        if (zeroInitialize)
        {
            NativeMemory.Clear(ptr, bytes);
        }

        _pointer = ptr;
        _byteSize = byteSize;
        _alignment = alignment;
        _disposed = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan<T>() where T : unmanaged => MemoryMarshal.Cast<byte, T>(Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> AsReadOnlySpan<T>() where T : unmanaged => MemoryMarshal.Cast<byte, T>(ReadOnlySpan);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(NativeAlignedBlock));
        }
    }

    private void DisposeCore()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            void* ptr = _pointer;
            _pointer = null;
            if (ptr != null)
            {
                NativeMemory.Free(ptr);
            }
        }
    }

    public void Dispose()
    {
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    ~NativeAlignedBlock() => DisposeCore();
}
