using Axrone.Execution;
using RenderPump = Axrone.Execution.CommandPump<
    Axrone.Render.Core.RenderCommand,
    Axrone.Render.OpenGL.FrameGraph.RenderPumpContext,
    Axrone.Render.OpenGL.FrameGraph.RenderCommandProcessor,
    Axrone.Utility.Backoff.SpinPolicies.AdaptiveSpinBackoff,
    Axrone.Execution.NullExecutorTelemetry>;

namespace Axrone.Render.OpenGL.FrameGraph;

/// <summary>
/// The state every <see cref="FrameGraph{TPhase, TPolicy}"/> handle shares: the
/// pass table, the interned resource table, the compiled emission order, the GL
/// plumbing and the command pump.
/// </summary>
/// <remarks>
/// <para><b>Why a reference-shared class behind a struct handle.</b> The handle
/// is a <c>readonly struct</c> so that the typestate phase is carried in the type,
/// not in a flag, and a handle costs one reference: it is copied, never
/// aliased-by-copy, and <c>graph.AddPass(x)</c> keeps working whether the caller
/// uses the returned handle or ignores it, because both handles point at the
/// same storage. That is exactly what the non-generic mutable graph did, so no
/// caller has to change how it holds the value. A <c>ref struct</c> was not an
/// option: the handle has to be storable in a field across frames.</para>
/// <para><b>Thread affinity.</b> Single render thread, no locks, by design. The
/// graph mutates a pass table, interns names into a dictionary, rebuilds pooled
/// scratch and drives a single-consumer command pump; none of that is made
/// correct by a lock, because the GL context itself is thread-affine (see
/// <see cref="GLContext.AssertRenderThread"/>). Every public entry point must be
/// called from the render thread that owns the graph, and <see cref="Clear"/> and
/// <see cref="Dispose"/> must not race a walk. This is the same contract the
/// previous class documented implicitly through its single-consumer pump.</para>
/// <para><b>No fixed capacity.</b> The pass table, the interned resource table
/// and the interned span buffers are all dynamic, doubling as they grow. A cap
/// would have to be guessed (too small and a shipped scene fails, too large and
/// every graph pays for a frame it does not use) and the frame graph is a
/// mutation-leg structure, not a per-frame hot allocation.</para>
/// <para><b>Pooled scratch, not a lifetime allocator.</b> The compile walk rents
/// its O(passes) and O(edges) scratch from <see cref="ArrayPool{T}"/> and returns
/// it in <c>finally</c>; the interned span buffers are owned by the storage and
/// are reused across compiles, so steady-state compiles allocate nothing at all
/// except the emission order itself. A bump/arena lifetime allocator would only
/// be justified by a measured allocation profile, and there is none: compile
/// runs on the mutation leg, not per frame.</para>
/// </remarks>
internal sealed class FrameGraphStorage : IDisposable
{
    /// <summary>Initial capacity of the pass table.</summary>
    private const int InitialPassCapacity = 32;

    /// <summary>Initial capacity of an interned span buffer.</summary>
    private const int InitialSpanCapacity = 64;

