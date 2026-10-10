namespace Axrone.Utility.NativeBuffer;

using System.Buffers;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Axrone.Utility.Alignment;
using Axrone.Utility.Backoff.SpinPolicies;

/// <summary>
/// Control word plus lifetime counters, isolated on separate cache lines.
/// Namespace-level because the CLR forbids explicit layout on types nested
/// in a generic class.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 192)]
internal struct BufferExecutionTopology
{
    [FieldOffset(0)]
    public long AtomicControlWord;

    [FieldOffset(64)]
    public long LifetimeAcquisitions;

    [FieldOffset(128)]
    public long LifetimeReleases;
}

/// <summary>
/// By-value enumerator over native elements.
/// </summary>
public unsafe ref struct NativeBufferEnumerator<T> where T : unmanaged
{
    private readonly T* _pointer;
    private readonly nuint _length;
    private nuint _index;

    /// <summary>Creates an enumerator; prefer <c>GetEnumerator</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal NativeBufferEnumerator(T* pointer, nuint length)
    {
        _pointer = pointer;
        _length = length;
        _index = unchecked((nuint)(-1));
    }

    /// <summary>Advances to the next element, if any.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext()
    {
        nuint next = _index + 1;
        if (next < _length)
        {
            _index = next;
            return true;
        }
        return false;
    }

    /// <summary>The element at the current position.</summary>
    public ref readonly T Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref *(_pointer + _index);
    }
}

