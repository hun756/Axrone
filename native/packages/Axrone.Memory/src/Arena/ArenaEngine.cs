namespace Axrone.Memory.Arena;

/// <summary>
/// Unified bump arena: a linked list of 64-byte-header chunks with geometric
/// growth, standby-list reuse, lock-free fast-path allocation and a
/// complete/drain/dispose lifecycle. Allocations are addressed by markers and
/// rewound in bulk; individual frees do not exist. Fixed-capacity behavior
/// (the old linear arena) is the <see cref="FixedGrowthPolicy"/> with the
/// ceiling pinned to the initial size.
/// </summary>
/// <typeparam name="TGrowth">Chunk growth policy.</typeparam>
/// <typeparam name="TBackoff">Contention backoff policy.</typeparam>
/// <typeparam name="TMetrics">Chunk lifetime metrics sink.</typeparam>
public class Arena<TGrowth, TBackoff, TMetrics> : IMemoryArena
    where TGrowth : struct, IArenaGrowthPolicy
    where TBackoff : struct, IBackoffPolicy
    where TMetrics : struct, IArenaMetricsSink
{
    private static class StateConstants
    {
        public const int Active = 0;
        public const int Reclaiming = 1;
        public const int Draining = 2;
        public const int Faulted = 3;
        public const int Disposed = 4;
    }

    private const nuint ChunkHeaderSize = 64;
    private const nuint HardwareCacheAlignment = 64;

    private readonly Lock _sync = new();
    private PaddedArenaState _state;
    private nint _chunksHead;
    private nint _standbyHead;
    private long _totalCommitted;
    private ExceptionDispatchInfo? _faultInfo;

    /// <inheritdoc/>
    public ByteSize TotalCommittedBytes => new((nuint)Volatile.Read(ref _totalCommitted));

    /// <inheritdoc/>
    public ByteSize TotalAllocatedBytes
    {
        get
        {
            lock (_sync)
            {
                nuint sum = 0;
                unsafe
                {
                    ChunkNode* curr = (ChunkNode*)_chunksHead;
                    while (curr != null)
                    {
                        sum += (nuint)Volatile.Read(ref curr->Cursor);
                        curr = curr->Next;
                    }
                }
                return new ByteSize(sum);
            }
        }
    }

    /// <inheritdoc/>
    public ChunkEnumerable Chunks
    {
        get
        {
            lock (_sync)
            {
                unsafe
                {
                    return new ChunkEnumerable((void*)_chunksHead);
                }
            }
        }
    }

    /// <summary>Creates an arena with the given tuning.</summary>
    public unsafe Arena(ArenaOptions options = default)
    {
        nuint init = options.InitialChunkSize.Value < 4096 ? 65536 : options.InitialChunkSize.Value;
        nuint max = options.MaxChunkSize.Value < 4096 ? 67108864 : options.MaxChunkSize.Value;
        if (max < init) max = init;
        nuint align = options.DefaultAlignment.Value == 0 ? (nuint)UIntPtr.Size : (nuint)options.DefaultAlignment.Value;

        _state.InitialChunkSize = init;
        _state.MaxChunkSize = max;
        _state.DefaultAlignment = align;
        _state.ZeroOnReset = options.ZeroOnReset ? 1u : 0u;
        _state.LifecycleState = StateConstants.Active;
        _state.ActiveLeases = 0;
        _state.ChunkSequence = 0;

        lock (_sync)
        {
            ChunkNode* initial = CreateChunkNode(_state.InitialChunkSize);
            if (initial == null)
            {
                ThrowHelper.ThrowOutOfMemory(_state.InitialChunkSize, HardwareCacheAlignment);
            }
            _chunksHead = (nint)initial;
            Volatile.Write(ref _state.ActiveChunk, (IntPtr)initial);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe AllocationResult TryAllocate(ByteSize byteCount, Alignment alignment)
    {
        if (alignment.Value == 0 || !BitOperations.IsPow2(alignment.Value))
        {
            return AllocationResult.Failed(AllocationStatus.InvalidAlignment);
        }

        if (byteCount.Value == 0)
        {
            return AllocationResult.Succeeded(new ArenaAllocationHandle(null, ByteSize.Zero));
        }

        if (Volatile.Read(ref _state.LifecycleState) != StateConstants.Active)
        {
            return AllocationResult.Failed(AllocationStatus.ArenaInactive);
        }

        Interlocked.Increment(ref _state.ActiveLeases);
        if (Volatile.Read(ref _state.LifecycleState) != StateConstants.Active)
        {
            Interlocked.Decrement(ref _state.ActiveLeases);
            return AllocateSlow(byteCount, alignment);
        }

        try
        {
            ChunkNode* chunk = (ChunkNode*)Volatile.Read(ref _state.ActiveChunk);
            if (chunk != null)
            {
                int spinCount = 0;
                nuint reqBytes = byteCount.Value;
                nuint alignMask = (nuint)alignment.Value - 1;

                while (true)
                {
                    long current = Volatile.Read(ref chunk->Cursor);
                    nuint rawAddr = (nuint)chunk->Buffer + (nuint)current;
                    nuint alignedAddr = (rawAddr + alignMask) & ~alignMask;
                    nuint padding = alignedAddr - rawAddr;
                    nuint required = padding + reqBytes;

                    if ((nuint)current + required < (nuint)current)
                    {
                        return AllocationResult.Failed(AllocationStatus.OutOfMemory);
                    }

                    long nextCursor = current + (long)required;
                    if ((nuint)nextCursor <= chunk->Capacity)
                    {
                        if (Interlocked.CompareExchange(ref chunk->Cursor, nextCursor, current) == current)
                        {
                            return AllocationResult.Succeeded(new ArenaAllocationHandle((byte*)alignedAddr, byteCount));
                        }

                        TBackoff.Backoff(ref spinCount);
                        continue;
                    }

                    break;
                }
            }

            return AllocateSlow(byteCount, alignment);
        }
        finally
        {
            Interlocked.Decrement(ref _state.ActiveLeases);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ArenaAllocationHandle Allocate(ByteSize byteCount, Alignment alignment)
    {
        AllocationResult result = TryAllocate(byteCount, alignment);
        if (result.IsSuccess)
        {
            return result.Handle;
        }

        if (result.Status == AllocationStatus.OutOfMemory)
        {
            ThrowHelper.ThrowOutOfMemory(byteCount.Value, alignment.Value);
        }

        if (result.Status == AllocationStatus.ArenaInactive)
        {
            ThrowHelper.ThrowNotActive(Volatile.Read(ref _state.LifecycleState), _faultInfo);
        }

        ThrowHelper.ThrowInvalidOperationException("Unrecognized allocation outcome.");
        return default;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe ref T Allocate<T>() where T : unmanaged
    {
        ByteSize size = ByteSize.From((nuint)sizeof(T));
        Alignment align = GetAlignment<T>();
        ArenaAllocationHandle handle = Allocate(size, align);
        return ref Unsafe.AsRef<T>(handle.Pointer);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe Span<T> AllocateSpan<T>(nuint count) where T : unmanaged
    {
        if (count == 0) return Span<T>.Empty;
        if (count > (nuint)int.MaxValue)
        {
            ThrowHelper.ThrowCountOutOfRange();
        }

        ByteSize bytes = ByteSize.From(checked(count * (nuint)sizeof(T)));
        Alignment align = GetAlignment<T>();
        ArenaAllocationHandle handle = Allocate(bytes, align);
        return new Span<T>(handle.Pointer, (int)count);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe Span<byte> AllocateBytes(ByteSize byteCount, Alignment alignment)
    {
        if (byteCount.Value == 0) return Span<byte>.Empty;
        if (byteCount.Value > (nuint)int.MaxValue)
        {
            ThrowHelper.ThrowCountOutOfRange();
        }

        ArenaAllocationHandle handle = Allocate(byteCount, alignment);
        return new Span<byte>(handle.Pointer, (int)byteCount.Value);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Span<T> AllocateAndCopy<T>(ReadOnlySpan<T> items) where T : unmanaged
    {
        if (items.IsEmpty) return Span<T>.Empty;
        Span<T> destination = AllocateSpan<T>((nuint)items.Length);
        items.CopyTo(destination);
        return destination;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExecuteScoped<TState, TAction>(ByteSize byteCount, Alignment alignment, ref TState state, TAction action)
        where TState : allows ref struct
        where TAction : struct, ISpanAction<TState>
    {
        Span<byte> memory = AllocateBytes(byteCount, alignment);
        action.Execute(memory, ref state);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe ArenaMarker CreateMarker()
    {
        ChunkNode* active = (ChunkNode*)Volatile.Read(ref _state.ActiveChunk);
        if (active == null) return ArenaMarker.Zero;
        return new ArenaMarker(new ChunkId(active->Id), new ByteSize((nuint)Volatile.Read(ref active->Cursor)));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArenaScope<TGrowth, TBackoff, TMetrics> CreateScope() => new(this);

    /// <inheritdoc/>
    public unsafe void Rewind(ArenaMarker marker)
    {
        lock (_sync)
        {
            EnsureActiveOrThrow();

            Volatile.Write(ref _state.LifecycleState, StateConstants.Reclaiming);
            try
            {
                SpinWaitForDrainedLeases();

                if (marker.IsZero)
                {
                    ResetInternal();
                    return;
                }

                ChunkNode* target = null;
                ChunkNode* curr = (ChunkNode*)_chunksHead;
                while (curr != null)
                {
                    if (curr->Id == marker.ChunkId.Value)
                    {
                        target = curr;
                        break;
                    }
                    curr = curr->Next;
                }

                if (target == null || marker.Offset.Value > target->Capacity)
                {
                    ThrowHelper.ThrowInvalidMarker();
                }

                ChunkNode* orphan = target->Next;
                target->Next = null;
                target->Cursor = (long)marker.Offset.Value;

                if (_state.ZeroOnReset != 0 && target->Buffer != null && marker.Offset.Value < target->Capacity)
                {
                    nuint clearSize = target->Capacity - marker.Offset.Value;
                    NativeMemory.Clear(target->Buffer + marker.Offset.Value, clearSize);
                }

                ChunkNode* standby = (ChunkNode*)_standbyHead;
                while (orphan != null)
                {
                    ChunkNode* next = orphan->Next;
                    orphan->Cursor = 0;
                    if (_state.ZeroOnReset != 0 && orphan->Buffer != null)
                    {
                        NativeMemory.Clear(orphan->Buffer, orphan->Capacity);
                    }
                    orphan->Next = standby;
                    standby = orphan;
                    orphan = next;
                }
                _standbyHead = (nint)standby;

                Volatile.Write(ref _state.ActiveChunk, (IntPtr)target);
            }
            finally
            {
                Volatile.Write(ref _state.LifecycleState, StateConstants.Active);
            }
        }
    }

    /// <inheritdoc/>
    public void Reset()
    {
        lock (_sync)
        {
            EnsureActiveOrThrow();

            Volatile.Write(ref _state.LifecycleState, StateConstants.Reclaiming);
            try
            {
                SpinWaitForDrainedLeases();
                ResetInternal();
            }
            finally
            {
                Volatile.Write(ref _state.LifecycleState, StateConstants.Active);
            }
        }
    }

    /// <inheritdoc/>
    public void Complete(Exception? error = null)
    {
        lock (_sync)
        {
            int state = _state.LifecycleState;
            if (state == StateConstants.Disposed) return;

            if (error != null)
            {
                _faultInfo = ExceptionDispatchInfo.Capture(error);
                Volatile.Write(ref _state.LifecycleState, StateConstants.Faulted);
            }
            else if (state == StateConstants.Active || state == StateConstants.Reclaiming)
            {
                Volatile.Write(ref _state.LifecycleState, StateConstants.Draining);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_state.LifecycleState == StateConstants.Active)
            {
                Volatile.Write(ref _state.LifecycleState, StateConstants.Draining);
            }
        }

        int spin = 0;
        while (Volatile.Read(ref _state.ActiveLeases) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (spin < 16)
            {
                Thread.SpinWait(1 << Math.Min(spin, 6));
                spin++;
            }
            else
            {
                await Task.Yield();
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        int prev = Interlocked.Exchange(ref _state.LifecycleState, StateConstants.Disposed);
        if (prev == StateConstants.Disposed) return;

        await DrainAsync(CancellationToken.None).ConfigureAwait(false);

        lock (_sync)
        {
            FreeAllAllocatedNodes();
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases native chunks; called by Dispose and the finalizer.</summary>
    /// <param name="disposing">Whether managed state may be touched.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _state.LifecycleState, StateConstants.Disposed) == StateConstants.Disposed)
        {
            return;
        }

        lock (_sync)
        {
            SpinWaitForDrainedLeases();
            FreeAllAllocatedNodes();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private unsafe AllocationResult AllocateSlow(ByteSize byteCount, Alignment alignment)
    {
        lock (_sync)
        {
            int state = Volatile.Read(ref _state.LifecycleState);
            if (state != StateConstants.Active)
            {
                return AllocationResult.Failed(AllocationStatus.ArenaInactive);
            }

            ChunkNode* current = (ChunkNode*)_state.ActiveChunk;
            nuint reqBytes = byteCount.Value;
            nuint alignMask = (nuint)alignment.Value - 1;

            if (current != null)
            {
                long currentCursor = current->Cursor;
                nuint rawAddr = (nuint)current->Buffer + (nuint)currentCursor;
                nuint alignedAddr = (rawAddr + alignMask) & ~alignMask;
                nuint padding = alignedAddr - rawAddr;
                nuint required = padding + reqBytes;

                if ((nuint)currentCursor + required <= current->Capacity)
                {
                    current->Cursor += (long)required;
                    return AllocationResult.Succeeded(new ArenaAllocationHandle((byte*)alignedAddr, byteCount));
                }
            }

            nuint currentCap = current != null ? current->Capacity : _state.InitialChunkSize;
            nuint minRequired = checked(reqBytes + (nuint)alignment.Value);
            ByteSize nextCap = TGrowth.ComputeNextSize(
                new ByteSize(currentCap),
                new ByteSize(minRequired),
                new ByteSize(_state.MaxChunkSize));

            if (nextCap.Value < minRequired)
            {
                nextCap = new ByteSize(minRequired);
            }

            ChunkNode* chunk = AcquireChunkNode(nextCap.Value);
            if (chunk == null)
            {
                return AllocationResult.Failed(AllocationStatus.OutOfMemory);
            }

            if (current != null)
            {
                current->Next = chunk;
            }
            else
            {
                _chunksHead = (nint)chunk;
            }

            Volatile.Write(ref _state.ActiveChunk, (IntPtr)chunk);

            nuint chunkRaw = (nuint)chunk->Buffer;
            nuint chunkAligned = (chunkRaw + alignMask) & ~alignMask;
            nuint chunkPadding = chunkAligned - chunkRaw;
            nuint chunkRequired = chunkPadding + reqBytes;

            chunk->Cursor = (long)chunkRequired;
            return AllocationResult.Succeeded(new ArenaAllocationHandle((byte*)chunkAligned, byteCount));
        }
    }

    private unsafe void ResetInternal()
    {
        ChunkNode* head = (ChunkNode*)_chunksHead;
        if (head == null) return;

        nuint reclaimed = 0;
        ChunkNode* first = head;
        reclaimed += (nuint)first->Cursor;
        first->Cursor = 0;

        if (_state.ZeroOnReset != 0 && first->Buffer != null)
        {
            NativeMemory.Clear(first->Buffer, first->Capacity);
        }

        ChunkNode* curr = first->Next;
        first->Next = null;

        ChunkNode* standby = (ChunkNode*)_standbyHead;
        while (curr != null)
        {
            ChunkNode* next = curr->Next;
            reclaimed += (nuint)curr->Cursor;
            curr->Cursor = 0;
            if (_state.ZeroOnReset != 0 && curr->Buffer != null)
            {
                NativeMemory.Clear(curr->Buffer, curr->Capacity);
            }
            curr->Next = standby;
            standby = curr;
            curr = next;
        }
        _standbyHead = (nint)standby;

        Volatile.Write(ref _state.ActiveChunk, (IntPtr)first);
        TMetrics.OnReset(new ByteSize(reclaimed));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureActiveOrThrow()
    {
        int state = Volatile.Read(ref _state.LifecycleState);
        if (state != StateConstants.Active)
        {
            ThrowHelper.ThrowNotActive(state, _faultInfo);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SpinWaitForDrainedLeases()
    {
        int spin = 0;
        while (Volatile.Read(ref _state.ActiveLeases) > 0)
        {
            if (spin < 10)
            {
                Thread.SpinWait(1 << spin);
            }
            else
            {
                Thread.Yield();
            }
            spin++;
        }
    }

    private unsafe ChunkNode* AcquireChunkNode(nuint capacity)
    {
        ChunkNode* prev = null;
        ChunkNode* curr = (ChunkNode*)_standbyHead;

        while (curr != null)
        {
            if (curr->Capacity >= capacity)
            {
                if (prev == null)
                {
                    _standbyHead = (nint)curr->Next;
                }
                else
                {
                    prev->Next = curr->Next;
                }
                curr->Next = null;
                curr->Cursor = 0;
                return curr;
            }
            prev = curr;
            curr = curr->Next;
        }

        return CreateChunkNode(capacity);
    }

    private unsafe ChunkNode* CreateChunkNode(nuint capacity)
    {
        nuint totalBytes = checked(ChunkHeaderSize + capacity);
        byte* memory = (byte*)NativeMemory.AlignedAlloc(totalBytes, HardwareCacheAlignment);
        if (memory == null)
        {
            TMetrics.OnAllocationFailed(new ByteSize(capacity));
            return null;
        }

        if (_state.ZeroOnReset != 0)
        {
            NativeMemory.Clear(memory, totalBytes);
        }

        ChunkNode* header = (ChunkNode*)memory;
        header->Next = null;
        header->Buffer = memory + ChunkHeaderSize;
        header->Capacity = capacity;
        header->Cursor = 0;
        header->Id = unchecked(++_state.ChunkSequence);
        header->Flags = 0;
        header->CommittedAllocationSize = totalBytes;

        Interlocked.Add(ref _totalCommitted, (long)totalBytes);
        TMetrics.OnChunkAllocated(new ByteSize(capacity));

        return header;
    }

    private unsafe void FreeChunkNode(ChunkNode* chunk)
    {
        if (chunk == null) return;
        nuint allocSize = chunk->CommittedAllocationSize;
        nuint cap = chunk->Capacity;

        NativeMemory.AlignedFree(chunk);

        Interlocked.Add(ref _totalCommitted, -(long)allocSize);
        TMetrics.OnChunkFreed(new ByteSize(cap));
    }

    private unsafe void FreeAllAllocatedNodes()
    {
        ChunkNode* curr = (ChunkNode*)_chunksHead;
        _chunksHead = 0;
        Volatile.Write(ref _state.ActiveChunk, IntPtr.Zero);

        while (curr != null)
        {
            ChunkNode* next = curr->Next;
            FreeChunkNode(curr);
            curr = next;
        }

        curr = (ChunkNode*)_standbyHead;
        _standbyHead = 0;
        while (curr != null)
        {
            ChunkNode* next = curr->Next;
            FreeChunkNode(curr);
            curr = next;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe Alignment GetAlignment<T>() where T : unmanaged
    {
        nuint size = (nuint)sizeof(T);
        if (size <= 1) return Alignment.Byte;
        nuint pow2 = (nuint)1 << BitOperations.TrailingZeroCount(size);
        return new Alignment((uint)(pow2 > 64 ? 64 : pow2));
    }

    ~Arena()
    {
        Dispose(false);
    }
}

/// <summary>The default arena: geometric growth, adaptive backoff, no metrics.</summary>
public sealed class Arena : Arena<GeometricGrowthPolicy, AdaptiveSpinBackoff, NullMetricsSink>
{
    /// <summary>Creates an arena with the given tuning.</summary>
    public Arena(ArenaOptions options = default) : base(options) { }
}

/// <summary>Scope guard that rewinds the arena to its entry marker on dispose.</summary>
/// <typeparam name="TGrowth">Chunk growth policy.</typeparam>
/// <typeparam name="TBackoff">Contention backoff policy.</typeparam>
/// <typeparam name="TMetrics">Chunk lifetime metrics sink.</typeparam>
public readonly ref struct ArenaScope<TGrowth, TBackoff, TMetrics>
    where TGrowth : struct, IArenaGrowthPolicy
    where TBackoff : struct, IBackoffPolicy
    where TMetrics : struct, IArenaMetricsSink
{
    private readonly Arena<TGrowth, TBackoff, TMetrics> _arena;
    private readonly ArenaMarker _marker;

    /// <summary>Opens a scope over <paramref name="arena"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ArenaScope(Arena<TGrowth, TBackoff, TMetrics> arena)
    {
        _arena = arena;
        _marker = arena.CreateMarker();
    }

    /// <summary>Rewinds the arena to the entry marker.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        _arena.Rewind(_marker);
    }
}
