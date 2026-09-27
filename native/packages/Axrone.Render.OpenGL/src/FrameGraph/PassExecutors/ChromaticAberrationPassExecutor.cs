namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Chromatic aberration post-process pass. Samples the input colour texture with
/// a small radial per-channel offset to mimic lens dispersion, using a
/// caller-supplied program that generates its own screen-filling triangle.
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
///   <item><description><c>u_maxOffset</c> (float) — see <see cref="MaxOffset"/>.</description></item>
/// </list>
/// <para><see cref="MaxOffset"/> is expressed in texels at the input resolution; the
/// caller's shader is responsible for converting it to UV space (for example via its
/// own <c>u_texelSize</c> uniform or a hardcoded resolution). It is mutable and is
/// expected to be updated per frame before <see cref="RenderPass.Execute"/> runs.</para>
/// </remarks>
public sealed class ChromaticAberrationPassExecutor : RenderPass
{
    private const int InvalidUniformLocation = -1;

    private const int InputTextureUnit = 0;

    private const string InputUniform = "u_inputTexture";
    private const string MaxOffsetUniform = "u_maxOffset";

    // Sane bound for the radial per-channel offset, in texels: 64 texels at 1080p is
    // roughly 3% of the screen width — already an extreme, heavily distorted grade.
    private const float MinMaxOffsetTexels = 0f;
    private const float MaxMaxOffsetTexels = 64f;

    private readonly GLProgram _program;
    private readonly string _inputTextureName;
    private readonly string _outputTextureName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChromaticAberrationPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="program">The caller-supplied chromatic aberration program. Never compiled here.</param>
    /// <param name="inputName">Name of the input colour texture in the pass context.</param>
    /// <param name="outputName">Name of the aberrated output target in the pass context.</param>
    public ChromaticAberrationPassExecutor(
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
    /// Gets or sets the maximum radial per-channel offset, in texels at the input
    /// resolution. Must be in [0, 64]. Update per frame if animated.
    /// </summary>
    public float MaxOffset { get; set; } = 4f;

    /// <summary>Gets the input colour texture resource name.</summary>
    public string InputTextureName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inputTextureName;
    }

    /// <summary>Gets the aberrated output target name.</summary>
    public string OutputTextureName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _outputTextureName;
    }

    /// <summary>
    /// Sets the maximum radial per-channel offset, in texels.
    /// </summary>
    /// <param name="maxOffset">The offset in texels. Must be in [0, 64].</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ChromaticAberrationPassExecutor WithMaxOffset(float maxOffset)
    {
        MaxOffset = maxOffset;
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (_program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Chromatic aberration shader program has been disposed", nameof(ChromaticAberrationPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_inputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Input texture name must not be empty", nameof(ChromaticAberrationPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_outputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Chromatic aberration output name must not be empty", nameof(ChromaticAberrationPassExecutor));
        }

        if (MaxOffset < MinMaxOffsetTexels || MaxOffset > MaxMaxOffsetTexels)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Chromatic aberration max offset must be in [0, 64] texels, got {MaxOffset}", nameof(ChromaticAberrationPassExecutor));
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

        // Bind the caller-supplied chromatic aberration program.
        state.UseProgram(_program.Id);

        // Bind the colour input at unit 0.
        state.BindTexture2D((uint)InputTextureUnit, inputTexture.Id);

        int location = _program.GetUniformLocation(InputUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, InputTextureUnit);
        }

        location = _program.GetUniformLocation(MaxOffsetUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, MaxOffset);
        }

        // Draw the screen-filling triangle (3 vertices, no VAO needed).
        state.BindVertexArray(0);
        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}
