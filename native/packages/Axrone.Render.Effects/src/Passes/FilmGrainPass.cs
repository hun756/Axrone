namespace Axrone.Render.Effects;

/// <summary>
/// Payload for the film grain pass, owning the pass's setup, validate and execute
/// phases.
/// </summary>
public sealed class FilmGrainPassData
    : IPassSetup<FilmGrainPassData>, IPassValidate<FilmGrainPassData>, IPassExecute<FilmGrainPassData>
{
    /// <summary>Film grain shader program. Assigned by the factory before the pass is constructed.</summary>
    public GLProgram Program { get; set; } = null!;

    /// <summary>Input colour texture resource name.</summary>
    public string InputTextureName { get; set; } = string.Empty;

    /// <summary>Grained output target name.</summary>
    public string OutputTextureName { get; set; } = string.Empty;

    /// <summary>Grain strength. Must be in [0, 1]. Update per frame if animated.</summary>
    public float Intensity { get; set; }

    /// <summary>Grain animation seed in seconds. Must be non-negative.</summary>
    public float Time { get; set; }

    /// <summary>Post-process phase. Display-referred effects run after tone mapping.</summary>
    public PostProcessPhase Phase { get; set; }

    /// <inheritdoc/>
    public static void Declare(IRenderPassBuilder builder, ref FilmGrainPassData data)
    {
        builder.Reads(data.InputTextureName);
        builder.Writes(data.OutputTextureName);
    }

    /// <inheritdoc/>
    public static void Validate(in FilmGrainPassData data)
    {
        if (data.Program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Film grain shader program has been disposed", nameof(FilmGrainPass));
        }

        if (string.IsNullOrWhiteSpace(data.InputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Input texture name must not be empty", nameof(FilmGrainPass));
        }

        if (string.IsNullOrWhiteSpace(data.OutputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Film grain output name must not be empty", nameof(FilmGrainPass));
        }

        if (data.Intensity < 0f || data.Intensity > 1f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Film grain intensity must be in [0, 1], got {data.Intensity}", nameof(FilmGrainPass));
        }

        if (data.Time < 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Film grain time must be non-negative, got {data.Time}", nameof(FilmGrainPass));
        }
    }

    /// <inheritdoc/>
    public static void Execute(in FilmGrainPassData data, IRenderContext context, PassExecutionContext ctx)
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

        location = data.Program.GetUniformLocation("u_intensity");
        if (location != -1)
        {
            gl.Uniform1(location, data.Intensity);
        }

        location = data.Program.GetUniformLocation("u_time");
        if (location != -1)
        {
            gl.Uniform1(location, data.Time);
        }

        state.BindVertexArray(0);
        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}

/// <summary>
/// Factory for the film grain post-process pass.
/// </summary>
public static class FilmGrainPass
{
    /// <summary>Creates a film grain pass.</summary>
    public static RenderPass<FilmGrainPassData> Create(
        string name,
        GLProgram program,
        string inputName,
        string outputName,
        float intensity = 0.04f,
        float time = 0f)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(inputName);
        ArgumentNullException.ThrowIfNull(outputName);
        return new RenderPass<FilmGrainPassData>(
            name,
            FramePassKind.PostProcess,
            new FilmGrainPassData
            {
                Program = program,
                InputTextureName = inputName,
                OutputTextureName = outputName,
                Intensity = intensity,
                Time = time,
                Phase = PostProcessPhase.AfterTonemap
            });
    }
}
