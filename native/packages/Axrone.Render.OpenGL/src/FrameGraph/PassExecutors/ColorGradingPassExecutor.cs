namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Colour grading post-process pass. Applies photographic contrast, saturation, and
/// brightness adjustments to the input colour image using a caller-supplied program
/// that generates its own screen-filling triangle.
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
///   <item><description><c>u_contrast</c> (float) — see <see cref="Contrast"/>.</description></item>
///   <item><description><c>u_saturation</c> (float) — see <see cref="Saturation"/>.</description></item>
///   <item><description><c>u_brightness</c> (float) — see <see cref="Brightness"/>.</description></item>
/// </list>
/// <para>All three parameters are multiplicative factors centred on 1.0 (neutral):
/// 0 removes the attribute entirely (flat contrast / grayscale / black), 1 is the
/// unmodified image, and 2 is an aggressive grade. They are mutable and are expected
/// to be updated per frame before <see cref="RenderPass.Execute"/> runs.</para>
/// </remarks>
public sealed class ColorGradingPassExecutor : RenderPass
{
    private const int InvalidUniformLocation = -1;

    private const int InputTextureUnit = 0;

    private const string InputUniform = "u_inputTexture";
    private const string ContrastUniform = "u_contrast";
    private const string SaturationUniform = "u_saturation";
    private const string BrightnessUniform = "u_brightness";

    // Photographic grading range: 0 removes the attribute entirely, 1 is neutral,
    // 2 is an aggressive but still displayable grade.
    private const float MinGradingFactor = 0f;
    private const float MaxGradingFactor = 2f;

    private readonly GLProgram _program;
    private readonly string _inputTextureName;
    private readonly string _outputTextureName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorGradingPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="program">The caller-supplied colour grading program. Never compiled here.</param>
    /// <param name="inputName">Name of the input colour texture in the pass context.</param>
    /// <param name="outputName">Name of the graded output target in the pass context.</param>
    public ColorGradingPassExecutor(
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
    /// Gets or sets the contrast factor. Must be in [0, 2]. Update per frame if animated.
    /// </summary>
    public float Contrast { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the saturation factor. Must be in [0, 2]. Update per frame if animated.
    /// </summary>
    public float Saturation { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the brightness factor. Must be in [0, 2]. Update per frame if animated.
    /// </summary>
    public float Brightness { get; set; } = 1f;

    /// <summary>Gets the input colour texture resource name.</summary>
    public string InputTextureName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inputTextureName;
    }

    /// <summary>Gets the graded output target name.</summary>
    public string OutputTextureName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _outputTextureName;
    }

    /// <summary>
    /// Sets the contrast factor.
    /// </summary>
    /// <param name="contrast">The contrast factor. Must be in [0, 2].</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ColorGradingPassExecutor WithContrast(float contrast)
    {
        Contrast = contrast;
        return this;
    }

    /// <summary>
    /// Sets the saturation factor.
    /// </summary>
    /// <param name="saturation">The saturation factor. Must be in [0, 2].</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ColorGradingPassExecutor WithSaturation(float saturation)
    {
        Saturation = saturation;
        return this;
    }

    /// <summary>
    /// Sets the brightness factor.
    /// </summary>
    /// <param name="brightness">The brightness factor. Must be in [0, 2].</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ColorGradingPassExecutor WithBrightness(float brightness)
    {
        Brightness = brightness;
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (_program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Colour grading shader program has been disposed", nameof(ColorGradingPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_inputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Input texture name must not be empty", nameof(ColorGradingPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_outputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Colour grading output name must not be empty", nameof(ColorGradingPassExecutor));
        }

        if (Contrast < MinGradingFactor || Contrast > MaxGradingFactor)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Colour grading contrast must be in [0, 2], got {Contrast}", nameof(ColorGradingPassExecutor));
        }

        if (Saturation < MinGradingFactor || Saturation > MaxGradingFactor)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Colour grading saturation must be in [0, 2], got {Saturation}", nameof(ColorGradingPassExecutor));
        }

        if (Brightness < MinGradingFactor || Brightness > MaxGradingFactor)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Colour grading brightness must be in [0, 2], got {Brightness}", nameof(ColorGradingPassExecutor));
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override void Execute(GLContext context, PassExecutionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(ctx);

        context.AssertRenderThread();

        var gl = context.GL;
        var state = context.State;

        // Resolve the colour input and the render target.
        var inputTexture = ctx.GetTexture(_inputTextureName);
        var outputTexture = ctx.GetTexture(_outputTextureName);

        // Bind the output framebuffer when the caller registered "<output>_fbo".
        if (ctx.HasResource(_outputTextureName + "_fbo"))
        {
            state.BindFramebuffer(GLConst.Framebuffer, ctx.GetFramebuffer(_outputTextureName + "_fbo").Id);
        }

        state.SetViewport(0, 0, outputTexture.Width, outputTexture.Height);
        state.SetDepthTest(false);
        state.SetBlend(false);
        state.SetCullFace(false);
        state.SetColorMask(true, true, true, true);

        // Bind the caller-supplied colour grading program.
        state.UseProgram(_program.Id);

        // Bind the colour input at unit 0.
        state.BindTexture2D((uint)InputTextureUnit, inputTexture.Id);

        int location = _program.GetUniformLocation(InputUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, InputTextureUnit);
        }

        location = _program.GetUniformLocation(ContrastUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, Contrast);
        }

        location = _program.GetUniformLocation(SaturationUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, Saturation);
        }

        location = _program.GetUniformLocation(BrightnessUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, Brightness);
        }

        // Draw the screen-filling triangle (3 vertices, no VAO needed).
        state.BindVertexArray(0);
        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}
