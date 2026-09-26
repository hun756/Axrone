namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Blits (copies) content from one framebuffer to another with optional filtering.
/// </summary>
public sealed class BlitPassExecutor : RenderPass
{
    private readonly string _sourceFramebufferName;
    private readonly string _destinationFramebufferName;
    private readonly uint _filterMode;
    private readonly uint _blitMask;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlitPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="sourceFramebufferName">The source framebuffer resource name.</param>
    /// <param name="destinationFramebufferName">The destination framebuffer resource name.</param>
    /// <param name="filterMode">The filter mode for blitting (default: nearest).</param>
    /// <param name="blitMask">The buffer mask for blitting (default: color buffer).</param>
    public BlitPassExecutor(
        string name,
        string sourceFramebufferName,
        string destinationFramebufferName,
        uint filterMode = GLConst.NearestFilter,
        uint blitMask = GLConst.ColorBufferBit)
        : base(name, FramePassKind.Blit)
    {
        ArgumentNullException.ThrowIfNull(sourceFramebufferName);
        ArgumentNullException.ThrowIfNull(destinationFramebufferName);

        _sourceFramebufferName = sourceFramebufferName;
        _destinationFramebufferName = destinationFramebufferName;
        _filterMode = filterMode;
        _blitMask = blitMask;

        Reads(sourceFramebufferName);
        Writes(destinationFramebufferName);
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (string.Equals(_sourceFramebufferName, _destinationFramebufferName, StringComparison.OrdinalIgnoreCase))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Source and destination framebuffers must be different", nameof(BlitPassExecutor));
        }
    }

    /// <inheritdoc/>
    public override void Execute(GLContext context, PassExecutionContext ctx)
    {
        context.AssertRenderThread();

        var gl = context.GL;
        var state = context.State;

        // Get source and destination framebuffers
        var sourceFbo = ctx.GetFramebuffer(_sourceFramebufferName);
        var destFbo = ctx.GetFramebuffer(_destinationFramebufferName);

        // Blit from source to destination
        sourceFbo.BlitTo(
            destFbo,
            0, 0, sourceFbo.Width, sourceFbo.Height,
            0, 0, destFbo.Width, destFbo.Height,
            _blitMask,
            _filterMode);
    }
}
