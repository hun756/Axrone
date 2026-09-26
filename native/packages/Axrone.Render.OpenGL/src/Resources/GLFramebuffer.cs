namespace Axrone.Render.OpenGL.Resources;

/// <summary>
/// Framebuffer Object (FBO) resource with attachment management and completeness validation.
/// Zero-allocation hot path, context-loss recovery enabled, MRT support.
/// </summary>
public sealed class GLFramebuffer : IGLResource, IDisposable
{
    private readonly GLContext _context;
    private readonly IGLApi _gl;
    private readonly List<GLTexture> _colorAttachments = new(8);
    private GLTexture? _depthAttachment;
    private GLTexture? _depthStencilAttachment;
    private GLRenderbuffer? _depthRenderbuffer;
    private int _disposed;

    /// <summary>Gets the framebuffer handle.</summary>
    public uint Id { get; private set; }

    /// <summary>Gets the framebuffer width.</summary>
    public int Width { get; }

    /// <summary>Gets the framebuffer height.</summary>
    public int Height { get; }

    /// <summary>Gets the debug label.</summary>
    public string Label { get; private set; }

    /// <inheritdoc/>
    public int RegistrySequence { get; set; }

    /// <inheritdoc/>
    public int RebuildPriority => 40;

    /// <summary>Gets a value indicating whether this framebuffer has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Gets the number of color attachments.</summary>
    public int ColorAttachmentCount => _colorAttachments.Count;

    /// <summary>Gets a value indicating whether this framebuffer has a depth attachment.</summary>
    public bool HasDepth => _depthAttachment != null || _depthRenderbuffer != null;

    /// <summary>Gets a value indicating whether this framebuffer has a depth-stencil attachment.</summary>
    public bool HasDepthStencil => _depthStencilAttachment != null;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLFramebuffer"/> class.
    /// </summary>
    public GLFramebuffer(GLContext context, int width, int height, string label = "framebuffer")
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _gl = context.GL;
        Width = width;
        Height = height;
        Label = label;