    private readonly GLContext _context;
    private readonly GLRenderContext _renderContext;
    private readonly PassExecutionContext _execContext;
    private readonly RenderPump _pump;
    private PassEntry[] _passes = new PassEntry[InitialPassCapacity];
    private int _passCount;
    private readonly Dictionary<string, ResourceId> _resourceIds = new(StringComparer.OrdinalIgnoreCase);
    private int[] _readIds = new int[InitialSpanCapacity];
    private int[] _writeIds = new int[InitialSpanCapacity];
    private int _readIdCount;
    private int _writeIdCount;
    private PassId[] _order = Array.Empty<PassId>();
    private int _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameGraphStorage"/> class.
    /// </summary>
    /// <param name="context">The GL context the graph renders on.</param>
    /// <param name="pumpCapacity">Ring slot capacity of the graph-owned command pump.</param>
    internal FrameGraphStorage(GLContext context, int pumpCapacity)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _execContext = new PassExecutionContext(context);
        _renderContext = new GLRenderContext(context, _execContext);
        _pump = new RenderPump(new ExecutorOptions { Capacity = pumpCapacity });
    }

    /// <summary>
    /// Gets a value indicating whether the graph has been disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

    /// <summary>Gets the number of passes currently in the graph.</summary>
    public int PassCount => _passCount;

    /// <summary>Gets the graph-owned command pump.</summary>
    public RenderPump Pump => _pump;

    /// <summary>Gets the graph-owned render context every pass executes on.</summary>
    public GLRenderContext RenderContext => _renderContext;

    /// <summary>Gets the graph-owned per-frame resource scratch map.</summary>
    public PassExecutionContext ExecutionContext => _execContext;

    /// <summary>Gets the compiled emission order, empty until a compile succeeds.</summary>
    public ReadOnlySpan<PassId> Order => _order;

    /// <summary>
    /// Throws when the graph has been disposed. Every public entry point calls
    /// this first, so a use-after-dispose is an
    /// <see cref="ObjectDisposedException"/> rather than a null dereference.
    /// </summary>
    public void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            ThrowHelper.ThrowObjectDisposed(GraphConstants.Name);
        }
    }

    /// <summary>
    /// Resolves a pass id to its pass. Ids are dense, so this is a bounds-checked
    /// array walk and not a lookup.
    /// </summary>
    /// <param name="id">The pass id.</param>
    /// <returns>The pass stored in that slot.</returns>
    public IRenderPass GetPass(PassId id) => _passes[(int)id.Value].Pass;

    /// <summary>
    /// Adds a pass and interns every resource name it declares, in declaration
    /// order, into the interned span buffers.
    /// </summary>
    /// <remarks>
    /// <para>Interning here, not at compile, is what makes the compile walk
    /// integer-only: the names are authored by users and editors and are
    /// comfortable to read and diff, but the walk is machine work that would
    /// otherwise pay <see cref="StringComparer.OrdinalIgnoreCase"/> hashing and
    /// comparison for every candidate edge.</para>
    /// <para>Each pass owns one contiguous offset/count span per direction inside
    /// the shared buffer, so the walk reads a pass's dependencies as a slice of
    /// one array. The buffer is append-only: a pass's span never moves, so growth
    /// is a copy and never invalidates a span that was already handed out.
    /// Declaration order is preserved inside the span, which is what keeps the
    /// compiled emission order identical to the order the non-generic graph
    /// produced.</para>
    /// </remarks>
    /// <param name="pass">The pass to add.</param>
    public void AddPass(IRenderPass pass)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(pass);

        // Grow before appending so the interned spans a pass is about to own are
        // never invalidated, and so the pass itself lands in a slot that stays
        // put for the graph's lifetime: a pass id is a slot, not an object
        // reference the walk has to re-resolve.
        if (_passCount == _passes.Length)
        {
            var grown = new PassEntry[_passes.Length * 2];
            Array.Copy(_passes, grown, _passCount);
            _passes = grown;
        }

        int readStart = _readIdCount;
        ReadOnlySpan<string> reads = pass.GetReadResources();
        for (int i = 0; i < reads.Length; i++)
        {
            _readIds = AppendInterned(_readIds, ref _readIdCount, reads[i]);
        }

        int writeStart = _writeIdCount;
        ReadOnlySpan<string> writes = pass.GetWrittenResources();
        for (int i = 0; i < writes.Length; i++)
        {
            _writeIds = AppendInterned(_writeIds, ref _writeIdCount, writes[i]);
        }

        _passes[_passCount] = new PassEntry(pass, readStart, _readIdCount - readStart, writeStart, _writeIdCount - writeStart);
        _passCount++;
    }

    /// <summary>
    /// Interns a resource name, assigning it the next dense
    /// <see cref="ResourceId"/> on first sight.
    /// </summary>
    /// <param name="name">The declared resource name.</param>
    /// <returns>The interned id. Case-insensitive, matching the dependency semantics of the previous graph.</returns>
    private ResourceId Intern(string name)
    {
        if (_resourceIds.TryGetValue(name, out ResourceId existing))
        {
            return existing;
        }

        // Ids are dense from zero and the dictionary holds exactly one entry per
        // id, so the count is the next id: no separate counter to keep in sync.
        var interned = new ResourceId((uint)_resourceIds.Count);
        _resourceIds.Add(name, interned);
        return interned;
    }

    /// <summary>
    /// Appends the interned id of <paramref name="name"/> to
    /// <paramref name="buffer"/>, growing it by doubling when full.
    /// </summary>
    /// <param name="buffer">The pooled span buffer.</param>
    /// <param name="count">The buffer's fill count, advanced on append.</param>
    /// <param name="name">The declared resource name.</param>
    /// <returns>The (possibly reallocated) buffer to keep appending into.</returns>
    private int[] AppendInterned(int[] buffer, ref int count, string name)
    {
        if (count == buffer.Length)
        {
            int capacity = buffer.Length * 2;
            var grown = new int[capacity];
            Array.Copy(buffer, grown, buffer.Length);
            buffer = grown;
        }

        buffer[count] = (int)Intern(name).Value;
        count++;
        return buffer;
    }

    /// <summary>
    /// Clears the graph for the next frame, leaving the command pump alive and
    /// the pooled span buffers allocated for reuse.
    /// </summary>
    /// <remarks>
    /// Resource ids restart at zero, so <see cref="PassId"/> and
    /// <see cref="ResourceId"/> values are only meaningful until the next
    /// <c>Reset</c>. Commands the pump still holds survive, exactly as before.
    /// </remarks>
    public void Clear()
    {
        ThrowIfDisposed();

        Array.Clear(_passes, 0, _passCount);
        _passCount = 0;
        _readIdCount = 0;
        _writeIdCount = 0;
        _resourceIds.Clear();
        _order = Array.Empty<PassId>();
        _execContext.Clear();
    }

    /// <summary>
    /// Validates every enabled pass and then orders the enabled passes
    /// topologically, over interned resource ids.
    /// </summary>
    /// <remarks>
    /// <para><b>Validation first, always.</b> Every enabled pass has its
    /// <see cref="IRenderPass.Validate"/> called, and the attachment-action
    /// metadata check (a <see cref="AttachmentLoadAction.DontCare"/> pass that
    /// writes nothing) runs against the same cached read/write snapshot the
    /// dependency build below uses, so validation costs no extra traversal and no
    /// allocation. A disabled pass is neither validated nor ordered — that is the
    /// same contract as before, and it is why a pass can be toggled off without
    /// its configuration becoming illegal.</para>
    /// <para><b>The walk.</b> Three steps, all on pooled arrays: build the
    /// resource-to-readers index (one counting pass and one fill pass over the
    /// interned read spans), count and then fill the writer-to-readers edges into
    /// offset/count adjacency spans, and run Kahn with a pooled ring queue. No
    /// strings, no <c>Dictionary</c>, no <c>List&lt;int&gt;</c> per writer, and no
    /// per-edge allocation: the adjacency is two flat <c>int[]</c> spans. Only the
    /// emission order itself is a fresh array, so a steady-state compile
    /// allocates one <c>PassId[]</c> and nothing else.</para>
    /// <para><b>Order stability.</b> Writers are visited in ascending pass id,
    /// each writer's writes in declaration order, and each resource's reader list
    /// in ascending pass id, so the adjacency of a writer is in ascending reader
    /// order; the queue is seeded in ascending pass id and drains FIFO. The
    /// resulting order is therefore deterministic and identical to the order the
    /// previous dictionary-and-list walk produced, which is what keeps
    /// <c>GetTopologicalOrder</c> and the execution walk reproducible.</para>
    /// <para><b>Edges.</b> A reader depends on every <i>writer</i> of the
    /// resources it reads. Two writers of the same resource get no edge between
    /// them: a resource that is overwritten twice in a frame carries no ordering
    /// information between those two passes, and imposing one would reject graphs
    /// that render correctly today. A (writer, reader) pair is emitted once even
    /// when the pair shares several resources, which a per-writer stamp array
    /// detects in O(1) without a set or a re-scan.</para>
    /// <para><b>Cycles.</b> A cycle is reported, not repaired: the walk returns
    /// <c>false</c> with the totals, the caller routes them to its policy, and no
    /// partial order is ever cached. A half-ordered graph is worse than a
    /// failure, because it renders a frame with GL effects in an order the caller
    /// never asked for.</para>
    /// </remarks>
    /// <param name="resolvedPasses">The number of passes ordered before the walk stalled.</param>
    /// <param name="totalPasses">The number of enabled passes the walk tried to order.</param>
    /// <returns><c>true</c> when every enabled pass was ordered; the order is cached on success.</returns>
    public bool TryCompile(out uint resolvedPasses, out uint totalPasses)
    {
        ThrowIfDisposed();

        resolvedPasses = 0;

        ValidateEnabledPasses();

        int passCount = _passCount;
        int enabledCount = CountEnabledPasses(passCount);
        totalPasses = (uint)enabledCount;
        if (enabledCount == 0)
        {
            _order = Array.Empty<PassId>();
            return true;
        }

        int resourceCount = _resourceIds.Count;
        int readEntryCount = _readIdCount;

        int[] inDegree = ArrayPool<int>.Shared.Rent(passCount);
        int[] edgeCounts = ArrayPool<int>.Shared.Rent(passCount);
        int[] edgeOffsets = ArrayPool<int>.Shared.Rent(passCount + 1);
        int[] edgeStamp = ArrayPool<int>.Shared.Rent(passCount);
        int[] edgeTargets = Array.Empty<int>();
        int[] queueStorage = ArrayPool<int>.Shared.Rent(passCount);
        int[] resourceOffsets = ArrayPool<int>.Shared.Rent(resourceCount + 1);
        int[] resourceReaders = ArrayPool<int>.Shared.Rent(readEntryCount);
        int[] resourceCursor = ArrayPool<int>.Shared.Rent(resourceCount);
        try
        {
            for (int i = 0; i < passCount; i++)
            {
                // -1 = disabled pass (never emitted, never ordered), otherwise
                // the remaining dependency count.
                inDegree[i] = _passes[i].Pass.IsEnabled ? 0 : -1;
                edgeCounts[i] = 0;
            }

            BuildReaderIndex(passCount, resourceCount, readEntryCount, resourceOffsets, resourceReaders, resourceCursor);

            // Count pass: edges per writer and the remaining dependency count per
            // reader. Same traversal as the fill pass below, so both see the
            // identical (writer, reader) sequence.
            CountEdges(passCount, resourceOffsets, resourceReaders, inDegree, edgeCounts, edgeStamp);

            // Offset/count adjacency spans over the counted edges. The fill needs
            // room for exactly as many targets as the count pass recorded, so the
            // target buffer is sized from the same prefix sum.
            int totalEdges = 0;
            for (int i = 0; i < passCount; i++)
            {
                edgeOffsets[i] = totalEdges;
                totalEdges += edgeCounts[i];
            }

            edgeOffsets[passCount] = totalEdges;
            edgeTargets = ArrayPool<int>.Shared.Rent(Math.Max(totalEdges, 1));
            Array.Fill(edgeStamp, -1);
            FillEdges(passCount, resourceOffsets, resourceReaders, inDegree, edgeOffsets, edgeStamp, edgeTargets);

            PassId[] sorted = GC.AllocateUninitializedArray<PassId>(enabledCount);
            int sortedCount = Emit(passCount, inDegree, edgeOffsets, edgeTargets, queueStorage, sorted);

            if (sortedCount != enabledCount)
            {
                resolvedPasses = (uint)sortedCount;
                return false;
            }

            resolvedPasses = (uint)sortedCount;
            _order = sorted;
            return true;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(inDegree);
            ArrayPool<int>.Shared.Return(edgeCounts);
            ArrayPool<int>.Shared.Return(edgeOffsets);
            ArrayPool<int>.Shared.Return(edgeStamp);
            if (edgeTargets.Length > 0)
            {
                ArrayPool<int>.Shared.Return(edgeTargets);
            }

            ArrayPool<int>.Shared.Return(queueStorage);
            ArrayPool<int>.Shared.Return(resourceOffsets);
            ArrayPool<int>.Shared.Return(resourceReaders);
            ArrayPool<int>.Shared.Return(resourceCursor);
        }
    }

    /// <summary>
    /// Calls <see cref="IRenderPass.Validate"/> on every enabled pass and runs the
    /// attachment-action metadata check against the same declared write set the
    /// dependency build uses.
    /// </summary>
    private void ValidateEnabledPasses()
    {
        int passCount = _passCount;
        for (int i = 0; i < passCount; i++)
        {
            IRenderPass pass = _passes[i].Pass;
            if (!pass.IsEnabled)
            {
                continue;
            }

            pass.Validate();

            // Attachment-action metadata validation (opt-in, metadata only):
            // DontCare promises the pass overwrites the attachment in full, which
            // requires that it actually writes something. The snapshot read here is
            // the pass's own interned write span source, so this adds no allocation
            // and no extra traversal of the declarations.
            if (pass.LoadAction == AttachmentLoadAction.DontCare && pass.GetWrittenResources().IsEmpty)
            {
                ThrowHelper.Throw(
                    RenderErrorCode.InvalidPassConfiguration,
                    $"Pass '{pass.Name}' declares AttachmentLoadAction.DontCare but writes no resources; the attachment would never be filled",
                    GraphConstants.Name);
            }
        }
    }

    /// <summary>
    /// Counts the passes that are enabled, which is the emission target of the
    /// walk and the <c>totalPasses</c> a cycle is reported against.
    /// </summary>
    /// <param name="passCount">The number of passes in the graph.</param>
    /// <returns>The number of enabled passes.</returns>
    private int CountEnabledPasses(int passCount)
    {
        int enabled = 0;
        for (int i = 0; i < passCount; i++)
        {
            if (_passes[i].Pass.IsEnabled)
            {
                enabled++;
            }
        }

        return enabled;
    }

    /// <summary>
    /// Builds the resource-to-readers index in offset/count spans: two linear
    /// passes over the interned read spans, no per-resource list.
    /// </summary>
    /// <param name="passCount">The number of passes in the graph.</param>
    /// <param name="resourceCount">The number of interned resources.</param>
    /// <param name="readEntryCount">The total number of declared read entries.</param>
    /// <param name="resourceOffsets">Receives the per-resource start offsets; length <c>resourceCount + 1</c>.</param>
    /// <param name="resourceReaders">Receives the reader pass ids; length <c>readEntryCount</c>.</param>
    /// <param name="resourceCursor">Scratch holding the per-resource fill cursor; length <c>resourceCount</c>.</param>
    private void BuildReaderIndex(int passCount, int resourceCount, int readEntryCount, int[] resourceOffsets, int[] resourceReaders, int[] resourceCursor)
    {
        Array.Clear(resourceOffsets, 0, resourceCount + 1);
        for (int i = 0; i < passCount; i++)
        {
            ref readonly PassEntry entry = ref _passes[i];
            for (int r = 0; r < entry.ReadCount; r++)
            {
                resourceOffsets[_readIds[entry.ReadStart + r] + 1]++;
            }
        }

        for (int resource = 0; resource < resourceCount; resource++)
        {
            resourceOffsets[resource + 1] += resourceOffsets[resource];
        }

        Debug.Assert(resourceOffsets[resourceCount] == readEntryCount, "The reader index and the interned read spans must cover the same entries.");

        Array.Copy(resourceOffsets, resourceCursor, resourceCount);

        // Filling in ascending pass order is what makes every reader list
        // ascending, which is what keeps each writer's adjacency ascending and
        // therefore keeps the emission order deterministic.
        for (int i = 0; i < passCount; i++)
        {
            ref readonly PassEntry entry = ref _passes[i];
            for (int r = 0; r < entry.ReadCount; r++)
            {
                int resource = _readIds[entry.ReadStart + r];
                resourceReaders[resourceCursor[resource]++] = i;
            }
        }
    }

    /// <summary>
    /// Counts the writer-to-readers edges and the remaining dependency count of
    /// every reader, deduplicating a (writer, reader) pair with a per-writer
    /// stamp instead of a set.
    /// </summary>
    /// <param name="passCount">The number of passes in the graph.</param>
    /// <param name="resourceOffsets">The per-resource start offsets of the reader index.</param>
    /// <param name="resourceReaders">The reader pass ids of the reader index.</param>
    /// <param name="inDegree">Receives -1 for disabled passes and the dependency count for enabled ones.</param>
    /// <param name="edgeCounts">Receives the number of readers of each writer.</param>
    /// <param name="edgeStamp">Scratch stamped with the last writer that already counted a reader.</param>
    private void CountEdges(int passCount, int[] resourceOffsets, int[] resourceReaders, int[] inDegree, int[] edgeCounts, int[] edgeStamp)
    {
        Array.Fill(edgeStamp, -1);

        for (int i = 0; i < passCount; i++)
        {
            if (inDegree[i] < 0)
            {
                continue;
            }

            ref readonly PassEntry entry = ref _passes[i];
            int written = 0;
            for (int w = 0; w < entry.WriteCount; w++)
            {
                int resource = _writeIds[entry.WriteStart + w];
                int end = resourceOffsets[resource + 1];
                for (int k = resourceOffsets[resource]; k < end; k++)
                {
                    int reader = resourceReaders[k];
                    if (reader == i || inDegree[reader] < 0)
                    {
                        continue;
                    }

                    if (edgeStamp[reader] == i)
                    {
                        continue;
                    }

                    edgeStamp[reader] = i;
                    inDegree[reader]++;
                    written++;
                }
            }

            edgeCounts[i] = written;
        }
    }

    /// <summary>
    /// Fills the adjacency spans counted by
    /// <see cref="CountEdges"/>, in the identical traversal order.
    /// </summary>
    /// <param name="passCount">The number of passes in the graph.</param>
    /// <param name="resourceOffsets">The per-resource start offsets of the reader index.</param>
    /// <param name="resourceReaders">The reader pass ids of the reader index.</param>
    /// <param name="inDegree">The dependency table, still unmutated: a negative entry marks a disabled pass.</param>
    /// <param name="edgeOffsets">The per-writer start offsets of the adjacency.</param>
    /// <param name="edgeStamp">Scratch stamped with the last writer that already emitted a reader.</param>
    /// <param name="edgeTargets">Receives the reader pass ids of each writer.</param>
    private void FillEdges(int passCount, int[] resourceOffsets, int[] resourceReaders, int[] inDegree, int[] edgeOffsets, int[] edgeStamp, int[] edgeTargets)
    {
        for (int i = 0; i < passCount; i++)
        {
            // A disabled writer is a slot in the adjacency with a zero-length
            // span: its reader list was never counted, so emitting into it would
            // overwrite the next writer's span.
            if (inDegree[i] < 0)
            {
                continue;
            }

            ref readonly PassEntry entry = ref _passes[i];
            int position = edgeOffsets[i];
            for (int w = 0; w < entry.WriteCount; w++)
            {
                int resource = _writeIds[entry.WriteStart + w];
                int end = resourceOffsets[resource + 1];
                for (int k = resourceOffsets[resource]; k < end; k++)
                {
                    int reader = resourceReaders[k];
                    if (reader == i || inDegree[reader] < 0 || edgeStamp[reader] == i)
                    {
                        continue;
                    }

                    // Disabled readers and self-edges are skipped exactly as the
                    // counting pass skipped them, so the fill reproduces that
                    // traversal edge for edge and lands inside every counted span.
                    edgeStamp[reader] = i;
                    edgeTargets[position++] = reader;
                }
            }
        }
    }

    /// <summary>
    /// Runs Kahn's algorithm over the adjacency spans and writes the emission
    /// order into <paramref name="sorted"/>.
    /// </summary>
    /// <param name="passCount">The number of passes in the graph.</param>
    /// <param name="inDegree">The remaining dependency count of every pass.</param>
    /// <param name="edgeOffsets">The per-writer start offsets of the adjacency.</param>
    /// <param name="edgeTargets">The reader pass ids of each writer.</param>
    /// <param name="queueStorage">Ring storage for the ready queue.</param>
    /// <param name="sorted">Receives the emission order.</param>
    /// <returns>The number of passes emitted; less than the enabled count means a cycle.</returns>
    private static int Emit(int passCount, int[] inDegree, int[] edgeOffsets, int[] edgeTargets, int[] queueStorage, PassId[] sorted)
    {
        // Ring queue: each pass is enqueued at most once, so passCount slots
        // suffice. Seeding in ascending pass id keeps the emission order stable.
        int head = 0;
        int tail = 0;
        int count = 0;
        for (int i = 0; i < passCount; i++)
        {
            if (inDegree[i] == 0)
            {
                queueStorage[tail] = i;
                tail++;
                if (tail >= passCount)
                {
                    tail = 0;
                }

                count++;
            }
        }

        int sortedCount = 0;
        while (count > 0)
        {
            int current = queueStorage[head];
            head++;
            if (head >= passCount)
            {
                head = 0;
            }

            count--;
            sorted[sortedCount] = new PassId((uint)current);
            sortedCount++;

            int end = edgeOffsets[current + 1];
            for (int k = edgeOffsets[current]; k < end; k++)
            {
                int reader = edgeTargets[k];
                int remaining = inDegree[reader] - 1;
                inDegree[reader] = remaining;
                if (remaining == 0)
                {
                    queueStorage[tail] = reader;
                    tail++;
                    if (tail >= passCount)
                    {
                        tail = 0;
                    }

                    count++;
                }
            }
        }

        return sortedCount;
    }

    /// <summary>
    /// Pumps every command currently queued in the owned
    /// <see cref="Pump"/> through the render command processor.
    /// </summary>
    /// <returns>The number of commands processed.</returns>
    public uint PumpQueuedCommands()
    {
        var pumpContext = new RenderPumpContext(_context);
        return (uint)_pump.PumpAll(ref pumpContext);
    }

    /// <summary>
    /// Releases the graph: clears the tables, clears the per-frame resource
    /// scratch and disposes the command pump and its native ring memory.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            Array.Clear(_passes, 0, _passCount);
            _passCount = 0;
            _readIdCount = 0;
            _writeIdCount = 0;
            _resourceIds.Clear();
            _order = Array.Empty<PassId>();
            _execContext.Clear();
            _pump.Dispose();
        }
    }

    /// <summary>
    /// One pass plus the offset/count spans that hold its interned read and write
    /// resource ids.
    /// </summary>
    /// <remarks>
    /// The spans are offsets into the storage's append-only interned buffers, so
    /// a pass entry is 20 bytes and the dependencies of every pass live in two
    /// flat arrays the walk can stream sequentially.
    /// </remarks>
    private readonly struct PassEntry
    {
        /// <summary>The pass.</summary>
        public readonly IRenderPass Pass;

        /// <summary>Start of the pass's read span in the interned read buffer.</summary>
        public readonly int ReadStart;

        /// <summary>Length of the pass's read span.</summary>
        public readonly int ReadCount;

        /// <summary>Start of the pass's write span in the interned write buffer.</summary>
        public readonly int WriteStart;

        /// <summary>Length of the pass's write span.</summary>
        public readonly int WriteCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="PassEntry"/> struct.
        /// </summary>
        /// <param name="pass">The pass.</param>
        /// <param name="readStart">Start of the read span.</param>
        /// <param name="readCount">Length of the read span.</param>
        /// <param name="writeStart">Start of the write span.</param>
        /// <param name="writeCount">Length of the write span.</param>
        public PassEntry(IRenderPass pass, int readStart, int readCount, int writeStart, int writeCount)
        {
            Pass = pass;
            ReadStart = readStart;
            ReadCount = readCount;
            WriteStart = writeStart;
            WriteCount = writeCount;
        }
    }
}
