namespace Axrone.Utility.Alignment;

public sealed unsafe class PinnedAlignedBlock : IAlignedBlock
{
    private readonly byte[] _storage;
    private readonly GCHandle _handle;
    private readonly void* _alignedPointer;
    private readonly ByteSize _byteSize;
    private readonly Alignment _alignment;
    private int _disposed;

    public void* BasePointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return _alignedPointer;
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
            return new Span<byte>(_alignedPointer, checked((int)_byteSize.Value));
        }
    }

    public ReadOnlySpan<byte> ReadOnlySpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return new ReadOnlySpan<byte>(_alignedPointer, checked((int)_byteSize.Value));
        }
    }

    public PinnedAlignedBlock(ByteSize byteSize, Alignment alignment, bool zeroInitialize)
    {
        nuint requiredBytes = byteSize.Value + alignment.Value;
        byte[] storage = new byte[checked((int)requiredBytes)];

        GCHandle handle = GCHandle.Alloc(storage, GCHandleType.Pinned);
        void* rawBase = (void*)handle.AddrOfPinnedObject();
        void* alignedBase = alignment.AlignUp(rawBase);

        if (zeroInitialize)
        {
            NativeMemory.Clear(alignedBase, byteSize.Value);
        }

        _storage = storage;
        _handle = handle;
        _alignedPointer = alignedBase;
        _byteSize = byteSize;
        _alignment = alignment;
        _disposed = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(PinnedAlignedBlock));
        }
    }

    private void DisposeCore()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
        }
    }

    public void Dispose()
    {
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    ~PinnedAlignedBlock() => DisposeCore();
}
