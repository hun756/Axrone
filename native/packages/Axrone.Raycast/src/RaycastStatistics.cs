namespace Axrone.Raycast;

using System.Runtime.InteropServices;

/// <summary>
/// Thread-safe counters for one raycast engine. Each counter sits on its own cache line,
/// so concurrent increments on different counters never share a line.
/// </summary>
public sealed class RaycastStatistics
{
    private PaddedCounter _queries;
    private PaddedCounter _hits;
    private PaddedCounter _misses;
    private PaddedCounter _cacheHits;

    /// <summary>Gets the number of traversals that reached the broadphase.</summary>
    public long TotalQueries => Volatile.Read(ref _queries.Value);

    /// <summary>Gets the number of traversals that confirmed at least one contact.</summary>
    public long TotalHits => Volatile.Read(ref _hits.Value);

    /// <summary>Gets the number of traversals that confirmed no contact.</summary>
    public long TotalMisses => Volatile.Read(ref _misses.Value);

    /// <summary>Gets the number of traversals served from the nearest-contact cache.</summary>
    public long CacheHits => Volatile.Read(ref _cacheHits.Value);

    /// <summary>Gets the fraction of traversals that hit, in the range [0, 1].</summary>
    public double HitRate
    {
        get
        {
            long total = TotalQueries;
            return total == 0 ? 0d : (double)TotalHits / total;
        }
    }

    /// <summary>Gets the fraction of queries served from the cache, in the range [0, 1].</summary>
    public double CacheHitRate
    {
        get
        {
            long total = TotalQueries + CacheHits;
            return total == 0 ? 0d : (double)CacheHits / total;
        }
    }

    /// <summary>Records one traversal and whether it produced a contact.</summary>
    /// <param name="hit">Whether the traversal confirmed at least one contact.</param>
    public void RecordQuery(bool hit)
    {
        Interlocked.Increment(ref _queries.Value);
        if (hit)
        {
            Interlocked.Increment(ref _hits.Value);
        }
        else
        {
            Interlocked.Increment(ref _misses.Value);
        }
    }

    /// <summary>Records one traversal served from the nearest-contact cache.</summary>
    public void RecordCacheHit() => Interlocked.Increment(ref _cacheHits.Value);

    /// <summary>Resets every counter.</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _queries.Value, 0);
        Interlocked.Exchange(ref _hits.Value, 0);
        Interlocked.Exchange(ref _misses.Value, 0);
        Interlocked.Exchange(ref _cacheHits.Value, 0);
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct PaddedCounter
    {
        [FieldOffset(0)]
        public long Value;
    }
}
