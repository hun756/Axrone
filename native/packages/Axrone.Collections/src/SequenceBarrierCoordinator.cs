namespace Axrone.Collections;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// Cacheline-isolated concurrency barrier eliminating destructive false sharing.
/// Separates enqueue and dequeue cursors by 128 bytes to isolate dual-line prefetchers
/// on both Intel (64-byte) and AMD (64-byte) cache-line architectures.
/// Total struct size: 384 bytes (3 × 128-byte cache lines).
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 384)]
internal struct SequenceBarrierCoordinator
{
    private const int CacheLineBoundary = 128;

    [FieldOffset(CacheLineBoundary * 1)]
    private nuint _enqueuePos;

    [FieldOffset(CacheLineBoundary * 2)]
    private nuint _dequeuePos;

    public SequenceNumber EnqueuePos
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => new(Volatile.Read(ref Unsafe.AsRef(in _enqueuePos)));
    }

    public SequenceNumber DequeuePos
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => new(Volatile.Read(ref Unsafe.AsRef(in _dequeuePos)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdvanceEnqueue(SequenceNumber expected, SequenceNumber target)
        => Interlocked.CompareExchange(ref _enqueuePos, target.Value, expected.Value) == expected.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdvanceDequeue(SequenceNumber expected, SequenceNumber target)
        => Interlocked.CompareExchange(ref _dequeuePos, target.Value, expected.Value) == expected.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int ComputeCount(uint capacity)
    {
        nuint enq = Volatile.Read(ref Unsafe.AsRef(in _enqueuePos));
        nuint deq = Volatile.Read(ref Unsafe.AsRef(in _dequeuePos));
        nuint diff = enq - deq;

        if (diff > capacity)
        {
            return (nint)diff < 0 ? 0 : (int)capacity;
        }

        return (int)diff;
    }
}
