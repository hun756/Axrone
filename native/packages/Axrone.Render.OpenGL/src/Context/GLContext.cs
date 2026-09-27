namespace Axrone.Render.OpenGL.Context;

/// <summary>
/// Main GL context that manages capabilities, extensions, state cache, and resource lifecycle.
/// Thread-affine: all GL operations must occur on the creating thread unless explicitly noted.
/// </summary>
public sealed class GLContext : IDisposable
{
    private readonly int _renderThreadId;
    private int _isDisposed;
    private int _isLost;

    /// <summary>
    /// Gets the GL API.
    /// </summary>
    public IGLApi GL { get; }

    /// <summary>
    /// Gets the GL capabilities.
    /// </summary>
    public GLCapabilities Capabilities { get; }

    /// <summary>
    /// Gets the extension registry.
    /// </summary>
    public GLExtensionRegistry Extensions { get; }

    /// <summary>
    /// Gets the state cache.
    /// </summary>
    public GLStateCache State { get; }

    /// <summary>
    /// Gets the lifecycle manager for context-loss tracking.
    /// </summary>
    public GLContextLifecycle Lifecycle { get; }

    /// <summary>
    /// Gets the resource registry for lifecycle management.
    /// </summary>
    public GLResourceRegistry Registry { get; }

    /// <summary>
    /// Gets a value indicating whether debug labels are supported and enabled.
    /// </summary>
    public bool DebugLabelsEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether the context has been lost.
    /// </summary>
    public bool IsLost => Volatile.Read(ref _isLost) != 0;

    /// <summary>
    /// Gets a value indicating whether the context has been disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLContext"/> class.
    /// </summary>
    /// <param name="gl">The GL API.</param>
    public GLContext(IGLApi gl)
    {
        ArgumentNullException.ThrowIfNull(gl);

        _renderThreadId = Environment.CurrentManagedThreadId;

        GL = gl;
        Capabilities = new GLCapabilities(gl);
        Extensions = new GLExtensionRegistry(gl);
        State = new GLStateCache(gl);
        Lifecycle = new GLContextLifecycle(this);
        Registry = new GLResourceRegistry(this);

        DebugLabelsEnabled = Extensions.IsSupported("GL_KHR_debug") ||
                             Extensions.IsSupported("KHR_debug");
    }

    /// <summary>
    /// Asserts that the current thread is the render thread.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void AssertRenderThread()
    {
        if (Environment.CurrentManagedThreadId != _renderThreadId)
        {
            ThrowHelper.ThrowInvalidOperation("GL operation called from non-render thread");
        }
    }

    /// <summary>
    /// Notifies that the GL context has been lost.
    /// Invalidates all resources and state.
    /// </summary>
    public void NotifyContextLost()
    {
        if (Interlocked.Exchange(ref _isLost, 1) == 0)
        {
            State.Invalidate();
            Registry.InvalidateAll();
            Lifecycle.RaiseLost();
        }
    }

    /// <summary>
    /// Notifies that the GL context has been restored.
    /// Rebuilds all resources and resets state.
    /// </summary>
    public void NotifyContextRestored()
    {
        if (Interlocked.Exchange(ref _isLost, 0) == 1)
        {
            Registry.RebuildAll();
            State.Reset();
            Lifecycle.RaiseRestored();
        }
    }

    /// <summary>
    /// Flushes all pending GL commands.
    /// </summary>
    public void Flush()
    {
        if (IsDisposed)
            ThrowHelper.ThrowContextDisposed();

        GL.Flush();
    }

    /// <summary>
    /// Blocks until all GL commands have completed.
    /// </summary>
    public void Finish()
    {
        if (IsDisposed)
            ThrowHelper.ThrowContextDisposed();

        GL.Finish();
    }

    /// <summary>
    /// Gets the current GL error.
    /// </summary>
    /// <returns>The current error code.</returns>
    public uint GetError()
    {
        if (IsDisposed)
            ThrowHelper.ThrowContextDisposed();

        return GL.GetError();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            Registry.DisposeAll();
        }
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"GLContext: {Capabilities.Version}, {(IsLost ? "LOST" : "Active")}, {Registry.Count} resources";
}

/// <summary>
/// Tracks context-loss lifecycle state.
/// </summary>
public sealed class GLContextLifecycle
{
    private readonly GLContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLContextLifecycle"/> class.
    /// </summary>
    internal GLContextLifecycle(GLContext context) => _context = context;

    /// <summary>
    /// Gets a value indicating whether the context is currently lost.
    /// </summary>
    public bool IsLost => _context.IsLost;

