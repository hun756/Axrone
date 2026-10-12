namespace Axrone.Patterns;

/// <summary>
/// Read-only view over a node arena: live count, capacity, slot lookup and
/// range spans. Both arena flavors and both phases satisfy it.
/// </summary>
/// <typeparam name="TNode">The node type stored in the arena.</typeparam>
public interface IReadOnlyArena<TNode>
{
    /// <summary>Number of live slots.</summary>
    int Count { get; }

    /// <summary>Slots available before the arena must grow.</summary>
    int Capacity { get; }

    /// <summary>Reads the node at <paramref name="handle"/>.</summary>
    ref readonly TNode Get(NodeHandle handle);

    /// <summary>Reads the slots covered by <paramref name="range"/>.</summary>
    ReadOnlySpan<TNode> AsSpan(NodeRange range);
}

/// <summary>Marker for the compile-time phase of a node arena.</summary>
public interface IArenaPhase { }

/// <summary>Phase in which an arena accepts structural writes.</summary>
public readonly struct WritablePhase : IArenaPhase { }

/// <summary>Phase in which an arena is frozen and only traversed.</summary>
public readonly struct SealedPhase : IArenaPhase { }

/// <summary>
/// Builds a native (aligned, unmanaged-memory) node arena with the given capacity.
/// </summary>
/// <typeparam name="TNode">The node type stored in the arena.</typeparam>
public sealed class NativeNodeArenaBuilder<TNode> where TNode : unmanaged
{
    /// <summary>Slots reserved up front; must stay positive.</summary>
    public int InitialCapacity
    {
        get => field;
        set
        {
            if (value <= 0) ThrowHelper.ThrowArgumentOutOfRange(nameof(value));
            field = value;
        }
    } = 1024;

    /// <summary>Overrides the initial capacity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativeNodeArenaBuilder<TNode> WithCapacity(int capacity)
    {
        InitialCapacity = capacity;
        return this;
    }

    /// <summary>Creates the writable arena.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativeNodeArena<TNode, WritablePhase> Build() =>
        NativeNodeArena<TNode, WritablePhase>.Create(InitialCapacity);
}