/// <summary>
/// Fixed-length native buffer with reference-counted leases, sub-slices,
/// visitor sweeps and a drain/complete/dispose lifecycle. Storage comes from
/// the compile-time allocator; the length is fixed at construction.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <typeparam name="TAllocator">The allocation policy.</typeparam>
public sealed class NativeBuffer<T, TAllocator> :
    IBufferReader<T>,
    IBufferWriterContract<T>,
    ILeasableBuffer<T, TAllocator>,
    ISliceableBuffer<T, TAllocator>,
    IBufferVisitorEndpoint<T>,
    IAdministrativeBuffer,
    IDangerousBufferPointer<T>,
    IMemoryOwner<T>,
    IAsyncDisposable,
    IValueTaskSource,
    IEquatable<NativeBuffer<T, TAllocator>>
    where T : unmanaged
    where TAllocator : struct, INativeAllocator
{
    private const long StatusActive = 0L;
    private const long StatusDraining = 1L << 32;
    private const long StatusDrained = 2L << 32;
    private const long StatusDisposed = 3L << 32;
    private const long StatusFaulted = 4L << 32;

    private const long StatusMask = 0x7L << 32;
    private const long LeaseCounterMask = 0xFFFF_FFFFL;

    private readonly Lock _gate = new();
    private BufferExecutionTopology _topology;
    private unsafe T* _pointer;
    private ExceptionDispatchInfo? _fault;

    private ManualResetValueTaskSourceCore<bool> _drainTaskSource;
    private CancellationTokenRegistration _drainCancellation;
    private MemoryManagerAdapter? _memoryManagerAdapter;

    /// <summary>Live elements; zero lengths are rejected.</summary>
    public ElementCount Length
    {
        get;
        private init => field = value.Value != 0 ? value : ThrowHelper.ThrowArgumentZero<ElementCount>();
    }

    /// <summary>Committed bytes.</summary>
    public ByteSize ByteCapacity { get; private init; }

    /// <summary>Grant alignment; must be at least pointer-sized and a power of two.</summary>
    public MemoryAlignment Alignment
    {
        get;
        private init => field = (value.Value >= (nuint)UIntPtr.Size && BitOperations.IsPow2(value.Value))
            ? value
            : ThrowHelper.ThrowInvalidAlignment<MemoryAlignment>(value.Value);
    }

    /// <summary>Whether the buffer is disposed.</summary>
    public bool IsDisposed => (Volatile.Read(ref _topology.AtomicControlWord) & StatusMask) == StatusDisposed;

    /// <summary>Whether the buffer is draining or drained.</summary>
    public bool IsDraining => (Volatile.Read(ref _topology.AtomicControlWord) & StatusMask) >= StatusDraining;

    /// <summary>Leases held right now.</summary>
    public uint ActiveLeases => (uint)(Volatile.Read(ref _topology.AtomicControlWord) & LeaseCounterMask);

    /// <summary>Leases ever taken.</summary>
    public long LifetimeAcquisitions => Volatile.Read(ref _topology.LifetimeAcquisitions);

    /// <summary>Leases ever returned.</summary>
    public long LifetimeReleases => Volatile.Read(ref _topology.LifetimeReleases);

    /// <summary>Mutable view of everything.</summary>
    public Span<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            EnsureOperational();
            if (Length.Value > (nuint)int.MaxValue)
            {
                ThrowHelper.ThrowSpanLengthExceeded(Length.Value);
            }
            unsafe
            {
                return new Span<T>(_pointer, (int)Length.Value);
            }
        }
    }

    /// <summary>Read-only view of everything.</summary>
    public ReadOnlySpan<T> ReadOnlySpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            EnsureOperational();
            if (Length.Value > (nuint)int.MaxValue)
            {
                ThrowHelper.ThrowSpanLengthExceeded(Length.Value);
            }
            unsafe
            {
                return new ReadOnlySpan<T>(_pointer, (int)Length.Value);
            }
        }
    }

    /// <summary>Pinned view of everything.</summary>
    public Memory<T> Memory
    {
        get
        {
            EnsureOperational();
            if (Length.Value > (nuint)int.MaxValue)
            {
                ThrowHelper.ThrowSpanLengthExceeded(Length.Value);
            }

            MemoryManagerAdapter? adapter = Volatile.Read(ref _memoryManagerAdapter);
            if (adapter is null)
            {
                lock (_gate)
                {
                    adapter = _memoryManagerAdapter;
                    if (adapter is null)
                    {
                        adapter = new MemoryManagerAdapter(this);
                        Volatile.Write(ref _memoryManagerAdapter, adapter);
                    }
                }
            }

            return adapter.Memory;
        }
    }

    /// <summary>Indexes into the buffer.</summary>
    public ref T this[BufferIndex index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if (index.Value >= Length.Value)
            {
                ThrowHelper.ThrowIndexOutOfRange(index.Value, Length.Value);
            }
            unsafe
            {
                return ref Unsafe.Add(ref *_pointer, index.Value);
            }
        }
    }

    ref readonly T IBufferReader<T>.this[BufferIndex index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => ref this[index];
    }

    /// <summary>Creates a buffer of <paramref name="length"/> elements.</summary>
    public unsafe NativeBuffer(ElementCount length, MemoryAlignment alignment, bool zeroInitialize = true)
    {
        Length = length;
        Alignment = alignment;

        nuint byteCapacity = checked(length.Value * (nuint)sizeof(T));
        ByteCapacity = ByteSize.From(byteCapacity);

        void* memory = TAllocator.Allocate(ByteCapacity, alignment);
        if (memory == null)
        {
            ThrowHelper.ThrowOutOfMemory(ByteCapacity.Value, alignment.Value);
        }

        _pointer = (T*)memory;

        if (zeroInitialize)
        {
            VectorizedOperations.ZeroMemory((byte*)memory, ByteCapacity);
        }

        _topology.AtomicControlWord = StatusActive;
        _drainTaskSource.RunContinuationsAsynchronously = true;
    }

    /// <summary>Allocates with a custom initialization policy instead of the flag.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NativeBuffer<T, TAllocator> AllocateCustomWithPolicy<TInitPolicy>(
        ElementCount length,
        MemoryAlignment alignment)
        where TInitPolicy : struct, IMemoryInitializationPolicy
    {
        var buffer = new NativeBuffer<T, TAllocator>(length, alignment, zeroInitialize: false);
        unsafe
        {
            TInitPolicy.Initialize((byte*)buffer._pointer, buffer.ByteCapacity);
        }
        return buffer;
    }

    /// <summary>Finalizer; prefer explicit disposal.</summary>
    ~NativeBuffer()
    {
        FreeNativeMemory();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe T* DangerousGetPointer()
    {
        EnsureOperational();
        return _pointer;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe T* DangerousGetPointerUnchecked() => _pointer;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe ref readonly T GetPinnableReference()
    {
        EnsureOperational();
        return ref *_pointer;
    }

    /// <inheritdoc/>
    public LeaseResult<T, TAllocator> TryAcquireLease()
    {
        int backoff = 0;
        while (true)
        {
            long current = Volatile.Read(ref _topology.AtomicControlWord);
            long status = current & StatusMask;
            if (status != StatusActive)
            {
                return status switch
                {
                    StatusDraining => LeaseResult<T, TAllocator>.Failed(LeaseOutcome.BufferDraining),
                    StatusDrained => LeaseResult<T, TAllocator>.Failed(LeaseOutcome.BufferDrained),
                    StatusDisposed => LeaseResult<T, TAllocator>.Failed(LeaseOutcome.BufferDisposed),
                    StatusFaulted => LeaseResult<T, TAllocator>.Failed(LeaseOutcome.BufferFaulted),
                    _ => ThrowHelper.ThrowInvalidState<LeaseResult<T, TAllocator>>(status)
                };
            }

            uint count = (uint)(current & LeaseCounterMask);
            if (count == uint.MaxValue)
            {
                ThrowHelper.ThrowCounterOverflow();
            }

            long next = current + 1L;
            if (Interlocked.CompareExchange(ref _topology.AtomicControlWord, next, current) == current)
            {
                Interlocked.Increment(ref _topology.LifetimeAcquisitions);
                uint clampedLength = (uint)Math.Min(Length.Value, (nuint)uint.MaxValue);
                var lease = new NativeBufferLease<T, TAllocator>(this, clampedLength);
                return LeaseResult<T, TAllocator>.Acquired(lease);
            }

            AdaptiveSpinBackoff.Advance(ref backoff);
        }
    }

    /// <inheritdoc/>
    public bool TryLease(out NativeBufferLease<T, TAllocator> lease)
    {
        var result = TryAcquireLease();
        if (result.IsSuccess)
        {
            lease = result.Value;
            return true;
        }
        lease = default;
        return false;
    }

    /// <inheritdoc/>
    public NativeBufferLease<T, TAllocator> Lease()
    {
        var result = TryAcquireLease();
        if (result.IsSuccess)
        {
            return result.Value;
        }
        ThrowHelper.ThrowAcquisitionFailed(Volatile.Read(ref _topology.AtomicControlWord));
        return default;
    }

    /// <summary>Returns one lease; the drain completes when the last one lands.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal void ReleaseLease()
    {
        int backoff = 0;
        while (true)
        {
            long current = Volatile.Read(ref _topology.AtomicControlWord);
            uint count = (uint)(current & LeaseCounterMask);
            if (count == 0)
            {
                ThrowHelper.ThrowCounterUnderflow();
            }

            long next = current - 1L;
            if (Interlocked.CompareExchange(ref _topology.AtomicControlWord, next, current) == current)
            {
                Interlocked.Increment(ref _topology.LifetimeReleases);
                if ((next & LeaseCounterMask) == 0 && (next & StatusMask) == StatusDraining)
                {
                    SignalDrained();
                }
                return;
            }

            AdaptiveSpinBackoff.Advance(ref backoff);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe NativeBufferSlice<T, TAllocator> Slice(ElementCount offset, ElementCount length)
    {
        EnsureOperational();
        if (offset.Value + length.Value > Length.Value)
        {
            ThrowHelper.ThrowRangeInvalid(offset.Value, length.Value, Length.Value);
        }
        return new NativeBufferSlice<T, TAllocator>(this, offset, length);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe void Read(ElementCount sourceOffset, Span<T> destination)
    {
        EnsureOperational();
        nuint count = (nuint)destination.Length;
        if (sourceOffset.Value + count > Length.Value)
        {
            ThrowHelper.ThrowRangeInvalid(sourceOffset.Value, ElementCount.From(count).Value, Length.Value);
        }

        fixed (T* destPtr = destination)
        {
            VectorizedOperations.CopyMemory(
                (byte*)(_pointer + sourceOffset.Value),
                (byte*)destPtr,
                ByteSize.From(count * (nuint)sizeof(T)));
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe void Write(ElementCount destinationOffset, params ReadOnlySpan<T> source)
    {
        EnsureOperational();
        nuint count = (nuint)source.Length;
        if (destinationOffset.Value + count > Length.Value)
        {
            ThrowHelper.ThrowRangeInvalid(destinationOffset.Value, ElementCount.From(count).Value, Length.Value);
        }

        fixed (T* srcPtr = source)
        {
            VectorizedOperations.CopyMemory(
                (byte*)srcPtr,
                (byte*)(_pointer + destinationOffset.Value),
                ByteSize.From(count * (nuint)sizeof(T)));
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe TTo ReadUnaligned<TTo>(ByteOffset byteOffset) where TTo : unmanaged
    {
        EnsureOperational();
        if (byteOffset.Value + (nuint)sizeof(TTo) > ByteCapacity.Value)
        {
            ThrowHelper.ThrowByteRangeInvalid(byteOffset.Value, ByteSize.From((nuint)sizeof(TTo)).Value, ByteCapacity.Value);
        }
        return Unsafe.ReadUnaligned<TTo>((byte*)_pointer + byteOffset.Value);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe void WriteUnaligned<TTo>(ByteOffset byteOffset, in TTo value) where TTo : unmanaged
    {
        EnsureOperational();
        if (byteOffset.Value + (nuint)sizeof(TTo) > ByteCapacity.Value)
        {
            ThrowHelper.ThrowByteRangeInvalid(byteOffset.Value, ByteSize.From((nuint)sizeof(TTo)).Value, ByteCapacity.Value);
        }
        Unsafe.WriteUnaligned((byte*)_pointer + byteOffset.Value, value);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Fill(T value)
    {
        EnsureOperational();
        Span.Fill(value);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe void Clear()
    {
        EnsureOperational();
        VectorizedOperations.ZeroMemory((byte*)_pointer, ByteCapacity);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool SequenceEqual(ReadOnlySpan<T> other)
    {
        EnsureOperational();
        if (Length.Value != (nuint)other.Length)
        {
            return false;
        }

        fixed (T* otherPtr = other)
        {
            return VectorizedOperations.EqualsMemory((byte*)_pointer, (byte*)otherPtr, ByteCapacity);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool SequenceEqual(NativeBuffer<T, TAllocator> other)
    {
        EnsureOperational();
        other.EnsureOperational();

        if (Length != other.Length)
        {
            return false;
        }

        return VectorizedOperations.EqualsMemory((byte*)_pointer, (byte*)other._pointer, ByteCapacity);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Execute<TVisitor, TContext>(TVisitor visitor, ref TContext context)
        where TVisitor : struct, ISpanVisitor<T>
        where TContext : allows ref struct
    {
        visitor.Visit(Span, ref context);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExecuteReadOnly<TVisitor, TContext>(TVisitor visitor, ref TContext context)
        where TVisitor : struct, IReadOnlySpanVisitor<T>
        where TContext : allows ref struct
    {
        visitor.Visit(ReadOnlySpan, ref context);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Apply<TAction, TContext>(ref TContext context)
        where TAction : struct, ISpanAction<T, TContext>
        where TContext : allows ref struct
    {
        default(TAction).Invoke(Span, ref context);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyReadOnly<TAction, TContext>(ref TContext context)
        where TAction : struct, IReadOnlySpanAction<T, TContext>
        where TContext : allows ref struct
    {
        default(TAction).Invoke(ReadOnlySpan, ref context);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe NativeBufferEnumerator<T> GetEnumerator()
    {
        EnsureOperational();
        return new NativeBufferEnumerator<T>(_pointer, Length.Value);
    }

    /// <inheritdoc/>
    public ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            long current = Volatile.Read(ref _topology.AtomicControlWord);
            long status = current & StatusMask;

            if (status == StatusDisposed)
            {
                ThrowHelper.ThrowObjectDisposedException(nameof(NativeBuffer<T, TAllocator>));
            }
            if (status == StatusFaulted)
            {
                ThrowHelper.ThrowFaulted(_fault?.SourceException);
            }
            if (status == StatusDrained)
            {
                return ValueTask.CompletedTask;
            }

            if (status == StatusActive)
            {
                int backoff = 0;
                while (true)
                {
                    long activeState = Volatile.Read(ref _topology.AtomicControlWord);
                    long transition = (activeState & ~StatusMask) | StatusDraining;
                    if (Interlocked.CompareExchange(ref _topology.AtomicControlWord, transition, activeState) == activeState)
                    {
                        current = transition;
                        break;
                    }
                    AdaptiveSpinBackoff.Advance(ref backoff);
                }
            }

            if ((current & LeaseCounterMask) == 0)
            {
                Volatile.Write(ref _topology.AtomicControlWord, (current & ~StatusMask) | StatusDrained);
                return ValueTask.CompletedTask;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled(cancellationToken);
            }

            _drainTaskSource.Reset();

            if (cancellationToken.CanBeCanceled)
            {
                _drainCancellation = cancellationToken.UnsafeRegister(static (state, token) =>
                {
                    var instance = (NativeBuffer<T, TAllocator>)state!;
                    instance._drainTaskSource.SetException(new OperationCanceledException(token));
                }, this);
            }

            return new ValueTask(this, _drainTaskSource.Version);
        }
    }

    private void SignalDrained()
    {
        lock (_gate)
        {
            long current = Volatile.Read(ref _topology.AtomicControlWord);
            if ((current & StatusMask) == StatusDraining)
            {
                Volatile.Write(ref _topology.AtomicControlWord, (current & ~StatusMask) | StatusDrained);
                _drainCancellation.Unregister();
                _drainTaskSource.SetResult(true);
            }
        }
    }

    /// <inheritdoc/>
    public void Complete(Exception? error = null)
    {
        lock (_gate)
        {
            if (error is not null)
            {
                _fault = ExceptionDispatchInfo.Capture(error);
                Volatile.Write(ref _topology.AtomicControlWord, StatusFaulted);
                _drainCancellation.Unregister();
                _drainTaskSource.SetException(error);
            }
            else
            {
                Volatile.Write(ref _topology.AtomicControlWord, StatusDrained);
                _drainCancellation.Unregister();
                _drainTaskSource.SetResult(true);
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

    /// <summary>Releases native storage; called by Dispose and the finalizer.</summary>
    private void Dispose(bool disposing)
    {
        lock (_gate)
        {
            long current = Volatile.Read(ref _topology.AtomicControlWord);
            if ((current & StatusMask) == StatusDisposed)
            {
                return;
            }

            Volatile.Write(ref _topology.AtomicControlWord, StatusDisposed);
            _drainCancellation.Unregister();
        }

        FreeNativeMemory();
    }

    private void FreeNativeMemory()
    {
        lock (_gate)
        {
            unsafe
            {
                T* ptr = _pointer;
                _pointer = null;
                if (ptr != null)
                {
                    TAllocator.Free(ptr);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureOperational()
    {
        long status = Volatile.Read(ref _topology.AtomicControlWord) & StatusMask;
        if (status == StatusDisposed)
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(NativeBuffer<T, TAllocator>));
        }
        if (status == StatusFaulted)
        {
            ThrowHelper.ThrowFaulted(_fault?.SourceException);
        }
    }

    void IValueTaskSource.GetResult(short token) => _drainTaskSource.GetResult(token);

    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => _drainTaskSource.GetStatus(token);

    void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
        _drainTaskSource.OnCompleted(continuation, state, token, flags);

    /// <inheritdoc/>
    public bool Equals(NativeBuffer<T, TAllocator>? other) => ReferenceEquals(this, other);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);

    /// <inheritdoc/>
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    /// <inheritdoc/>
    public static bool operator ==(NativeBuffer<T, TAllocator>? left, NativeBuffer<T, TAllocator>? right) => ReferenceEquals(left, right);

    /// <inheritdoc/>
    public static bool operator !=(NativeBuffer<T, TAllocator>? left, NativeBuffer<T, TAllocator>? right) => !ReferenceEquals(left, right);

    private sealed class MemoryManagerAdapter : MemoryManager<T>
    {
        private readonly NativeBuffer<T, TAllocator> _owner;

        public MemoryManagerAdapter(NativeBuffer<T, TAllocator> owner)
        {
            _owner = owner;
        }

        public override Span<T> GetSpan() => _owner.Span;

        public unsafe override MemoryHandle Pin(int elementIndex = 0)
        {
            _owner.EnsureOperational();
            if ((uint)elementIndex > _owner.Length.Value)
            {
                ThrowHelper.ThrowIndexOutOfRange((nuint)elementIndex, _owner.Length.Value);
            }
            unsafe
            {
                return new MemoryHandle(_owner._pointer + elementIndex);
            }
        }

        public override void Unpin() { }

        protected override void Dispose(bool disposing) { }
    }
}
