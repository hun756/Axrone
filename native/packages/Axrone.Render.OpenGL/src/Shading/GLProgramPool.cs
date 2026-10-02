namespace Axrone.Render.OpenGL.Shading;

/// <summary>
/// Keyed cache of <see cref="GLProgram"/> instances with least-recently-used (LRU) eviction.
/// </summary>
/// <remarks>
/// <para>
/// Programs are keyed by a 64-bit key derived from two independent 32-bit FNV-1a hashes
/// (one over the vertex source, one over the fragment source), so repeated requests for the
/// same source pair reuse a single compiled program.
/// </para>
/// <para>
/// The pool is thread-affine: <see cref="GetOrCreate"/> and <see cref="Clear"/> must be called
/// from the render thread of the context that owns the cached programs. The cache key covers
/// shader sources only — use one pool per <see cref="GLContext"/>.
/// </para>
/// <para>
/// Evicted programs are disposed immediately by the pool. The pool does not track outstanding
/// references: a program that a caller is still using when it is evicted becomes invalid
/// (its GL name is deleted). The safe usage pattern is to re-acquire every frame through
/// <see cref="GetOrCreate"/> and never hold a program across frames without re-acquiring:
/// re-acquired programs stay most-recently-used, so eviction only reclaims programs that
/// have been unused for at least <see cref="Capacity"/> distinct program requests.
/// <see cref="EvictionCount"/> exposes how often eviction fired for capacity tuning.
/// </para>
/// </remarks>
public sealed class GLProgramPool : IDisposable
{
    private const int DefaultCapacity = 64;

    private readonly Dictionary<ulong, CacheEntry> _entries;
    private readonly LinkedList<ulong> _lru;
    private readonly int _capacity;
    private long _evictionCount;
    private int _isDisposed;

    /// <summary>
    /// Gets the maximum number of programs retained before the least recently used one is evicted.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets the number of programs currently cached.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Gets the total number of evictions since creation. A rising count against a
    /// stable working set means the capacity is undersized for the material variety.
    /// </summary>
    public long EvictionCount => Volatile.Read(ref _evictionCount);

    /// <summary>
    /// Gets a value indicating whether the pool has been disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLProgramPool"/> class.
    /// No programs are compiled until <see cref="GetOrCreate"/> is called.
    /// </summary>
    /// <param name="capacity">The maximum number of cached programs. Must be positive.</param>
    public GLProgramPool(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
            ThrowHelper.ThrowInvalidArgument("Capacity must be positive");

        _capacity = capacity;
        _entries = new Dictionary<ulong, CacheEntry>(capacity);
        _lru = new LinkedList<ulong>();
    }

    /// <summary>
    /// Returns the cached program for the given shader sources, compiling and caching a new
    /// <see cref="GLProgram"/> on a cache miss. On a hit the program is marked most recently used.
    /// </summary>
    /// <param name="context">The GL context that owns the cached programs.</param>
    /// <param name="vertexSource">The vertex shader source.</param>
    /// <param name="fragmentSource">The fragment shader source.</param>
    /// <returns>The cached or newly compiled program.</returns>
    public GLProgram GetOrCreate(GLContext context, string vertexSource, string fragmentSource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(vertexSource);
        ArgumentNullException.ThrowIfNull(fragmentSource);

        context.AssertRenderThread();

        if (IsDisposed)
            ThrowHelper.ThrowInvalidOperation("GLProgramPool disposed");

        ulong key = ComputeKey(vertexSource, fragmentSource);

        if (_entries.TryGetValue(key, out CacheEntry? entry))
        {
            _lru.Remove(entry.Node);
            _lru.AddFirst(entry.Node);
            return entry.Program;
        }

        GLProgram program = new(context, vertexSource, fragmentSource);
        LinkedListNode<ulong> node = _lru.AddFirst(key);
        _entries[key] = new CacheEntry(program, node);

        while (_entries.Count > _capacity)
        {
            LinkedListNode<ulong> evictedNode = _lru.Last!;
            _lru.RemoveLast();
            if (_entries.Remove(evictedNode.Value, out CacheEntry? evicted))
            {
                Interlocked.Increment(ref _evictionCount);
                evicted.Program.Dispose();
            }
        }

        return program;
    }

    /// <summary>
    /// Disposes every cached program and empties the pool.
    /// </summary>
    public void Clear()
    {
        if (IsDisposed)
            ThrowHelper.ThrowInvalidOperation("GLProgramPool disposed");

        foreach (CacheEntry entry in _entries.Values)
        {
            entry.Program.Dispose();
        }

        _entries.Clear();
        _lru.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        foreach (CacheEntry entry in _entries.Values)
        {
            entry.Program.Dispose();
        }

        _entries.Clear();
        _lru.Clear();
    }

    private static ulong ComputeKey(string vertexSource, string fragmentSource)
    {
        uint vertexHash = Fnv1a32Algorithm.Hash(MemoryMarshal.AsBytes(vertexSource.AsSpan())).Value;
        uint fragmentHash = Fnv1a32Algorithm.Hash(MemoryMarshal.AsBytes(fragmentSource.AsSpan())).Value;
        return ((ulong)vertexHash << 32) | fragmentHash;
    }

    private sealed class CacheEntry
    {
        public CacheEntry(GLProgram program, LinkedListNode<ulong> node)
        {
            Program = program;
            Node = node;
        }

        public GLProgram Program { get; }

        public LinkedListNode<ulong> Node { get; }
    }
}
