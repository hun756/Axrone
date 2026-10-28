namespace Axrone.Ecs;

/// <summary>Wrap-safe comparison of world change ticks.</summary>
public static class EcsTick
{
    /// <summary>
    /// Whether <paramref name="tick"/> is strictly newer than <paramref name="sinceTick"/>.
    /// The unsigned difference keeps the comparison correct across the signed wrap as long
    /// as the two ticks are less than 2^31 frames apart.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNewer(int tick, int sinceTick) => unchecked(tick - sinceTick) > 0;
}

/// <summary>One component removal retained for change queries.</summary>
internal readonly record struct RemovalRecord(Entity Entity, ComponentId ComponentId, int Tick);

/// <summary>
/// Bounded ring of component removals. Removals are structural events, so the journal
/// serializes writers and readers with one lock; queries on it are diagnostic rather than
/// per-row hot paths. When the ring is full, the oldest removal is overwritten and the
/// dropped count grows.
/// </summary>
internal sealed class RemovalJournal
{
    private readonly RemovalRecord[] _records;
    private readonly Lock _gate = new();
    private int _start;
    private int _count;
    private long _dropped;

    public RemovalJournal(int capacity = 4096)
    {
        if (capacity <= 0)
        {
            EcsThrowHelper.ThrowInvalidJournalCapacity(capacity);
        }

        _records = GC.AllocateArray<RemovalRecord>(capacity, pinned: true);
    }

    /// <summary>Retained removal count.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    /// <summary>Removals overwritten because the ring was full.</summary>
    public long DroppedCount
    {
        get
        {
            lock (_gate)
            {
                return _dropped;
            }
        }
    }

    /// <summary>Retains one removal.</summary>
    public void Record(in Entity entity, ComponentId componentId, int tick)
    {
        lock (_gate)
        {
            int index = _start + _count;
            if (_count == _records.Length)
            {
                index = _start;
                _start = (_start + 1) % _records.Length;
                _dropped++;
            }
            else
            {
                _count++;
            }

            _records[index % _records.Length] = new RemovalRecord(entity, componentId, tick);
        }
    }

    /// <summary>Collects removals of one component newer than <paramref name="sinceTick"/>.</summary>
    public int GetRemoved(ComponentId componentId, int sinceTick, Span<Entity> results) =>
        GetRemoved(componentId, sinceTick, results, default);

    /// <summary>
    /// Collects removals of one component and their removal ticks. The tick span may be
    /// shorter than the entity span; the shorter length bounds the result.
    /// </summary>
    public int GetRemoved(
        ComponentId componentId,
        int sinceTick,
        Span<Entity> results,
        Span<int> ticks)
    {
        lock (_gate)
        {
            int limit = ticks.IsEmpty ? results.Length : Math.Min(results.Length, ticks.Length);
            int written = 0;
            for (int offset = 0; offset < _count && written < limit; offset++)
            {
                RemovalRecord record = _records[(_start + offset) % _records.Length];
                if (record.ComponentId == componentId && EcsTick.IsNewer(record.Tick, sinceTick))
                {
                    results[written] = record.Entity;
                    if (!ticks.IsEmpty)
                    {
                        ticks[written] = record.Tick;
                    }

                    written++;
                }
            }

            return written;
        }
    }

    /// <summary>Whether an entity lost one component after <paramref name="sinceTick"/>.</summary>
    public bool WasRemoved(in Entity entity, ComponentId componentId, int sinceTick)
    {
        lock (_gate)
        {
            for (int offset = 0; offset < _count; offset++)
            {
                RemovalRecord record = _records[(_start + offset) % _records.Length];
                if (record.Entity == entity &&
                    record.ComponentId == componentId &&
                    EcsTick.IsNewer(record.Tick, sinceTick))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Clears the ring.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _start = 0;
            _count = 0;
            _dropped = 0;
            Array.Clear(_records);
        }
    }
}
