namespace Axrone.Batching;

/// <summary>
/// Single-producer/single-consumer triple buffer with stale-snapshot detection.
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
/// <remarks>
/// <para>
/// The producer owns one slot, the consumer one slot, and the third is the handoff. A snapshot is
/// a span over the consumer slot plus the generation it was taken at: if the producer laps the
/// consumer and recycles that slot, <see cref="IsStillValid"/> reports it instead of letting the
/// consumer silently read torn data. Holding a snapshot across rotations is a bug the generation
/// makes visible rather than silent.
/// </para>
/// <para>
/// Backed by managed arrays, so there is nothing to free and no finalizer to forget. Counts cross
/// the producer/consumer boundary with release/acquire ordering, which x86-TSO happens to provide
/// for free but ARM64 does not.
/// </para>
/// </remarks>
public sealed class TripleBuffer<T> where T : unmanaged
{
    private readonly T[][] _slots;
    private readonly int[] _counts;
    private readonly long[] _generations;
    private long _nextGeneration;

    private int _writeIndex;
    private int _cleanIndex;
    private int _readIndex;

    /// <summary>Elements per slot.</summary>
    public int Capacity { get; }

    /// <summary>Creates a buffer.</summary>
    /// <param name="capacity">Elements per slot; must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    public TripleBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(capacity));
        }

        Capacity = capacity;
        _slots = [new T[capacity], new T[capacity], new T[capacity]];
        _counts = new int[3];
        _generations = new long[3];
        _writeIndex = 0;
        _cleanIndex = 1;
        _readIndex = 2;
    }

    /// <summary>Appends one item to the producer slot.</summary>
    /// <param name="item">Item to append.</param>
    /// <returns><see langword="false"/> when the producer slot is full.</returns>
    public bool TryWrite(in T item)
    {
        var count = Volatile.Read(ref _counts[_writeIndex]);
        if (count >= Capacity)
        {
            return false;
        }

        _slots[_writeIndex][count] = item;
        Volatile.Write(ref _counts[_writeIndex], count + 1);
        return true;
    }

    /// <summary>Appends items to the producer slot.</summary>
    /// <param name="items">Items to append.</param>
    /// <returns>Elements accepted; may be fewer than <paramref name="items"/>.Length when full.</returns>
    public int WriteRange(ReadOnlySpan<T> items)
    {
        var count = Volatile.Read(ref _counts[_writeIndex]);
        var room = Capacity - count;
        var take = items.Length < room ? items.Length : room;
        if (take > 0)
        {
            items.Slice(0, take).CopyTo(_slots[_writeIndex].AsSpan(count, take));
            Volatile.Write(ref _counts[_writeIndex], count + take);
        }

        return take;
    }

    /// <summary>Publishes the producer slot and takes the next clean one.</summary>
    /// <remarks>
    /// The generation is bumped before the handoff exchange, so a consumer that observes the new
    /// clean index also observes the new generation.
    /// </remarks>
    public void SwapProducer()
    {
        var published = _writeIndex;
        Volatile.Write(ref _generations[published], Interlocked.Increment(ref _nextGeneration));
        var next = Interlocked.Exchange(ref _cleanIndex, published);
        _writeIndex = next;
        Volatile.Write(ref _counts[_writeIndex], 0);
    }

    /// <summary>Takes the latest published snapshot.</summary>
    /// <returns>Consumer view and its generation; empty when nothing was published.</returns>
    /// <remarks>
    /// Freshness is decided by generation, not by slot index: a repeated acquire with no new publish
    /// keeps returning the same read slot instead of bouncing back to the recycled clean slot.
    /// </remarks>
    public TripleBufferSnapshot<T> Acquire()
    {
        var clean = Volatile.Read(ref _cleanIndex);
        var read = _readIndex;
        if (Volatile.Read(ref _generations[clean]) > Volatile.Read(ref _generations[read]))
        {
            var stale = Interlocked.Exchange(ref _cleanIndex, read);
            _readIndex = stale;
        }

        var count = Volatile.Read(ref _counts[_readIndex]);
        var generation = Volatile.Read(ref _generations[_readIndex]);
        return new TripleBufferSnapshot<T>(new ReadOnlyMemory<T>(_slots[_readIndex], 0, count), generation);
    }

    /// <summary>Whether a snapshot is still the latest view of its slot.</summary>
    /// <param name="snapshot">Snapshot to validate.</param>
    /// <returns><see langword="false"/> when the consumer moved on (a later <see cref="Acquire"/>)
    /// or the slot was recycled since the acquire.</returns>
    /// <remarks>
    /// Generations come from one global counter, so a generation identifies a single publish. A
    /// snapshot is valid exactly while its generation still labels the current read slot.
    /// </remarks>
    public bool IsStillValid(in TripleBufferSnapshot<T> snapshot) =>
        snapshot.Generation == Volatile.Read(ref _generations[_readIndex]);
}

/// <summary>Snapshot of a <see cref="TripleBuffer{T}"/> consumer slot.</summary>
/// <typeparam name="T">Element type.</typeparam>
/// <remarks>
/// A snapshot borrows the consumer slot; the producer must not be allowed to lap it while the
/// snapshot is in use, or <see cref="TripleBuffer{T}.IsStillValid"/> reports the race. Copy the
/// items out if the consumer cannot keep up.
/// </remarks>
public readonly struct TripleBufferSnapshot<T> where T : unmanaged
{
    /// <summary>Items visible at acquire time.</summary>
    public ReadOnlyMemory<T> Items { get; }

    /// <summary>Generation the snapshot was taken at.</summary>
    public long Generation { get; }

    /// <summary>Creates a snapshot.</summary>
    public TripleBufferSnapshot(ReadOnlyMemory<T> items, long generation)
    {
        Items = items;
        Generation = generation;
    }
}