    /// <summary>
    /// Gets a value indicating whether the context is disposed.
    /// </summary>
    public bool IsDisposed => _context.IsDisposed;

    /// <summary>Event raised when context is lost.</summary>
    public event EventHandler? ContextLost;

    /// <summary>Event raised when context is restored.</summary>
    public event EventHandler? ContextRestored;

    internal void RaiseLost() => ContextLost?.Invoke(this, EventArgs.Empty);
    internal void RaiseRestored() => ContextRestored?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Manages GPU resource registration, disposal, and context-loss recovery.
/// Table-driven: a generational <see cref="DescriptorTable{TDescriptor}"/> owns the
/// lifecycle identity (ABA-safe slot reuse) while the managed <see cref="IGLResource"/>
/// instances live in a slot-indexed sidecar. Resources are rebuilt in
/// <see cref="IGLResource.RebuildPriority"/> order and disposed in reverse
/// registration order. The hot path never touches the registry; all cold-path
/// mutations serialize on a lock.
/// </summary>
public sealed class GLResourceRegistry : IDisposable
{
    private const uint DefaultCapacity = 1024;

    private readonly GLContext _context;
    private readonly DescriptorTable<GLResourceNode> _table;
    private readonly IGLResource?[] _sidecar;
    private readonly Lock _syncRoot = new();
    private int _sequenceCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLResourceRegistry"/> class.
    /// </summary>
    internal GLResourceRegistry(GLContext context)
    {
        _context = context;
        _table = new DescriptorTable<GLResourceNode>(new DescriptorTableOptions { Capacity = DefaultCapacity });
        _sidecar = new IGLResource?[_table.Capacity];
    }

    /// <summary>
    /// Gets the count of registered resources. Lock-free read of the table count.
    /// </summary>
    public int Count => (int)_table.ActiveCount;

    /// <summary>
    /// Registers a resource for lifecycle management and returns its generational handle.
    /// </summary>
    /// <param name="resource">The resource to register.</param>
    /// <returns>The generational handle naming this registration.</returns>
    public DescriptorHandle<GLResourceNode> Register(IGLResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        lock (_syncRoot)
        {
            if (_context.IsDisposed)
                ThrowHelper.ThrowContextDisposed();

            DescriptorHandle<GLResourceNode> existing = resource.RegistryHandle;
            if (existing.IsValid && _table.TryGet(in existing, out _))
                ThrowHelper.ThrowInvalidOperation("Resource is already registered");

            var node = new GLResourceNode(resource.RebuildPriority, Interlocked.Increment(ref _sequenceCounter));
            if (!_table.TryAllocate(in node, out DescriptorHandle<GLResourceNode> handle))
                ThrowHelper.ThrowInvalidOperation($"Resource registry exhausted (capacity {_table.Capacity})");

            _table.TrySetStatus(in handle, DescriptorStatus.Active);
            _sidecar[handle.SlotIndex] = resource;
            resource.RegistryHandle = handle;
            return handle;
        }
    }

    /// <summary>
    /// Unregisters a resource from lifecycle management. Stale handles are a no-op.
    /// </summary>
    /// <param name="resource">The resource to unregister.</param>
    public void Unregister(IGLResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        lock (_syncRoot)
        {
            DescriptorHandle<GLResourceNode> handle = resource.RegistryHandle;
            if (!handle.IsValid || !_table.TryGet(in handle, out _))
                return;

            if (!ReferenceEquals(_sidecar[handle.SlotIndex], resource))
                return; // Slot recycled since: stale handle, fail closed.

            _sidecar[handle.SlotIndex] = null;
            _table.TryFree(in handle);
            resource.RegistryHandle = default;
        }
    }

    /// <summary>
    /// Resolves a generational handle to its live resource. Stale handles
    /// (freed slots, recycled generations) fail closed — no scan, ABA-safe.
    /// </summary>
    /// <param name="handle">The handle to resolve.</param>
    /// <param name="resource">The live resource if the handle is current.</param>
    /// <returns>True if the handle names a live registration; otherwise, false.</returns>
    public bool TryResolve(in DescriptorHandle<GLResourceNode> handle, out IGLResource? resource)
    {
        lock (_syncRoot)
        {
            if (handle.IsValid && _table.TryGet(in handle, out _))
            {
                IGLResource? candidate = _sidecar[handle.SlotIndex];
                if (candidate is not null && candidate.RegistryHandle == handle)
                {
                    resource = candidate;
                    return true;
                }
            }

            resource = null;
            return false;
        }
    }

