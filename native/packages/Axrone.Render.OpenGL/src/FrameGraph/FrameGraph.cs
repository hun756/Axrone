using Axrone.Execution;
using RenderPump = Axrone.Execution.CommandPump<
    Axrone.Render.Core.RenderCommand,
    Axrone.Render.OpenGL.FrameGraph.RenderPumpContext,
    Axrone.Render.OpenGL.FrameGraph.RenderCommandProcessor,
    Axrone.Utility.Backoff.SpinPolicies.AdaptiveSpinBackoff,
    Axrone.Execution.NullExecutorTelemetry>;

namespace Axrone.Render.OpenGL.FrameGraph;

/// <summary>
/// Frame graph that manages render pass ordering, resource lifetime, and execution.
/// Dependencies form a DAG over resource writes/reads, ordered by Kahn's algorithm.
/// </summary>
public sealed class FrameGraph : IDisposable
{
    private readonly GLContext _context;
    private readonly List<RenderPass> _passes = new(32);
    private readonly Dictionary<string, FrameGraphResource> _transientResources = new(StringComparer.OrdinalIgnoreCase);
    private readonly PassExecutionContext _execContext;
    private readonly RenderPump _pump;
    private RenderPass[]? _sortedPasses;
    private int _isDisposed;

    /// <summary>
    /// Gets a value indicating whether this frame graph has been disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

