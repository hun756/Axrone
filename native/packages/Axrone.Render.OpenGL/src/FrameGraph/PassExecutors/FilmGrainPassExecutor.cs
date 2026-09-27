namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Film grain post-process pass. Adds animated noise to the input colour image, breaking up
/// flat gradients and banding with a caller-supplied program that generates its own
/// screen-filling triangle.
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
///   <item><description><c>u_time</c> (float) — see <see cref="Time"/>, the animation seed.</description></item>
/// </list>
/// <para><see cref="Intensity"/> and <see cref="Time"/> are mutable and are expected to be
/// updated per frame before <see cref="RenderPass.Execute"/> runs.</para>
/// </remarks>
public sealed class FilmGrainPassExecutor : RenderPass
{
    private const int InvalidUniformLocation = -1;

    private const int InputTextureUnit = 0;

    private const string InputUniform = "u_inputTexture";
    private const string IntensityUniform = "u_intensity";
    private const string TimeUniform = "u_time";

    private readonly GLProgram _program;
    private readonly string _inputTextureName;
    private readonly string _outputTextureName;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilmGrainPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="program">The caller-supplied film grain program. Never compiled here.</param>
    /// <param name="inputName">Name of the input colour texture in the pass context.</param>
    /// <param name="outputName">Name of the grained output target in the pass context.</param>
    public FilmGrainPassExecutor(
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
    /// Gets or sets the grain strength. Must be in [0, 1]. Update per frame if animated.
    /// </summary>
    public float Intensity { get; set; } = 0.04f;

    /// <summary>
    /// Gets or sets the grain animation seed in seconds. Must be non-negative.
    /// Update per frame to animate the noise pattern.
    /// </summary>
    public float Time { get; set; }

    /// <summary>Gets the input colour texture resource name.</summary>
    public string InputTextureName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inputTextureName;
    }

    /// <summary>Gets the grained output target name.</summary>
    public string OutputTextureName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _outputTextureName;
    }

    /// <summary>
    /// Sets the grain strength.
    /// </summary>
    /// <param name="intensity">The intensity. Must be in [0, 1].</param>
    /// <returns>This instance for fluent chaining.</returns>
    public FilmGrainPassExecutor WithIntensity(float intensity)
    {
        Intensity = intensity;
        return this;
    }

    /// <summary>
    /// Sets the grain animation seed.
    /// </summary>
    /// <param name="time">The seed in seconds. Must be non-negative.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public FilmGrainPassExecutor WithTime(float time)
    {
        Time = time;
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (_program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Film grain shader program has been disposed", nameof(FilmGrainPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_inputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Input texture name must not be empty", nameof(FilmGrainPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_outputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Film grain output name must not be empty", nameof(FilmGrainPassExecutor));
        }

        if (Intensity < 0f || Intensity > 1f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Film grain intensity must be in [0, 1], got {Intensity}", nameof(FilmGrainPassExecutor));
        }

        if (Time < 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Film grain time must be non-negative, got {Time}", nameof(FilmGrainPassExecutor));
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

        // Bind the caller-supplied film grain program.
        state.UseProgram(_program.Id);

        // Bind the colour input at unit 0.
        state.BindTexture2D((uint)InputTextureUnit, inputTexture.Id);

        int location = _program.GetUniformLocation(InputUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, InputTextureUnit);
        }

        location = _program.GetUniformLocation(IntensityUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, Intensity);
        }

        location = _program.GetUniformLocation(TimeUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, Time);
        }

        // Draw the screen-filling triangle (3 vertices, no VAO needed).
        state.BindVertexArray(0);
        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}
