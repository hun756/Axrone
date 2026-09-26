namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Tone mapping operators for HDR to LDR conversion.
/// </summary>
public enum ToneMapOperator
{
    /// <summary>Classic Reinhard operator: <c>color / (1 + color)</c>.</summary>
    Reinhard = 0,

    /// <summary>ACES (Academy Color Encoding System) filmic curve.</summary>
    Aces = 1,

    /// <summary>Uncharted 2 filmic tone mapping by John Hable.</summary>
    Uncharted2 = 2,

    /// <summary>Simple exposure-only scaling: <c>color * exposure</c>.</summary>
    ExposureOnly = 3,
}

/// <summary>
/// HDR to LDR tone mapping pass. Converts a high dynamic range scene texture to a
/// displayable low dynamic range output using a configurable tone mapping operator,
/// exposure, and gamma correction.
/// </summary>
/// <remarks>
/// <para>The shader is expected to expose the following uniforms:</para>
/// <list type="bullet">
///   <item><description><c>u_texture</c> (sampler2D) — the input HDR texture bound at unit 0.</description></item>
///   <item><description><c>u_exposure</c> (float) — exposure multiplier.</description></item>
///   <item><description><c>u_gamma</c> (float) — gamma correction exponent.</description></item>
///   <item><description><c>u_operator</c> (int) — the <see cref="ToneMapOperator"/> enum value.</description></item>
/// </list>
/// </remarks>
public sealed class ToneMapPassExecutor : RenderPass
{
    private readonly GLProgram _shader;
    private readonly string _inputTextureName;
    private readonly string _outputTextureName;

    private ToneMapOperator _operator = ToneMapOperator.Aces;
    private float _exposure = 1.0f;
    private float _gamma = 2.2f;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToneMapPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="shader">The tone mapping shader program.</param>
    /// <param name="inputTextureName">Name of the input HDR texture in the pass context.</param>
    /// <param name="outputTextureName">Name of the output LDR texture in the pass context.</param>
    public ToneMapPassExecutor(
        string name,
        GLProgram shader,
        string inputTextureName = "scene_hdr",
        string outputTextureName = "scene_ldr")
        : base(name, FramePassKind.ToneMap)
    {
        ArgumentNullException.ThrowIfNull(shader);

        _shader = shader;
        _inputTextureName = inputTextureName;
        _outputTextureName = outputTextureName;

        Reads(inputTextureName);
        Writes(outputTextureName);
    }

    /// <summary>Gets the active tone mapping operator.</summary>
    public ToneMapOperator Operator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _operator;
    }

    /// <summary>Gets the exposure multiplier.</summary>
    public float Exposure
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _exposure;
    }

    /// <summary>Gets the gamma correction exponent.</summary>
    public float Gamma
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _gamma;
    }

    /// <summary>
    /// Sets the tone mapping operator.
    /// </summary>
    /// <param name="op">The operator to use.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ToneMapPassExecutor WithOperator(ToneMapOperator op)
    {
        _operator = op;
        return this;
    }

    /// <summary>
    /// Sets the exposure multiplier applied to the HDR color before tone mapping.
    /// </summary>
    /// <param name="exposure">The exposure value. Must be positive.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ToneMapPassExecutor WithExposure(float exposure)
    {
        _exposure = exposure;
        return this;
    }

    /// <summary>
    /// Sets the gamma correction exponent applied after tone mapping.
    /// </summary>
    /// <param name="gamma">The gamma value. Must be positive.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ToneMapPassExecutor WithGamma(float gamma)
    {
        _gamma = gamma;
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (_exposure <= 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Exposure must be positive, got {_exposure}", nameof(ToneMapPassExecutor));
        }

        if (_gamma <= 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Gamma must be positive, got {_gamma}", nameof(ToneMapPassExecutor));
        }
    }

    /// <inheritdoc/>
    public override void Execute(GLContext context, PassExecutionContext ctx)
    {
        context.AssertRenderThread();

        var inputTexture = ctx.GetTexture(_inputTextureName);
        var outputTexture = ctx.GetTexture(_outputTextureName);

        // Bind output framebuffer.
        if (ctx.HasResource(_outputTextureName + "_fbo"))
        {
            context.State.BindFramebuffer(GLConst.Framebuffer, ctx.GetFramebuffer(_outputTextureName + "_fbo").Id);
        }

        context.State.SetViewport(0, 0, outputTexture.Width, outputTexture.Height);
        context.State.SetDepthTest(false);
        context.State.SetCullFace(false);

        // Bind tone mapping shader.
        context.State.UseProgram(_shader.Id);

        // Bind input HDR texture at unit 0.
        context.State.BindTexture2D(0, inputTexture.Id);

        // Set uniforms.
        var gl = context.GL;
        gl.Uniform1(_shader.GetUniformLocation("u_exposure"), _exposure);
        gl.Uniform1(_shader.GetUniformLocation("u_gamma"), _gamma);
        gl.Uniform1(_shader.GetUniformLocation("u_operator"), (int)_operator);

        // Draw fullscreen triangle.
        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}
