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
/// Resources are sorted by <see cref="IGLResource.RebuildPriority"/> on rebuild.
/// </summary>
public sealed class GLResourceRegistry
{
    private readonly GLContext _context;
    private readonly List<IGLResource> _resources = new(256);
    private readonly Lock _syncRoot = new();
    private int _sequenceCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLResourceRegistry"/> class.
    /// </summary>
    internal GLResourceRegistry(GLContext context) => _context = context;

    /// <summary>
    /// Gets the count of registered resources.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_syncRoot) return _resources.Count;
        }
    }

    /// <summary>
    /// Registers a resource for lifecycle management.
    /// </summary>
    /// <param name="resource">The resource to register.</param>
    public void Register(IGLResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        lock (_syncRoot)
        {
            if (_context.IsDisposed)
                ThrowHelper.ThrowContextDisposed();

            resource.RegistrySequence = Interlocked.Increment(ref _sequenceCounter);
            _resources.Add(resource);
        }
    }

    /// <summary>
    /// Unregisters a resource from lifecycle management.
    /// </summary>
    /// <param name="resource">The resource to unregister.</param>
    public void Unregister(IGLResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        lock (_syncRoot)
        {
            _resources.Remove(resource);
        }
    }

    /// <summary>
    /// Invalidates all registered resources (context-loss).
    /// </summary>
    internal void InvalidateAll()
    {
        lock (_syncRoot)
        {
            for (int i = 0; i < _resources.Count; i++)
            {
                _resources[i].Invalidate();
                _resources[i].OnContextLost();
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
            // Sort by rebuild priority (lower = rebuilt first)
            _resources.Sort(static (a, b) => a.RebuildPriority.CompareTo(b.RebuildPriority));

            for (int i = 0; i < _resources.Count; i++)
            {
                _resources[i].OnContextRestored();
                _resources[i].Rebuild();
            }
        }
    }

    /// <summary>
    /// Disposes all registered resources in reverse order. Every resource is
    /// attempted even when predecessors fail; failures surface together instead
    /// of vanishing into a swallow.
    /// </summary>
    internal void DisposeAll()
    {
        List<Exception>? failures = null;
        lock (_syncRoot)
        {
            for (int i = _resources.Count - 1; i >= 0; i--)
            {
                try
                {
                    _resources[i].Dispose();
                }
                catch (Exception ex)
                {
                    failures ??= new List<Exception>(1);
                    failures.Add(ex);
                }
            }

            _resources.Clear();
        }

        if (failures is not null)
        {
            throw new AggregateException("One or more GPU resources failed to dispose.", failures);
        }
    }
}
