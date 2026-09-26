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
    /// Initializes a new instance of the <see cref="FrameGraph"/> class.
    /// </summary>
    /// <param name="context">The GL context.</param>
    public FrameGraph(GLContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _execContext = new PassExecutionContext(context);
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
            }
        }

        // 2. Build writer -> readers edges from resource writes/reads.
        // A reader depends on every writer of the resources it reads (self excluded).
        var edges = new Dictionary<int, List<int>>();
        var inDegree = new Dictionary<int, int>();

        for (int i = 0; i < _passes.Count; i++)
        {
            if (!_passes[i].IsEnabled)
                continue;

            inDegree[i] = 0;
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

                                if (!inDegree.TryGetValue(j, out int degree))
                                {
                                    degree = 0;
                                }

                                inDegree[j] = degree + 1;
                            }
                        }
                    }
                }
            }
        }

        // 3. Kahn's algorithm: emit zero-in-degree passes, release their readers.
        var queue = new Queue<int>();
        foreach (int index in inDegree.Keys)
        {
            if (inDegree[index] == 0)
            {
                queue.Enqueue(index);
            }
        }

        var sorted = new List<RenderPass>(_passes.Count);
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            sorted.Add(_passes[current]);

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
                    queue.Enqueue(reader);
                }
            }
        }

        // 4. Detect cycles
        int enabledPassCount = 0;
        for (int i = 0; i < _passes.Count; i++)
        {
            if (_passes[i].IsEnabled)
                enabledPassCount++;
        }

        if (sorted.Count != enabledPassCount)
        {
            ThrowHelper.Throw(RenderErrorCode.GraphCycleDetected, "Frame graph contains cycles and cannot be executed", nameof(FrameGraph));
        }

        // 5. Store sorted order
        _sortedPasses = sorted.ToArray();
    }

    /// <summary>
    /// Executes all passes in topological order.
    /// </summary>
    public void Execute()
    {
        if (IsDisposed)
            ThrowHelper.ThrowObjectDisposed(nameof(FrameGraph));

        // 1. Compile if not already sorted
        if (_sortedPasses == null)
        {
            Compile();
        }

        // 2. Execute each pass in order
        for (int i = 0; i < _sortedPasses!.Length; i++)
        {
            RenderPass pass = _sortedPasses[i];

            if (!pass.IsEnabled)
                continue;

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
    }

    /// <summary>
    /// Resets the frame graph for the next frame.
    /// </summary>
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
