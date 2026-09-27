namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Presents a frame-graph framebuffer to the default framebuffer (id 0).
/// Blits the full source rect onto the default framebuffer, then resets the viewport.
/// </summary>
public sealed class PresentPassExecutor : RenderPass
{
    private const uint DefaultFramebufferId = 0;

    private readonly string _sourceFramebufferName;
    private readonly int _destinationWidth;
    private readonly int _destinationHeight;
    private readonly uint _filterMode;
    private readonly uint _blitMask;

    /// <summary>
    /// Initializes a new instance of the <see cref="PresentPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="sourceFramebufferName">The source framebuffer resource name.</param>
    /// <param name="destinationWidth">The destination width on the default framebuffer. Zero follows the source width.</param>
    /// <param name="destinationHeight">The destination height on the default framebuffer. Zero follows the source height.</param>
    /// <param name="filterMode">The filter mode for blitting (default: nearest).</param>
    /// <param name="blitMask">The buffer mask for blitting (default: color buffer).</param>
    public PresentPassExecutor(
        string name,
        string sourceFramebufferName,
        int destinationWidth = 0,
        int destinationHeight = 0,
        uint filterMode = GLConst.NearestFilter,
        uint blitMask = GLConst.ColorBufferBit)
        : base(name, FramePassKind.Present)
    {
        ArgumentNullException.ThrowIfNull(sourceFramebufferName);

        _sourceFramebufferName = sourceFramebufferName;
        _destinationWidth = destinationWidth;
        _destinationHeight = destinationHeight;
        _filterMode = filterMode;
        _blitMask = blitMask;

        Reads(sourceFramebufferName);
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (string.IsNullOrWhiteSpace(_sourceFramebufferName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Source framebuffer name must not be empty", nameof(PresentPassExecutor));
        }

        if (_destinationWidth < 0 || _destinationHeight < 0)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Destination dimensions must not be negative", nameof(PresentPassExecutor));
        }

        if ((_destinationWidth == 0) != (_destinationHeight == 0))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Destination width and height must either both be set or both follow the source", nameof(PresentPassExecutor));
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override void Execute(GLContext context, PassExecutionContext ctx)
    {
        context.AssertRenderThread();

        var gl = context.GL;
        var state = context.State;

        var sourceFbo = ctx.GetFramebuffer(_sourceFramebufferName);

        int dstWidth = _destinationWidth != 0 ? _destinationWidth : sourceFbo.Width;
        int dstHeight = _destinationHeight != 0 ? _destinationHeight : sourceFbo.Height;

        state.BindFramebuffer(GLConst.ReadFramebuffer, sourceFbo.Id);
        state.BindFramebuffer(GLConst.DrawFramebuffer, DefaultFramebufferId);

        gl.BlitFramebuffer(
            0, 0, sourceFbo.Width, sourceFbo.Height,
            0, 0, dstWidth, dstHeight,
            _blitMask,
            _filterMode);

        state.BindFramebuffer(GLConst.Framebuffer, DefaultFramebufferId);
        state.SetViewport(0, 0, dstWidth, dstHeight);
    }
}
