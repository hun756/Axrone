using Axrone.Utility.Alignment;

namespace Axrone.Memory.Arena;

public sealed unsafe class LinearArena<T> : IDisposable where T : unmanaged
{
    private readonly T* _base;
    private readonly nuint _capacityBytes;
    private readonly bool _ownsStorage;
    private long _position;
    private int _disposed;

    public LinearArena(nuint capacityBytes, Alignment alignment = default, MemoryTopology topology = MemoryTopology.NativeAligned)
    {
        _capacityBytes = capacityBytes;
        _ownsStorage = true;

        if (topology == MemoryTopology.NativeAligned)
        {
            nuint align = alignment.Value > 0 ? alignment.Value : (nuint)64;
            _base = (T*)NativeMemory.AlignedAlloc(capacityBytes, align);
            NativeMemory.Clear(_base, capacityBytes);
        }
        else
        {
            ThrowHelper.ThrowNotSupportedException("LinearArena only supports NativeAligned topology.");
            _base = default;
        }
    }

    internal LinearArena(T* basePointer, nuint capacityBytes)
    {
        _base = basePointer;
        _capacityBytes = capacityBytes;
        _ownsStorage = false;
    }

    public nuint CapacityBytes => _capacityBytes;

    public nuint UsedBytes => (nuint)Volatile.Read(ref _position);

    public nuint RemainingBytes => _capacityBytes - UsedBytes;

    public ArenaMarker CurrentMarker => new(0, (nuint)Volatile.Read(ref _position));

    public Span<T> Allocate(nuint elementCount)
    {
        ThrowIfDisposed();
        nuint elementSize = (nuint)sizeof(T);
        nuint byteCount = elementCount * elementSize;

        while (true)
        {
            long currentPos = Volatile.Read(ref _position);
            long newPos = currentPos + (long)byteCount;

            if ((nuint)newPos > _capacityBytes)
            {
                ThrowHelper.ThrowInsufficientMemory(
                    $"Linear arena exhausted: requested {byteCount} bytes, remaining {_capacityBytes - (nuint)currentPos} bytes.");
            }

            if (Interlocked.CompareExchange(ref _position, newPos, currentPos) == currentPos)
            {
                return new Span<T>(_base + (nuint)currentPos / elementSize, (int)elementCount);
            }
        }
    }

    public Span<T> AllocateZeroed(nuint elementCount)
    {
        Span<T> span = Allocate(elementCount);
        span.Clear();
        return span;
    }

    public LinearArenaScope<T> BeginScope()
    {
        ThrowIfDisposed();
        return new LinearArenaScope<T>(this, CurrentMarker);
    }

    public void RewindTo(ArenaMarker marker)
    {
        ThrowIfDisposed();

        if (marker.ChunkIndex != 0)
        {
            ThrowHelper.ThrowInvalidMarker();
        }

        long target = (long)marker.Offset;
        long current = Volatile.Read(ref _position);

        if (target < 0 || (nuint)target > _capacityBytes || target > current)
        {
            ThrowHelper.ThrowInvalidMarker();
        }

        Volatile.Write(ref _position, target);
    }

    public void Reset()
    {
        ThrowIfDisposed();
        Volatile.Write(ref _position, 0);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(LinearArena<T>));
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _ownsStorage)
        {
            NativeMemory.AlignedFree(_base);
        }
    }
}
