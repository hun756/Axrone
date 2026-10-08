using Axrone.Execution;
using RenderPump = Axrone.Execution.CommandPump<
    Axrone.Render.Core.RenderCommand,
    Axrone.Render.OpenGL.FrameGraph.RenderPumpContext,
    Axrone.Render.OpenGL.FrameGraph.RenderCommandProcessor,
    Axrone.Utility.Backoff.SpinPolicies.AdaptiveSpinBackoff,
    Axrone.Execution.NullExecutorTelemetry>;

#pragma warning disable CA1002 // Payloads expose concrete collections for allocation-free access.
#pragma warning disable CA2227 // The setup protocol assigns payload collections once at creation.
#pragma warning disable CA1062 // Phase parameters are supplied by the frame graph, which null-checks before dispatch.

namespace Axrone.Render.OpenGL.FrameGraph.Passes;

/// <summary>
/// Payload for the clear pass, owning the pass's setup, validate, execute and
/// pump-enqueue phases.
/// </summary>
public record struct ClearPassData
    : IPassSetup<ClearPassData>, IPassValidate<ClearPassData>, IPassExecute<ClearPassData>, IPassEnqueue<ClearPassData>
{
    /// <summary>Target framebuffer resource name, or null for the default framebuffer.</summary>
    public string? TargetFramebufferName { get; set; }

    /// <summary>Clear color.</summary>
    public Vec4 ClearColor { get; set; }

    /// <summary>Clear depth value.</summary>
    public float ClearDepth { get; set; }

    /// <summary>Clear stencil value.</summary>
    public int ClearStencil { get; set; }

    /// <summary>Whether to clear the color buffer.</summary>
    public bool ClearColorEnabled { get; set; }

    /// <summary>Whether to clear the depth buffer.</summary>
    public bool ClearDepthEnabled { get; set; }

    /// <summary>Whether to clear the stencil buffer.</summary>
    public bool ClearStencilEnabled { get; set; }

    /// <inheritdoc/>
    public static void Declare(IRenderPassBuilder builder, ref ClearPassData data)
    {
        if (data.TargetFramebufferName is not null)
        {
            builder.Reads(data.TargetFramebufferName);
        }
    }

    /// <inheritdoc/>
    public static void Validate(in ClearPassData data)
    {
        if (!data.ClearColorEnabled && !data.ClearDepthEnabled && !data.ClearStencilEnabled)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Clear pass must clear at least one buffer", nameof(ClearPass));
        }
    }

    /// <inheritdoc/>
    public static EnqueueResult EnqueueCommands(in ClearPassData data, RenderPump pump, PassExecutionContext ctx)
    {
        if (data.TargetFramebufferName is null || !ctx.HasResource(data.TargetFramebufferName))
        {
            return new EnqueueResult(-1, EnqueueStatus.Closed);
        }

        uint mask = 0;
        if (data.ClearColorEnabled) mask |= GLConst.ColorBufferBit;
        if (data.ClearDepthEnabled) mask |= GLConst.DepthBufferBit;
        if (data.ClearStencilEnabled) mask |= GLConst.StencilBufferBit;

        DescriptorHandle<GLResourceNode> targetHandle = ctx.GetFramebuffer(data.TargetFramebufferName).RegistryHandle;

        RenderCommand command = RenderCommand.CreateClear(
            in targetHandle,
            data.ClearColor.X, data.ClearColor.Y, data.ClearColor.Z, data.ClearColor.W,
            data.ClearDepth, data.ClearStencil, mask);

        return pump.TryEnqueue(in command);
    }

    /// <inheritdoc/>
    public static void Execute(in ClearPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();

        var gl = glContext.GL;
        var state = glContext.State;

        uint fboId = 0;
        if (data.TargetFramebufferName is not null && ctx.HasResource(data.TargetFramebufferName))
        {
            fboId = ctx.GetFramebuffer(data.TargetFramebufferName).Id;
        }

        state.BindFramebuffer(GLConst.Framebuffer, fboId);

        if (data.ClearColorEnabled)
        {
            state.SetClearColor(data.ClearColor.X, data.ClearColor.Y, data.ClearColor.Z, data.ClearColor.W);
        }

        if (data.ClearDepthEnabled)
        {
            state.SetClearDepth(data.ClearDepth);
        }

        if (data.ClearStencilEnabled)
        {
            state.SetClearStencil(data.ClearStencil);
        }

        uint mask = 0;
        if (data.ClearColorEnabled) mask |= GLConst.ColorBufferBit;
        if (data.ClearDepthEnabled) mask |= GLConst.DepthBufferBit;
        if (data.ClearStencilEnabled) mask |= GLConst.StencilBufferBit;

        if (mask != 0)
        {
            gl.Clear(mask);
        }
    }
}

/// <summary>
/// Factory for the framebuffer clear pass (color / depth / stencil).
/// </summary>
public static class ClearPass
{
    /// <summary>Creates a pump-capable clear pass.</summary>
    public static PumpRenderPass<ClearPassData> Create(
        string name,
        string? targetFramebufferName = null,
        Vec4 clearColor = default,
        float clearDepth = 1.0f,
        int clearStencil = 0,
        bool clearColorEnabled = true,
        bool clearDepthEnabled = true,
        bool clearStencilEnabled = false)
    {
        return new PumpRenderPass<ClearPassData>(
            name,
            FramePassKind.Clear,
            new ClearPassData
            {
                TargetFramebufferName = targetFramebufferName,
                ClearColor = clearColor,
                ClearDepth = clearDepth,
                ClearStencil = clearStencil,
                ClearColorEnabled = clearColorEnabled,
                ClearDepthEnabled = clearDepthEnabled,
                ClearStencilEnabled = clearStencilEnabled
            });
    }
}
