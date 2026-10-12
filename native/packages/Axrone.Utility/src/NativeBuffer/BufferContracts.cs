namespace Axrone.Utility.NativeBuffer;

using Axrone.Utility.Alignment;

/// <summary>Native allocation policy, bound statically so calls devirtualize.</summary>
public interface INativeAllocator
{
    /// <summary>Allocates <paramref name="byteCount"/> aligned bytes.</summary>
    static abstract unsafe void* Allocate(ByteSize byteCount, MemoryAlignment alignment);

    /// <summary>Frees a pointer made by <see cref="Allocate"/>.</summary>
    static abstract unsafe void Free(void* pointer);
}

/// <summary>Aligned native allocator over the platform allocator.</summary>
public readonly struct AlignedNativeAllocator : INativeAllocator
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void* Allocate(ByteSize byteCount, MemoryAlignment alignment) =>
        NativeMemory.AlignedAlloc(byteCount.Value, alignment.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void Free(void* pointer) =>
        NativeMemory.AlignedFree(pointer);
}

/// <summary>How freshly committed memory is initialized.</summary>
public unsafe interface IMemoryInitializationPolicy
{
    /// <summary>Initializes <paramref name="byteCount"/> bytes at <paramref name="destination"/>.</summary>
    static abstract unsafe void Initialize(byte* destination, ByteSize byteCount);
}

/// <summary>Zeroes memory with tiered vector stores.</summary>
public readonly struct ZeroInitializationPolicy : IMemoryInitializationPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Initialize(byte* destination, ByteSize byteCount) =>
        VectorizedOperations.ZeroMemory(destination, byteCount);
}

/// <summary>Skips initialization; the caller overwrites everything anyway.</summary>
public readonly struct SkipInitializationPolicy : IMemoryInitializationPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void Initialize(byte* destination, ByteSize byteCount)
    {
    }
}

/// <summary>Reads mutable elements through a visitor.</summary>
public interface ISpanVisitor<T> where T : unmanaged
{
    /// <summary>Visits <paramref name="span"/>.</summary>
    void Visit<TContext>(Span<T> span, ref TContext context) where TContext : allows ref struct;
}

/// <summary>Reads read-only elements through a visitor.</summary>
public interface IReadOnlySpanVisitor<T> where T : unmanaged
{
    /// <summary>Visits <paramref name="span"/>.</summary>
    void Visit<TContext>(ReadOnlySpan<T> span, ref TContext context) where TContext : allows ref struct;
}

/// <summary>Runs an action over a mutable span.</summary>
public interface ISpanAction<T, TContext>
    where T : unmanaged
    where TContext : allows ref struct
{
    /// <summary>Runs against <paramref name="span"/>.</summary>
    void Invoke(Span<T> span, ref TContext context);
}

/// <summary>Runs an action over a read-only span.</summary>
public interface IReadOnlySpanAction<T, TContext>
    where T : unmanaged
    where TContext : allows ref struct
{
    /// <summary>Runs against <paramref name="span"/>.</summary>
    void Invoke(ReadOnlySpan<T> span, ref TContext context);
}

/// <summary>The read half of a buffer.</summary>
public interface IBufferReader<T> where T : unmanaged
{
    /// <summary>Live elements.</summary>
    ElementCount Length { get; }

    /// <summary>Committed bytes.</summary>
    ByteSize ByteCapacity { get; }

    /// <summary>Indexes into the buffer.</summary>
    ref readonly T this[BufferIndex index] { get; }

    /// <summary>Copies from <paramref name="sourceOffset"/> into <paramref name="destination"/>.</summary>
    unsafe void Read(ElementCount sourceOffset, Span<T> destination);

    /// <summary>Reads an unaligned value at <paramref name="byteOffset"/>.</summary>
    TTo ReadUnaligned<TTo>(ByteOffset byteOffset) where TTo : unmanaged;

    /// <summary>Read-only view of everything.</summary>
    ReadOnlySpan<T> ReadOnlySpan { get; }

    /// <summary>Element-wise comparison against <paramref name="other"/>.</summary>
    bool SequenceEqual(ReadOnlySpan<T> other);
}

