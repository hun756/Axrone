namespace Axrone.Collections;

[StructLayout(LayoutKind.Explicit, Size = 256)]
internal struct PaddedRingPosition
{
    [FieldOffset(0)]
    public long EnqueuePosition;

    [FieldOffset(128)]
    public long DequeuePosition;
}

public sealed class MpmcRingBuffer<T> : IRingBuffer<T>
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct Slot
    {
        public long Sequence;
        public T Value;
    }

    private sealed class ProducerEndpoint : IProducer<T>
    {
        private readonly MpmcRingBuffer<T> _buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ProducerEndpoint(MpmcRingBuffer<T> buffer) => _buffer = buffer;

        public int Capacity => _buffer.Capacity;
        public int Count => _buffer.Count;
        public bool IsFull => _buffer.IsFull;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(in T item) => _buffer.TryEnqueue(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RingBufferOperationStatus Enqueue(in T item, TimeSpan timeout, CancellationToken cancellationToken = default)
            => _buffer.Enqueue(item, timeout, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(in T item, CancellationToken cancellationToken = default)
            => _buffer.Enqueue(item, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int EnqueueRange(ReadOnlySpan<T> source) => _buffer.EnqueueRange(source);
    }

    private sealed class ConsumerEndpoint : IConsumer<T>
    {
        private readonly MpmcRingBuffer<T> _buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConsumerEndpoint(MpmcRingBuffer<T> buffer) => _buffer = buffer;

        public int Capacity => _buffer.Capacity;
        public int Count => _buffer.Count;
        public bool IsEmpty => _buffer.IsEmpty;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue([MaybeNullWhen(false)] out T item) => _buffer.TryDequeue(out item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RingBufferResult<T> Dequeue(TimeSpan timeout, CancellationToken cancellationToken = default)
            => _buffer.Dequeue(timeout, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Dequeue(CancellationToken cancellationToken = default)
            => _buffer.Dequeue(cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DequeueRange(Span<T> destination) => _buffer.DequeueRange(destination);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DrainTo(Span<T> destination) => _buffer.DrainTo(destination);
    }

    private const int StateActive = 0;
    private const int StateDisposed = 1;

    private readonly Slot[] _slots;
    private readonly int _capacity;
    private readonly long _mask;
    private readonly IWaitStrategy _waitStrategy;
    private readonly bool _autoClearOnDispose;

    private readonly ProducerEndpoint _producerEndpoint;
    private readonly ConsumerEndpoint _consumerEndpoint;

    private PaddedRingPosition _positions;
    private int _state;

    public MpmcRingBuffer(int capacity)
        : this(new RingBufferOptions { Capacity = capacity })
    {
    }

    public MpmcRingBuffer(RingBufferOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Capacity < 2)
        {
            ThrowCapacityOutOfRange(options.Capacity);
        }

        int actualCapacity = (int)BitOperations.RoundUpToPowerOf2((uint)options.Capacity);
        if (actualCapacity < 2)
        {
            ThrowCapacityTooLarge(options.Capacity);
        }

        _capacity = actualCapacity;
        _mask = actualCapacity - 1;
        _waitStrategy = options.WaitStrategy ?? new AdaptiveWaitStrategy();
        _autoClearOnDispose = options.AutoClearOnDispose;

        _slots = new Slot[actualCapacity];
        for (int i = 0; i < actualCapacity; i++)
        {
            _slots[i].Sequence = i;
        }

        _positions.EnqueuePosition = 0;
        _positions.DequeuePosition = 0;
        _state = StateActive;

        _producerEndpoint = new ProducerEndpoint(this);
        _consumerEndpoint = new ConsumerEndpoint(this);
    }

    public int Capacity => _capacity;

    public int Count
    {
        get
        {
            SpinWait spinner = new();
            while (true)
            {
                long headBefore = Volatile.Read(ref _positions.DequeuePosition);
                long tail = Volatile.Read(ref _positions.EnqueuePosition);
                long headAfter = Volatile.Read(ref _positions.DequeuePosition);

                if (headBefore == headAfter)
                {
                    long diff = tail - headBefore;
                    if (diff < 0) return 0;
                    if (diff > _capacity) return _capacity;
                    return (int)diff;
                }

                spinner.SpinOnce();
            }
        }
    }

    public bool IsEmpty => Count == 0;
    public bool IsFull => Count >= _capacity;
    public bool IsDisposed => Volatile.Read(ref _state) != StateActive;
    public IProducer<T> Producer => _producerEndpoint;
    public IConsumer<T> Consumer => _consumerEndpoint;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnqueue(in T item)
    {
        ThrowIfDisposed();

        Slot[] slots = _slots;
        long mask = _mask;
        long pos = Volatile.Read(ref _positions.EnqueuePosition);

        while (true)
        {
            ref Slot slot = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(slots), (nint)(pos & mask));
            long seq = Volatile.Read(ref slot.Sequence);
            long diff = seq - pos;

            if (diff == 0)
            {
                long actualPos = Interlocked.CompareExchange(ref _positions.EnqueuePosition, pos + 1, pos);
                if (actualPos == pos)
                {
                    slot.Value = item;
                    Volatile.Write(ref slot.Sequence, pos + 1);
                    return true;
                }
                pos = actualPos;
            }
            else if (diff < 0)
            {
                return false;
            }
            else
            {
                pos = Volatile.Read(ref _positions.EnqueuePosition);
            }
        }
    }

    public RingBufferOperationStatus Enqueue(in T item, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (TryEnqueue(item)) return RingBufferOperationStatus.Success;
        if (timeout == TimeSpan.Zero) return RingBufferOperationStatus.Full;

        long startTimestamp = Stopwatch.GetTimestamp();
        IWaitStrategy waitStrategy = _waitStrategy;
        waitStrategy.Reset();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (timeout != Timeout.InfiniteTimeSpan && Stopwatch.GetElapsedTime(startTimestamp) >= timeout)
                return RingBufferOperationStatus.Timeout;

            waitStrategy.Wait();
            if (TryEnqueue(item)) return RingBufferOperationStatus.Success;
        }
    }

    public void Enqueue(in T item, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (TryEnqueue(item)) return;

        IWaitStrategy waitStrategy = _waitStrategy;
        waitStrategy.Reset();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            waitStrategy.Wait();
            if (TryEnqueue(item)) return;
        }
    }

    public int EnqueueRange(ReadOnlySpan<T> source)
    {
        ThrowIfDisposed();
        int written = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (!TryEnqueue(source[i])) break;
            written++;
        }
        return written;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue([MaybeNullWhen(false)] out T item)
    {
        ThrowIfDisposed();
        return TryDequeueInternal(out item);
    }

    public RingBufferResult<T> Dequeue(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (TryDequeue(out T? immediateItem))
            return RingBufferResult<T>.Success(immediateItem);

        if (timeout == TimeSpan.Zero)
            return RingBufferResult<T>.Failure(RingBufferOperationStatus.Empty);

        long startTimestamp = Stopwatch.GetTimestamp();
        IWaitStrategy waitStrategy = _waitStrategy;
        waitStrategy.Reset();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (timeout != Timeout.InfiniteTimeSpan && Stopwatch.GetElapsedTime(startTimestamp) >= timeout)
                return RingBufferResult<T>.Failure(RingBufferOperationStatus.Timeout);

            waitStrategy.Wait();
            if (TryDequeue(out T? item))
                return RingBufferResult<T>.Success(item);
        }
    }

    public T Dequeue(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (TryDequeue(out T? immediateItem)) return immediateItem;

        IWaitStrategy waitStrategy = _waitStrategy;
        waitStrategy.Reset();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            waitStrategy.Wait();
            if (TryDequeue(out T? item)) return item;
        }
    }

    public int DequeueRange(Span<T> destination)
    {
        ThrowIfDisposed();
        int read = 0;
        for (int i = 0; i < destination.Length; i++)
        {
            if (!TryDequeue(out destination[i])) break;
            read++;
        }
        return read;
    }

    public int DrainTo(Span<T> destination)
    {
        ThrowIfDisposed();
        int drained = 0;
        while (drained < destination.Length && TryDequeue(out destination[drained]))
            drained++;
        return drained;
    }

    public void Clear()
    {
        ThrowIfDisposed();
        while (TryDequeueInternal(out _)) { }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _state, StateDisposed) != StateActive) return;

        if (_autoClearOnDispose)
        {
            while (TryDequeueInternal(out _)) { }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryDequeueInternal([MaybeNullWhen(false)] out T item)
    {
        Slot[] slots = _slots;
        long mask = _mask;
        long pos = Volatile.Read(ref _positions.DequeuePosition);

        while (true)
        {
            ref Slot slot = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(slots), (nint)(pos & mask));
            long seq = Volatile.Read(ref slot.Sequence);
            long diff = seq - (pos + 1);

            if (diff == 0)
            {
                long actualPos = Interlocked.CompareExchange(ref _positions.DequeuePosition, pos + 1, pos);
                if (actualPos == pos)
                {
                    item = slot.Value;
                    if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                        slot.Value = default!;
                    Volatile.Write(ref slot.Sequence, pos + mask + 1);
                    return true;
                }
                pos = actualPos;
            }
            else if (diff < 0)
            {
                item = default;
                return false;
            }
            else
            {
                pos = Volatile.Read(ref _positions.DequeuePosition);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _state) != StateActive) ThrowObjectDisposed();
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowObjectDisposed() =>
        throw new ObjectDisposedException(typeof(MpmcRingBuffer<T>).FullName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCapacityOutOfRange(int capacity) =>
        throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than or equal to 2.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCapacityTooLarge(int capacity) =>
        throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Requested capacity exceeds maximum allocatable power-of-two size.");
}
