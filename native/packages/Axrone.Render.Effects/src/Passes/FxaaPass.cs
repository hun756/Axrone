namespace Axrone.Render.Effects;

/// <summary>
/// Payload for the FXAA (Fast Approximate Anti-Aliasing) pass.
/// </summary>
public record struct FxaaPassData
{
    /// <summary>FXAA shader program.</summary>
    public GLProgram Shader { get; set; }

    /// <summary>Input LDR texture resource name.</summary>
    public string InputTextureName { get; set; }

    /// <summary>Output anti-aliased texture resource name.</summary>
    public string OutputTextureName { get; set; }

    /// <summary>Sub-pixel aliasing removal quality in [0, 1]. Update per frame if animated.</summary>
    public float SubpixelQuality { get; set; }

    /// <summary>Edge detection luminance threshold. Must be non-negative.</summary>
    public float EdgeThreshold { get; set; }

    /// <summary>Absolute minimum luminance for FXAA application. Must be non-negative.</summary>
    public float EdgeThresholdMin { get; set; }
}

/// <summary>
/// Factory for the FXAA post-process pass.
/// The shader is expected to expose <c>u_texture</c>, <c>u_texelSize</c>,
/// <c>u_subpixelQuality</c>, <c>u_edgeThreshold</c> and <c>u_edgeThresholdMin</c>.
/// </summary>
public static class FxaaPass
{
    /// <summary>Creates an FXAA pass.</summary>
    public static RenderPass<FxaaPassData> Create(
        string name,
        GLProgram shader,
        string inputTextureName = "scene_ldr",
        string outputTextureName = "scene_aa",
        float subpixelQuality = 0.75f,
        float edgeThreshold = 0.125f,
        float edgeThresholdMin = 0.0312f)
    {
        ArgumentNullException.ThrowIfNull(shader);
        return new RenderPass<FxaaPassData>(
            name,
            FramePassKind.Fxaa,
            (IRenderPassBuilder builder, ref FxaaPassData data) =>
            {
                data.Shader = shader;
                data.InputTextureName = inputTextureName;
                data.OutputTextureName = outputTextureName;
                data.SubpixelQuality = subpixelQuality;
                data.EdgeThreshold = edgeThreshold;
                data.EdgeThresholdMin = edgeThresholdMin;
                builder.Reads(inputTextureName);
                builder.Writes(outputTextureName);
            },
            Execute,
            Validate);
    }

    private static void Validate(in FxaaPassData data)
    {
        if (data.SubpixelQuality < 0f || data.SubpixelQuality > 1f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Subpixel quality must be in [0, 1], got {data.SubpixelQuality}", nameof(FxaaPass));
        }

        if (data.EdgeThreshold < 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Edge threshold must be non-negative, got {data.EdgeThreshold}", nameof(FxaaPass));
        }

        if (data.EdgeThresholdMin < 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Edge threshold min must be non-negative, got {data.EdgeThresholdMin}", nameof(FxaaPass));
        }
    }

    private static void Execute(in FxaaPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();

        var inputTexture = ctx.GetTexture(data.InputTextureName);
        var outputTexture = ctx.GetTexture(data.OutputTextureName);

        if (ctx.HasResource(data.OutputTextureName + "_fbo"))
        {
            glContext.State.BindFramebuffer(GLConst.Framebuffer, ctx.GetFramebuffer(data.OutputTextureName + "_fbo").Id);
        }

        glContext.State.SetViewport(0, 0, outputTexture.Width, outputTexture.Height);
        glContext.State.SetDepthTest(false);
        glContext.State.SetCullFace(false);

        glContext.State.UseProgram(data.Shader.Id);
        glContext.State.BindTexture2D(0, inputTexture.Id);

        var gl = glContext.GL;
        gl.Uniform2(data.Shader.GetUniformLocation("u_texelSize"),
            1.0f / inputTexture.Width, 1.0f / inputTexture.Height);
        gl.Uniform1(data.Shader.GetUniformLocation("u_subpixelQuality"), data.SubpixelQuality);
        gl.Uniform1(data.Shader.GetUniformLocation("u_edgeThreshold"), data.EdgeThreshold);
        gl.Uniform1(data.Shader.GetUniformLocation("u_edgeThresholdMin"), data.EdgeThresholdMin);

        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}
