using Axrone.Execution;
using RenderPump = Axrone.Execution.CommandPump<
    Axrone.Render.Core.RenderCommand,
    Axrone.Render.OpenGL.FrameGraph.RenderPumpContext,
    Axrone.Render.OpenGL.FrameGraph.RenderCommandProcessor,
    Axrone.Utility.Backoff.SpinPolicies.AdaptiveSpinBackoff,
    Axrone.Execution.NullExecutorTelemetry>;

namespace Axrone.Render.OpenGL.FrameGraph.Passes;

/// <summary>
/// Payload for the presentation pass (blit to the default framebuffer).
/// </summary>
public record struct PresentPassData
{
    /// <summary>Source framebuffer resource name.</summary>
    public string SourceFramebufferName { get; set; }

    /// <summary>Destination width on the default framebuffer. Zero follows the source width.</summary>
    public int DestinationWidth { get; set; }

    /// <summary>Destination height on the default framebuffer. Zero follows the source height.</summary>
    public int DestinationHeight { get; set; }

    /// <summary>Filter mode for blitting.</summary>
    public uint FilterMode { get; set; }

    /// <summary>Buffer mask for blitting.</summary>
    public uint BlitMask { get; set; }
}

/// <summary>
/// Factory for the presentation pass.
/// </summary>
public static class PresentPass
{
    private const uint DefaultFramebufferId = 0;

    /// <summary>Creates a pump-capable present pass.</summary>
    public static PumpRenderPass<PresentPassData> Create(
        string name,
        string sourceFramebufferName,
        int destinationWidth = 0,
        int destinationHeight = 0,
        uint filterMode = GLConst.NearestFilter,
        uint blitMask = GLConst.ColorBufferBit)
    {
        ArgumentNullException.ThrowIfNull(sourceFramebufferName);
        return new PumpRenderPass<PresentPassData>(
            name,
            FramePassKind.Present,
            (IRenderPassBuilder builder, ref PresentPassData data) =>
            {
                data.SourceFramebufferName = sourceFramebufferName;
                data.DestinationWidth = destinationWidth;
                data.DestinationHeight = destinationHeight;
                data.FilterMode = filterMode;
                data.BlitMask = blitMask;
                builder.Reads(sourceFramebufferName);
            },
            Execute,
            Enqueue,
            Validate);
    }

    private static void Validate(in PresentPassData data)
    {
        if (string.IsNullOrWhiteSpace(data.SourceFramebufferName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Source framebuffer name must not be empty", nameof(PresentPass));
        }

        if (data.DestinationWidth < 0 || data.DestinationHeight < 0)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Destination dimensions must not be negative", nameof(PresentPass));
        }

        if ((data.DestinationWidth == 0) != (data.DestinationHeight == 0))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Destination width and height must either both be set or both follow the source", nameof(PresentPass));
        }
    }

    private static void Execute(in PresentPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();

        var gl = glContext.GL;
        var state = glContext.State;

        var sourceFbo = ctx.GetFramebuffer(data.SourceFramebufferName);

        int dstWidth = data.DestinationWidth != 0 ? data.DestinationWidth : sourceFbo.Width;
        int dstHeight = data.DestinationHeight != 0 ? data.DestinationHeight : sourceFbo.Height;

        state.BindFramebuffer(GLConst.ReadFramebuffer, sourceFbo.Id);
        state.BindFramebuffer(GLConst.DrawFramebuffer, DefaultFramebufferId);

        gl.BlitFramebuffer(
            0, 0, sourceFbo.Width, sourceFbo.Height,
            0, 0, dstWidth, dstHeight,
            data.BlitMask,
            data.FilterMode);

        state.BindFramebuffer(GLConst.Framebuffer, DefaultFramebufferId);
        state.SetViewport(0, 0, dstWidth, dstHeight);
    }

    private static EnqueueResult Enqueue(in PresentPassData data, RenderPump pump, PassExecutionContext ctx)
    {
        var sourceFbo = ctx.GetFramebuffer(data.SourceFramebufferName);

        int dstWidth = data.DestinationWidth != 0 ? data.DestinationWidth : sourceFbo.Width;
        int dstHeight = data.DestinationHeight != 0 ? data.DestinationHeight : sourceFbo.Height;

        DescriptorHandle<GLResourceNode> sourceHandle = sourceFbo.RegistryHandle;

        RenderCommand command = RenderCommand.CreatePresent(
            in sourceHandle,
            0, 0, sourceFbo.Width, sourceFbo.Height,
            0, 0, dstWidth, dstHeight,
            data.BlitMask, data.FilterMode);

        return pump.TryEnqueue(in command);
    }
}
