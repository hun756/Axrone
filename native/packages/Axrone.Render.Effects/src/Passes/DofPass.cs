namespace Axrone.Render.Effects;

/// <summary>
/// Payload for the depth-of-field (DoF) pass.
/// </summary>
public record struct DofPassData
{
    /// <summary>Depth-of-field shader program.</summary>
    public GLProgram Program { get; set; }

    /// <summary>Input colour texture resource name.</summary>
    public string InputTextureName { get; set; }

    /// <summary>Depth texture resource name.</summary>
    public string DepthTextureName { get; set; }

    /// <summary>Defocused output target name.</summary>
    public string OutputTextureName { get; set; }

    /// <summary>Focus plane distance in scene units. Must be positive.</summary>
    public float FocusDistance { get; set; }

    /// <summary>In-focus depth band around the focus plane. Must be non-negative.</summary>
    public float FocusRange { get; set; }

    /// <summary>Maximum circle-of-confusion radius in pixels. Must be in [0, 64].</summary>
    public float MaxBlur { get; set; }

    /// <summary>Post-process phase. HDR-space effects run before tone mapping.</summary>
    public PostProcessPhase Phase { get; set; }
}

/// <summary>
/// Factory for the depth-of-field post-process pass.
/// </summary>
public static class DofPass
{
    private const float MaxBlurPixels = 64f;

    /// <summary>Creates a depth-of-field pass.</summary>
    public static RenderPass<DofPassData> Create(
        string name,
        GLProgram program,
        string inputName,
        string depthName,
        string outputName,
        float focusDistance = 10f,
        float focusRange = 5f,
        float maxBlur = 8f)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(inputName);
        ArgumentNullException.ThrowIfNull(depthName);
        ArgumentNullException.ThrowIfNull(outputName);
        return new RenderPass<DofPassData>(
            name,
            FramePassKind.PostProcess,
            (IRenderPassBuilder builder, ref DofPassData data) =>
            {
                data.Program = program;
                data.InputTextureName = inputName;
                data.DepthTextureName = depthName;
                data.OutputTextureName = outputName;
                data.FocusDistance = focusDistance;
                data.FocusRange = focusRange;
                data.MaxBlur = maxBlur;
                data.Phase = PostProcessPhase.BeforeTonemap;
                builder.Reads(inputName);
                builder.Reads(depthName);
                builder.Writes(outputName);
            },
            Execute,
            Validate);
    }

    private static void Validate(in DofPassData data)
    {
        if (data.Program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Depth-of-field shader program has been disposed", nameof(DofPass));
        }

        if (string.IsNullOrWhiteSpace(data.InputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Input texture name must not be empty", nameof(DofPass));
        }

        if (string.IsNullOrWhiteSpace(data.DepthTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Depth texture name must not be empty", nameof(DofPass));
        }

        if (string.IsNullOrWhiteSpace(data.OutputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Depth-of-field output name must not be empty", nameof(DofPass));
        }

        if (data.FocusDistance <= 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Focus distance must be positive, got {data.FocusDistance}", nameof(DofPass));
        }

        if (data.FocusRange < 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Focus range must be non-negative, got {data.FocusRange}", nameof(DofPass));
        }

        if (data.MaxBlur < 0f || data.MaxBlur > MaxBlurPixels)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Max blur must be in [0, {MaxBlurPixels}] pixels, got {data.MaxBlur}", nameof(DofPass));
        }
    }

    private static void Execute(in DofPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();

        var gl = glContext.GL;
        var state = glContext.State;

        var inputTexture = ctx.GetTexture(data.InputTextureName);
        var depthTexture = ctx.GetTexture(data.DepthTextureName);
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
        state.BindTexture2D(0, inputTexture.Id);
        state.BindTexture2D(1, depthTexture.Id);

        int location = data.Program.GetUniformLocation("u_inputTexture");
        if (location != -1)
        {
            gl.Uniform1(location, 0);
        }

        location = data.Program.GetUniformLocation("u_depth");
        if (location != -1)
        {
            gl.Uniform1(location, 1);
        }

        location = data.Program.GetUniformLocation("u_texelSize");
        if (location != -1)
        {
            gl.Uniform2(location, 1.0f / inputTexture.Width, 1.0f / inputTexture.Height);
        }

        location = data.Program.GetUniformLocation("u_focusDistance");
        if (location != -1)
        {
            gl.Uniform1(location, data.FocusDistance);
        }

        location = data.Program.GetUniformLocation("u_focusRange");
        if (location != -1)
        {
            gl.Uniform1(location, data.FocusRange);
        }

        location = data.Program.GetUniformLocation("u_maxBlur");
        if (location != -1)
        {
            gl.Uniform1(location, data.MaxBlur);
        }

        state.BindVertexArray(0);
        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}
