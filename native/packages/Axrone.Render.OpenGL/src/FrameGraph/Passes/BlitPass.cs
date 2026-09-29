#pragma warning disable CA1062 // Phase parameters are supplied by the frame graph, which null-checks before dispatch.

using Axrone.Execution;
using RenderPump = Axrone.Execution.CommandPump<
    Axrone.Render.Core.RenderCommand,
    Axrone.Render.OpenGL.FrameGraph.RenderPumpContext,
    Axrone.Render.OpenGL.FrameGraph.RenderCommandProcessor,
    Axrone.Utility.Backoff.SpinPolicies.AdaptiveSpinBackoff,
    Axrone.Execution.NullExecutorTelemetry>;

namespace Axrone.Render.OpenGL.FrameGraph.Passes;

/// <summary>
/// Payload for the framebuffer blit (copy) pass, owning the pass's setup, validate,
/// execute and pump-enqueue phases.
/// </summary>
public record struct BlitPassData
    : IPassSetup<BlitPassData>, IPassValidate<BlitPassData>, IPassExecute<BlitPassData>, IPassEnqueue<BlitPassData>
{
    /// <summary>Source framebuffer resource name.</summary>
    public string SourceFramebufferName { get; set; }

    /// <summary>Destination framebuffer resource name.</summary>
    public string DestinationFramebufferName { get; set; }

    /// <summary>Filter mode for blitting.</summary>
    public uint FilterMode { get; set; }

    /// <summary>Buffer mask for blitting.</summary>
    public uint BlitMask { get; set; }

    /// <inheritdoc/>
    public static void Declare(IRenderPassBuilder builder, ref BlitPassData data)
    {
        builder.Reads(data.SourceFramebufferName);
        builder.Writes(data.DestinationFramebufferName);
    }

    /// <inheritdoc/>
    public static void Validate(in BlitPassData data)
    {
        if (string.Equals(data.SourceFramebufferName, data.DestinationFramebufferName, StringComparison.OrdinalIgnoreCase))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Source and destination framebuffers must be different", nameof(BlitPass));
        }
    }

    /// <inheritdoc/>
    public static EnqueueResult EnqueueCommands(in BlitPassData data, RenderPump pump, PassExecutionContext ctx)
    {
        var sourceFbo = ctx.GetFramebuffer(data.SourceFramebufferName);
        var destFbo = ctx.GetFramebuffer(data.DestinationFramebufferName);
        DescriptorHandle<GLResourceNode> sourceHandle = sourceFbo.RegistryHandle;
        DescriptorHandle<GLResourceNode> destinationHandle = destFbo.RegistryHandle;

        RenderCommand command = RenderCommand.CreateBlit(
            in sourceHandle, in destinationHandle,
            0, 0, sourceFbo.Width, sourceFbo.Height,
            0, 0, destFbo.Width, destFbo.Height,
            data.BlitMask, data.FilterMode);

        return pump.TryEnqueue(in command);
    }

    /// <inheritdoc/>
    public static void Execute(in BlitPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        ctx.Context.AssertRenderThread();

        var sourceFbo = ctx.GetFramebuffer(data.SourceFramebufferName);
        var destFbo = ctx.GetFramebuffer(data.DestinationFramebufferName);

        // The bind+blit sequence lives once in the command, so the direct (pumpless)
        // leg and the pump replay leg issue exactly the same GL calls.
        var blitCommand = new BlitFramebufferCommand<GLFramebufferInvoker>(
            sourceFbo.Id, destFbo.Id,
            0, 0, sourceFbo.Width, sourceFbo.Height,
            0, 0, destFbo.Width, destFbo.Height,
            data.BlitMask, data.FilterMode);

        var invoker = new GLFramebufferInvoker(ctx.Context);

        BlitDispatcher.Dispatch(ref invoker, ref blitCommand);
    }
}

/// <summary>
/// Factory for the framebuffer blit pass.
/// </summary>
public static class BlitPass
{
    /// <summary>Creates a pump-capable blit pass.</summary>
    public static PumpRenderPass<BlitPassData> Create(
        string name,
        string sourceFramebufferName,
        string destinationFramebufferName,
        uint filterMode = GLConst.NearestFilter,
        uint blitMask = GLConst.ColorBufferBit)
    {
        ArgumentNullException.ThrowIfNull(sourceFramebufferName);
        ArgumentNullException.ThrowIfNull(destinationFramebufferName);
        return new PumpRenderPass<BlitPassData>(
            name,
            FramePassKind.Blit,
            new BlitPassData
            {
                SourceFramebufferName = sourceFramebufferName,
                DestinationFramebufferName = destinationFramebufferName,
                FilterMode = filterMode,
                BlitMask = blitMask
            });
    }
}