/// <summary>The write half of a buffer.</summary>
public interface IBufferWriterContract<T> where T : unmanaged
{
    /// <summary>Indexes into the buffer.</summary>
    ref T this[BufferIndex index] { get; }

    /// <summary>Copies into the buffer from <paramref name="source"/>.</summary>
    unsafe void Write(ElementCount destinationOffset, params ReadOnlySpan<T> source);

    /// <summary>Writes an unaligned value at <paramref name="byteOffset"/>.</summary>
    void WriteUnaligned<TTo>(ByteOffset byteOffset, in TTo value) where TTo : unmanaged;

    /// <summary>Fills every element with <paramref name="value"/>.</summary>
    void Fill(T value);

    /// <summary>Zeroes every element.</summary>
    void Clear();

    /// <summary>Mutable view of everything.</summary>
    Span<T> Span { get; }
}

/// <summary>Lease half: reference-counted access the drain waits on.</summary>
public interface ILeasableBuffer<T, TAllocator>
    where T : unmanaged
    where TAllocator : struct, INativeAllocator
{
    /// <summary>Leases held right now.</summary>
    uint ActiveLeases { get; }

    /// <summary>Tries to take a lease without throwing.</summary>
    bool TryLease(out NativeBufferLease<T, TAllocator> lease);

    /// <summary>Takes a lease, reporting why it could not when terminal.</summary>
    LeaseResult<T, TAllocator> TryAcquireLease();

    /// <summary>Takes a lease, throwing when the buffer refuses.</summary>
    NativeBufferLease<T, TAllocator> Lease();
}

/// <summary>Slice half: cheap subviews.</summary>
public interface ISliceableBuffer<T, TAllocator>
    where T : unmanaged
    where TAllocator : struct, INativeAllocator
{
    /// <summary>Creates a subview.</summary>
    NativeBufferSlice<T, TAllocator> Slice(ElementCount offset, ElementCount length);
}

/// <summary>Visitor half: monomorphic element sweeps.</summary>
public interface IBufferVisitorEndpoint<T> where T : unmanaged
{
    /// <summary>Sweeps a mutable visitor.</summary>
    void Execute<TVisitor, TContext>(TVisitor visitor, ref TContext context)
        where TVisitor : struct, ISpanVisitor<T>
        where TContext : allows ref struct;

    /// <summary>Sweeps a read-only visitor.</summary>
    void ExecuteReadOnly<TVisitor, TContext>(TVisitor visitor, ref TContext context)
        where TVisitor : struct, IReadOnlySpanVisitor<T>
        where TContext : allows ref struct;

    /// <summary>Runs a mutable action.</summary>
    void Apply<TAction, TContext>(ref TContext context)
        where TAction : struct, ISpanAction<T, TContext>
        where TContext : allows ref struct;

    /// <summary>Runs a read-only action.</summary>
    void ApplyReadOnly<TAction, TContext>(ref TContext context)
        where TAction : struct, IReadOnlySpanAction<T, TContext>
        where TContext : allows ref struct;
}

/// <summary>Lifecycle half: drain, complete, dispose.</summary>
public interface IAdministrativeBuffer
{
    /// <summary>Whether the buffer is disposed.</summary>
    bool IsDisposed { get; }

    /// <summary>Whether the buffer is draining or drained.</summary>
    bool IsDraining { get; }

    /// <summary>Awaits every lease being returned.</summary>
    ValueTask DrainAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes the buffer, optionally faulted.</summary>
    void Complete(Exception? error = null);
}

/// <summary>Pointed half: escape hatches for interop and unsafe kernels.</summary>
public unsafe interface IDangerousBufferPointer<T> where T : unmanaged
{
    /// <summary>Returns the base pointer, verifying the buffer is operational.</summary>
    T* DangerousGetPointer();

    /// <summary>Returns the base pointer without any check.</summary>
    T* DangerousGetPointerUnchecked();

    /// <summary>Returns a pinnable reference to the first element.</summary>
    ref readonly T GetPinnableReference();
}
