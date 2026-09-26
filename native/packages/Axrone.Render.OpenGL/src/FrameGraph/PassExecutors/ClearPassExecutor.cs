namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Clears color, depth, and/or stencil buffers of a target framebuffer.
/// </summary>
public sealed class ClearPassExecutor : RenderPass
{
    private readonly string? _targetFramebufferName;
    private readonly Vector4 _clearColor;
    private readonly double _clearDepth;
    private readonly int _clearStencil;
    private readonly bool _clearColorEnabled;
    private readonly bool _clearDepthEnabled;
    private readonly bool _clearStencilEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClearPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="targetFramebufferName">The target framebuffer resource name, or null for the default framebuffer.</param>
    /// <param name="clearColor">The clear color.</param>
    /// <param name="clearDepth">The clear depth value.</param>
    /// <param name="clearStencil">The clear stencil value.</param>
    /// <param name="clearColorEnabled">Whether to clear the color buffer.</param>
    /// <param name="clearDepthEnabled">Whether to clear the depth buffer.</param>
    /// <param name="clearStencilEnabled">Whether to clear the stencil buffer.</param>
    public ClearPassExecutor(
        string name,
        string? targetFramebufferName = null,
        Vector4 clearColor = default,
        double clearDepth = 1.0,
        int clearStencil = 0,
        bool clearColorEnabled = true,
        bool clearDepthEnabled = true,
        bool clearStencilEnabled = false)
        : base(name, FramePassKind.Clear)
    {
        _targetFramebufferName = targetFramebufferName;
        _clearColor = clearColor;
        _clearDepth = clearDepth;
        _clearStencil = clearStencil;
        _clearColorEnabled = clearColorEnabled;
        _clearDepthEnabled = clearDepthEnabled;
        _clearStencilEnabled = clearStencilEnabled;

        if (targetFramebufferName != null)
        {
            Reads(targetFramebufferName);
        }
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (!_clearColorEnabled && !_clearDepthEnabled && !_clearStencilEnabled)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "ClearPassExecutor must clear at least one buffer", nameof(ClearPassExecutor));
        }
    }

    /// <inheritdoc/>
    public override void Execute(GLContext context, PassExecutionContext ctx)
    {
        context.AssertRenderThread();

        var gl = context.GL;
        var state = context.State;

        // Bind target framebuffer
        uint fboId = 0;
        if (_targetFramebufferName != null && ctx.HasResource(_targetFramebufferName))
        {
            var fbo = ctx.GetFramebuffer(_targetFramebufferName);
            fboId = fbo.Id;
        }

        state.BindFramebuffer(GLConst.Framebuffer, fboId);

        // Set clear values
        if (_clearColorEnabled)
        {
            state.SetClearColor(_clearColor.X, _clearColor.Y, _clearColor.Z, _clearColor.W);
        }

        if (_clearDepthEnabled)
        {
            state.SetClearDepth(_clearDepth);
        }

        if (_clearStencilEnabled)
        {
            state.SetClearStencil(_clearStencil);
        }

        // Build clear mask
        uint mask = 0;
        if (_clearColorEnabled) mask |= GLConst.ColorBufferBit;
        if (_clearDepthEnabled) mask |= GLConst.DepthBufferBit;
        if (_clearStencilEnabled) mask |= GLConst.StencilBufferBit;

        if (mask != 0)
        {
            gl.Clear(mask);
        }
    }
}
