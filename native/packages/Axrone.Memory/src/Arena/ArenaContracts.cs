namespace Axrone.Memory.Arena;

/// <summary>Action over a scratch span with caller-owned state.</summary>
/// <typeparam name="TState">The state type; may be a ref struct.</typeparam>
public interface ISpanAction<TState> where TState : allows ref struct
{
    /// <summary>Runs against <paramref name="buffer"/>.</summary>
    void Execute(Span<byte> buffer, ref TState state);
}

/// <summary>The allocating half of a unified arena.</summary>
public interface IArenaAllocator
{
    /// <summary>Tries to grant <paramref name="byteCount"/> bytes without throwing.</summary>
    AllocationResult TryAllocate(ByteSize byteCount, Alignment alignment);

    /// <summary>Grants <paramref name="byteCount"/> bytes or throws.</summary>
    ArenaAllocationHandle Allocate(ByteSize byteCount, Alignment alignment);

    /// <summary>Grants one value.</summary>
    ref T Allocate<T>() where T : unmanaged;

    /// <summary>Grants room for <paramref name="count"/> values.</summary>
    Span<T> AllocateSpan<T>(nuint count) where T : unmanaged;

    /// <summary>Grants raw bytes.</summary>
    Span<byte> AllocateBytes(ByteSize byteCount, Alignment alignment);

    /// <summary>Grants room for <paramref name="items"/> and copies them in.</summary>
    Span<T> AllocateAndCopy<T>(ReadOnlySpan<T> items) where T : unmanaged;

    /// <summary>Grants scratch bytes and runs <paramref name="action"/> over them.</summary>
    void ExecuteScoped<TState, TAction>(ByteSize byteCount, Alignment alignment, ref TState state, TAction action)
        where TState : allows ref struct
        where TAction : struct, ISpanAction<TState>;
}

/// <summary>The read-only inspection half of a unified arena.</summary>
public interface IArenaInspector
{
    /// <summary>Bytes handed out across all chunks.</summary>
    ByteSize TotalAllocatedBytes { get; }

    /// <summary>Bytes committed from the OS across all chunks.</summary>
    ByteSize TotalCommittedBytes { get; }

    /// <summary>Walks the live chunks.</summary>
    ChunkEnumerable Chunks { get; }
}

/// <summary>Lifecycle half: rewind, drain and dispose.</summary>
public interface IArenaLifecycle : IDisposable, IAsyncDisposable
{
    /// <summary>Captures the current head position.</summary>
    ArenaMarker CreateMarker();

    /// <summary>Returns the arena to <paramref name="marker"/>, retiring newer chunks.</summary>
    void Rewind(ArenaMarker marker);

    /// <summary>Returns the arena to empty, keeping the first chunk.</summary>
    void Reset();

    /// <summary>Closes the stream, optionally faulted.</summary>
    void Complete(Exception? error = null);

    /// <summary>Awaits in-flight allocations.</summary>
    ValueTask DrainAsync(CancellationToken cancellationToken = default);
}

/// <summary>Full unified-arena contract.</summary>
public interface IMemoryArena : IArenaAllocator, IArenaInspector, IArenaLifecycle
{
}
