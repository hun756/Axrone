#pragma warning disable CA1002 // Payloads expose concrete collections for allocation-free access.
#pragma warning disable CA2227 // The setup protocol assigns payload collections once at creation.
#pragma warning disable CA1062 // Phase parameters are supplied by the frame graph, which null-checks before dispatch.

namespace Axrone.Render.OpenGL.FrameGraph.Passes;

/// <summary>
/// Payload for the shadow map pass, owning the pass's setup, validate and execute phases.
/// </summary>
public record struct ShadowPassData
    : IPassSetup<ShadowPassData>, IPassValidate<ShadowPassData>, IPassExecute<ShadowPassData>
{
    /// <summary>Depth-only shader program.</summary>
    public GLProgram DepthProgram { get; set; }

    /// <summary>Shadow map texture resource name.</summary>
    public string ShadowMapTextureName { get; set; }

    /// <summary>Shadow framebuffer resource name.</summary>
    public string ShadowFramebufferName { get; set; }

    /// <summary>Light view-projection matrix. Update per frame before execution.</summary>
    public Mat4 LightViewProjection { get; set; }

    /// <summary>Shadow map width.</summary>
    public int ShadowMapWidth { get; set; }

    /// <summary>Shadow map height.</summary>
    public int ShadowMapHeight { get; set; }

    /// <summary>Meshes rendered into the shadow map.</summary>
    public List<GLMesh> Meshes { get; set; }

    /// <inheritdoc/>
    public static void Declare(IRenderPassBuilder builder, ref ShadowPassData data)
    {
        builder.Writes(data.ShadowMapTextureName);
        builder.Writes(data.ShadowFramebufferName);
    }

    /// <inheritdoc/>
    public static void Validate(in ShadowPassData data)
    {
        if (data.DepthProgram.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Depth shader program has been disposed", nameof(ShadowPass));
        }

        if (data.ShadowMapWidth <= 0 || data.ShadowMapHeight <= 0)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Shadow map dimensions must be positive", nameof(ShadowPass));
        }
    }

    /// <inheritdoc/>
    public static void Execute(in ShadowPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();

        var gl = glContext.GL;
        var state = glContext.State;

        uint fboId = 0;
        if (ctx.HasResource(data.ShadowFramebufferName))
        {
            fboId = ctx.GetFramebuffer(data.ShadowFramebufferName).Id;
        }

        state.BindFramebuffer(GLConst.Framebuffer, fboId);
        state.SetViewport(0, 0, data.ShadowMapWidth, data.ShadowMapHeight);

        state.SetDepthTest(true);
        state.SetDepthFunc(GLConst.Lequal);
        state.SetDepthMask(true);
        state.SetBlend(false);
        state.SetCullFace(true);
        state.SetCullMode(GLConst.Front);
        state.SetColorMask(false, false, false, false);

        state.UseProgram(data.DepthProgram.Id);

        int lightMatrixLocation = data.DepthProgram.GetUniformLocation("u_lightViewProjection");
        if (lightMatrixLocation >= 0)
        {
            unsafe
            {
                Mat4 matrix = data.LightViewProjection;
                gl.UniformMatrix4(lightMatrixLocation, 1, false, &matrix.M11);
            }
        }

        var meshSpan = CollectionsMarshal.AsSpan(data.Meshes);
        for (int i = 0; i < meshSpan.Length; i++)
        {
            ref var mesh = ref meshSpan[i];
            mesh.Draw();
        }

        state.SetColorMask(true, true, true, true);
    }
}

/// <summary>
/// Factory for the shadow map pass (depth-only from the light's perspective).
/// </summary>
public static class ShadowPass
{
    /// <summary>Creates a shadow map pass.</summary>
    public static RenderPass<ShadowPassData> Create(
        string name,
        GLProgram depthProgram,
        string shadowMapTextureName,
        string shadowFramebufferName,
        Mat4 lightViewProjection,
        int shadowMapWidth = 2048,
        int shadowMapHeight = 2048)
    {
        ArgumentNullException.ThrowIfNull(depthProgram);
        ArgumentNullException.ThrowIfNull(shadowMapTextureName);
        ArgumentNullException.ThrowIfNull(shadowFramebufferName);
        return new RenderPass<ShadowPassData>(
            name,
            FramePassKind.Shadow,
            new ShadowPassData
            {
                DepthProgram = depthProgram,
                ShadowMapTextureName = shadowMapTextureName,
                ShadowFramebufferName = shadowFramebufferName,
                LightViewProjection = lightViewProjection,
                ShadowMapWidth = shadowMapWidth,
                ShadowMapHeight = shadowMapHeight,
                Meshes = new List<GLMesh>()
            });
    }
}
