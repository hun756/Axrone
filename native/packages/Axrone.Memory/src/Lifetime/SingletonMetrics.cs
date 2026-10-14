namespace Axrone.Memory.Lifetime;

/// <summary>
/// Classifies the kind of event a metric record describes.
/// </summary>
public enum MetricKind : byte
{
    /// <summary>The instance was successfully published.</summary>
    InstanceCreated = 0,

    /// <summary>The factory failed while producing the instance.</summary>
    InstanceFaulted = 1,

    /// <summary>A lease was acquired.</summary>
    LeaseAcquired = 2,

    /// <summary>A lease was released.</summary>
    LeaseReleased = 3,

    /// <summary>A consumer began executing under a lease.</summary>
    ExecuteStarted = 4,

    /// <summary>A consumer finished executing under a lease.</summary>
    ExecuteCompleted = 5,

    /// <summary>A drain attempt began.</summary>
    DrainStarted = 6,

    /// <summary>A drain attempt ended.</summary>
    DrainCompleted = 7,

    /// <summary>The engine was permanently disposed.</summary>
    Disposed = 8
}

/// <summary>
/// A single immutable metric observation recorded by the singleton engine.
/// </summary>
/// <param name="Kind">The kind of event observed.</param>
/// <param name="Timestamp">The monotonic timestamp at which the event was recorded.</param>
/// <param name="Iteration">The engine iteration at which the event was recorded.</param>
/// <param name="LeaseCount">The number of leases held when the event was recorded.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct MetricRecord(MetricKind Kind, EpochTimestamp Timestamp, Iteration Iteration, LeaseCount LeaseCount);

/// <summary>
/// Fixed-capacity ring buffer of metric records shared by reference between the engine
/// and its observers. Enumeration is duck-typed: the sequence exposes
/// <see cref="GetEnumerator"/> directly so <see langword="foreach"/> works without
/// allocating an <see cref="IEnumerable{T}"/> wrapper.
/// </summary>
public sealed class MetricSequence
{
    private readonly MetricRecord[] _records;
    private int _head;
    private int _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricSequence"/> class.
    /// </summary>
    /// <param name="capacity">The maximum number of records retained; older records are overwritten.</param>
    public MetricSequence(int capacity)
    {
        if (capacity <= 0)
        {
            SingletonThrowHelper.ThrowInvalidMetricCapacity(capacity);
        }

        _records = new MetricRecord[capacity];
    }

    /// <summary>Gets the maximum number of records the sequence retains.</summary>
    public int Capacity => _records.Length;

    /// <summary>Gets the number of records currently retained.</summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>
    /// Gets the record at the specified logical index, where index zero is the oldest
    /// retained record.
    /// </summary>
    /// <param name="index">The zero-based logical index.</param>
    /// <returns>The record at the specified index.</returns>
    public MetricRecord this[int index]
    {
        get
        {
            var count = Volatile.Read(ref _count);
            if ((uint)index >= (uint)count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The index is outside the retained record range.");
            }

            var head = Volatile.Read(ref _head);
            var capacity = _records.Length;
            return _records[(head + index) % capacity];
        }
    }

    /// <summary>
    /// Appends a record to the sequence, overwriting the oldest record when full.
    /// </summary>
    /// <param name="record">The record to append.</param>
    public void Append(MetricRecord record)
    {
        lock (_records)
        {
            var capacity = _records.Length;
            if (_count < capacity)
            {
                _records[(_head + _count) % capacity] = record;
                _count++;
            }
            else
            {
                _records[_head] = record;
                _head = (_head + 1) % capacity;
            }
        }
    }

    /// <summary>Returns a struct enumerator over the retained records in chronological order.</summary>
    /// <returns>The enumerator.</returns>
    public MetricEnumerator GetEnumerator() => new(this);
}

/// <summary>
/// Struct enumerator over a <see cref="MetricSequence"/>. Being a ref struct, the enumerator
/// keeps the iteration state on the stack and avoids the boxed enumerator allocation.
/// </summary>
public ref struct MetricEnumerator
{
    private readonly MetricSequence _sequence;
    private int _index;

    /// <summary>Initializes a new instance of the <see cref="MetricEnumerator"/> struct.</summary>
    /// <param name="sequence">The sequence to enumerate.</param>
    public MetricEnumerator(MetricSequence sequence)
    {
        _sequence = sequence;
        _index = -1;
    }

    /// <summary>Gets the record at the enumerator's current position.</summary>
    public MetricRecord Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_index < 0)
            {
                throw new InvalidOperationException("The enumerator has not started; call MoveNext first.");
            }

            return _sequence[_index];
        }
    }

    /// <summary>Advances the enumerator to the next record.</summary>
    /// <returns><see langword="true"/> when the enumerator advanced; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        var next = _index + 1;
        if (next >= _sequence.Count)
        {
            return false;
        }

        _index = next;
        return true;
    }
}
