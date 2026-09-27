namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Depth-of-field post-process pass. Derives a circle-of-confusion from the scene depth buffer
/// against a focus plane and blurs the input colour accordingly, using a caller-supplied
/// program that generates its own screen-filling triangle.
/// </summary>
/// <remarks>
/// <para>The GLSL program is supplied by the caller and is never compiled here. Bindings:</para>
/// <list type="bullet">
///   <item><description>The input colour texture is bound at texture unit 0.</description></item>
///   <item><description>The depth texture is bound at texture unit 1.</description></item>
/// </list>
/// <para>Optional uniforms — every one is skipped when the program does not expose it
/// (reflected location <c>-1</c>), so a minimal program is valid:</para>
/// <list type="bullet">
///   <item><description><c>u_inputTexture</c> (sampler2D) — receives the unit index 0.</description></item>
///   <item><description><c>u_depth</c> (sampler2D) — receives the unit index 1.</description></item>
///   <item><description><c>u_texelSize</c> (vec2) — <c>1.0 / inputTextureSize</c>, the blur step.</description></item>
///   <item><description><c>u_focusDistance</c> (float) — see <see cref="FocusDistance"/>.</description></item>
///   <item><description><c>u_focusRange</c> (float) — see <see cref="FocusRange"/>.</description></item>
///   <item><description><c>u_maxBlur</c> (float) — see <see cref="MaxBlur"/>, in pixels.</description></item>
/// </list>
/// <para>The depth texture must be sampleable as a regular texture (no compare mode); this
/// executor never rebinds sampler or filter state, which stays owned by texture creation.</para>
/// </remarks>
public sealed class DofPassExecutor : RenderPass
{
    private const int InvalidUniformLocation = -1;

    private const int InputTextureUnit = 0;
    private const int DepthTextureUnit = 1;

    private const string InputUniform = "u_inputTexture";
    private const string DepthUniform = "u_depth";
    private const string TexelSizeUniform = "u_texelSize";
    private const string FocusDistanceUniform = "u_focusDistance";
    private const string FocusRangeUniform = "u_focusRange";
    private const string MaxBlurUniform = "u_maxBlur";

    /// <summary>Upper bound for <see cref="MaxBlur"/>, in pixels.</summary>
    private const float MaxBlurPixels = 64f;

    private readonly GLProgram _program;
    private readonly string _inputTextureName;
    private readonly string _depthTextureName;
    private readonly string _outputTextureName;

    private float _focusDistance = 10f;
    private float _focusRange = 5f;
    private float _maxBlur = 8f;

    /// <summary>
    /// Initializes a new instance of the <see cref="DofPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="program">The caller-supplied depth-of-field program. Never compiled here.</param>
    /// <param name="inputName">Name of the input colour texture in the pass context.</param>
    /// <param name="depthName">Name of the depth texture in the pass context.</param>
    /// <param name="outputName">Name of the defocused output target in the pass context.</param>
    public DofPassExecutor(
        string name,
        GLProgram program,
        string inputName,
        string depthName,
        string outputName)
        : base(name, FramePassKind.PostProcess)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(inputName);
        ArgumentNullException.ThrowIfNull(depthName);
        ArgumentNullException.ThrowIfNull(outputName);

        _program = program;
        _inputTextureName = inputName;
        _depthTextureName = depthName;
        _outputTextureName = outputName;

        Reads(inputName);
        Reads(depthName);
        Writes(outputName);
    }

    /// <summary>Gets the focus plane distance in scene units. Must be positive.</summary>
    public float FocusDistance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _focusDistance;
    }

    /// <summary>Gets the in-focus depth band around <see cref="FocusDistance"/>. Must be non-negative.</summary>
    public float FocusRange
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _focusRange;
    }

    /// <summary>Gets the maximum circle-of-confusion radius in pixels. Must be in <c>[0, 64]</c>.</summary>
    public float MaxBlur
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _maxBlur;
    }

    /// <summary>Gets the input colour texture resource name.</summary>
    public string InputTextureName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inputTextureName;
    }

    /// <summary>Gets the depth texture resource name.</summary>
    public string DepthTextureName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _depthTextureName;
    }

    /// <summary>Gets the defocused output target name.</summary>
    public string OutputTextureName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _outputTextureName;
    }

    /// <summary>
    /// Sets the focus plane distance in scene units.
    /// </summary>
    /// <param name="focusDistance">The focus distance. Must be positive.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public DofPassExecutor WithFocusDistance(float focusDistance)
    {
        _focusDistance = focusDistance;
        return this;
    }

    /// <summary>
    /// Sets the depth band around the focus plane that stays sharp.
    /// </summary>
    /// <param name="focusRange">The focus range. Must be non-negative.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public DofPassExecutor WithFocusRange(float focusRange)
    {
        _focusRange = focusRange;
        return this;
    }

    /// <summary>
    /// Sets the maximum circle-of-confusion radius in pixels.
    /// </summary>
    /// <param name="maxBlur">The maximum blur radius. Must be in [0, 64].</param>
    /// <returns>This instance for fluent chaining.</returns>
    public DofPassExecutor WithMaxBlur(float maxBlur)
    {
        _maxBlur = maxBlur;
        return this;
    }

    /// <inheritdoc/>
    /// <summary>
    /// Gets or sets the post-process phase. HDR-space effects run before tone mapping.
    /// </summary>
    public PostProcessPhase Phase { get; set; } = PostProcessPhase.BeforeTonemap;

    public override void Validate()
    {
        if (_program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Depth-of-field shader program has been disposed", nameof(DofPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_inputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Input texture name must not be empty", nameof(DofPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_depthTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Depth texture name must not be empty", nameof(DofPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_outputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Depth-of-field output name must not be empty", nameof(DofPassExecutor));
        }

        if (_focusDistance <= 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Focus distance must be positive, got {_focusDistance}", nameof(DofPassExecutor));
        }

        if (_focusRange < 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Focus range must be non-negative, got {_focusRange}", nameof(DofPassExecutor));
        }

        if (_maxBlur < 0f || _maxBlur > MaxBlurPixels)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Max blur must be in [0, {MaxBlurPixels}] pixels, got {_maxBlur}", nameof(DofPassExecutor));
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

        // Resolve the colour input, the depth input and the render target.
        var inputTexture = ctx.GetTexture(_inputTextureName);
        var depthTexture = ctx.GetTexture(_depthTextureName);
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

        // Bind the caller-supplied depth-of-field program.
        state.UseProgram(_program.Id);

        // Bind the colour input at unit 0 and the depth input at unit 1.
        state.BindTexture2D((uint)InputTextureUnit, inputTexture.Id);
        state.BindTexture2D((uint)DepthTextureUnit, depthTexture.Id);

        int location = _program.GetUniformLocation(InputUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, InputTextureUnit);
        }

        location = _program.GetUniformLocation(DepthUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, DepthTextureUnit);
        }

        // The blur step follows the colour buffer being blurred.
        location = _program.GetUniformLocation(TexelSizeUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform2(location, 1.0f / inputTexture.Width, 1.0f / inputTexture.Height);
        }

        location = _program.GetUniformLocation(FocusDistanceUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, _focusDistance);
        }

        location = _program.GetUniformLocation(FocusRangeUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, _focusRange);
        }

        location = _program.GetUniformLocation(MaxBlurUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, _maxBlur);
        }

        // Draw the screen-filling triangle (3 vertices, no VAO needed).
        state.BindVertexArray(0);
        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}
