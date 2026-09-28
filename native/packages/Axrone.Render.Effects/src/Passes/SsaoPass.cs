namespace Axrone.Render.Effects;

/// <summary>
/// Payload for the screen-space ambient occlusion (SSAO) pass.
/// </summary>
public record struct SsaoPassData
{
    /// <summary>SSAO shader program.</summary>
    public GLProgram Program { get; set; }

    /// <summary>Depth texture resource name.</summary>
    public string DepthTextureName { get; set; }

    /// <summary>View-space normal texture resource name.</summary>
    public string NormalTextureName { get; set; }

    /// <summary>Occlusion output target name.</summary>
    public string OutputTextureName { get; set; }

    /// <summary>Depth sampler uniform name.</summary>
    public string DepthUniform { get; set; }

    /// <summary>Normal sampler uniform name.</summary>
    public string NormalUniform { get; set; }

    /// <summary>SSAO sampling radius in scene units. Must be in (0, 64].</summary>
    public float Radius { get; set; }

    /// <summary>Depth bias in scene units. Must be in [0, 1].</summary>
    public float Bias { get; set; }

    /// <summary>Occlusion strength multiplier. Must be in [0, 1].</summary>
    public float Intensity { get; set; }

    /// <summary>Post-process phase. HDR-space effects run before tone mapping.</summary>
    public PostProcessPhase Phase { get; set; }
}

/// <summary>
/// Factory for the SSAO post-process pass.
/// </summary>
public static class SsaoPass
{
    private const float MaxRadius = 64f;
    private const float MaxBias = 1f;

    /// <summary>Creates an SSAO pass.</summary>
    public static RenderPass<SsaoPassData> Create(
        string name,
        GLProgram program,
        string depthTextureName,
        string normalTextureName,
        string outputName,
        string depthUniform = "u_depth",
        string normalUniform = "u_normal",
        float radius = 0.5f,
        float bias = 0.02f,
        float intensity = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(depthTextureName);
        ArgumentNullException.ThrowIfNull(normalTextureName);
        ArgumentNullException.ThrowIfNull(outputName);
        return new RenderPass<SsaoPassData>(
            name,
            FramePassKind.PostProcess,
            (IRenderPassBuilder builder, ref SsaoPassData data) =>
            {
                data.Program = program;
                data.DepthTextureName = depthTextureName;
                data.NormalTextureName = normalTextureName;
                data.OutputTextureName = outputName;
                data.DepthUniform = depthUniform;
                data.NormalUniform = normalUniform;
                data.Radius = radius;
                data.Bias = bias;
                data.Intensity = intensity;
                data.Phase = PostProcessPhase.BeforeTonemap;
                builder.Reads(depthTextureName);
                builder.Reads(normalTextureName);
                builder.Writes(outputName);
            },
            Execute,
            Validate);
    }

    private static void Validate(in SsaoPassData data)
    {
        if (data.Program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "SSAO shader program has been disposed", nameof(SsaoPass));
        }

        if (string.IsNullOrWhiteSpace(data.DepthTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Depth texture name must not be empty", nameof(SsaoPass));
        }

        if (string.IsNullOrWhiteSpace(data.NormalTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Normal texture name must not be empty", nameof(SsaoPass));
        }

        if (string.IsNullOrWhiteSpace(data.OutputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "SSAO output name must not be empty", nameof(SsaoPass));
        }

        if (string.IsNullOrWhiteSpace(data.DepthUniform) || string.IsNullOrWhiteSpace(data.NormalUniform))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "SSAO sampler uniform names must not be empty", nameof(SsaoPass));
        }

        if (data.Radius <= 0f || data.Radius > MaxRadius)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"SSAO radius must be in (0, {MaxRadius}], got {data.Radius}", nameof(SsaoPass));
        }

        if (data.Bias < 0f || data.Bias > MaxBias)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"SSAO bias must be in [0, {MaxBias}], got {data.Bias}", nameof(SsaoPass));
        }

        if (data.Intensity < 0f || data.Intensity > 1f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"SSAO intensity must be in [0, 1], got {data.Intensity}", nameof(SsaoPass));
        }
    }

    private static void Execute(in SsaoPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();

        var gl = glContext.GL;
        var state = glContext.State;

        var depthTexture = ctx.GetTexture(data.DepthTextureName);
        var normalTexture = ctx.GetTexture(data.NormalTextureName);
        var outputTexture = ctx.GetTexture(data.OutputTextureName);

        if (ctx.HasResource(data.OutputTextureName + "_fbo"))
        {
            state.BindFramebuffer(GLConst.Framebuffer, ctx.GetFramebuffer(data.OutputTextureName + "_fbo").Id);
        }

        state.SetViewport(0, 0, outputTexture.Width, outputTexture.Height);
        state.SetDepthTest(false);
        state.SetBlend(false);
        state.SetCullFace(false);
        state.SetColorMask(true, true, true, true);

        state.UseProgram(data.Program.Id);
        state.BindTexture2D(0, depthTexture.Id);
        state.BindTexture2D(1, normalTexture.Id);

        int location = data.Program.GetUniformLocation(data.DepthUniform);
        if (location != -1)
        {
            gl.Uniform1(location, 0);
        }

        location = data.Program.GetUniformLocation(data.NormalUniform);
        if (location != -1)
        {
            gl.Uniform1(location, 1);
        }

        location = data.Program.GetUniformLocation("u_texelSize");
        if (location != -1)
        {
            gl.Uniform2(location, 1.0f / depthTexture.Width, 1.0f / depthTexture.Height);
        }

        location = data.Program.GetUniformLocation("u_radius");
        if (location != -1)
        {
            gl.Uniform1(location, data.Radius);
        }

        location = data.Program.GetUniformLocation("u_bias");
        if (location != -1)
        {
            gl.Uniform1(location, data.Bias);
        }

        location = data.Program.GetUniformLocation("u_intensity");
        if (location != -1)
        {
            gl.Uniform1(location, data.Intensity);
        }

        state.BindVertexArray(0);
        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}
