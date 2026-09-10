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
    private byte* _currentSegmentBase;
    private nint _currentSegmentCapacity;
    private long _currentOffset;
    private DisposalTracker _tracker;

    public MonotonicArenaBuffer(nint segmentCapacity = 1048576)
    {
        if (segmentCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(segmentCapacity));

        _segmentCapacity = AlignTo64(segmentCapacity);
        _segments = new ArenaSegment[4];
        void* initialAlloc = NativeMemory.AlignedAlloc((nuint)_segmentCapacity, 64);
        if (initialAlloc == null) throw new InsufficientMemoryException("Failed to allocate initial arena chunk.");

        _segments[0] = new ArenaSegment { MemoryBlock = initialAlloc, ByteCapacity = _segmentCapacity };
        _activeSegmentCount = 1;
        _currentSegmentBase = (byte*)initialAlloc;
        _currentSegmentCapacity = _segmentCapacity;
        _currentOffset = 0;
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

        // Lock-free fast path: atomic bump pointer
        nint offset = (nint)Interlocked.Add(ref _currentOffset, (long)alignedAllocationSize);
        if (offset <= Volatile.Read(ref _currentSegmentCapacity))
        {
            return _currentSegmentBase + (offset - alignedAllocationSize);
        }

        // Slow path: segment overflow, expansion lock required
        return AllocateOverflowSlow(alignedAllocationSize);
    }

    private void* AllocateOverflowSlow(nint alignedSize)
    {
        lock (_expansionLock)
        {
            nint newCapacity = Math.Max(_segmentCapacity, alignedSize);
            int nextIndex = _currentSegmentIndex + 1;

            if (nextIndex < _activeSegmentCount && _segments[nextIndex].ByteCapacity >= newCapacity)
            {
                _currentSegmentIndex = nextIndex;
                _currentSegmentBase = (byte*)_segments[nextIndex].MemoryBlock;
                _currentSegmentCapacity = _segments[nextIndex].ByteCapacity;
            }
            else
            {
                void* newBlock = NativeMemory.AlignedAlloc((nuint)newCapacity, 64);
                if (newBlock == null) throw new InsufficientMemoryException($"Failed to allocate arena chunk of {newCapacity} bytes.");

                if (_activeSegmentCount == _segments.Length) Array.Resize(ref _segments, _segments.Length * 2);

                _segments[_activeSegmentCount] = new ArenaSegment { MemoryBlock = newBlock, ByteCapacity = newCapacity };
                _currentSegmentIndex = _activeSegmentCount;
                _activeSegmentCount++;
                _currentSegmentBase = (byte*)newBlock;
                _currentSegmentCapacity = newCapacity;
            }

            _currentOffset = (long)alignedSize;
            return _currentSegmentBase;
        }
    }

    private int _currentSegmentIndex;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        ThrowIfDisposed();
        lock (_expansionLock)
        {
            _currentSegmentIndex = 0;
            if (_activeSegmentCount > 0)
            {
                _currentSegmentBase = (byte*)_segments[0].MemoryBlock;
                _currentSegmentCapacity = _segments[0].ByteCapacity;
                _currentOffset = 0;
            }
        }
    }

    public void Dispose()
    {
        if (_tracker.TryDispose())
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
                _currentSegmentBase = null;
                _currentSegmentCapacity = 0;
                _currentOffset = 0;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nint AlignTo64(nint size) => (size + 63) & ~63;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() => _tracker.ThrowIfDisposed(nameof(MonotonicArenaBuffer));
}
