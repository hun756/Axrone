namespace Axrone.Batching;

/// <summary>
/// Chase-Lev work-stealing deque: owner pushes/pops one end, thieves steal the other.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
/// <remarks>
/// <para>
/// The owner works the bottom (LIFO: the most recently pushed item pops first, keeping hot data in
/// cache); thieves steal from the top (FIFO: the oldest item, least likely to be in the owner's
/// working set). The last-item race resolves with one compare-exchange, so an item is never handed
/// to both sides.
/// </para>
/// <para>
/// Fixed capacity, like the bounded queues in <c>Axrone.Collections</c>: <see cref="TryPush"/>
/// reports <see langword="false"/> when full and the scheduler sizes the deque or spills elsewhere.
/// The policy overload makes the other choice explicit — <see cref="DropOldestEvictionPolicy"/>
/// drops the oldest item and counts it in <see cref="DroppedItems"/> — so overflow is never a
/// silent default.
/// </para>
/// </remarks>
public sealed class WorkStealingDeque<T>
{
    private readonly T[] _buffer;
    private readonly int _mask;

    private long _top;
    private long _bottom;
    private long _dropped;

    /// <summary>Elements the deque holds.</summary>
    public int Capacity => _buffer.Length;

    /// <summary>Best-effort item count; may shift under concurrent steal.</summary>
    public int Count
    {
        get
        {
            var count = Volatile.Read(ref _bottom) - Volatile.Read(ref _top);
            return count > 0 ? (int)Math.Min(count, _buffer.Length) : 0;
        }
    }

    /// <summary>Whether the deque looks empty; may shift under concurrent steal.</summary>
    public bool IsEmpty => Volatile.Read(ref _bottom) <= Volatile.Read(ref _top);

    /// <summary>Items dropped by drop-oldest pushes so far.</summary>
    public long DroppedItems => Volatile.Read(ref _dropped);

    /// <summary>Creates a deque.</summary>
    /// <param name="capacityPowerOfTwo">Slot count; must be a power of two, at least 2.</param>
    /// <exception cref="ArgumentOutOfRangeException">Not a usable capacity.</exception>
    public WorkStealingDeque(int capacityPowerOfTwo)
    {
        if (capacityPowerOfTwo < 2 || !BitOperations.IsPow2(capacityPowerOfTwo))
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(capacityPowerOfTwo));
        }

        _buffer = new T[capacityPowerOfTwo];
        _mask = capacityPowerOfTwo - 1;
    }

    /// <summary>Pushes an item (owner only).</summary>
    /// <param name="item">Item to push.</param>
    /// <returns><see langword="false"/> when full.</returns>
    public bool TryPush(T item) => TryPush<RejectNewEvictionPolicy>(item);

    /// <summary>Pushes an item with an overflow policy (owner only).</summary>
    /// <typeparam name="TPolicy">Eviction policy, monomorphized at the call site.</typeparam>
    /// <param name="item">Item to push.</param>
    /// <returns>
    /// <see langword="true"/> when the item landed. With <see cref="RejectNewEvictionPolicy"/>
    /// <see langword="false"/> means full and nothing moved; with
    /// <see cref="DropOldestEvictionPolicy"/> the oldest item was dropped to make room.
    /// </returns>
    public bool TryPush<TPolicy>(T item)
        where TPolicy : struct, IBatchEvictionPolicy
    {
        var bottom = Volatile.Read(ref _bottom);
        var top = Volatile.Read(ref _top);
        if (bottom - top >= _buffer.Length)
        {
            if (!TPolicy.DropOldest)
            {
                return false;
            }

            Interlocked.Increment(ref _top);
            TPolicy.OnDropped(ref _dropped, 1);
            bottom = Volatile.Read(ref _bottom);
        }

        _buffer[bottom & _mask] = item;
        Volatile.Write(ref _bottom, bottom + 1);
        return true;
    }

    /// <summary>Pops the newest item (owner only).</summary>
    /// <param name="item">Popped item when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when empty or a thief won the last-item race.</returns>
    public bool TryPopBottom(out T item)
    {
        var bottom = Volatile.Read(ref _bottom) - 1;
        Volatile.Write(ref _bottom, bottom);
        Interlocked.MemoryBarrier();

        var top = Volatile.Read(ref _top);
        var remaining = bottom - top;
        if (remaining < 0)
        {
            Volatile.Write(ref _bottom, top);
            item = default!;
            return false;
        }

        item = _buffer[bottom & _mask];
        if (remaining > 0)
        {
            return true;
        }

        if (Interlocked.CompareExchange(ref _top, top + 1, top) != top)
        {
            item = default!;
            Volatile.Write(ref _bottom, top + 1);
            return false;
        }

        Volatile.Write(ref _bottom, top + 1);
        return true;
    }

    /// <summary>Steals the oldest item (any thread).</summary>
    /// <param name="item">Stolen item when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when empty or another thread won the race.</returns>
    public bool TrySteal(out T item)
    {
        var top = Volatile.Read(ref _top);
        Interlocked.MemoryBarrier();
        if (top >= Volatile.Read(ref _bottom))
        {
            item = default!;
            return false;
        }

        item = _buffer[top & _mask];
        if (Interlocked.CompareExchange(ref _top, top + 1, top) != top)
        {
            item = default!;
            return false;
        }

        return true;
    }
}
