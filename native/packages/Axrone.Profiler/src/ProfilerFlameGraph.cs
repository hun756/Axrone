namespace Axrone.Profiler;

/// <summary>Flame graph builder options.</summary>
public readonly record struct FlameGraphOptions(
    int MaxDepth = 64,
    bool SortByDuration = true);

/// <summary>One aggregated flame node.</summary>
public sealed class FlameGraphNode
{
    private readonly List<FlameGraphNode> _children = new();
    private readonly HashSet<string> _signatures = new(StringComparer.Ordinal);
    private long _durationNs;

    /// <summary>Creates a node.</summary>
    public FlameGraphNode(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    /// <summary>Node name.</summary>
    public string Name { get; }

    /// <summary>Total accumulated nanoseconds.</summary>
    public long DurationNs => Volatile.Read(ref _durationNs);

    /// <summary>Child nodes.</summary>
    public IReadOnlyList<FlameGraphNode> Children
    {
        get
        {
            lock (_children)
            {
                return _children.ToArray();
            }
        }
    }

    /// <summary>Adds duration.</summary>
    public void AddDuration(long durationNs)
    {
        if (durationNs < 0L)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(durationNs), "Duration must not be negative.");
        }

        Interlocked.Add(ref _durationNs, durationNs);
    }

    /// <summary>Adds a stack signature.</summary>
    public void AddSignature(string signature)
    {
        ArgumentException.ThrowIfNullOrEmpty(signature);
        lock (_signatures)
        {
            _signatures.Add(signature);
        }
    }

    /// <summary>Unique signatures observed.</summary>
    public string[] GetUniqueSignatures()
    {
        lock (_signatures)
        {
            return _signatures.ToArray();
        }
    }

    /// <summary>Gets or creates a child by name.</summary>
    public FlameGraphNode GetOrAddChild(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        lock (_children)
        {
            foreach (FlameGraphNode child in _children)
            {
                if (string.Equals(child.Name, name, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            var created = new FlameGraphNode(name);
            _children.Add(created);
            return created;
        }
    }
}

/// <summary>Aggregates sampled stacks into flame hierarchies.</summary>
public sealed class FlameGraphBuilder : IDisposable
{
    private readonly List<FlameGraphNode> _roots = new();
    private readonly Stack<FlameGraphNode> _stack = new();
    private readonly FlameGraphOptions _options;
    private readonly Lock _gate = new();
    private int _disposed;

    /// <summary>Creates a builder.</summary>
    public FlameGraphBuilder(FlameGraphOptions? options = null)
    {
        FlameGraphOptions resolved = options ?? new FlameGraphOptions(MaxDepth: 64, SortByDuration: true);
        _options = resolved.MaxDepth < 1
            ? new FlameGraphOptions(MaxDepth: 64, SortByDuration: resolved.SortByDuration)
            : resolved;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }

    private FlameGraphNode GetOrAddRoot(string name)
    {
        foreach (FlameGraphNode root in _roots)
        {
            if (string.Equals(root.Name, name, StringComparison.Ordinal))
            {
                return root;
            }
        }

        var created = new FlameGraphNode(name);
        _roots.Add(created);
        return created;
    }

    /// <summary>Records a root-level frame.</summary>
    public void RecordFrame(string name, long durationNs = 0L)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (durationNs < 0L)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(durationNs), "Duration must not be negative.");
        }

        lock (_gate)
        {
            FlameGraphNode node = GetOrAddRoot(name);
            node.AddDuration(durationNs);
        }
    }

    /// <summary>Records a stack signature on a root frame.</summary>
    public void RecordSignature(string name, string? signature = null)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(name);

        lock (_gate)
        {
            FlameGraphNode node = GetOrAddRoot(name);
            node.AddSignature(signature ?? name);
        }
    }

    /// <summary>Opens a nested frame; deeper than max depth is ignored.</summary>
    /// <returns>The depth after opening, or -1 when ignored.</returns>
    public int BeginFrame(string name)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(name);

        lock (_gate)
        {
            if (_stack.Count >= _options.MaxDepth)
            {
                return -1;
            }

            FlameGraphNode node = _stack.Count == 0
                ? GetOrAddRoot(name)
                : _stack.Peek().GetOrAddChild(name);
            _stack.Push(node);
            return _stack.Count;
        }
    }

    /// <summary>Closes the innermost frame, adding its duration.</summary>
    public void EndFrame(long durationNs = 0L)
    {
        ThrowIfDisposed();
        if (durationNs < 0L)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(durationNs), "Duration must not be negative.");
        }

        lock (_gate)
        {
            if (_stack.Count == 0)
            {
                ProfilerThrowHelper.ThrowArgument(nameof(EndFrame), "No open frame to close.");
            }

            _stack.Pop().AddDuration(durationNs);
        }
    }

    /// <summary>Records a full stack with one duration at the leaf.</summary>
    public void RecordStack(ReadOnlySpan<string> stack, long durationNs = 0L)
    {
        ThrowIfDisposed();
        if (durationNs < 0L)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(durationNs), "Duration must not be negative.");
        }

        lock (_gate)
        {
            FlameGraphNode? node = null;
            int depth = 0;
            foreach (string name in stack)
            {
                if (string.IsNullOrEmpty(name) || depth >= _options.MaxDepth)
                {
                    break;
                }

                node = node is null ? GetOrAddRoot(name) : node.GetOrAddChild(name);
                depth++;
            }

            node?.AddDuration(durationNs);
        }
    }

    /// <summary>Root nodes, optionally sorted by duration descending.</summary>
    public IReadOnlyList<FlameGraphNode> GetRoots()
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            var roots = _roots.ToArray();
            if (_options.SortByDuration)
            {
                Array.Sort(roots, static (a, b) => b.DurationNs.CompareTo(a.DurationNs));
            }

            return roots;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _roots.Clear();
            _stack.Clear();
        }

        GC.SuppressFinalize(this);
    }
}