/// <summary>
/// Append-only arena of unmanaged nodes in 64-byte-aligned native memory.
/// Handles are dense slot indices, so they stay stable across growth; sealing
/// moves the storage into the <see cref="SealedPhase"/> typestate and freezes it.
/// </summary>
/// <typeparam name="TNode">The node type stored in the arena.</typeparam>
/// <typeparam name="TPhase">The compile-time phase of the arena.</typeparam>
public sealed unsafe class NativeNodeArena<TNode, TPhase> : IReadOnlyArena<TNode>, IDisposable
    where TNode : unmanaged
    where TPhase : struct, IArenaPhase
{
    private const nuint CacheLineAlignment = 64;

    private readonly Lock _sync = new();
    private TNode* _memory;
    private int _capacity;
    private CachePaddedAtomicCounters _counters;
    private int _isDisposed;

    /// <inheritdoc/>
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _capacity);
    }

    /// <inheritdoc/>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _counters.Value);
    }

    internal static NativeNodeArena<TNode, WritablePhase> Create(int initialCapacity)
    {
        if (initialCapacity <= 0) ThrowHelper.ThrowArgumentOutOfRange(nameof(initialCapacity));

        nuint byteCount = checked((nuint)initialCapacity * (nuint)sizeof(TNode));
        TNode* memory = (TNode*)NativeMemory.AlignedAlloc(byteCount, CacheLineAlignment);
        NativeMemory.Clear(memory, byteCount);

        return new NativeNodeArena<TNode, WritablePhase>(memory, initialCapacity, 0);
    }

    private NativeNodeArena(TNode* memory, int capacity, int count)
    {
        _memory = memory;
        _capacity = capacity;
        _counters.Value = count;
        _counters.LockSignal = 0;
        _isDisposed = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureAlive()
    {
        if (Volatile.Read(ref _isDisposed) == 1)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(NativeNodeArena<TNode, TPhase>));
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly TNode Get(NodeHandle handle)
    {
        EnsureAlive();
        uint index = (uint)handle.Value;
        if (index >= (uint)_counters.Value)
        {
            ThrowHelper.ThrowIndexOutOfRange();
        }
        return ref _memory[index];
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<TNode> AsSpan(NodeRange range)
    {
        EnsureAlive();
        nuint offset = (nuint)(uint)range.Offset;
        nuint length = (nuint)(uint)range.Length;
        if (offset + length > (nuint)(uint)_counters.Value)
        {
            ThrowHelper.ThrowIndexOutOfRange();
        }
        return new ReadOnlySpan<TNode>(_memory + offset, (int)length);
    }

    internal NodeHandle InternalAllocate(in TNode value)
    {
        lock (_sync)
        {
            EnsureAlive();
            int index = _counters.Value;
            if ((uint)index >= (uint)_capacity)
            {
                Grow(index + 1);
            }

            _memory[index] = value;
            _counters.Value = index + 1;
            return new NodeHandle(index);
        }
    }

    internal NodeRange InternalAllocateBatch(ReadOnlySpan<TNode> nodes)
    {
        if (nodes.IsEmpty) return NodeRange.Empty;

        lock (_sync)
        {
            EnsureAlive();
            int offset = _counters.Value;
            int newCount = checked(offset + nodes.Length);

            if ((uint)newCount > (uint)_capacity)
            {
                Grow(newCount);
            }

            fixed (TNode* srcPtr = nodes)
            {
                nuint bytesToCopy = checked((nuint)nodes.Length * (nuint)sizeof(TNode));
                nuint destTotalBytes = checked((nuint)_capacity * (nuint)sizeof(TNode));
                Buffer.MemoryCopy(srcPtr, _memory + offset, (long)destTotalBytes, (long)bytesToCopy);
            }

            _counters.Value = newCount;
            return new NodeRange(offset, nodes.Length);
        }
    }

    internal ref TNode InternalGetMutable(NodeHandle handle)
    {
        EnsureAlive();
        uint index = (uint)handle.Value;
        if (index >= (uint)_counters.Value)
        {
            ThrowHelper.ThrowIndexOutOfRange();
        }
        return ref _memory[index];
    }

    internal Span<TNode> InternalAsMutableSpan(NodeRange range)
    {
        EnsureAlive();
        nuint offset = (nuint)(uint)range.Offset;
        nuint length = (nuint)(uint)range.Length;
        if (offset + length > (nuint)(uint)_counters.Value)
        {
            ThrowHelper.ThrowIndexOutOfRange();
        }
        return new Span<TNode>(_memory + offset, (int)length);
    }

    internal NativeNodeArena<TNode, SealedPhase> InternalSeal()
    {
        lock (_sync)
        {
            EnsureAlive();
            TNode* memory = _memory;
            int capacity = _capacity;
            int count = _counters.Value;

            _memory = null;
            _capacity = 0;
            _counters.Value = 0;
            Volatile.Write(ref _isDisposed, 1);

            return new NativeNodeArena<TNode, SealedPhase>(memory, capacity, count);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int minCapacity)
    {
        int newCap = checked(Math.Max(_capacity * 2, minCapacity));
        nuint newByteCount = checked((nuint)newCap * (nuint)sizeof(TNode));
        TNode* newMemory = (TNode*)NativeMemory.AlignedAlloc(newByteCount, CacheLineAlignment);
        NativeMemory.Clear(newMemory, newByteCount);

        nuint existingByteCount = (nuint)_counters.Value * (nuint)sizeof(TNode);
        if (existingByteCount > 0 && _memory != null)
        {
            Buffer.MemoryCopy(_memory, newMemory, (long)newByteCount, (long)existingByteCount);
            NativeMemory.AlignedFree(_memory);
        }

        _memory = newMemory;
        _capacity = newCap;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            lock (_sync)
            {
                if (_memory != null)
                {
                    NativeMemory.AlignedFree(_memory);
                    _memory = null;
                }
                _capacity = 0;
                _counters.Value = 0;
            }
        }
    }
}

/// <summary>Structural writes, available only on the writable phase.</summary>
public static class WritableArenaExtensions
{
    /// <summary>Appends one node and returns its handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NodeHandle Allocate<TNode>(
        this NativeNodeArena<TNode, WritablePhase> arena,
        in TNode value)
        where TNode : unmanaged
    {
        ArgumentNullException.ThrowIfNull(arena);
        return arena.InternalAllocate(in value);
    }

    /// <summary>Appends a batch and returns the covered range.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NodeRange AllocateBatch<TNode>(
        this NativeNodeArena<TNode, WritablePhase> arena,
        ReadOnlySpan<TNode> nodes)
        where TNode : unmanaged
    {
        ArgumentNullException.ThrowIfNull(arena);
        return arena.InternalAllocateBatch(nodes);
    }

    /// <summary>Returns a mutable reference to the node at <paramref name="handle"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TNode GetMutable<TNode>(
        this NativeNodeArena<TNode, WritablePhase> arena,
        NodeHandle handle)
        where TNode : unmanaged
    {
        ArgumentNullException.ThrowIfNull(arena);
        return ref arena.InternalGetMutable(handle);
    }

    /// <summary>Returns a mutable span over <paramref name="range"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<TNode> AsMutableSpan<TNode>(
        this NativeNodeArena<TNode, WritablePhase> arena,
        NodeRange range)
        where TNode : unmanaged
    {
        ArgumentNullException.ThrowIfNull(arena);
        return arena.InternalAsMutableSpan(range);
    }

    /// <summary>Freezes the arena into the sealed phase; the writer is spent.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NativeNodeArena<TNode, SealedPhase> Seal<TNode>(
        this NativeNodeArena<TNode, WritablePhase> arena)
        where TNode : unmanaged
    {
        ArgumentNullException.ThrowIfNull(arena);
        return arena.InternalSeal();
    }
}

