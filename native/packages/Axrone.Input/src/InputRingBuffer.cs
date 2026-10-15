namespace Axrone.Input;

/// <summary>
/// Lock-free multi-producer multi-consumer ring buffer for raw input events
/// (Vyukov bounded queue). Producer threads (OS callbacks) never block the
/// consumer (frame loop). Backing memory lives in cache-line-aligned native
/// memory; steady-state enqueue/dequeue allocates nothing.
/// </summary>
/// <typeparam name="TBackoff">Contention backoff policy.</typeparam>
/// <typeparam name="TMetrics">Queue telemetry sink.</typeparam>
public sealed class LockFreeInputRingBuffer<TBackoff, TMetrics> :
    IInputEventQueue, IAsyncDisposable
    where TBackoff : struct, IBackoffPolicy
    where TMetrics : struct, IInputMetricsSink
{
    private const int CacheLineSize = 64;

    /// <summary>One queue cell: sequence gate plus payload.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    private struct Slot
    {
        public nint Sequence;
        public RawInputEvent Item;
        private readonly long _padding;
    }

    /// <summary>Head/tail cursors on isolated cache lines (sequential with
    /// manual padding: explicit layout is illegal on generic types).</summary>
    [StructLayout(LayoutKind.Sequential, Size = CacheLineSize * 4)]
    private struct QueueMetadata
    {
        /// <summary>Producer cursor.</summary>
        public nint EnqueuePos;
        private readonly long _padE0;
        private readonly long _padE1;
        private readonly long _padE2;
        private readonly long _padE3;
        private readonly long _padE4;
        private readonly long _padE5;
        private readonly long _padE6;

        /// <summary>Consumer cursor.</summary>
        public nint DequeuePos;
        private readonly long _padD0;
        private readonly long _padD1;
        private readonly long _padD2;
        private readonly long _padD3;
        private readonly long _padD4;
        private readonly long _padD5;
        private readonly long _padD6;

        /// <summary>Index mask.</summary>
        public nuint Mask;

        /// <summary>Slot count.</summary>
        public nuint Capacity;
        private readonly long _padM0;
        private readonly long _padM1;
        private readonly long _padM2;
        private readonly long _padM3;
        private readonly long _padM4;
        private readonly long _padM5;

        /// <summary>Dispose latch.</summary>
        public int Disposed;

        /// <summary>Completion latch.</summary>
        public int Completed;
        private readonly long _padC0;
        private readonly long _padC1;
        private readonly long _padC2;
        private readonly long _padC3;
        private readonly long _padC4;
        private readonly long _padC5;
        private readonly long _padC6;
    }

    private readonly nint _slots;
    private readonly nint _metadata;

    /// <summary>Queue capacity.</summary>
    public QueueCapacity Capacity { get; }

    /// <summary>Creates a queue with the given capacity.</summary>
    public LockFreeInputRingBuffer(QueueCapacity capacity)
    {
        Capacity = capacity;

        unsafe
        {
            var metadata = (QueueMetadata*)NativeMemory.AlignedAlloc((nuint)sizeof(QueueMetadata), CacheLineSize);
            NativeMemory.Clear(metadata, (nuint)sizeof(QueueMetadata));

            metadata->Mask = capacity.Mask;
            metadata->Capacity = capacity.Value;
            metadata->EnqueuePos = 0;
            metadata->DequeuePos = 0;
            metadata->Disposed = 0;
            metadata->Completed = 0;
            _metadata = (nint)metadata;

            nuint totalSlotBytes = (nuint)(capacity.Value * sizeof(Slot));
            var slots = (Slot*)NativeMemory.AlignedAlloc(totalSlotBytes, CacheLineSize);

            for (nuint i = 0; i < capacity.Value; i++)
            {
                slots[i].Sequence = (nint)i;
                slots[i].Item = default;
            }

            _slots = (nint)slots;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWrite(in RawInputEvent evt) => TryEnqueue(in evt);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead(out RawInputEvent evt) => TryDequeue(out evt);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryEnqueue(in RawInputEvent evt)
    {
        unsafe
        {
            var slots = (Slot*)_slots;
            var metadata = (QueueMetadata*)_metadata;
            nuint mask = metadata->Mask;
            int spinCount = 0;

            nint pos = Volatile.Read(ref metadata->EnqueuePos);

            while (true)
            {
                if (Volatile.Read(ref metadata->Disposed) != 0 || Volatile.Read(ref metadata->Completed) != 0)
                {
                    return false;
                }

                Slot* slot = &slots[(nuint)pos & mask];
                nint seq = Volatile.Read(ref slot->Sequence);
                nint diff = seq - pos;

                if (diff == 0)
                {
                    if (Interlocked.CompareExchange(ref metadata->EnqueuePos, pos + 1, pos) == pos)
                    {
                        slot->Item = evt;
                        Volatile.Write(ref slot->Sequence, pos + 1);
                        TMetrics.OnEventEnqueued(evt.EventType);
                        return true;
                    }
                }
                else if (diff < 0)
                {
                    TMetrics.OnEventDropped(evt.EventType);
                    return false;
                }
                else
                {
                    pos = Volatile.Read(ref metadata->EnqueuePos);
                }

                TBackoff.SpinOnce(ref spinCount);
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryDequeue(out RawInputEvent evt)
    {
        unsafe
        {
            var slots = (Slot*)_slots;
            var metadata = (QueueMetadata*)_metadata;
            nuint mask = metadata->Mask;
            int spinCount = 0;

            nint pos = Volatile.Read(ref metadata->DequeuePos);

            while (true)
            {
                Slot* slot = &slots[(nuint)pos & mask];
                nint seq = Volatile.Read(ref slot->Sequence);
                nint diff = seq - (pos + 1);

                if (diff == 0)
                {
                    if (Interlocked.CompareExchange(ref metadata->DequeuePos, pos + 1, pos) == pos)
                    {
                        evt = slot->Item;
                        Volatile.Write(ref slot->Sequence, pos + (nint)mask + 1);
                        return true;
                    }
                }
                else if (diff < 0)
                {
                    evt = default;
                    return false;
                }
                else
                {
                    pos = Volatile.Read(ref metadata->DequeuePos);
                }

                TBackoff.SpinOnce(ref spinCount);
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int Drain(Span<RawInputEvent> destination)
    {
        int count = 0;
        nuint limit = (nuint)destination.Length;

        while ((nuint)count < limit && TryDequeue(out destination[count]))
        {
            count++;
        }

        if (count > 0)
        {
            TMetrics.OnEventsDrained(count);
        }

        return count;
    }

    /// <inheritdoc/>
    public void Complete(Exception? failure = null)
    {
        unsafe
        {
            var metadata = (QueueMetadata*)_metadata;
            Volatile.Write(ref metadata->Completed, 1);
        }
    }

    /// <summary>Whether queued events remain.</summary>
    private bool HasPending()
    {
        unsafe
        {
            var metadata = (QueueMetadata*)_metadata;
            return Volatile.Read(ref metadata->EnqueuePos) != Volatile.Read(ref metadata->DequeuePos);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        SpinWait spinner = new();
        while (HasPending())
        {
            cancellationToken.ThrowIfCancellationRequested();
            spinner.SpinOnce();
            if (spinner.NextSpinWillYield)
            {
                await Task.Yield();
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        unsafe
        {
            var metadata = (QueueMetadata*)_metadata;
            if (Interlocked.Exchange(ref metadata->Disposed, 1) == 0)
            {
                NativeMemory.AlignedFree((void*)_slots);
                NativeMemory.AlignedFree((void*)_metadata);
                GC.SuppressFinalize(this);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        Complete();
        try
        {
            await DrainAsync().ConfigureAwait(false);
        }
        finally
        {
            Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>Finalizer frees native memory when Dispose was skipped.</summary>
    ~LockFreeInputRingBuffer()
    {
        unsafe
        {
            var metadata = (QueueMetadata*)_metadata;
            if (metadata != null && Volatile.Read(ref metadata->Disposed) == 0)
            {
                NativeMemory.AlignedFree((void*)_slots);
                NativeMemory.AlignedFree((void*)_metadata);
            }
        }
    }
}
