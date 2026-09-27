namespace Axrone.Execution;

using Axrone.Utility.Alignment;

/// <summary>
/// One ring slot: a Vyukov-style sequence gate plus an unmanaged command.
/// Top-level because generic owners forbid explicit layout on nested types.
/// </summary>
/// <typeparam name="TCommand">Command type.</typeparam>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct PumpSlot<TCommand> where TCommand : unmanaged
{
    /// <summary>Sequence gate: slot is claimable when it equals the claim cursor.</summary>
    public long Sequence;

    /// <summary>Command payload. Cleared after processing.</summary>
    public TCommand Item;
}

/// <summary>
/// Generic lock-free command-executor library: a multi-producer / single-consumer
/// ring pump with static-abstract processors, pluggable spin backoff, and
/// cold-path telemetry. Render and engine executors derive from this pump
/// instead of reimplementing rings, backoff, or lifecycle state machines.
/// </summary>
/// <remarks>
/// <para>
/// Threading contract: any number of producer threads may call
/// <see cref="TryEnqueue"/> concurrently (lock-free CAS claim). Exactly ONE
/// consumer thread owns <see cref="Pump"/> — the consumer needs no atomics on
/// the drain path. There is deliberately NO worker thread: the owner pumps on
/// its own thread (for graphics, the render thread that owns the GL context),
/// so context affinity, teardown ordering, and test determinism stay trivial.
/// Producer faults never exist (enqueue is infallible bookkeeping);
/// processor exceptions propagate synchronously to the <see cref="Pump"/>
/// caller, which owns the recovery policy.
/// </para>
/// <para>
/// Backing memory is 64-byte aligned native memory released by
/// <see cref="Dispose"/>. Drain the pump before disposing; undisposed items
/// still queued at dispose time are dropped.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">Command type. Unmanaged so the ring backs onto native memory.</typeparam>
/// <typeparam name="TContext">Consumer context. May be a ref struct.</typeparam>
/// <typeparam name="TProcessor">Static processing kernel. Struct for JIT devirtualization.</typeparam>
/// <typeparam name="TBackoff">Spin policy for blocking enqueue (our Utility backoff).</typeparam>
/// <typeparam name="TTelemetry">Cold-path telemetry sink.</typeparam>
public sealed class CommandPump<TCommand, TContext, TProcessor, TBackoff, TTelemetry> : IDisposable
    where TCommand : unmanaged
    where TContext : allows ref struct
    where TProcessor : struct, ICommandProcessor<TCommand, TContext>
    where TBackoff : struct, ISpinBackoff
    where TTelemetry : struct, IExecutorTelemetry
{
    private readonly nuint _capacity;
    private readonly nuint _mask;
    private readonly nuint _slots;

    private AlignedAtomicCounter64 _head;
    private long _tail;
    private int _state;
    private int _isDisposed;

    /// <summary>Ring slot capacity.</summary>
    public nuint Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _capacity;
    }

    /// <summary>Lifecycle state.</summary>
    public PumpState State
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (PumpState)Volatile.Read(ref _state);
    }

    /// <summary>
    /// Approximate queued depth (head minus consumer tail). Racy by design:
    /// producers observe a stale tail. Exact only when producers are quiescent.
    /// </summary>
    public nuint Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            long diff = _head.Value - Volatile.Read(ref _tail);
            return diff > 0 ? (nuint)diff : 0;
        }
    }

    /// <summary>
    /// Creates a pump. The ring starts pre-filled: every slot sequence reads as
    /// immediately claimable and the lifecycle opens as
    /// <see cref="PumpState.Running"/>.
    /// </summary>
    /// <param name="options">Sizing. Defaults to 1024 slots.</param>
    public unsafe CommandPump(ExecutorOptions? options = null)
    {
        options ??= new ExecutorOptions();
        _capacity = (nuint)options.Capacity;
        _mask = _capacity - 1;

        nuint slotBytes = (nuint)options.Capacity * (nuint)sizeof(PumpSlot<TCommand>);
        _slots = (nuint)NativeMemory.AlignedAlloc(slotBytes, 64);

        var slots = (PumpSlot<TCommand>*)_slots;
        for (nuint i = 0; i < _capacity; i++)
        {
            slots[i].Sequence = (long)i;
            slots[i].Item = default;
        }

        _head.Reset();
        _tail = 0;
        Volatile.Write(ref _state, (int)PumpState.Running);
    }

    /// <summary>
    /// Attempts to enqueue one command. Lock-free; safe from any producer thread.
    /// </summary>
    /// <param name="item">The command to queue.</param>
    /// <returns>The claim receipt (sequence) or the refusal reason.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe EnqueueResult TryEnqueue(in TCommand item)
    {
        if (Volatile.Read(ref _state) != (int)PumpState.Running)
            return new EnqueueResult(-1, EnqueueStatus.Closed);

        var slots = (PumpSlot<TCommand>*)_slots;
        long head = _head.Value;
        while (true)
        {
            nuint index = (nuint)(head & (long)_mask);
            PumpSlot<TCommand>* slot = slots + index;
            long diff = Volatile.Read(ref slot->Sequence) - head;

            if (diff == 0)
            {
                if (_head.CompareExchange(head + 1, head))
                {
                    slot->Item = item;
                    Volatile.Write(ref slot->Sequence, head + 1);
                    TTelemetry.ItemEnqueued();
                    return new EnqueueResult(head, EnqueueStatus.Enqueued);
                }

                head = _head.Value;
            }
            else if (diff < 0)
            {
                TTelemetry.QueueSaturated();
                return new EnqueueResult(-1, EnqueueStatus.QueueFull);
            }
            else
            {
                head = _head.Value;
            }
        }
    }

    /// <summary>
    /// Enqueues a span of commands, stopping at the first refusal.
    /// </summary>
    /// <param name="items">Commands to queue.</param>
    /// <returns>Commands accepted before the first refusal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint TryEnqueueBatch(params ReadOnlySpan<TCommand> items)
    {
        nuint accepted = 0;
        nuint length = (nuint)items.Length;
        for (nuint i = 0; i < length; i++)
        {
            if (!TryEnqueue(in items[(int)i]).IsEnqueued)
                break;
            accepted++;
        }

        return accepted;
    }

    /// <summary>
    /// Enqueues one command, spinning with <typeparamref name="TBackoff"/>
    /// while the ring is full. Cancellation and pump closure abort the wait.
    /// </summary>
    /// <param name="item">The command to queue.</param>
    /// <param name="cancellationToken">Aborts the wait.</param>
    /// <returns>The claim receipt, or a closed refusal.</returns>
    public EnqueueResult Enqueue(in TCommand item, CancellationToken cancellationToken = default)
    {
        TBackoff.Initialize(out int spin);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            EnqueueResult result = TryEnqueue(in item);
            if (result.Status != EnqueueStatus.QueueFull)
                return result;

            TBackoff.Advance(ref spin);
        }
    }

    /// <summary>
    /// Processes up to <paramref name="maxItems"/> queued commands through
    /// <typeparamref name="TProcessor"/>. SINGLE-CONSUMER ONLY: exactly one
    /// thread may pump. Processor exceptions propagate to the caller; the
    /// faulting command stays claimed (tail does not advance past it), so a
    /// retry observes the same head of queue.
    /// </summary>
    /// <param name="context">Consumer-owned context, threaded by ref.</param>
    /// <param name="maxItems">Maximum commands to process.</param>
    /// <returns>Commands processed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe nuint Pump(ref TContext context, nuint maxItems)
    {
        var slots = (PumpSlot<TCommand>*)_slots;
        long tail = _tail;
        nuint processed = 0;

        while (processed < maxItems)
        {
            nuint index = (nuint)(tail & (long)_mask);
            PumpSlot<TCommand>* slot = slots + index;
            if (Volatile.Read(ref slot->Sequence) - (tail + 1) != 0)
                break;

            TProcessor.Process(ref slot->Item, ref context);
            slot->Item = default;
            Volatile.Write(ref slot->Sequence, tail + (long)_mask + 1);
            TTelemetry.ItemDequeued();
            tail++;
            processed++;
        }

        _tail = tail;
        return processed;
    }

    /// <summary>
    /// Processes every queued command. SINGLE-CONSUMER ONLY.
    /// </summary>
    /// <param name="context">Consumer-owned context, threaded by ref.</param>
    /// <returns>Commands processed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint PumpAll(ref TContext context) => Pump(ref context, nuint.MaxValue);

    /// <summary>
    /// Refuses further enqueues; already queued work still pumps.
    /// Idempotent.
    /// </summary>
    public void Complete()
    {
        int current = Volatile.Read(ref _state);
        if (current == (int)PumpState.Running)
            Interlocked.CompareExchange(ref _state, (int)PumpState.Draining, current);
    }

    /// <summary>
    /// Completes, then pumps until the ring is empty (re-checking the head so
    /// claims published between completion and drain are not stranded), and
    /// parks the lifecycle at <see cref="PumpState.Stopped"/>.
    /// SINGLE-CONSUMER ONLY.
    /// </summary>
    /// <param name="context">Consumer-owned context, threaded by ref.</param>
    /// <param name="cancellationToken">Aborts the wait.</param>
    /// <returns>Commands processed during the drain.</returns>
    public nuint Drain(ref TContext context, CancellationToken cancellationToken = default)
    {
        Complete();

        nuint total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += PumpAll(ref context);
            if (_head.Value == Volatile.Read(ref _tail))
                break;
        }

        int current = Volatile.Read(ref _state);
        if (current == (int)PumpState.Draining)
            Interlocked.CompareExchange(ref _state, (int)PumpState.Stopped, current);

        return total;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        Volatile.Write(ref _state, (int)PumpState.Disposed);
        unsafe
        {
            NativeMemory.AlignedFree((void*)_slots);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Backstop for undisposed pumps. Drain first; queued items are dropped.</summary>
    ~CommandPump()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        unsafe
        {
            NativeMemory.AlignedFree((void*)_slots);
        }
    }
}
