namespace Axrone.Execution;

using System.Runtime.ExceptionServices;
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
/// processor exceptions are contained rather than left to chance: the drain
/// catches, moves the lifecycle to <see cref="PumpState.Faulted"/>, reports
/// <see cref="IExecutorTelemetry.FaultEncountered"/>, and rethrows the captured
/// <see cref="ExceptionDispatchInfo"/> with its original stack. The
/// <see cref="Pump"/> caller still owns the recovery policy, but the ring is left
/// in a defined, observable state instead of an undefined one.
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
    /// Every state other than <see cref="PumpState.Running"/> — draining,
    /// stopped, <b>faulted</b>, or disposed — refuses through
    /// <see cref="EnqueueStatus.Closed"/>, so a faulted pump never accepts more
    /// work and no producer has to branch on the fault itself.
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
    /// while the ring is full. Cancellation and pump closure abort the wait, and
    /// every retry round that loses the race for a slot reports
    /// <see cref="IExecutorTelemetry.ContentionDetected"/> before backing off.
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

            TTelemetry.ContentionDetected();
            TBackoff.Advance(ref spin);
        }
    }

    /// <summary>
    /// Processes up to <paramref name="maxItems"/> queued commands through
    /// <typeparamref name="TProcessor"/>. SINGLE-CONSUMER ONLY: exactly one
    /// thread may pump. A processor exception is contained: the pump moves to
    /// <see cref="PumpState.Faulted"/>, reports
    /// <see cref="IExecutorTelemetry.FaultEncountered"/>, and rethrows the
    /// original exception with its stack intact. The faulting command stays
    /// claimed (tail does not advance past it), so a retry observes the same head
    /// of queue.
    /// </summary>
    /// <param name="context">Consumer-owned context, threaded by ref.</param>
    /// <param name="maxItems">Maximum commands to process.</param>
    /// <returns>Commands processed.</returns>
    /// <exception cref="Exception">
    /// Whatever the processor threw, rethrown through the captured
    /// <see cref="ExceptionDispatchInfo"/> so the processor frames survive.
    /// </exception>
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

            try
            {
                TProcessor.Process(ref slot->Item, ref context);
            }
            catch (Exception ex)
            {
                ExceptionDispatchInfo fault = ExceptionDispatchInfo.Capture(ex);
                Fault(ex);
                fault.Throw();
                throw; // Fault + captured rethrow above never return; this ends the drain for the compiler too.
            }

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
    /// Idempotent, and inert once the pump is faulted: a fault is terminal until
    /// <see cref="Dispose"/>.
    /// </summary>
    public void Complete()
    {
        int current = Volatile.Read(ref _state);
        if (current == (int)PumpState.Running)
            Interlocked.CompareExchange(ref _state, (int)PumpState.Draining, current);
    }

    /// <summary>
    /// Reports a terminal processor fault: moves the lifecycle to
    /// <see cref="PumpState.Faulted"/> and signals
    /// <see cref="IExecutorTelemetry.FaultEncountered"/>. Producers are refused
    /// from this point on through <see cref="EnqueueStatus.Closed"/>.
    /// </summary>
    /// <param name="exception">The faulting processor's exception.</param>
    /// <remarks>
    /// This method never throws — propagating the fault is the caller's job, so
    /// that the drain can rethrow the original exception through a captured
    /// <see cref="ExceptionDispatchInfo"/> with its stack intact. A pump whose
    /// native memory is already released keeps reporting
    /// <see cref="PumpState.Disposed"/>; the telemetry signal still fires, since
    /// the fault happened either way.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
    public void Fault(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (Volatile.Read(ref _state) != (int)PumpState.Disposed)
            Volatile.Write(ref _state, (int)PumpState.Faulted);

        TTelemetry.FaultEncountered(exception);
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
    /// <exception cref="Exception">
    /// A processor fault propagates out of the drain, leaving the pump
    /// <see cref="PumpState.Faulted"/> with the remaining queue intact.
    /// </exception>
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
