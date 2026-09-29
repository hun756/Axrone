namespace Axrone.Render.Effects;

/// <summary>
/// Payload for the vignette pass, owning the pass's setup, validate and execute
/// phases.
/// </summary>
public sealed class VignettePassData
    : IPassSetup<VignettePassData>, IPassValidate<VignettePassData>, IPassExecute<VignettePassData>
{
    /// <summary>Vignette shader program. Assigned by the factory before the pass is constructed.</summary>
    public GLProgram Program { get; set; } = null!;

    /// <summary>Input colour texture resource name.</summary>
    public string InputTextureName { get; set; } = string.Empty;

    /// <summary>Vignetted output target name.</summary>
    public string OutputTextureName { get; set; } = string.Empty;

    /// <summary>Vignette strength. Must be in [0, 1]. Update per frame if animated.</summary>
    public float Intensity { get; set; }

    /// <summary>Vignette falloff softness. Must be in [0, 1]. Update per frame if animated.</summary>
    public float Smoothness { get; set; }

    /// <summary>Post-process phase. Display-referred effects run after tone mapping.</summary>
    public PostProcessPhase Phase { get; set; }

    /// <inheritdoc/>
    public static void Declare(IRenderPassBuilder builder, ref VignettePassData data)
    {
        builder.Reads(data.InputTextureName);
        builder.Writes(data.OutputTextureName);
    }

    /// <inheritdoc/>
    public static void Validate(in VignettePassData data)
    {
        if (data.Program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Vignette shader program has been disposed", nameof(VignettePass));
        }

        if (string.IsNullOrWhiteSpace(data.InputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Input texture name must not be empty", nameof(VignettePass));
        }

        if (string.IsNullOrWhiteSpace(data.OutputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Vignette output name must not be empty", nameof(VignettePass));
        }

        if (data.Intensity < 0f || data.Intensity > 1f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Vignette intensity must be in [0, 1], got {data.Intensity}", nameof(VignettePass));
        }

        if (data.Smoothness < 0f || data.Smoothness > 1f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Vignette smoothness must be in [0, 1], got {data.Smoothness}", nameof(VignettePass));
        }
    }

    /// <inheritdoc/>
    public static void Execute(in VignettePassData data, IRenderContext context, PassExecutionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context is not GLRenderContext glCtx || glCtx.PassContext is null)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation,
                $"Vignette pass '{data.InputTextureName}' requires a GLRenderContext with a configured PassContext.",
                nameof(VignettePass));
            return;
        }

        var inputTexture = ctx.GetTexture(data.InputTextureName);
        var outputTexture = ctx.GetTexture(data.OutputTextureName);

        uint targetFramebuffer = ctx.HasResource(data.OutputTextureName + "_fbo")
            ? ctx.GetFramebuffer(data.OutputTextureName + "_fbo").Id
            : 0u;

        Span<AttachmentDescriptor> attachments = stackalloc AttachmentDescriptor[1]
        {
            AttachmentDescriptor.Color(0, AttachmentLoadAction.Load, AttachmentStoreAction.Store)
        };

        var descriptor = new RenderPassDescriptor(
            targetFramebuffer,
            new ViewportRect(0, 0, outputTexture.Width, outputTexture.Height),
            attachments);

        glCtx.BeginPass(in descriptor);

        glCtx.SetDepthState(testEnabled: false, writeEnabled: false);
        glCtx.SetBlendState(enabled: false);
        glCtx.SetCullState(enabled: false);
        glCtx.SetColorMask(red: true, green: true, blue: true, alpha: true);

        glCtx.BindProgram(data.Program.Id);
        glCtx.BindTexture(0, inputTexture.Id);

        int location = data.Program.GetUniformLocation("u_inputTexture");
        if (location != -1)
        {
            glCtx.SetUniform(location, 0);
        }

        location = data.Program.GetUniformLocation("u_intensity");
        if (location != -1)
        {
            glCtx.SetUniform(location, data.Intensity);
        }

        location = data.Program.GetUniformLocation("u_smoothness");
        if (location != -1)
        {
            glCtx.SetUniform(location, data.Smoothness);
        }

        glCtx.BindVertexArray(0);
        glCtx.DrawFullscreenQuad();

        glCtx.EndPass();
    }
}

/// <summary>
/// Factory for the vignette post-process pass.
/// Renders through a descriptor-driven pass lifecycle (<c>BeginPass</c>/<c>EndPass</c>)
/// and issues only hardware-agnostic verbs.
/// </summary>
public static class VignettePass
{
    /// <summary>Creates a vignette pass.</summary>
    public static RenderPass<VignettePassData> Create(
        string name,
        GLProgram program,
        string inputName,
        string outputName,
        float intensity = 0.4f,
        float smoothness = 0.4f)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(inputName);
        ArgumentNullException.ThrowIfNull(outputName);
        return new RenderPass<VignettePassData>(
            name,
            FramePassKind.PostProcess,
            new VignettePassData
            {
                Program = program,
                InputTextureName = inputName,
                OutputTextureName = outputName,
                Intensity = intensity,
                Smoothness = smoothness,
                Phase = PostProcessPhase.AfterTonemap
            });
    }
}
