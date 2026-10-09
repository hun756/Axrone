#pragma warning disable CA1062 // Phase parameters are supplied by the frame graph, which null-checks before dispatch.

namespace Axrone.Render.OpenGL.FrameGraph.Passes;

/// <summary>
/// Payload for the cubemap skybox pass, owning the pass's setup, validate and execute phases.
/// The cubemap is a direct GPU handle (not a graph resource).
/// </summary>
public record struct SkyboxPassData
    : IPassSetup<SkyboxPassData>, IPassValidate<SkyboxPassData>, IPassExecute<SkyboxPassData>
{
    /// <summary>Skybox shader program.</summary>
    public GLProgram SkyboxProgram { get; set; }

    /// <summary>Cubemap texture sampled by view direction.</summary>
    public GLTexture Cubemap { get; set; }

    /// <summary>View-projection matrix uniform name.</summary>
    public string ViewProjectionUniform { get; set; }

    /// <summary>Cubemap sampler uniform name.</summary>
    public string CubemapUniform { get; set; }

    /// <summary>View-projection matrix uploaded on each execution. Update per frame.</summary>
    public Mat4 ViewProjection { get; set; }

    /// <summary>Output framebuffer name, or null to keep the current binding.</summary>
    public string? OutputFramebufferName { get; set; }

    /// <inheritdoc/>
    public static void Declare(IRenderPassBuilder builder, ref SkyboxPassData data)
    {
        if (data.OutputFramebufferName is not null)
        {
            builder.Writes(data.OutputFramebufferName);
        }
    }

    /// <inheritdoc/>
    public static void Validate(in SkyboxPassData data)
    {
        if (data.SkyboxProgram.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Skybox shader program has been disposed", nameof(SkyboxPass));
        }

        if (data.Cubemap.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Skybox cubemap texture has been disposed", nameof(SkyboxPass));
        }

        if (string.IsNullOrWhiteSpace(data.ViewProjectionUniform))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "View-projection uniform name must not be empty", nameof(SkyboxPass));
        }

        if (string.IsNullOrWhiteSpace(data.CubemapUniform))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Cubemap uniform name must not be empty", nameof(SkyboxPass));
        }

        if (data.Cubemap.Target != GLConst.TextureCubeMap)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Skybox texture must be a cube map", nameof(SkyboxPass));
        }
    }

    /// <inheritdoc/>
    public static void Execute(in SkyboxPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();

        var gl = glContext.GL;
        var state = glContext.State;

        if (data.OutputFramebufferName is not null && ctx.HasResource(data.OutputFramebufferName))
        {
            var fbo = ctx.GetFramebuffer(data.OutputFramebufferName);
            state.BindFramebuffer(GLConst.Framebuffer, fbo.Id);
            state.SetViewport(0, 0, fbo.Width, fbo.Height);
        }

        state.SetDepthTest(true);
        state.SetDepthFunc(GLConst.Lequal);
        state.SetDepthMask(false);
        state.SetBlend(false);
        state.SetCullFace(false);
        state.SetColorMask(true, true, true, true);

        state.UseProgram(data.SkyboxProgram.Id);

        state.ActiveTexture(0);
        state.BindTexture(GLConst.TextureCubeMap, data.Cubemap.Id);

        int cubemapLocation = data.SkyboxProgram.GetUniformLocation(data.CubemapUniform);
        if (cubemapLocation != -1)
        {
            gl.Uniform1(cubemapLocation, 0);
        }

        int viewProjectionLocation = data.SkyboxProgram.GetUniformLocation(data.ViewProjectionUniform);
        if (viewProjectionLocation != -1)
        {
            unsafe
            {
                Mat4 matrix = data.ViewProjection;
                gl.UniformMatrix4(viewProjectionLocation, 1, false, &matrix.M11);
            }
        }

        state.BindVertexArray(0);
        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}

/// <summary>
/// Factory for the cubemap skybox background pass (far-plane fullscreen triangle).
/// </summary>
public static class SkyboxPass
{
    /// <summary>Creates a skybox pass.</summary>
    public static RenderPass<SkyboxPassData> Create(
        string name,
        GLProgram skyboxProgram,
        GLTexture cubemap,
        string viewProjectionUniform,
        string cubemapUniform = "u_skybox",
        string? outputFramebufferName = null)
    {
        ArgumentNullException.ThrowIfNull(skyboxProgram);
        ArgumentNullException.ThrowIfNull(cubemap);
        ArgumentNullException.ThrowIfNull(viewProjectionUniform);
        ArgumentNullException.ThrowIfNull(cubemapUniform);

        if (string.IsNullOrWhiteSpace(viewProjectionUniform))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "View-projection uniform name must not be empty", nameof(SkyboxPass));
        }

        if (string.IsNullOrWhiteSpace(cubemapUniform))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Cubemap uniform name must not be empty", nameof(SkyboxPass));
        }

        return new RenderPass<SkyboxPassData>(
            name,
            FramePassKind.Skybox,
            new SkyboxPassData
            {
                SkyboxProgram = skyboxProgram,
                Cubemap = cubemap,
                ViewProjectionUniform = viewProjectionUniform,
                CubemapUniform = cubemapUniform,
                ViewProjection = Mat4.Identity,
                OutputFramebufferName = outputFramebufferName
            });
    }
}
