using Axrone.Execution;
using Axrone.Utility.Backoff.SpinPolicies;
using RenderPump = Axrone.Execution.CommandPump<
    Axrone.Render.Core.RenderCommand,
    Axrone.Render.OpenGL.FrameGraph.RenderPumpContext,
    Axrone.Render.OpenGL.FrameGraph.RenderCommandProcessor,
    Axrone.Utility.Backoff.SpinPolicies.AdaptiveSpinBackoff,
    Axrone.Execution.NullExecutorTelemetry>;

namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Presents a frame-graph framebuffer to the default framebuffer (id 0).
/// Blits the full source rect onto the default framebuffer, then resets the viewport.
/// </summary>
public sealed class PresentPassExecutor : RenderPass, IPumpEnqueue
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

    /// <summary>
    /// Enqueues this pass's present as a render command into a pump.
    /// The pump-derived execution path: identical pixels, library-driven dispatch.
    /// </summary>
    /// <remarks>
    /// The source is resolved exactly as <see cref="Execute"/> resolves it — the
    /// pass has no present-name special case on the source side, because the
    /// source is always a named, registry-managed framebuffer. The destination
    /// needs no resolution at all: it is always framebuffer 0, which the packet
    /// expresses by carrying no destination handle.
    /// </remarks>
    /// <param name="pump">The pump receiving the command.</param>
    /// <param name="ctx">The pass execution context for resource resolution.</param>
    /// <returns>The enqueue receipt.</returns>
    /// <inheritdoc cref="IPumpEnqueue.EnqueueCommands"/>
    public EnqueueResult EnqueueCommands(RenderPump pump, PassExecutionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(pump);
        ArgumentNullException.ThrowIfNull(ctx);

        var sourceFbo = ctx.GetFramebuffer(_sourceFramebufferName);

        // Same destination sizing as Execute: an unset pair follows the source.
        int dstWidth = _destinationWidth != 0 ? _destinationWidth : sourceFbo.Width;
        int dstHeight = _destinationHeight != 0 ? _destinationHeight : sourceFbo.Height;

        // Copy the handle out before passing it by reference: CS8156 forbids
        // `in fbo.RegistryHandle`, and the packet travels by handle, not by GL name.
        DescriptorHandle<GLResourceNode> sourceHandle = sourceFbo.RegistryHandle;

        RenderCommand command = RenderCommand.CreatePresent(
            in sourceHandle,
            0, 0, sourceFbo.Width, sourceFbo.Height,
            0, 0, dstWidth, dstHeight,
            _blitMask, _filterMode);

        return pump.TryEnqueue(in command);
    }
}