    /// <summary>
    /// Invalidates all registered resources (context-loss).
    /// </summary>
    internal void InvalidateAll()
    {
        lock (_syncRoot)
        {
            IGLResource?[] sidecar = _sidecar;
            for (nuint i = 0; i < (nuint)sidecar.Length; i++)
            {
                IGLResource? resource = sidecar[i];
                if (resource is not null)
                {
                    resource.Invalidate();
                    resource.OnContextLost();
                }
            }
        }
    }

    /// <summary>
    /// Rebuilds all registered resources in priority order (context-restore).
    /// </summary>
    internal void RebuildAll()
    {
        lock (_syncRoot)
        {
            int count = (int)_table.ActiveCount;
            if (count == 0)
                return;

            IGLResource[] batch = ArrayPool<IGLResource>.Shared.Rent(count);
            try
            {
                int collected = 0;
                foreach (IGLResource? resource in _sidecar)
                {
                    if (resource is not null)
                        batch[collected++] = resource;
                }

                // Sort by rebuild priority (lower = rebuilt first)
                Array.Sort(batch, 0, collected, RebuildPriorityComparer.Instance);

                for (int i = 0; i < collected; i++)
                {
                    IGLResource resource = batch[i];
                    batch[i] = null!;
                    resource.OnContextRestored();
                    resource.Rebuild();
                }
            }
            finally
            {
                ArrayPool<IGLResource>.Shared.Return(batch);
            }
        }
    }

    /// <summary>
    /// Disposes all registered resources in reverse registration order (same as
    /// <see cref="DisposeAll"/>). Prefer disposing the owning <see cref="GLContext"/>.
    /// </summary>
    public void Dispose() => DisposeAll();

    /// <summary>
    /// Disposes all registered resources in reverse registration order. Every resource is
    /// attempted even when predecessors fail; failures surface together instead
    /// of vanishing into a swallow. Slots are reclaimed before disposal runs, so
    /// reentrant <see cref="Unregister"/> calls from resource teardown are safe no-ops.
    /// </summary>
    internal void DisposeAll()
    {
        DisposalSnapshot[] batch;
        int collected;
        lock (_syncRoot)
        {
            int count = (int)_table.ActiveCount;
            batch = count == 0 ? [] : ArrayPool<DisposalSnapshot>.Shared.Rent(count);
            collected = 0;
            IGLResource?[] sidecar = _sidecar;
            for (nuint i = 0; i < (nuint)sidecar.Length; i++)
            {
                IGLResource? resource = sidecar[i];
                if (resource is null)
                    continue;

                DescriptorHandle<GLResourceNode> handle = resource.RegistryHandle;
                int sequence = 0;
                if (_table.TryGet(in handle, out GLResourceNode node))
                    sequence = node.Sequence;

                batch[collected++] = new DisposalSnapshot(resource, handle, sequence);
                sidecar[i] = null;
            }

            // Reverse registration order, as before.
            Array.Sort(batch, 0, collected, DisposalSnapshot.SequenceDescendingComparer.Instance);
            for (int i = 0; i < collected; i++)
            {
                DescriptorHandle<GLResourceNode> handle = batch[i].Handle;
                _table.TryFree(in handle);
            }
        }

        List<Exception>? failures = null;
        for (int i = 0; i < collected; i++)
        {
            try
            {
                batch[i].Resource.Dispose();
            }
            catch (Exception ex)
            {
                failures ??= new List<Exception>(1);
                failures.Add(ex);
            }

            batch[i] = default;
        }

        if (batch.Length > 0)
            ArrayPool<DisposalSnapshot>.Shared.Return(batch);

        _table.Dispose();

        if (failures is not null)
        {
            throw new AggregateException("One or more GPU resources failed to dispose.", failures);
        }
    }

    private sealed class RebuildPriorityComparer : IComparer<IGLResource>
    {
        public static readonly RebuildPriorityComparer Instance = new();

        public int Compare(IGLResource? x, IGLResource? y)
        {
            if (ReferenceEquals(x, y))
                return 0;
            if (x is null)
                return -1;
            if (y is null)
                return 1;
            return x.RebuildPriority.CompareTo(y.RebuildPriority);
        }
    }

    private readonly record struct DisposalSnapshot(IGLResource Resource, DescriptorHandle<GLResourceNode> Handle, int Sequence)
    {
        public sealed class SequenceDescendingComparer : IComparer<DisposalSnapshot>
        {
            public static readonly SequenceDescendingComparer Instance = new();

            public int Compare(DisposalSnapshot x, DisposalSnapshot y) =>
                y.Sequence.CompareTo(x.Sequence);
        }
    }
}
