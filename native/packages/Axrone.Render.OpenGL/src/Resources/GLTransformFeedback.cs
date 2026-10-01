namespace Axrone.Render.OpenGL.Resources;

/// <summary>
/// Transform feedback resource for GPU stream output and rasterizer discard.
/// Wraps an OpenGL transform feedback object with context-loss recovery.
/// </summary>
public sealed class GLTransformFeedback : IGLResource, IDisposable
{
    private readonly GLContext _context;
    private readonly IGLApi _gl;
    private int _disposed;

    // Indexed buffer bindings captured by BindBuffer so a context restore can re-apply them.
    private readonly Dictionary<uint, GLBuffer> _bufferBindings = new();

    /// <summary>Gets the transform feedback handle.</summary>
    public uint Id { get; private set; }

    /// <summary>Gets the debug label.</summary>
    public string Label { get; }

    /// <inheritdoc/>
    public DescriptorHandle<GLResourceNode> RegistryHandle { get; set; }

    /// <inheritdoc/>
    public int RebuildPriority => 45;

    /// <summary>Gets a value indicating whether this transform feedback has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Gets a value indicating whether transform feedback is currently active.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GLTransformFeedback"/> class.
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="label">The debug label.</param>
    public GLTransformFeedback(GLContext context, string label = "transform_feedback")
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _gl = context.GL;
        Label = label;

        Id = _gl.GenTransformFeedback();
        context.Registry.Register(this);

        if (context.DebugLabelsEnabled)
        {
            _gl.ObjectLabel(GLConst.TransformFeedback, Id, label);
        }
    }

    /// <summary>
    /// Binds this transform feedback object.
    /// </summary>
    /// <returns>This transform feedback for fluent chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GLTransformFeedback Bind()
    {
        EnsureAlive();
        _gl.BindTransformFeedback(GLConst.TransformFeedback, Id);
        return this;
    }

    /// <summary>
    /// Unbinds this transform feedback object.
    /// </summary>
    /// <returns>This transform feedback for fluent chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GLTransformFeedback Unbind()
    {
        _gl.BindTransformFeedback(GLConst.TransformFeedback, 0);
        return this;
    }

    /// <summary>
    /// Begins transform feedback capture.
    /// </summary>
    /// <param name="primitiveMode">The primitive mode (e.g. <see cref="GLConst.Triangles"/>, <see cref="GLConst.Lines"/>, <see cref="GLConst.Points"/>).</param>
    /// <exception cref="GLException">Thrown if transform feedback is already active.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Begin(uint primitiveMode)
    {
        _context.AssertRenderThread();
        EnsureAlive();

        if (IsActive)
        {
            ThrowHelper.ThrowInvalidOperation("Transform feedback is already active");
        }

        _gl.BeginTransformFeedback(primitiveMode);
        IsActive = true;
    }

    /// <summary>
    /// Pauses transform feedback capture.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Pause()
    {
        _context.AssertRenderThread();
        EnsureAlive();
        _gl.PauseTransformFeedback();
    }

    /// <summary>
    /// Resumes transform feedback capture.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Resume()
    {
        _context.AssertRenderThread();
        EnsureAlive();
        _gl.ResumeTransformFeedback();
    }

    /// <summary>
    /// Ends transform feedback capture.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void End()
    {
        _context.AssertRenderThread();
        EnsureAlive();
        _gl.EndTransformFeedback();
        IsActive = false;
    }

    /// <summary>
    /// Binds a buffer to a transform feedback binding point.
    /// </summary>
    /// <param name="index">The binding point index.</param>
    /// <param name="buffer">The buffer to bind.</param>
    /// <returns>This transform feedback for fluent chaining.</returns>
    public GLTransformFeedback BindBuffer(uint index, GLBuffer buffer)
    {
        _context.AssertRenderThread();
        EnsureAlive();
        ArgumentNullException.ThrowIfNull(buffer);
        _bufferBindings[index] = buffer;
        _gl.BindBufferBase(GLConst.TransformFeedbackBuffer, index, buffer.Id);
        return this;
    }

    /// <inheritdoc/>
    public void OnContextLost()
    {
        Id = 0;
        IsActive = false;
    }

    /// <inheritdoc/>
    public void Invalidate() => Id = 0;

    /// <inheritdoc/>
    public void Rebuild() => OnContextRestored();

    /// <inheritdoc/>
    public void OnContextRestored()
    {
        if (IsDisposed) return;

        Id = _gl.GenTransformFeedback();

        if (_context.DebugLabelsEnabled && !string.IsNullOrEmpty(Label))
        {
            _gl.ObjectLabel(GLConst.TransformFeedback, Id, Label);
        }

        // Re-apply the indexed buffer bindings captured by BindBuffer; a fresh TF object has none.
        // Buffers (priority 10) are rebuilt before transform-feedback objects (priority 45).
        if (_bufferBindings.Count > 0)
        {
            _gl.BindTransformFeedback(GLConst.TransformFeedback, Id);
            foreach ((uint index, GLBuffer buffer) in _bufferBindings)
            {
                _gl.BindBufferBase(GLConst.TransformFeedbackBuffer, index, buffer.Id);
            }
        }
    }

    private void EnsureAlive()
    {
        if (IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "Transform feedback has been disposed", nameof(GLTransformFeedback));
        }

        if (_context.Lifecycle.IsLost)
        {
            ThrowHelper.Throw(RenderErrorCode.ContextLost, "GL context is lost", nameof(GLTransformFeedback));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        if (Id != 0)
        {
            _gl.DeleteTransformFeedback(Id);
            Id = 0;
        }

        IsActive = false;
        _context.Registry.Unregister(this);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"GLTransformFeedback: Id={Id}, Active={IsActive}, Label=\"{Label}\"";
}