/// <summary>
/// Builds a pinned managed-array node arena with the given capacity.
/// </summary>
/// <typeparam name="TNode">The node type stored in the arena.</typeparam>
public sealed class PinnedNodeArenaBuilder<TNode> where TNode : struct
{
    /// <summary>Slots reserved up front; must stay positive.</summary>
    public int InitialCapacity
    {
        get => field;
        set
        {
            if (value <= 0) ThrowHelper.ThrowArgumentOutOfRange(nameof(value));
            field = value;
        }
    } = 1024;

    /// <summary>Overrides the initial capacity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PinnedNodeArenaBuilder<TNode> WithCapacity(int capacity)
    {
        InitialCapacity = capacity;
        return this;
    }

    /// <summary>Creates the writable arena.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PinnedNodeArena<TNode, WritablePhase> Build() =>
        PinnedNodeArena<TNode, WritablePhase>.Create(InitialCapacity);
}

/// <summary>
/// Append-only arena of nodes in a pinned managed array: no native memory,
/// same handle discipline and seal typestate as the native flavor.
/// </summary>
/// <typeparam name="TNode">The node type stored in the arena.</typeparam>
/// <typeparam name="TPhase">The compile-time phase of the arena.</typeparam>
public sealed class PinnedNodeArena<TNode, TPhase> : IReadOnlyArena<TNode>, IDisposable
    where TNode : struct
    where TPhase : struct, IArenaPhase
{
    private readonly Lock _sync = new();
    private TNode[] _buffer;
    private CachePaddedAtomicCounters _counters;
    private int _isDisposed;

    /// <inheritdoc/>
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _buffer).Length;
    }

    /// <inheritdoc/>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _counters.Value);
    }

    internal static PinnedNodeArena<TNode, WritablePhase> Create(int initialCapacity)
    {
        if (initialCapacity <= 0) ThrowHelper.ThrowArgumentOutOfRange(nameof(initialCapacity));
        TNode[] buffer = GC.AllocateArray<TNode>(initialCapacity, pinned: true);
        return new PinnedNodeArena<TNode, WritablePhase>(buffer, 0);
    }

    private PinnedNodeArena(TNode[] buffer, int count)
    {
        _buffer = buffer;
        _counters.Value = count;
        _counters.LockSignal = 0;
        _isDisposed = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureAlive()
    {
        if (Volatile.Read(ref _isDisposed) == 1)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(PinnedNodeArena<TNode, TPhase>));
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly TNode Get(NodeHandle handle)
    {
        EnsureAlive();
        uint index = (uint)handle.Value;
        if (index >= (uint)_counters.Value)
        {
            ThrowHelper.ThrowIndexOutOfRange();
        }
        return ref _buffer[index];
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<TNode> AsSpan(NodeRange range)
    {
        EnsureAlive();
        nuint offset = (nuint)(uint)range.Offset;
        nuint length = (nuint)(uint)range.Length;
        if (offset + length > (nuint)(uint)_counters.Value)
        {
            ThrowHelper.ThrowIndexOutOfRange();
        }
        return new ReadOnlySpan<TNode>(_buffer, (int)offset, (int)length);
    }

    internal NodeHandle InternalAllocate(in TNode node)
    {
        lock (_sync)
        {
            EnsureAlive();
            int index = _counters.Value;
            if ((uint)index >= (uint)_buffer.Length)
            {
                Grow(index + 1);
            }

            _buffer[index] = node;
            _counters.Value = index + 1;
            return new NodeHandle(index);
        }
    }

    internal ref TNode InternalGetMutable(NodeHandle handle)
    {
        EnsureAlive();
        uint index = (uint)handle.Value;
        if (index >= (uint)_counters.Value)
        {
            ThrowHelper.ThrowIndexOutOfRange();
        }
        return ref _buffer[index];
    }

    internal PinnedNodeArena<TNode, SealedPhase> InternalSeal()
    {
        lock (_sync)
        {
            EnsureAlive();
            TNode[] buffer = _buffer;
            int count = _counters.Value;

            _buffer = [];
            _counters.Value = 0;
            Volatile.Write(ref _isDisposed, 1);

            return new PinnedNodeArena<TNode, SealedPhase>(buffer, count);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int minCapacity)
    {
        int newCap = checked(Math.Max(_buffer.Length * 2, minCapacity));
        TNode[] newBuf = GC.AllocateArray<TNode>(newCap, pinned: true);
        Array.Copy(_buffer, newBuf, _counters.Value);
        _buffer = newBuf;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            lock (_sync)
            {
                _buffer = [];
                _counters.Value = 0;
            }
        }
    }
}

/// <summary>Structural writes, available only on the writable phase.</summary>
public static class WritablePinnedArenaExtensions
{
    /// <summary>Appends one node and returns its handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NodeHandle Allocate<TNode>(
        this PinnedNodeArena<TNode, WritablePhase> arena,
        in TNode node)
        where TNode : struct
    {
        ArgumentNullException.ThrowIfNull(arena);
        return arena.InternalAllocate(in node);
    }

    /// <summary>Returns a mutable reference to the node at <paramref name="handle"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TNode GetMutable<TNode>(
        this PinnedNodeArena<TNode, WritablePhase> arena,
        NodeHandle handle)
        where TNode : struct
    {
        ArgumentNullException.ThrowIfNull(arena);
        return ref arena.InternalGetMutable(handle);
    }

    /// <summary>Freezes the arena into the sealed phase; the writer is spent.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PinnedNodeArena<TNode, SealedPhase> Seal<TNode>(
        this PinnedNodeArena<TNode, WritablePhase> arena)
        where TNode : struct
    {
        ArgumentNullException.ThrowIfNull(arena);
        return arena.InternalSeal();
    }
}
