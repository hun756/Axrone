namespace Axrone.Utility.Alignment;

public sealed unsafe class AlignedArenaAllocator : IDisposable
{
    private void* _baseAddress;
    private readonly nuint _capacity;
    private readonly Alignment _alignment;

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct PositionState
    {
        [FieldOffset(64)]
        public nuint Offset;
    }

    private PositionState _position;
    private readonly Lock _lifecycleGate = new();
    private int _disposed;

    public nuint Capacity => _capacity;
    public Alignment BaseAlignment => _alignment;
    public nuint AllocatedBytes => Volatile.Read(ref _position.Offset);
    public nuint RemainingBytes => _capacity - AllocatedBytes;
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public AlignedArenaAllocator(ByteSize capacity, Alignment baseAlignment)
    {
        Alignment effectiveAlignment = baseAlignment.Value < Alignment.Page4K.Value ? Alignment.Page4K : baseAlignment;
        nuint bytes = effectiveAlignment.AlignUp(capacity.Value);

        void* ptr = NativeMemory.AlignedAlloc(bytes, effectiveAlignment.Value);
        if (ptr == null)
        {
            ThrowHelper.ThrowOutOfMemory(bytes, effectiveAlignment.Value);
        }

        NativeMemory.Clear(ptr, bytes);

        _baseAddress = ptr;
        _capacity = bytes;
        _alignment = effectiveAlignment;
        _position.Offset = 0;
        _disposed = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TryAllocate(ByteSize size, Alignment alignment, out Span<byte> allocated)
    {
        ThrowIfDisposed();

        nuint requested = size.Value;

        nuint baseAddr = (nuint)_baseAddress;
        nuint currentOffset = Volatile.Read(ref _position.Offset);

        while (true)
        {
            nuint currentAddress = baseAddr + currentOffset;
            nuint alignedAddress = alignment.AlignUp(currentAddress);
            nuint newOffset = (alignedAddress - baseAddr) + requested;

            if (newOffset > _capacity)
            {
                allocated = default;
                return false;
            }

            nuint priorOffset = Interlocked.CompareExchange(ref _position.Offset, newOffset, currentOffset);
            if (priorOffset == currentOffset)
            {
                allocated = new Span<byte>((void*)alignedAddress, checked((int)requested));
                return true;
            }

            currentOffset = priorOffset;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAllocate<T>(int count, Alignment alignment, out Span<T> typed) where T : unmanaged
    {
        if (count < 0)
        {
            ThrowHelper.ThrowNegativeCount();
        }

        nuint totalBytes = checked((nuint)count * (nuint)sizeof(T));
        if (TryAllocate(new ByteSize(totalBytes), alignment, out Span<byte> bytes))
        {
            typed = MemoryMarshal.Cast<byte, T>(bytes);
            return true;
        }

        typed = default;
        return false;
    }

    public void Reset()
    {
        ThrowIfDisposed();
        lock (_lifecycleGate)
        {
            Volatile.Write(ref _position.Offset, 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(AlignedArenaAllocator));
        }
    }

    private void DisposeCore()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            lock (_lifecycleGate)
            {
                void* ptr = _baseAddress;
                _baseAddress = null;
                if (ptr != null)
                {
                    NativeMemory.AlignedFree(ptr);
                }
            }
        }
    }

    public void Dispose()
    {
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    ~AlignedArenaAllocator() => DisposeCore();
}
