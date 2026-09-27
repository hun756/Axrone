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

    /// <summary>
    /// Enqueues this pass's blit as a render command into a pump.
    /// The pump-derived execution path: identical pixels, library-driven dispatch.
    /// </summary>
    /// <param name="pump">The pump receiving the command.</param>
    /// <param name="ctx">The pass execution context for resource resolution.</param>
    /// <returns>The enqueue receipt.</returns>
    internal EnqueueResult EnqueueCommands(RenderPump pump, PassExecutionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(pump);
        ArgumentNullException.ThrowIfNull(ctx);

        var sourceFbo = ctx.GetFramebuffer(_sourceFramebufferName);
        var destFbo = ctx.GetFramebuffer(_destinationFramebufferName);
        DescriptorHandle<GLResourceNode> sourceHandle = sourceFbo.RegistryHandle;
        DescriptorHandle<GLResourceNode> destinationHandle = destFbo.RegistryHandle;

        RenderCommand command = RenderCommand.CreateBlit(
            in sourceHandle, in destinationHandle,
            0, 0, sourceFbo.Width, sourceFbo.Height,
            0, 0, destFbo.Width, destFbo.Height,
            _blitMask, _filterMode);

        return pump.TryEnqueue(in command);
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
