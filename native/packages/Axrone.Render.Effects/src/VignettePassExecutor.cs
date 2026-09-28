namespace Axrone.Render.Effects;

/// <summary>
/// Vignette post-process pass. Darkens the edges of the input colour image,
/// drawing the eye toward the centre, using a caller-supplied program that
/// generates its own screen-filling triangle.
/// </summary>
/// <remarks>
/// <para>The GLSL program is supplied by the caller and is never compiled here. Bindings:</para>
/// <list type="bullet">
///   <item><description>The input colour texture is bound at texture unit 0.</description></item>
/// </list>
/// <para>Optional uniforms — every one is skipped when the program does not expose it
/// (reflected location <c>-1</c>), so a minimal program is valid:</para>
/// <list type="bullet">
///   <item><description><c>u_inputTexture</c> (sampler2D) — receives the unit index 0.</description></item>
///   <item><description><c>u_intensity</c> (float) — see <see cref="Intensity"/>.</description></item>
///   <item><description><c>u_smoothness</c> (float) — see <see cref="Smoothness"/>.</description></item>
/// </list>
/// <para><see cref="Intensity"/> and <see cref="Smoothness"/> are mutable and are expected
/// to be updated per frame before the pass executes.</para>
/// </remarks>
public sealed class VignettePassExecutor : RenderPass, IRenderPass
{
    private const int InvalidUniformLocation = -1;

    private const int InputTextureUnit = 0;

    private const string InputUniform = "u_inputTexture";
    private const string IntensityUniform = "u_intensity";
    private const string SmoothnessUniform = "u_smoothness";

    private readonly GLProgram _program;
    private readonly string _inputTextureName;
    private readonly string _outputTextureName;

    /// <summary>
    /// Initializes a new instance of the <see cref="VignettePassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="program">The caller-supplied vignette program. Never compiled here.</param>
    /// <param name="inputName">Name of the input colour texture in the pass context.</param>
    /// <param name="outputName">Name of the vignetted output target in the pass context.</param>
    public VignettePassExecutor(
        string name,
        GLProgram program,
        string inputName,
        string outputName)
        : base(name, FramePassKind.PostProcess)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(inputName);
        ArgumentNullException.ThrowIfNull(outputName);

        _program = program;
        _inputTextureName = inputName;
        _outputTextureName = outputName;

        Reads(inputName);
        Writes(outputName);
    }

    /// <summary>
    /// Gets or sets the vignette strength. Must be in [0, 1]. Update per frame if animated.
    /// </summary>
    public float Intensity { get; set; } = 0.4f;

    /// <summary>
    /// Gets or sets the vignette falloff softness. Must be in [0, 1]. Update per frame if animated.
    /// </summary>
    public float Smoothness { get; set; } = 0.4f;

    /// <summary>Gets the input colour texture resource name.</summary>
    public string InputTextureName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inputTextureName;
    }

    /// <summary>Gets the vignetted output target name.</summary>
    public string OutputTextureName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _outputTextureName;
    }

    /// <summary>
    /// Sets the vignette strength.
    /// </summary>
    /// <param name="intensity">The intensity. Must be in [0, 1].</param>
    /// <returns>This instance for fluent chaining.</returns>
    public VignettePassExecutor WithIntensity(float intensity)
    {
        Intensity = intensity;
        return this;
    }

    /// <summary>
    /// Sets the vignette falloff softness.
    /// </summary>
    /// <param name="smoothness">The smoothness. Must be in [0, 1].</param>
    /// <returns>This instance for fluent chaining.</returns>
    public VignettePassExecutor WithSmoothness(float smoothness)
    {
        Smoothness = smoothness;
        return this;
    }

    /// <inheritdoc/>
    /// <summary>
    /// Gets or sets the post-process phase. Display-referred effects run after tone mapping.
    /// </summary>
    public PostProcessPhase Phase { get; set; } = PostProcessPhase.AfterTonemap;

    public override void Validate()
    {
        if (_program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Vignette shader program has been disposed", nameof(VignettePassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_inputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Input texture name must not be empty", nameof(VignettePassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_outputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Vignette output name must not be empty", nameof(VignettePassExecutor));
        }

        if (Intensity < 0f || Intensity > 1f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Vignette intensity must be in [0, 1], got {Intensity}", nameof(VignettePassExecutor));
        }

        if (Smoothness < 0f || Smoothness > 1f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Vignette smoothness must be in [0, 1], got {Smoothness}", nameof(VignettePassExecutor));
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This pass is <see cref="IRenderContext"/>-native: it renders through a
    /// descriptor-driven pass lifecycle (<c>BeginPass</c>/<c>EndPass</c>) and issues
    /// only hardware-agnostic verbs, so the legacy GLContext bridge is left
    /// unimplemented and fails closed.
    /// </remarks>
    void IRenderPass.Execute(IRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context is GLRenderContext glCtx && glCtx.PassContext is not null)
        {
            ExecuteCore(glCtx, glCtx.PassContext);
            return;
        }

        ThrowHelper.Throw(RenderErrorCode.InvalidOperation,
            $"{Name} resolves resources through the pass context and requires a GLRenderContext with a configured PassContext.",
            nameof(VignettePassExecutor));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void ExecuteCore(GLRenderContext context, PassExecutionContext ctx)
    {
        // Resolve the colour input and the render target.
        var inputTexture = ctx.GetTexture(_inputTextureName);
        var outputTexture = ctx.GetTexture(_outputTextureName);

        // Target the registered "<output>_fbo" when present, else the backbuffer.
        uint targetFramebuffer = ctx.HasResource(_outputTextureName + "_fbo")
            ? ctx.GetFramebuffer(_outputTextureName + "_fbo").Id
            : 0u;

        Span<AttachmentDescriptor> attachments = stackalloc AttachmentDescriptor[1]
        {
            AttachmentDescriptor.Color(0, AttachmentLoadAction.Load, AttachmentStoreAction.Store)
        };

        var descriptor = new RenderPassDescriptor(
            targetFramebuffer,
            new ViewportRect(0, 0, outputTexture.Width, outputTexture.Height),
            attachments);

        context.BeginPass(in descriptor);

        context.SetDepthState(testEnabled: false, writeEnabled: false);
        context.SetBlendState(enabled: false);
        context.SetCullState(enabled: false);
        context.SetColorMask(red: true, green: true, blue: true, alpha: true);

        // Bind the caller-supplied vignette program.
        context.BindProgram(_program.Id);

        // Bind the colour input at unit 0.
        context.BindTexture(InputTextureUnit, inputTexture.Id);

        int location = _program.GetUniformLocation(InputUniform);
        if (location != InvalidUniformLocation)
        {
            context.SetUniform(location, InputTextureUnit);
        }

        location = _program.GetUniformLocation(IntensityUniform);
        if (location != InvalidUniformLocation)
        {
            context.SetUniform(location, Intensity);
        }

        location = _program.GetUniformLocation(SmoothnessUniform);
        if (location != InvalidUniformLocation)
        {
            context.SetUniform(location, Smoothness);
        }

        // Draw the screen-filling triangle (3 vertices, no VAO needed).
        context.BindVertexArray(0);
        context.DrawFullscreenQuad();

        context.EndPass();
    }
}