    /// <summary>
    /// Gets the number of passes in the graph.
    /// </summary>
    public int PassCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _passes.Count;
    }

    /// <summary>
    /// Gets the command pump owned by this graph. Pump-capable passes
    /// (<see cref="IPumpEnqueue"/>) enqueue library commands into this pump during
    /// <see cref="EnqueuePumpPasses"/>; the consumer drains it via
    /// <see cref="PumpQueuedCommands"/>. The pump is created with the graph and
    /// disposed with it; <see cref="Reset"/> leaves it alive.
    /// </summary>
    public RenderPump Pump
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _pump;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameGraph"/> class.
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="pumpCapacity">
    /// Ring slot capacity for the owned command pump. Must be a power of two
    /// greater than or equal to 2; the validation performed by
    /// <see cref="ExecutorOptions"/> propagates its argument exception unchanged.
    /// </param>
    public FrameGraph(GLContext context, int pumpCapacity = 256)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _execContext = new PassExecutionContext(context);
        _pump = new RenderPump(new ExecutorOptions { Capacity = pumpCapacity });
    }

    /// <summary>
    /// Adds a render pass to the graph.
    /// </summary>
    /// <param name="pass">The render pass to add.</param>
    /// <returns>This frame graph for fluent chaining.</returns>
    public FrameGraph AddPass(RenderPass pass)
    {
        if (IsDisposed)
            ThrowHelper.ThrowObjectDisposed(nameof(FrameGraph));

        ArgumentNullException.ThrowIfNull(pass);
        _passes.Add(pass);
        _sortedPasses = null;
        return this;
    }

    /// <summary>
    /// Compiles the frame graph: validates, resolves dependencies, topologically sorts.
    /// Runs on graph changes only (result cached); not on the per-frame hot path.
    /// </summary>
    public void Compile()
    {
        if (IsDisposed)
            ThrowHelper.ThrowObjectDisposed(nameof(FrameGraph));

        // 1. Validate all passes
        for (int i = 0; i < _passes.Count; i++)
        {
            if (_passes[i].IsEnabled)
            {
                _passes[i].Validate();

                // Attachment-action metadata validation (opt-in, metadata only):
                // DontCare promises the pass overwrites the attachment in full, which
                // requires that it actually writes something. The snapshot taken here
                // is cached and reused by the dependency build below, so this adds no
                // allocation and no extra traversal.
                if (_passes[i].LoadAction == AttachmentLoadAction.DontCare && _passes[i].GetWrittenResources().IsEmpty)
                {
                    ThrowHelper.Throw(
                        RenderErrorCode.InvalidPassConfiguration,
                        $"Pass '{_passes[i].Name}' declares AttachmentLoadAction.DontCare but writes no resources; the attachment would never be filled",
                        nameof(FrameGraph));
                }
            }
        }

        int passCount = _passes.Count;
        // 2. Build writer -> readers edges from resource writes/reads.
        // A reader depends on every writer of the resources it reads (self excluded).
        // NOTE (alloc): edges stays Dictionary<int, List<int>> by design. Pass indices are
        // dense 0..N-1, but adjacency is sparse and variable-length with Contains-dedup per
        // writer; pooling the inner lists would retain pooled buffers across compiles and add
        // length bookkeeping for no steady-state win (Compile runs on mutation only, the result
        // is cached in _sortedPasses and reused by Execute). The per-compile scratch that scales
        // with N (Queue backing array, sorted List backing array + ToArray copy, inDegree
        // dictionary) is removed below via a rented inDegree array + ArrayPool<int> ring queue
        // + GC.AllocateUninitializedArray direct fill instead.
        var edges = new Dictionary<int, List<int>>(passCount);

        int enabledPassCount = 0;
        for (int i = 0; i < passCount; i++)
        {
            if (_passes[i].IsEnabled)
            {
                enabledPassCount++;
            }
        }

        if (enabledPassCount == 0)
        {
            _sortedPasses = Array.Empty<RenderPass>();
            return;
        }

        // inDegree is indexed directly by pass index (indices are dense 0..N-1):
        // -1 = disabled pass (never emitted), otherwise the remaining dependency count.
        // Seeding in ascending index order preserves the original Dictionary insertion
        // (ascending) + Queue FIFO emission order bit-identically.
        int[] inDegree = ArrayPool<int>.Shared.Rent(passCount);
        try
        {
            // Ring queue storage: each pass index is enqueued at most once, so capacity
            // passCount suffices; head/tail wrap as a ring and preserve Queue<T> FIFO order.
            int[] queueStorage = ArrayPool<int>.Shared.Rent(passCount);
            try
            {
                for (int i = 0; i < passCount; i++)
                {
                    inDegree[i] = _passes[i].IsEnabled ? 0 : -1;
                }

                for (int i = 0; i < _passes.Count; i++)
                {
                    if (!_passes[i].IsEnabled)
                        continue;

                    ReadOnlySpan<string> writes = _passes[i].GetWrittenResources();
                    for (int w = 0; w < writes.Length; w++)
                    {
                        string resourceName = writes[w];

                        // Track transient resource
                        if (!_transientResources.TryGetValue(resourceName, out FrameGraphResource? fgResource))
                        {
                            fgResource = new FrameGraphResource(resourceName);
                            _transientResources[resourceName] = fgResource;
                        }

                        fgResource.FirstWritePass = i;

                        // Find all passes that read this resource
                        for (int j = 0; j < _passes.Count; j++)
                        {
                            if (i == j || !_passes[j].IsEnabled)
                                continue;

                            ReadOnlySpan<string> reads = _passes[j].GetReadResources();
                            for (int r = 0; r < reads.Length; r++)
                            {
                                if (string.Equals(reads[r], resourceName, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!edges.TryGetValue(i, out List<int>? readers))
                                    {
                                        readers = new List<int>(4);
                                        edges[i] = readers;
                                    }

                                    if (!readers.Contains(j))
                                    {
                                        readers.Add(j);
                                        fgResource.LastReadPass = j;
                                        inDegree[j]++;
                                    }
                                }
                            }
                        }
                    }
                }

                // 3. Kahn's algorithm: emit zero-in-degree passes, release their readers.
                // Direct-fill into the final array (length = enabledPassCount, known up front).
                RenderPass[] sorted = GC.AllocateUninitializedArray<RenderPass>(enabledPassCount);
                int queueHead = 0;
                int queueTail = 0;
                int queueCount = 0;
                for (int i = 0; i < passCount; i++)
                {
                    if (inDegree[i] == 0)
                    {
                        queueStorage[queueTail] = i;
                        queueTail++;
                        if (queueTail >= passCount)
                        {
                            queueTail = 0;
                        }

                        queueCount++;
                    }
                }

                int sortedCount = 0;
                while (queueCount > 0)
                {
                    int current = queueStorage[queueHead];
                    queueHead++;
                    if (queueHead >= passCount)
                    {
                        queueHead = 0;
                    }

                    queueCount--;
                    sorted[sortedCount] = _passes[current];
                    sortedCount++;

                    if (!edges.TryGetValue(current, out List<int>? readers))
                    {
                        continue;
                    }

                    for (int n = 0; n < readers.Count; n++)
                    {
                        int reader = readers[n];
                        int remaining = inDegree[reader] - 1;
                        inDegree[reader] = remaining;
                        if (remaining == 0)
                        {
                            queueStorage[queueTail] = reader;
                            queueTail++;
                            if (queueTail >= passCount)
                            {
                                queueTail = 0;
                            }

                            queueCount++;
                        }
                    }
                }

                // 4. Detect cycles
                if (sortedCount != enabledPassCount)
                {
                    ThrowHelper.Throw(RenderErrorCode.GraphCycleDetected, "Frame graph contains cycles and cannot be executed", nameof(FrameGraph));
                }

                // 5. Store sorted order
                _sortedPasses = sorted;
            }
            finally
            {
                ArrayPool<int>.Shared.Return(queueStorage);
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(inDegree);
        }
    }

    /// <summary>
    /// Executes all passes in topological order, pump-driven where the pass
    /// supports it and direct otherwise.
    /// </summary>
    /// <remarks>
    /// <para>The walk is hybrid and strictly order-preserving. For every sorted,
    /// enabled pass:</para>
    /// <list type="bullet">
    /// <item><description>a pump-capable pass (<see cref="IPumpEnqueue"/>) enqueues
    /// its commands into <see cref="Pump"/> and moves on; the commands reach the GL
    /// API at the next drain point;</description></item>
    /// <item><description>a refused enqueue (ring full) drains the pump once and
    /// retries that enqueue exactly once;</description></item>
    /// <item><description>a pass still refused after the retry falls back to its
    /// direct <see cref="RenderPass.Execute"/> in place, so it still runs exactly
    /// once and its GL effects stay inside the global order;</description></item>
    /// <item><description>a classic pass drains the pump first — pumped work
    /// enqueued by earlier passes must be applied before a direct pass reads its
    /// inputs — and then executes directly.</description></item>
    /// </list>
    /// <para>One final drain closes the walk, so no command enqueued here outlives
    /// the call. Order is preserved because the ring is FIFO and enqueues follow
    /// the compiled order, so pumped work can never overtake an earlier pass, and
    /// the drain-before-direct rule keeps direct work behind everything enqueued
    /// before it.</para>
    /// <para>Known limitation: an exception raised by
    /// <see cref="RenderCommandProcessor"/> surfaces from the drain that runs the
    /// command, not from the pass that enqueued it, so it carries no pass name and
    /// is not wrapped in <see cref="RenderException"/> (only the direct leg is
    /// wrapped, per pass). The refusal fallback above is the mitigation for the
    /// common cause of a full ring; a processor fault stays attributable only to
    /// the failing command type.</para>
    /// <para>Retry granularity is per pass, not per command: a pass that enqueues
    /// several commands and is refused part-way through re-runs its whole enqueue
    /// after the drain, so the commands already accepted are executed twice. The
    /// shipped single-command pump passes are unaffected; a multi-command
    /// <see cref="IPumpEnqueue"/> must therefore size the pump for its per-frame
    /// command count.</para>
    /// <para>No other behavior changes: disabled passes are skipped, and pumping an
    /// empty ring is a no-op, so empty graphs and graphs without pump-capable
    /// passes behave exactly as before.</para>
    /// </remarks>
    public void Execute()
    {
        if (IsDisposed)
            ThrowHelper.ThrowObjectDisposed(nameof(FrameGraph));

        // 1. Compile if not already sorted
        if (_sortedPasses == null)
        {
            Compile();
        }

        // 2. Walk the sorted passes once, in order. Allocation-free: locals only,
        // no LINQ, no closures; pump legs and direct legs keep the same position.
        RenderPass[] sorted = _sortedPasses!;
        for (int i = 0; i < sorted.Length; i++)
        {
            RenderPass pass = sorted[i];

            if (!pass.IsEnabled)
                continue;

            if (pass is IPumpEnqueue pumpPass)
            {
                if (pumpPass.EnqueueCommands(Pump, _execContext).IsEnqueued)
                {
                    // Pump leg: the commands execute at the next drain point, in
                    // enqueue order, which is graph order.
                    continue;
                }

                // Refused (ring full): drain once. The pending pumped work lands in
                // graph order and the freed slots give this pass a second chance.
                PumpQueuedCommands();
                if (pumpPass.EnqueueCommands(Pump, _execContext).IsEnqueued)
                {
                    continue;
                }

                // Still refused: take the direct leg here rather than skipping the
                // pass, so its GL effects stay inside the global order. The
                // RenderPass test is the only fallback leg that exists today; an
                // IPumpEnqueue that is not a RenderPass has no direct path.
                if (pass is RenderPass refusedPass)
                {
                    ExecuteDirect(refusedPass);
                }

                continue;
            }

            // Classic pass: drain-before-direct, so pumped work enqueued by
            // earlier passes is already applied when this pass reads its inputs.
            PumpQueuedCommands();
            ExecuteDirect(pass);
        }

        // 3. Final drain: nothing enqueued by this walk survives the call.
        PumpQueuedCommands();
    }

    /// <summary>
    /// Executes one pass on the direct path, wrapping any failure with the pass
    /// identity. Shared by the classic leg and the refusal fallback.
    /// </summary>
    /// <param name="pass">The pass to execute.</param>
    private void ExecuteDirect(RenderPass pass)
    {
        try
        {
            pass.Execute(_context, _execContext);
        }
#pragma warning disable CA1031 // Pass failures are wrapped with pass identity; the type is preserved
        catch (Exception ex)
#pragma warning restore CA1031
        {
            throw new RenderException($"Pass '{pass.Name}' failed: {ex.Message}", RenderErrorCode.PassExecutionFailed, ex);
        }
    }

    /// <summary>
    /// Enqueues the work of every sorted, enabled pass that implements
    /// <see cref="IPumpEnqueue"/> into the owned <see cref="Pump"/>, in
    /// topological order. Non-pump passes are skipped silently; their path is
    /// the direct <see cref="Execute"/> traversal.
    /// </summary>
    /// <returns>The number of commands accepted by the pump.</returns>
    /// <remarks>
    /// Compiles first when needed (same lazy policy as <see cref="Execute"/>).
    /// A pump pass whose enqueue is refused (ring full) is not counted and does
    /// not stop later passes; enqueue failures thrown by a pass propagate to
    /// the caller unchanged.
    /// </remarks>
    public nuint EnqueuePumpPasses()
    {
        if (IsDisposed)
            ThrowHelper.ThrowObjectDisposed(nameof(FrameGraph));

        // 1. Compile if not already sorted (mirrors Execute)
        if (_sortedPasses == null)
        {
            Compile();
        }

        // 2. Enqueue each pump-capable pass in order; count accepted commands only.
        nuint enqueued = 0;
        for (int i = 0; i < _sortedPasses!.Length; i++)
        {
            RenderPass pass = _sortedPasses[i];

            if (!pass.IsEnabled)
                continue;

            if (pass is IPumpEnqueue pumpPass && pumpPass.EnqueueCommands(Pump, _execContext).IsEnqueued)
            {
                enqueued++;
            }
        }

        return enqueued;
    }

    /// <summary>
    /// Pumps every command currently queued in the owned <see cref="Pump"/>
    /// through the render command processor.
    /// </summary>
    /// <returns>The number of commands processed.</returns>
    /// <remarks>
    /// Single-consumer: exactly one thread may pump. Processor exceptions
    /// propagate to the caller; the faulting command stays claimed so a retry
    /// observes the same head of queue.
    /// </remarks>
    public nuint PumpQueuedCommands()
    {
        if (IsDisposed)
            ThrowHelper.ThrowObjectDisposed(nameof(FrameGraph));

        var pumpContext = new RenderPumpContext(_context);
        return Pump.PumpAll(ref pumpContext);
    }

    /// <summary>
    /// Resets the frame graph for the next frame.
    /// </summary>
    /// <remarks>
    /// The owned <see cref="Pump"/> is left alive and is NOT drained: commands
    /// queued by <see cref="EnqueuePumpPasses"/> but not yet pumped survive
    /// <see cref="Reset"/> and are still returned by
    /// <see cref="PumpQueuedCommands"/>. Only <see cref="Dispose"/> releases
    /// the pump's native ring memory.
    /// </remarks>
    public void Reset()
    {
        if (IsDisposed)
            ThrowHelper.ThrowObjectDisposed(nameof(FrameGraph));

        _passes.Clear();
        _transientResources.Clear();
        _sortedPasses = null;
        _execContext.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            _passes.Clear();
            _transientResources.Clear();
            _sortedPasses = null;
            _execContext.Clear();
            _pump.Dispose();
        }
    }
}

/// <summary>
/// Tracks a transient resource's lifetime in the frame graph.
/// </summary>
internal sealed class FrameGraphResource
{
    /// <summary>
    /// Gets the resource name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the index of the first pass that writes to this resource.
    /// </summary>
    public int FirstWritePass { get; set; }

    /// <summary>
    /// Gets or sets the index of the last pass that reads from this resource.
    /// </summary>
    public int LastReadPass { get; set; }

    /// <summary>
    /// Gets or sets the resource instance.
    /// </summary>
    public object? Instance { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this resource has been allocated.
    /// </summary>
    public bool IsAllocated { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameGraphResource"/> class.
    /// </summary>
    /// <param name="name">The resource name.</param>
    public FrameGraphResource(string name)
    {
        Name = name;
        FirstWritePass = -1;
        LastReadPass = -1;
    }
}
