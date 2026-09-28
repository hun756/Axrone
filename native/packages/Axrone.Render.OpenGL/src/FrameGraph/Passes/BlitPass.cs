using Axrone.Execution;
using RenderPump = Axrone.Execution.CommandPump<
    Axrone.Render.Core.RenderCommand,
    Axrone.Render.OpenGL.FrameGraph.RenderPumpContext,
    Axrone.Render.OpenGL.FrameGraph.RenderCommandProcessor,
    Axrone.Utility.Backoff.SpinPolicies.AdaptiveSpinBackoff,
    Axrone.Execution.NullExecutorTelemetry>;

namespace Axrone.Render.OpenGL.FrameGraph.Passes;

/// <summary>
/// Payload for the framebuffer blit (copy) pass.
/// </summary>
public record struct BlitPassData
{
    /// <summary>Source framebuffer resource name.</summary>
    public string SourceFramebufferName { get; set; }

    /// <summary>Destination framebuffer resource name.</summary>
    public string DestinationFramebufferName { get; set; }

    /// <summary>Filter mode for blitting.</summary>
    public uint FilterMode { get; set; }

    /// <summary>Buffer mask for blitting.</summary>
    public uint BlitMask { get; set; }
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
            (IRenderPassBuilder builder, ref BlitPassData data) =>
            {
                data.SourceFramebufferName = sourceFramebufferName;
                data.DestinationFramebufferName = destinationFramebufferName;
                data.FilterMode = filterMode;
                data.BlitMask = blitMask;
                builder.Reads(sourceFramebufferName);
                builder.Writes(destinationFramebufferName);
            },
            Execute,
            Enqueue,
            Validate);
    }

    private static void Validate(in BlitPassData data)
    {
        if (string.Equals(data.SourceFramebufferName, data.DestinationFramebufferName, StringComparison.OrdinalIgnoreCase))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Source and destination framebuffers must be different", nameof(BlitPass));
        }
    }

    private static EnqueueResult Enqueue(in BlitPassData data, RenderPump pump, PassExecutionContext ctx)
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

    private static void Execute(in BlitPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        ctx.Context.AssertRenderThread();

        var sourceFbo = ctx.GetFramebuffer(data.SourceFramebufferName);
        var destFbo = ctx.GetFramebuffer(data.DestinationFramebufferName);

        sourceFbo.BlitTo(
            destFbo,
            0, 0, sourceFbo.Width, sourceFbo.Height,
            0, 0, destFbo.Width, destFbo.Height,
            data.BlitMask,
            data.FilterMode);
    }
}