        if (width <= 0 || height <= 0)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidValue, "Framebuffer dimensions must be positive", nameof(GLFramebuffer));
        }

        Id = _gl.GenFramebuffer();
        context.Registry.Register(this);

        if (_context.DebugLabelsEnabled)
        {
            _gl.ObjectLabel(0x8D40, Id, label); // GL_FRAMEBUFFER
        }
    }

    /// <summary>
    /// Attaches a color texture.
    /// </summary>
    /// <param name="texture">The texture to attach.</param>
    /// <param name="attachmentIndex">The attachment index (0-7).</param>
    /// <returns>This framebuffer for fluent chaining.</returns>
    public GLFramebuffer AttachColor(GLTexture texture, int attachmentIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(texture);
        EnsureAlive();
        _context.AssertRenderThread();

        if (attachmentIndex < 0 || attachmentIndex >= 8)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidValue, "Color attachment index must be 0-7", nameof(GLFramebuffer));
        }

        ValidateAttachmentSize(texture);

        while (_colorAttachments.Count <= attachmentIndex)
        {
            _colorAttachments.Add(null!);
        }

        _colorAttachments[attachmentIndex] = texture;

        _context.State.BindFramebuffer(GLConst.Framebuffer, Id);
        _gl.FramebufferTexture2D(
            GLConst.Framebuffer,
            GLConst.ColorAttachment0 + (uint)attachmentIndex,
            texture.Target,
            texture.Id,
            0);

        UpdateDrawBuffers();
        return this;
    }

    /// <summary>
    /// Attaches a depth texture.
    /// </summary>
    /// <param name="texture">The depth texture.</param>
    /// <returns>This framebuffer for fluent chaining.</returns>
    public GLFramebuffer AttachDepth(GLTexture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        EnsureAlive();
        _context.AssertRenderThread();

        ValidateAttachmentSize(texture);

        if (!texture.FormatInfo.IsDepth)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidAttachment, "Texture is not a depth format", nameof(GLFramebuffer));
        }

        _depthAttachment = texture;

        _context.State.BindFramebuffer(GLConst.Framebuffer, Id);
        _gl.FramebufferTexture2D(
            GLConst.Framebuffer,
            GLConst.DepthAttachment,
            texture.Target,
            texture.Id,
            0);

        return this;
    }

    /// <summary>
    /// Attaches a depth-stencil texture.
    /// </summary>
    /// <param name="texture">The depth-stencil texture.</param>
    /// <returns>This framebuffer for fluent chaining.</returns>
    public GLFramebuffer AttachDepthStencil(GLTexture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        EnsureAlive();
        _context.AssertRenderThread();

        ValidateAttachmentSize(texture);

        if (!texture.FormatInfo.IsDepth || !texture.FormatInfo.IsStencil)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidAttachment, "Texture is not a depth-stencil format", nameof(GLFramebuffer));
        }

        _depthStencilAttachment = texture;

        _context.State.BindFramebuffer(GLConst.Framebuffer, Id);
        _gl.FramebufferTexture2D(
            GLConst.Framebuffer,
            GLConst.DepthStencilAttachment,
            texture.Target,
            texture.Id,
            0);

        return this;
    }

    /// <summary>
    /// Attaches a depth renderbuffer.
    /// </summary>
    /// <param name="renderbuffer">The depth renderbuffer.</param>
    /// <returns>This framebuffer for fluent chaining.</returns>
    public GLFramebuffer AttachDepthRenderbuffer(GLRenderbuffer renderbuffer)
    {
        ArgumentNullException.ThrowIfNull(renderbuffer);
        EnsureAlive();
        _context.AssertRenderThread();

        _depthRenderbuffer = renderbuffer;

        _context.State.BindFramebuffer(GLConst.Framebuffer, Id);
        _context.State.BindRenderbuffer(renderbuffer.Id);
        _gl.FramebufferRenderbuffer(
            GLConst.Framebuffer,
            GLConst.DepthAttachment,
            GLConst.Renderbuffer,
            renderbuffer.Id);

        return this;
    }

    private void ValidateAttachmentSize(GLTexture texture)
    {
        if (texture.Width != Width || texture.Height != Height)
        {
            ThrowHelper.Throw(
                RenderErrorCode.InvalidAttachment,
                $"Attachment size ({texture.Width}x{texture.Height}) does not match framebuffer size ({Width}x{Height})",
                nameof(GLFramebuffer));
        }
    }

    private void UpdateDrawBuffers()
    {
        if (_colorAttachments.Count == 0)
        {
            return;
        }

        Span<uint> buffers = stackalloc uint[_colorAttachments.Count];
        for (int i = 0; i < _colorAttachments.Count; i++)
        {
            buffers[i] = _colorAttachments[i] != null ? GLConst.ColorAttachment0 + (uint)i : GLConst.None;
        }

        _gl.DrawBuffers(buffers);
    }

    /// <summary>
    /// Validates framebuffer completeness.
    /// </summary>
    /// <returns>True if complete; otherwise, false.</returns>
    public bool ValidateCompleteness()
    {
        EnsureAlive();
        _context.AssertRenderThread();

        _context.State.BindFramebuffer(GLConst.Framebuffer, Id);
        uint status = _gl.CheckFramebufferStatus(GLConst.Framebuffer);

        return status == GLConst.FramebufferComplete;
    }

    /// <summary>
    /// Validates framebuffer completeness and throws if incomplete.
    /// </summary>
    public void EnsureComplete()
    {
        _context.State.BindFramebuffer(GLConst.Framebuffer, Id);
        uint status = _gl.CheckFramebufferStatus(GLConst.Framebuffer);

        if (status != GLConst.FramebufferComplete)
        {
            ThrowHelper.Throw(
                RenderErrorCode.IncompleteFramebuffer,
                $"Framebuffer is incomplete: 0x{status:X4}",
                nameof(GLFramebuffer));
        }
    }

    /// <summary>
    /// Binds this framebuffer.
    /// </summary>
    /// <param name="target">The framebuffer target.</param>
    /// <returns>This framebuffer for fluent chaining.</returns>
    public GLFramebuffer Bind(uint target = GLConst.Framebuffer)
    {
        EnsureAlive();
        _context.State.BindFramebuffer(target, Id);
        return this;
    }

    /// <summary>
    /// Unbinds this framebuffer.
    /// </summary>
    /// <param name="target">The framebuffer target.</param>
    /// <returns>This framebuffer for fluent chaining.</returns>
    public GLFramebuffer Unbind(uint target = GLConst.Framebuffer)
    {
        _context.State.BindFramebuffer(target, 0);
        return this;
    }

    /// <summary>
    /// Clears the framebuffer.
    /// </summary>
    /// <param name="color">Whether to clear the color buffer.</param>
    /// <param name="depth">Whether to clear the depth buffer.</param>
    /// <param name="stencil">Whether to clear the stencil buffer.</param>
    /// <returns>This framebuffer for fluent chaining.</returns>
    public GLFramebuffer Clear(bool color = true, bool depth = true, bool stencil = false)
    {
        EnsureAlive();
        _context.AssertRenderThread();

        _context.State.BindFramebuffer(GLConst.Framebuffer, Id);

        uint mask = 0;
        if (color && _colorAttachments.Count > 0) mask |= GLConst.ColorBufferBit;
        if (depth && HasDepth) mask |= GLConst.DepthBufferBit;
        if (stencil && HasDepthStencil) mask |= GLConst.StencilBufferBit;

        if (mask != 0)
        {
            _gl.Clear(mask);
        }

        return this;
    }

    /// <summary>
    /// Reads pixels from a color attachment.
    /// </summary>
    /// <param name="pixels">The output buffer.</param>
    /// <param name="attachmentIndex">The attachment index.</param>
    public unsafe void ReadPixels(Span<byte> pixels, int attachmentIndex = 0)
    {
        EnsureAlive();
        _context.AssertRenderThread();

        if (attachmentIndex >= _colorAttachments.Count || _colorAttachments[attachmentIndex] == null)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidValue, "Invalid color attachment index", nameof(GLFramebuffer));
        }

        var texture = _colorAttachments[attachmentIndex];
        var info = texture.FormatInfo;

        _context.State.BindFramebuffer(GLConst.Framebuffer, Id);
        _gl.ReadBuffer(GLConst.ColorAttachment0 + (uint)attachmentIndex);

        fixed (byte* ptr = pixels)
        {
            _gl.ReadPixels(0, 0, (uint)Width, (uint)Height, info.Format, info.Type, ptr);
        }
    }

    /// <summary>
    /// Blits this framebuffer to another.
    /// </summary>
    /// <param name="target">The target framebuffer.</param>
    /// <param name="srcX0">Source x0.</param>
    /// <param name="srcY0">Source y0.</param>
    /// <param name="srcX1">Source x1.</param>
    /// <param name="srcY1">Source y1.</param>
    /// <param name="dstX0">Destination x0.</param>
    /// <param name="dstY0">Destination y0.</param>
    /// <param name="dstX1">Destination x1.</param>
    /// <param name="dstY1">Destination y1.</param>
    /// <param name="mask">The clear buffer mask.</param>
    /// <param name="filter">The filter mode.</param>
    public void BlitTo(
        GLFramebuffer target,
        int srcX0, int srcY0, int srcX1, int srcY1,
        int dstX0, int dstY0, int dstX1, int dstY1,
        uint mask, uint filter = GLConst.NearestFilter)
    {
        ArgumentNullException.ThrowIfNull(target);
        EnsureAlive();
        _context.AssertRenderThread();

        _context.State.BindFramebuffer(GLConst.ReadFramebuffer, Id);
        _context.State.BindFramebuffer(GLConst.DrawFramebuffer, target.Id);

        _gl.BlitFramebuffer(srcX0, srcY0, srcX1, srcY1, dstX0, dstY0, dstX1, dstY1, mask, filter);
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

        Id = _gl.GenFramebuffer();

        // Re-attach all attachments
        for (int i = 0; i < _colorAttachments.Count; i++)
        {
            if (_colorAttachments[i] != null)
            {
                var texture = _colorAttachments[i];
                _context.State.BindFramebuffer(GLConst.Framebuffer, Id);
                _gl.FramebufferTexture2D(
                    GLConst.Framebuffer,
                    GLConst.ColorAttachment0 + (uint)i,
                    texture.Target,
                    texture.Id,
                    0);
            }
        }

        if (_depthAttachment != null)
        {
            _context.State.BindFramebuffer(GLConst.Framebuffer, Id);
            _gl.FramebufferTexture2D(
                GLConst.Framebuffer,
                GLConst.DepthAttachment,
                _depthAttachment.Target,
                _depthAttachment.Id,
                0);
        }

        if (_depthStencilAttachment != null)
        {
            _context.State.BindFramebuffer(GLConst.Framebuffer, Id);
            _gl.FramebufferTexture2D(
                GLConst.Framebuffer,
                GLConst.DepthStencilAttachment,
                _depthStencilAttachment.Target,
                _depthStencilAttachment.Id,
                0);
        }

        UpdateDrawBuffers();

        if (_context.DebugLabelsEnabled && !string.IsNullOrEmpty(Label))
        {
            _gl.ObjectLabel(0x8D40, Id, Label); // GL_FRAMEBUFFER
        }
    }

    private void EnsureAlive()
    {
        if (IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "Framebuffer has been disposed", nameof(GLFramebuffer));
        }

        if (_context.Lifecycle.IsLost)
        {
            ThrowHelper.Throw(RenderErrorCode.ContextLost, "GL context is lost", nameof(GLFramebuffer));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        if (Id != 0)
        {
            _gl.DeleteFramebuffer(Id);
            Id = 0;
        }

        _colorAttachments.Clear();
        _depthAttachment = null;
        _depthStencilAttachment = null;
        _depthRenderbuffer = null;

        _context.Registry.Unregister(this);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"GLFramebuffer: Id={Id}, Size={Width}x{Height}, Colors={_colorAttachments.Count}, Depth={HasDepth}, Label=\"{Label}\"";
}
