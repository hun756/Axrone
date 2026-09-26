namespace Axrone.Render.OpenGL.Resources;

/// <summary>
/// Sync wait result codes.
/// </summary>
public enum SyncWaitResult : uint
{
    /// <summary>No wait performed (zero sentinel).</summary>
    None = 0,

    /// <summary>The sync object was already signaled.</summary>
    AlreadySignaled = 0x911A,

    /// <summary>The wait condition was satisfied.</summary>
    ConditionSatisfied = 0x9119,

    /// <summary>The timeout expired before the condition was satisfied.</summary>
    TimeoutExpired = 0x911B,

    /// <summary>The wait operation failed.</summary>
    WaitFailed = 0x911D,
}

/// <summary>
/// Sync object status.
/// </summary>
public enum SyncStatus : int
{
    /// <summary>Unknown status (zero sentinel).</summary>
    None = 0,

    /// <summary>The sync object is signaled.</summary>
    Signaled = 0x9119,

    /// <summary>The sync object is unsignaled.</summary>
    Unsignaled = 0x911A,
}

/// <summary>
/// Fence sync resource for GPU/CPU synchronization.
/// </summary>
public sealed class GLSync : IGLResource, IDisposable
{
    private readonly GLContext _context;
    private readonly IGLApi _gl;
    private int _disposed;

    /// <summary>Gets the sync handle.</summary>
    public nint Handle { get; private set; }

    /// <summary>Gets the debug label.</summary>
    public string Label { get; }

    /// <inheritdoc/>
    public int RegistrySequence { get; set; }

    /// <inheritdoc/>
    public int RebuildPriority => 55;

    /// <summary>Gets a value indicating whether this sync object has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLSync"/> class.
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="label">The debug label.</param>
    public GLSync(GLContext context, string label = "sync")
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _gl = context.GL;
        Label = label;

        Handle = _gl.FenceSync(GLConst.SyncGpuCommandsComplete, 0);
        context.Registry.Register(this);

        if (context.DebugLabelsEnabled)
        {
            _gl.ObjectLabel(GLConst.Sync, (uint)Handle, label);
        }
    }

    /// <summary>
    /// Waits on the CPU for the sync object to become signaled.
    /// </summary>
    /// <param name="timeoutNs">The timeout in nanoseconds.</param>
    /// <param name="flags">The wait flags.</param>
    /// <returns>The sync wait result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SyncWaitResult ClientWait(ulong timeoutNs = 0, uint flags = 0)
    {
        EnsureAlive();
        _context.AssertRenderThread();
        return (SyncWaitResult)_gl.ClientWaitSync(Handle, flags, timeoutNs);
    }

    /// <summary>
    /// Inserts a wait on the GPU for the sync object to become signaled.
    /// </summary>
    /// <param name="flags">The wait flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ServerWait(uint flags = 0)
    {
        EnsureAlive();
        _context.AssertRenderThread();
        _gl.WaitSync(Handle, flags, ulong.MaxValue);
    }

    /// <summary>
    /// Queries the sync status.
    /// </summary>
    /// <returns>The sync status.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SyncStatus GetStatus()
    {
        EnsureAlive();
        _gl.GetSync(Handle, GLConst.SyncStatus, out int status);
        return (SyncStatus)status;
    }

    /// <summary>
    /// Checks if the sync object is signaled.
    /// </summary>
    /// <returns>True if signaled; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSignaled() => GetStatus() == SyncStatus.Signaled;

    /// <inheritdoc/>
    public void OnContextLost()
    {
        Handle = nint.Zero;
    }

    /// <inheritdoc/>
    public void Invalidate() => Handle = nint.Zero;

    /// <inheritdoc/>
    public void Rebuild() => OnContextRestored();

    /// <inheritdoc/>
    public void OnContextRestored()
    {
        if (IsDisposed) return;

        Handle = _gl.FenceSync(GLConst.SyncGpuCommandsComplete, 0);

        if (_context.DebugLabelsEnabled && !string.IsNullOrEmpty(Label))
        {
            _gl.ObjectLabel(GLConst.Sync, (uint)Handle, Label);
        }
    }

    private void EnsureAlive()
    {
        if (IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "Sync object has been disposed", nameof(GLSync));
        }

        if (_context.Lifecycle.IsLost)
        {
            ThrowHelper.Throw(RenderErrorCode.ContextLost, "GL context is lost", nameof(GLSync));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        if (Handle != nint.Zero)
        {
            _gl.DeleteSync(Handle);
            Handle = nint.Zero;
        }

        _context.Registry.Unregister(this);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"GLSync: Handle=0x{Handle:X}, Label=\"{Label}\"";
}
