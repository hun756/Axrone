namespace Axrone.Memory;

public sealed unsafe class MonotonicArenaBuffer : IDisposable
{
    private struct ArenaSegment
    {
        public void* MemoryBlock;
        public nint ByteCapacity;
    }

    private readonly nint _segmentCapacity;
    private readonly object _expansionLock = new();
    private ArenaSegment[] _segments;
    private int _activeSegmentCount;
    private int _currentSegmentIndex;
    private byte* _currentPointer;
    private nint _currentRemainingBytes;
    private int _isDisposed;

    public MonotonicArenaBuffer(nint segmentCapacity = 1048576)
    {
        if (segmentCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(segmentCapacity));

        _segmentCapacity = AlignTo64(segmentCapacity);
        _segments = new ArenaSegment[4];
        void* initialAlloc = NativeMemory.AlignedAlloc((nuint)_segmentCapacity, 64);
        if (initialAlloc == null) throw new InsufficientMemoryException("Failed to allocate initial arena chunk.");

        _segments[0] = new ArenaSegment { MemoryBlock = initialAlloc, ByteCapacity = _segmentCapacity };
        _activeSegmentCount = 1;
        _currentSegmentIndex = 0;
        _currentPointer = (byte*)initialAlloc;
        _currentRemainingBytes = _segmentCapacity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Span<T> AllocateSpan<T>(int count) where T : unmanaged
    {
        nint byteCount = checked((nint)count * (nint)sizeof(T));
        void* pointer = AllocateBytes(byteCount);
        return new Span<T>(pointer, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Memory<T> AllocateMemory<T>(int count) where T : unmanaged
    {
        nint byteCount = checked((nint)count * (nint)sizeof(T));
        void* pointer = AllocateBytes(byteCount);
        NativeBlockMemoryManager<T> manager = new((T*)pointer, count, 64);
        return manager.Memory;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void* AllocateBytes(nint byteCount)
    {
        ThrowIfDisposed();
        if (byteCount <= 0) return null;

        nint alignedAllocationSize = AlignTo64(byteCount);

        lock (_expansionLock)
        {
            if (_currentRemainingBytes >= alignedAllocationSize)
            {
                byte* result = _currentPointer;
                _currentPointer += alignedAllocationSize;
                _currentRemainingBytes -= alignedAllocationSize;
                return result;
            }
            return AllocateOverflowSegment(alignedAllocationSize);
        }
    }

    private void* AllocateOverflowSegment(nint alignedSize)
    {
        nint newCapacity = Math.Max(_segmentCapacity, alignedSize);
        int nextIndex = _currentSegmentIndex + 1;

        if (nextIndex < _activeSegmentCount && _segments[nextIndex].ByteCapacity >= newCapacity)
        {
            _currentSegmentIndex = nextIndex;
            _currentPointer = (byte*)_segments[nextIndex].MemoryBlock;
            _currentRemainingBytes = _segments[nextIndex].ByteCapacity;
        }
        else
        {
            void* newBlock = NativeMemory.AlignedAlloc((nuint)newCapacity, 64);
            if (newBlock == null) throw new InsufficientMemoryException($"Failed to allocate arena chunk of {newCapacity} bytes.");

            if (_activeSegmentCount == _segments.Length) Array.Resize(ref _segments, _segments.Length * 2);

            _segments[_activeSegmentCount] = new ArenaSegment { MemoryBlock = newBlock, ByteCapacity = newCapacity };
            _currentSegmentIndex = _activeSegmentCount;
            _activeSegmentCount++;
            _currentPointer = (byte*)newBlock;
            _currentRemainingBytes = newCapacity;
        }

        byte* allocatedAddress = _currentPointer;
        _currentPointer += alignedSize;
        _currentRemainingBytes -= alignedSize;
        return allocatedAddress;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        ThrowIfDisposed();
        lock (_expansionLock)
        {
            _currentSegmentIndex = 0;
            if (_activeSegmentCount > 0)
            {
                _currentPointer = (byte*)_segments[0].MemoryBlock;
                _currentRemainingBytes = _segments[0].ByteCapacity;
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            lock (_expansionLock)
            {
                for (int i = 0; i < _activeSegmentCount; i++)
                {
                    if (_segments[i].MemoryBlock != null)
                    {
                        NativeMemory.AlignedFree(_segments[i].MemoryBlock);
                        _segments[i].MemoryBlock = null;
                    }
                }
                _activeSegmentCount = 0;
                _currentPointer = null;
                _currentRemainingBytes = 0;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nint AlignTo64(nint size) => (size + 63) & ~63;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _isDisposed) != 0) ThrowDisposed();
    }

    [DoesNotReturn]
    private static void ThrowDisposed() => throw new ObjectDisposedException(nameof(MonotonicArenaBuffer));
}
