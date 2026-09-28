namespace Axrone.Render.Effects;

/// <summary>
/// Payload for the chromatic aberration pass.
/// </summary>
public record struct ChromaticAberrationPassData
{
    /// <summary>Chromatic aberration shader program.</summary>
    public GLProgram Program { get; set; }

    /// <summary>Input colour texture resource name.</summary>
    public string InputTextureName { get; set; }

    /// <summary>Aberrated output target name.</summary>
    public string OutputTextureName { get; set; }

    /// <summary>Maximum radial per-channel offset in texels. Must be in [0, 64].</summary>
    public float MaxOffset { get; set; }

    /// <summary>Post-process phase. Display-referred effects run after tone mapping.</summary>
    public PostProcessPhase Phase { get; set; }
}

/// <summary>
/// Factory for the chromatic aberration post-process pass.
/// </summary>
public static class ChromaticAberrationPass
{
    private const float MinMaxOffsetTexels = 0f;
    private const float MaxMaxOffsetTexels = 64f;

    /// <summary>Creates a chromatic aberration pass.</summary>
    public static RenderPass<ChromaticAberrationPassData> Create(
        string name,
        GLProgram program,
        string inputName,
        string outputName,
        float maxOffset = 4f)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(inputName);
        ArgumentNullException.ThrowIfNull(outputName);
        return new RenderPass<ChromaticAberrationPassData>(
            name,
            FramePassKind.PostProcess,
            (IRenderPassBuilder builder, ref ChromaticAberrationPassData data) =>
            {
                data.Program = program;
                data.InputTextureName = inputName;
                data.OutputTextureName = outputName;
                data.MaxOffset = maxOffset;
                data.Phase = PostProcessPhase.AfterTonemap;
                builder.Reads(inputName);
                builder.Writes(outputName);
            },
            Execute,
            Validate);
    }

    private static void Validate(in ChromaticAberrationPassData data)
    {
        if (data.Program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Chromatic aberration shader program has been disposed", nameof(ChromaticAberrationPass));
        }

        if (string.IsNullOrWhiteSpace(data.InputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Input texture name must not be empty", nameof(ChromaticAberrationPass));
        }

        if (string.IsNullOrWhiteSpace(data.OutputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Chromatic aberration output name must not be empty", nameof(ChromaticAberrationPass));
        }

        if (data.MaxOffset < MinMaxOffsetTexels || data.MaxOffset > MaxMaxOffsetTexels)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Chromatic aberration max offset must be in [0, 64] texels, got {data.MaxOffset}", nameof(ChromaticAberrationPass));
        }
    }

    private static void Execute(in ChromaticAberrationPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();

        var gl = glContext.GL;
        var state = glContext.State;

        var inputTexture = ctx.GetTexture(data.InputTextureName);
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

        int location = data.Program.GetUniformLocation("u_inputTexture");
        if (location != -1)
        {
            gl.Uniform1(location, 0);
        }

        location = data.Program.GetUniformLocation("u_maxOffset");
        if (location != -1)
        {
            gl.Uniform1(location, data.MaxOffset);
        }

        state.BindVertexArray(0);
        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}
