namespace Axrone.Utility.Descriptors;

using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Axrone.Utility.Alignment;
using Axrone.Utility.Backoff;

/// <summary>
/// Generational descriptor table over 64-byte aligned native memory: lock-free
/// allocation through a Vyukov MPMC freelist, ABA-proof handles, in-place access.
/// Backing addresses ride as integers so async lifecycle methods stay in safe code;
/// pointer arithmetic lives in explicitly marked unsafe members only.
/// </summary>
public sealed class DescriptorTable<TDescriptor, TBackoff, TMetrics> : IDisposable, IAsyncDisposable
    where TDescriptor : unmanaged
    where TBackoff : struct, ISpinBackoff
    where TMetrics : struct, IDescriptorMetricsSink
{
    private const int StateActive = 1;
    private const int StateDraining = 2;
    private const int StateTerminated = 3;
    private const int StateFaulted = 4;

    private readonly uint _capacity;
    private readonly uint _mask;

    private readonly nuint _controlWords;
    private readonly nuint _queueCells;
    private readonly nuint _descriptorStorage;
    private readonly int[] _refCounts;

    private AlignedAtomicCounter64 _enqueuePos;
    private AlignedAtomicCounter64 _dequeuePos;
    private AlignedAtomicCounter64 _activeCount;
    private AlignedAtomicCounter64 _lifecycleState;

    private readonly Lock _lifecycleLock = new();
    private ExceptionDispatchInfo? _terminalFault;
    private TaskCompletionSource<bool>? _drainCompletion;
    private int _isDisposed;

    private TMetrics _metrics;

    /// <summary>Slot capacity.</summary>
    public uint Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _capacity;
    }

    /// <summary>Index mask for power-of-two capacities.</summary>
    public uint Mask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _mask;
    }

    /// <summary>Live descriptors.</summary>
    public long ActiveCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _activeCount.Value;
    }

    /// <summary>Creates a table with 64-byte aligned native backing.</summary>
    public DescriptorTable(DescriptorTableOptions? options = null, TMetrics metrics = default)
    {
        options ??= new DescriptorTableOptions();
        _capacity = options.Capacity;
        _mask = _capacity - 1;
        _metrics = metrics;
        _refCounts = new int[_capacity];

        _lifecycleState.Reset();
        _lifecycleState.Add(StateActive);
        _activeCount.Reset();
        _dequeuePos.Reset();

        // The freelist starts pre-filled: cells already carry sequence i + 1 as if
        // N enqueues ran, so the enqueue cursor must start at N to stay in phase.
        // Starting it at zero deadlocks the first free in an infinite spin.
        _enqueuePos.Reset();
        _enqueuePos.Add(_capacity);

        unsafe
        {
            nuint controlBytes = (nuint)_capacity * (nuint)sizeof(long);
            nuint queueBytes = (nuint)_capacity * (nuint)sizeof(MpmcQueueCell);
            nuint storageBytes = (nuint)_capacity * (nuint)sizeof(TDescriptor);

            _controlWords = (nuint)NativeMemory.AlignedAlloc(controlBytes, 64);
            _queueCells = (nuint)NativeMemory.AlignedAlloc(queueBytes, 64);
            _descriptorStorage = (nuint)NativeMemory.AlignedAlloc(storageBytes, 64);

            NativeMemory.Clear((void*)_controlWords, controlBytes);
            NativeMemory.Clear((void*)_descriptorStorage, storageBytes);

            var controls = (long*)_controlWords;
            var cells = (MpmcQueueCell*)_queueCells;
            for (uint i = 0; i < _capacity; i++)
            {
                // Pre-filled freelist: dequeue expects sequence == pos + 1, so fresh
                // cells start at i + 1 (a zero init would read as permanently empty).
                cells[i].Sequence = i + 1;
                cells[i].SlotIndex = i;

                controls[i] = PackControlWord(1, (ushort)DescriptorStatus.Free, 0);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe ref long ControlWord(uint index) => ref ((long*)_controlWords)[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe ref TDescriptor DescriptorAt(uint index) => ref ((TDescriptor*)_descriptorStorage)[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long PackControlWord(uint generation, ushort status, ushort flags) =>
        ((long)generation << 32) | ((long)status << 16) | flags;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UnpackControlWord(long word, out uint generation, out ushort status, out ushort flags)
    {
        generation = (uint)(word >>> 32);
        status = (ushort)(word >>> 16);
        flags = (ushort)word;
    }

    /// <summary>Allocates a slot and copies the descriptor in.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool TryAllocate(in TDescriptor descriptor, out DescriptorHandle<TDescriptor> handle, ushort flags = 0)
    {
        if (_lifecycleState.Value != StateActive)
        {
            handle = DescriptorHandle<TDescriptor>.Invalid;
            return false;
        }

        var cells = (MpmcQueueCell*)_queueCells;
        TBackoff.Initialize(out int spin);
        while (true)
        {
            long pos = _dequeuePos.Value;
            uint cellIndex = (uint)(pos & _mask);
            long seq = Volatile.Read(ref cells[cellIndex].Sequence);
            long diff = seq - (pos + 1);

            if (diff == 0)
            {
                if (_dequeuePos.CompareExchange(pos + 1, pos))
                {
                    uint slotIndex = cells[cellIndex].SlotIndex;
                    Volatile.Write(ref cells[cellIndex].Sequence, pos + _mask + 1);

                    long currentWord = Volatile.Read(ref ControlWord(slotIndex));
                    UnpackControlWord(currentWord, out uint generation, out _, out _);

                    long newWord = PackControlWord(generation, (ushort)DescriptorStatus.Allocated, flags);
                    Volatile.Write(ref ControlWord(slotIndex), newWord);

                    DescriptorAt(slotIndex) = descriptor;

                    _activeCount.Increment();

                    handle = new DescriptorHandle<TDescriptor>(slotIndex, generation);
                    _metrics.OnAllocated(slotIndex);
                    return true;
                }
            }
            else if (diff < 0)
            {
                _metrics.OnExhausted();
                handle = DescriptorHandle<TDescriptor>.Invalid;
                return false;
            }
            else
            {
                TBackoff.Advance(ref spin);
            }
        }
    }

    /// <summary>Allocates an uninitialized slot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryAllocate(out DescriptorHandle<TDescriptor> handle, ushort flags = 0)
    {
        Unsafe.SkipInit(out TDescriptor descriptor);
        return TryAllocate(in descriptor, out handle, flags);
    }

    /// <summary>Allocates a slot, reporting the outcome as a discriminated result.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe AcquisitionResult<TDescriptor> TryAcquire(in TDescriptor descriptor, ushort flags = 0)
    {
        if (_lifecycleState.Value != StateActive)
        {
            return AcquisitionResult<TDescriptor>.Terminated();
        }

        if (TryAllocate(in descriptor, out DescriptorHandle<TDescriptor> handle, flags))
        {
            return AcquisitionResult<TDescriptor>.Ok(handle);
        }

        return _lifecycleState.Value != StateActive
            ? AcquisitionResult<TDescriptor>.Terminated()
            : AcquisitionResult<TDescriptor>.Exhausted();
    }

    /// <summary>Allocates an uninitialized slot, reporting the outcome as a discriminated result.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe AcquisitionResult<TDescriptor> TryAcquire(ushort flags = 0)
    {
        Unsafe.SkipInit(out TDescriptor descriptor);
        return TryAcquire(in descriptor, flags);
    }

    /// <summary>
    /// Reclaims a handle: generation bumps (wrapping past max), the slot returns to
    /// the freelist, and waiter release fires when the last descriptor drains.
    /// A slot with live leases retires instead and frees on the last release.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool TryFree(in DescriptorHandle<TDescriptor> handle)
    {
        if (handle.SlotIndex >= _capacity || !handle.IsValid)
        {
            return false;
        }

        long current = Volatile.Read(ref ControlWord(handle.SlotIndex));
        UnpackControlWord(current, out uint generation, out ushort status, out _);

        if (generation != handle.Generation
            || status == (ushort)DescriptorStatus.Free
            || status == (ushort)DescriptorStatus.Retired)
        {
            return false;
        }

        if (Volatile.Read(ref _refCounts[handle.SlotIndex]) > 0)
        {
            long retired = PackControlWord(generation, (ushort)DescriptorStatus.Retired, 0);
            if (Interlocked.CompareExchange(ref ControlWord(handle.SlotIndex), retired, current) != current)
            {
                return false;
            }

            if (Volatile.Read(ref _refCounts[handle.SlotIndex]) == 0)
            {
                long marked = Volatile.Read(ref ControlWord(handle.SlotIndex));
                UnpackControlWord(marked, out uint markedGeneration, out ushort markedStatus, out _);

                if (markedGeneration == generation && markedStatus == (ushort)DescriptorStatus.Retired)
                {
                    return FreeSlot(handle.SlotIndex, marked, generation);
                }
            }

            return true;
        }

        return FreeSlot(handle.SlotIndex, current, generation);
    }

    /// <summary>Completes the reclaim: bumps the generation and returns the slot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private unsafe bool FreeSlot(uint slotIndex, long currentWord, uint generation)
    {
        var cells = (MpmcQueueCell*)_queueCells;
        uint nextGeneration = generation == uint.MaxValue ? 1 : generation + 1;
        long next = PackControlWord(nextGeneration, (ushort)DescriptorStatus.Free, 0);

        if (Interlocked.CompareExchange(ref ControlWord(slotIndex), next, currentWord) != currentWord)
        {
            return false;
        }

        DescriptorAt(slotIndex) = default;

        TBackoff.Initialize(out int spin);
        while (true)
        {
            long pos = _enqueuePos.Value;
            uint cellIndex = (uint)(pos & _mask);
            long seq = Volatile.Read(ref cells[cellIndex].Sequence);
            long diff = seq - pos;

            if (diff == 0)
            {
                if (_enqueuePos.CompareExchange(pos + 1, pos))
                {
                    cells[cellIndex].SlotIndex = slotIndex;
                    Volatile.Write(ref cells[cellIndex].Sequence, pos + 1);
                    break;
                }
            }
            else
            {
                TBackoff.Advance(ref spin);
            }
        }

        _metrics.OnFreed(slotIndex);

        if (_activeCount.Decrement() == 0 && _lifecycleState.Value >= StateDraining)
        {
            SignalDrainCompleted();
        }

        return true;
    }

    /// <summary>Borrows a live slot as a stack-bound lease.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool TryBorrow(
        in DescriptorHandle<TDescriptor> handle,
        out DescriptorLease<TDescriptor, TBackoff, TMetrics> lease)
    {
        lease = default;

        if (!handle.IsValid || handle.SlotIndex >= _capacity)
        {
            return false;
        }

        long current = Volatile.Read(ref ControlWord(handle.SlotIndex));
        UnpackControlWord(current, out uint generation, out ushort status, out _);

        if (generation != handle.Generation
            || status == (ushort)DescriptorStatus.Free
            || status == (ushort)DescriptorStatus.Retired)
        {
            return false;
        }

        Interlocked.Increment(ref _refCounts[handle.SlotIndex]);

        long recheck = Volatile.Read(ref ControlWord(handle.SlotIndex));
        UnpackControlWord(recheck, out uint freshGeneration, out ushort freshStatus, out _);

        if (freshGeneration != handle.Generation
            || freshStatus == (ushort)DescriptorStatus.Free
            || freshStatus == (ushort)DescriptorStatus.Retired)
        {
            Interlocked.Decrement(ref _refCounts[handle.SlotIndex]);
            return false;
        }

        lease = new DescriptorLease<TDescriptor, TBackoff, TMetrics>(
            this,
            new SlotIndex(handle.SlotIndex),
            new Generation(handle.Generation));
        return true;
    }

    /// <summary>Releases a lease; completes a retired slot on last release.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal unsafe void ReleaseLease(SlotIndex index, Generation generation)
    {
        uint slot = index.Value;
        if (slot >= _capacity)
        {
            ThrowHelper.ThrowInvalidOperationException("The descriptor lease names a slot outside the table.");
        }

        int remaining = Interlocked.Decrement(ref _refCounts[slot]);
        if (remaining < 0)
        {
            Interlocked.Increment(ref _refCounts[slot]);
            ThrowHelper.ThrowInvalidOperationException("The descriptor lease was released more than once.");
        }

        if (remaining != 0)
        {
            return;
        }

        long current = Volatile.Read(ref ControlWord(slot));
        UnpackControlWord(current, out uint currentGeneration, out ushort status, out _);

        if (currentGeneration == generation.Value && status == (ushort)DescriptorStatus.Retired)
        {
            FreeSlot(slot, current, currentGeneration);
        }
    }

    /// <summary>Zero-copy payload for a leased slot; throws when the slot moved on.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal unsafe ref readonly TDescriptor GetLeasedRef(SlotIndex index, Generation generation)
    {
        uint slot = index.Value;
        if (slot < _capacity)
        {
            long current = Volatile.Read(ref ControlWord(slot));
            UnpackControlWord(current, out uint currentGeneration, out ushort status, out _);

            if (currentGeneration == generation.Value && status != (ushort)DescriptorStatus.Free)
            {
                return ref DescriptorAt(slot);
            }
        }

        ThrowHelper.ThrowInvalidOperationException("The descriptor lease no longer names a live slot.");
        return ref Unsafe.NullRef<TDescriptor>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SignalDrainCompleted()
    {
        lock (_lifecycleLock)
        {
            if (_lifecycleState.Value == StateDraining)
            {
                _lifecycleState.Reset();
                _lifecycleState.Add(StateTerminated);
                _drainCompletion?.TrySetResult(true);
            }
        }
    }

    /// <summary>Copies the payload out.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool TryGet(in DescriptorHandle<TDescriptor> handle, out TDescriptor descriptor)
    {
        if (handle.SlotIndex < _capacity && handle.IsValid)
        {
            long current = Volatile.Read(ref ControlWord(handle.SlotIndex));
            UnpackControlWord(current, out uint generation, out ushort status, out _);

            if (generation == handle.Generation && status != (ushort)DescriptorStatus.Free)
            {
                descriptor = DescriptorAt(handle.SlotIndex);
                return true;
            }
        }

        descriptor = default;
        return false;
    }

    /// <summary>Copies the payload with its observed status, failing on torn reads.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool TrySnapshot(in DescriptorHandle<TDescriptor> handle, out DescriptorSnapshot<TDescriptor> snapshot)
    {
        if (handle.SlotIndex < _capacity && handle.IsValid)
        {
            long before = Volatile.Read(ref ControlWord(handle.SlotIndex));
            UnpackControlWord(before, out uint generation, out ushort status, out _);

            if (generation == handle.Generation && status != (ushort)DescriptorStatus.Free)
            {
                TDescriptor descriptor = DescriptorAt(handle.SlotIndex);
                long after = Volatile.Read(ref ControlWord(handle.SlotIndex));

                if (before == after)
                {
                    snapshot = new DescriptorSnapshot<TDescriptor>(handle, descriptor, (DescriptorStatus)status);
                    return true;
                }
            }
        }

        snapshot = default;
        return false;
    }

    /// <summary>Allocates a run of descriptors, stopping at the first failure.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe int AllocateBatch(
        Span<DescriptorHandle<TDescriptor>> outHandles,
        ReadOnlySpan<TDescriptor> descriptors,
        ushort flags = 0)
    {
        int limit = Math.Min(descriptors.Length, outHandles.Length);
        int acquired = 0;

        for (int i = 0; i < limit; i++)
        {
            if (!TryAllocate(in descriptors[i], out outHandles[i], flags))
            {
                break;
            }

            acquired++;
        }

        return acquired;
    }

    /// <summary>Zero-copy readonly reference into table memory.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe ref readonly TDescriptor GetRef(in DescriptorHandle<TDescriptor> handle)
    {
        if (handle.SlotIndex < _capacity && handle.IsValid)
        {
            long current = Volatile.Read(ref ControlWord(handle.SlotIndex));
            UnpackControlWord(current, out uint generation, out ushort status, out _);

            if (generation == handle.Generation && status != (ushort)DescriptorStatus.Free)
            {
                return ref DescriptorAt(handle.SlotIndex);
            }
        }

        ThrowHelper.ThrowInvalidOperationException("The requested descriptor handle is invalid, stale, or already reclaimed.");
        return ref Unsafe.NullRef<TDescriptor>();
    }

    /// <summary>Overwrites the payload in place.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool TryUpdate(in DescriptorHandle<TDescriptor> handle, in TDescriptor updated)
    {
        if (handle.SlotIndex < _capacity && handle.IsValid)
        {
            long current = Volatile.Read(ref ControlWord(handle.SlotIndex));
            UnpackControlWord(current, out uint generation, out ushort status, out _);

            if (generation == handle.Generation && status != (ushort)DescriptorStatus.Free)
            {
                DescriptorAt(handle.SlotIndex) = updated;
                return true;
            }
        }

        return false;
    }

    /// <summary>Runs a static mutator in place (ref structs allowed).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool TryMutate<TMutator, TContext>(in DescriptorHandle<TDescriptor> handle, scoped ref TContext context)
        where TMutator : struct, IDescriptorMutator<TDescriptor, TContext>
        where TContext : allows ref struct
    {
        if (handle.SlotIndex < _capacity && handle.IsValid)
        {
            long current = Volatile.Read(ref ControlWord(handle.SlotIndex));
            UnpackControlWord(current, out uint generation, out ushort status, out _);

            if (generation == handle.Generation && status != (ushort)DescriptorStatus.Free)
            {
                TMutator.Mutate(ref DescriptorAt(handle.SlotIndex), ref context);
                return true;
            }
        }

        return false;
    }

    /// <summary>Runs a static zero-copy reader (ref structs allowed).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool TryAccess<TAccessor, TContext>(in DescriptorHandle<TDescriptor> handle, scoped ref TContext context)
        where TAccessor : struct, IDescriptorAccessor<TDescriptor, TContext>
        where TContext : allows ref struct
    {
        if (handle.SlotIndex < _capacity && handle.IsValid)
        {
            long current = Volatile.Read(ref ControlWord(handle.SlotIndex));
            UnpackControlWord(current, out uint generation, out ushort status, out _);

            if (generation == handle.Generation && status != (ushort)DescriptorStatus.Free)
            {
                TAccessor.Access(in DescriptorAt(handle.SlotIndex), ref context);
                return true;
            }
        }

        return false;
    }

    /// <summary>Atomically changes status (never to Free — use TryFree).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool TrySetStatus(in DescriptorHandle<TDescriptor> handle, DescriptorStatus newStatus)
    {
        if (handle.SlotIndex >= _capacity || !handle.IsValid || newStatus == DescriptorStatus.Free)
        {
            return false;
        }

        if (newStatus == DescriptorStatus.Retired)
        {
            return false;
        }

        long current = Volatile.Read(ref ControlWord(handle.SlotIndex));
        UnpackControlWord(current, out uint generation, out ushort status, out ushort flags);

        if (generation != handle.Generation || status == (ushort)DescriptorStatus.Free)
        {
            return false;
        }

        long next = PackControlWord(generation, (ushort)newStatus, flags);
        if (Interlocked.CompareExchange(ref ControlWord(handle.SlotIndex), next, current) == current)
        {
            _metrics.OnStatusChanged(handle.SlotIndex, newStatus);
            return true;
        }

        return false;
    }

    /// <summary>Reads status; stale handles report Free.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe DescriptorStatus GetStatus(in DescriptorHandle<TDescriptor> handle)
    {
        if (handle.SlotIndex < _capacity && handle.IsValid)
        {
            long current = Volatile.Read(ref ControlWord(handle.SlotIndex));
            UnpackControlWord(current, out uint generation, out ushort status, out _);

            if (generation == handle.Generation)
            {
                return (DescriptorStatus)status;
            }
        }

        return DescriptorStatus.Free;
    }

    /// <summary>Reclaims a batch; returns the freed count.</summary>
    public int FreeBatch(params ReadOnlySpan<DescriptorHandle<TDescriptor>> handles)
    {
        int freed = 0;
        nuint length = (nuint)handles.Length;
        for (nuint i = 0; i < length; i++)
        {
            if (TryFree(handles[(int)i]))
            {
                freed++;
            }
        }

        return freed;
    }

    /// <summary>Transitions to draining (or faulted with an error).</summary>
    public void Complete(Exception? error = null)
    {
        lock (_lifecycleLock)
        {
            if (error is not null)
            {
                _terminalFault = ExceptionDispatchInfo.Capture(error);
                _lifecycleState.Reset();
                _lifecycleState.Add(StateFaulted);
                _metrics.OnFaulted(error);
                _drainCompletion?.TrySetException(error);
                return;
            }

            long state = _lifecycleState.Value;
            if (state == StateActive)
            {
                _lifecycleState.Reset();
                _lifecycleState.Add(StateDraining);
                if (_activeCount.Value == 0)
                {
                    _lifecycleState.Reset();
                    _lifecycleState.Add(StateTerminated);
                    _drainCompletion?.TrySetResult(true);
                }
            }
        }
    }

    /// <summary>Awaits live descriptors to drain with cancellation support.</summary>
    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TaskCompletionSource<bool> completion;
        lock (_lifecycleLock)
        {
            long state = _lifecycleState.Value;
            if (state == StateTerminated)
            {
                return;
            }

            if (state == StateFaulted)
            {
                _terminalFault?.Throw();
                return;
            }

            if (state == StateActive)
            {
                _lifecycleState.Reset();
                _lifecycleState.Add(StateDraining);
            }

            if (_activeCount.Value == 0)
            {
                _lifecycleState.Reset();
                _lifecycleState.Add(StateTerminated);
                return;
            }

            _drainCompletion ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            completion = _drainCompletion;
        }

        if (cancellationToken.CanBeCanceled)
        {
            await using (cancellationToken.UnsafeRegister(static s =>
            {
                var target = (TaskCompletionSource<bool>)s!;
                target.TrySetCanceled();
            }, completion).ConfigureAwait(false))
            {
                await completion.Task.ConfigureAwait(false);
            }
        }
        else
        {
            await completion.Task.ConfigureAwait(false);
        }
    }

    private void ReleaseUnmanagedMemory()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        // AlignedAlloc blocks must pair with AlignedFree — plain Free corrupts the heap.
        unsafe
        {
            NativeMemory.AlignedFree((void*)_controlWords);
            NativeMemory.AlignedFree((void*)_queueCells);
            NativeMemory.AlignedFree((void*)_descriptorStorage);
        }
    }

    /// <summary>
    /// Drains then releases. Blocks until every live descriptor is freed — free
    /// everything first, or teardown waits forever by design (never dangle).
    /// </summary>
    public void Dispose()
    {
        Complete();

        TBackoff.Initialize(out int spin);
        while (_lifecycleState.Value == StateDraining && _activeCount.Value > 0)
        {
            TBackoff.Advance(ref spin);
        }

        ReleaseUnmanagedMemory();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        Complete();
        try
        {
            await DrainAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Terminal fault isolated.
        }
        finally
        {
            ReleaseUnmanagedMemory();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>Backstop for undisposed tables.</summary>
    ~DescriptorTable()
    {
        ReleaseUnmanagedMemory();
    }
}

/// <summary>Vyukov MPMC freelist cell. Top-level because generic owners forbid explicit layout on nested types.</summary>
[StructLayout(LayoutKind.Explicit, Size = 16)]
internal struct MpmcQueueCell
{
    [FieldOffset(0)]
    public long Sequence;

    [FieldOffset(8)]
    public uint SlotIndex;
}
