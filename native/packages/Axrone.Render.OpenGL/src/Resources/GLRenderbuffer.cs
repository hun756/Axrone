namespace Axrone.Render.OpenGL.Resources;

/// <summary>
/// Renderbuffer resource for depth/stencil attachments.
/// </summary>
public sealed class GLRenderbuffer : IGLResource, IDisposable
{
    private readonly GLContext _context;
    private readonly IGLApi _gl;
    private int _disposed;

    /// <summary>Gets the renderbuffer handle.</summary>
    public uint Id { get; private set; }

    /// <summary>Gets the internal format.</summary>
    public uint InternalFormat { get; }

    /// <summary>Gets the width.</summary>
    public int Width { get; }

    /// <summary>Gets the height.</summary>
    public int Height { get; }

    /// <summary>Gets the debug label.</summary>
    public string Label { get; }

    /// <inheritdoc/>
    public int RegistrySequence { get; set; }

    /// <inheritdoc/>
    public int RebuildPriority => 35;

    /// <summary>Gets a value indicating whether this renderbuffer has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLRenderbuffer"/> class.
    /// </summary>
    public GLRenderbuffer(GLContext context, uint internalFormat, int width, int height, string label = "renderbuffer")
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _gl = context.GL;
        InternalFormat = internalFormat;
        Width = width;
        Height = height;
        Label = label;

        if (width <= 0 || height <= 0)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidValue, "Renderbuffer dimensions must be positive", nameof(GLRenderbuffer));
        }

        Id = _gl.GenRenderbuffer();
        AllocateStorage();
        context.Registry.Register(this);

        if (context.DebugLabelsEnabled)
        {
            _gl.ObjectLabel(GLConst.Renderbuffer, Id, label);
        }
    }

    private void AllocateStorage()
    {
        _context.State.BindRenderbuffer(Id);
        _gl.RenderbufferStorage(GLConst.Renderbuffer, InternalFormat, (uint)Width, (uint)Height);
    }

    /// <summary>
    /// Binds this renderbuffer.
    /// </summary>
    /// <returns>This renderbuffer for fluent chaining.</returns>
    public GLRenderbuffer Bind()
    {
        EnsureAlive();
        _context.State.BindRenderbuffer(Id);
        return this;
    }

    /// <summary>
    /// Unbinds this renderbuffer.
    /// </summary>
    /// <returns>This renderbuffer for fluent chaining.</returns>
    public GLRenderbuffer Unbind()
    {
        _context.State.BindRenderbuffer(0);
        return this;
    }

    /// <inheritdoc/>
    public void OnContextLost()
    {
        Id = 0;
    }

    /// <inheritdoc/>
    public void Invalidate() => Id = 0;

    /// <inheritdoc/>
    public void Rebuild() => OnContextRestored();

    /// <inheritdoc/>
    public void OnContextRestored()
    {
        if (IsDisposed) return;

        Id = _gl.GenRenderbuffer();
        AllocateStorage();

        if (_context.DebugLabelsEnabled && !string.IsNullOrEmpty(Label))
        {
            _gl.ObjectLabel(GLConst.Renderbuffer, Id, Label);
        }
    }

    private void EnsureAlive()
    {
        if (IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "Renderbuffer has been disposed", nameof(GLRenderbuffer));
        }

        if (_context.Lifecycle.IsLost)
        {
            ThrowHelper.Throw(RenderErrorCode.ContextLost, "GL context is lost", nameof(GLRenderbuffer));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        if (Id != 0)
        {
            _gl.DeleteRenderbuffer(Id);
            Id = 0;
        }

        _context.Registry.Unregister(this);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"GLRenderbuffer: Id={Id}, Format=0x{InternalFormat:X}, Size={Width}x{Height}, Label=\"{Label}\"";
}
