namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Screen-space ambient occlusion (SSAO) post-process pass. Samples the depth and view-space
/// normal buffers around each pixel and darkens ambient occlusion estimated from the local
/// depth neighbourhood, using a caller-supplied program that generates its own
/// screen-filling triangle.
/// </summary>
/// <remarks>
/// <para>The GLSL program is supplied by the caller and is never compiled here. Bindings:</para>
/// <list type="bullet">
///   <item><description>The depth texture is bound at texture unit 0.</description></item>
///   <item><description>The normal texture is bound at texture unit 1.</description></item>
/// </list>
/// <para>Optional uniforms — every one is skipped when the program does not expose it
/// (reflected location <c>-1</c>), so a minimal program is valid:</para>
/// <list type="bullet">
///   <item><description>The depth uniform (<c>u_depth</c> by default, sampler2D) — receives the unit index 0.</description></item>
///   <item><description>The normal uniform (<c>u_normal</c> by default, sampler2D) — receives the unit index 1.</description></item>
///   <item><description><c>u_texelSize</c> (vec2) — <c>1.0 / depthTextureSize</c>, the SSAO sampling step.</description></item>
///   <item><description><c>u_radius</c> (float) — see <see cref="Radius"/>.</description></item>
///   <item><description><c>u_bias</c> (float) — see <see cref="Bias"/>.</description></item>
///   <item><description><c>u_intensity</c> (float) — see <see cref="Intensity"/>.</description></item>
/// </list>
/// <para>The depth texture must be sampleable as a regular texture (no compare mode); this
/// executor never rebinds sampler or filter state, which stays owned by texture creation.</para>
/// </remarks>
public sealed class SsaoPassExecutor : RenderPass
{
    private const int InvalidUniformLocation = -1;

    private const int DepthTextureUnit = 0;
    private const int NormalTextureUnit = 1;

    private const string TexelSizeUniform = "u_texelSize";
    private const string RadiusUniform = "u_radius";
    private const string BiasUniform = "u_bias";
    private const string IntensityUniform = "u_intensity";

    /// <summary>Upper bound for <see cref="Radius"/>, in scene units.</summary>
    private const float MaxRadius = 64f;

    /// <summary>Upper bound for <see cref="Bias"/>, in scene units.</summary>
    private const float MaxBias = 1f;

    private readonly GLProgram _program;
    private readonly string _depthTextureName;
    private readonly string _normalTextureName;
    private readonly string _outputTextureName;
    private readonly string _depthUniform;
    private readonly string _normalUniform;

    private float _radius = 0.5f;
    private float _bias = 0.02f;
    private float _intensity = 1.0f;

    /// <summary>
    /// Initializes a new instance of the <see cref="SsaoPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="program">The caller-supplied SSAO program. Never compiled here.</param>
    /// <param name="depthTextureName">Name of the depth texture in the pass context.</param>
    /// <param name="normalTextureName">Name of the view-space normal texture in the pass context.</param>
    /// <param name="outputName">Name of the occlusion output target in the pass context.</param>
    /// <param name="depthUniform">The depth sampler uniform name in the program.</param>
    /// <param name="normalUniform">The normal sampler uniform name in the program.</param>
    public SsaoPassExecutor(
        string name,
        GLProgram program,
        string depthTextureName,
        string normalTextureName,
        string outputName,
        string depthUniform = "u_depth",
        string normalUniform = "u_normal")
        : base(name, FramePassKind.PostProcess)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(depthTextureName);
        ArgumentNullException.ThrowIfNull(normalTextureName);
        ArgumentNullException.ThrowIfNull(outputName);
        ArgumentNullException.ThrowIfNull(depthUniform);
        ArgumentNullException.ThrowIfNull(normalUniform);

        _program = program;
        _depthTextureName = depthTextureName;
        _normalTextureName = normalTextureName;
        _outputTextureName = outputName;
        _depthUniform = depthUniform;
        _normalUniform = normalUniform;

        Reads(depthTextureName);
        Reads(normalTextureName);
        Writes(outputName);
    }

    /// <summary>Gets the SSAO sampling radius in scene units. Must be in <c>(0, 64]</c>.</summary>
    public float Radius
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _radius;
    }

    /// <summary>Gets the depth bias subtracted before the occlusion comparison, in scene units. Must be in <c>[0, 1]</c>.</summary>
    public float Bias
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bias;
    }

    /// <summary>Gets the occlusion strength multiplier. Must be in <c>[0, 1]</c>.</summary>
    public float Intensity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _intensity;
    }

    /// <summary>Gets the depth texture resource name.</summary>
    public string DepthTextureName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _depthTextureName;
    }

    /// <summary>Gets the normal texture resource name.</summary>
    public string NormalTextureName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _normalTextureName;
    }

    /// <summary>Gets the occlusion output target name.</summary>
    public string OutputTextureName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _outputTextureName;
    }

    /// <summary>Gets the depth sampler uniform name.</summary>
    public string DepthUniform
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _depthUniform;
    }

    /// <summary>Gets the normal sampler uniform name.</summary>
    public string NormalUniform
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _normalUniform;
    }

    /// <summary>
    /// Sets the SSAO sampling radius.
    /// </summary>
    /// <param name="radius">The radius in scene units. Must be positive and at most 64.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public SsaoPassExecutor WithRadius(float radius)
    {
        _radius = radius;
        return this;
    }

    /// <summary>
    /// Sets the depth bias that suppresses self-occlusion.
    /// </summary>
    /// <param name="bias">The bias in scene units. Must be non-negative and at most 1.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public SsaoPassExecutor WithBias(float bias)
    {
        _bias = bias;
        return this;
    }

    /// <summary>
    /// Sets the occlusion strength multiplier.
    /// </summary>
    /// <param name="intensity">The intensity. Must be in [0, 1].</param>
    /// <returns>This instance for fluent chaining.</returns>
    public SsaoPassExecutor WithIntensity(float intensity)
    {
        _intensity = intensity;
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (_program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "SSAO shader program has been disposed", nameof(SsaoPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_depthTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Depth texture name must not be empty", nameof(SsaoPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_normalTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Normal texture name must not be empty", nameof(SsaoPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_outputTextureName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "SSAO output name must not be empty", nameof(SsaoPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_depthUniform) || string.IsNullOrWhiteSpace(_normalUniform))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "SSAO sampler uniform names must not be empty", nameof(SsaoPassExecutor));
        }

        if (_radius <= 0f || _radius > MaxRadius)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"SSAO radius must be in (0, {MaxRadius}], got {_radius}", nameof(SsaoPassExecutor));
        }

        if (_bias < 0f || _bias > MaxBias)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"SSAO bias must be in [0, {MaxBias}], got {_bias}", nameof(SsaoPassExecutor));
        }

        if (_intensity < 0f || _intensity > 1f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"SSAO intensity must be in [0, 1], got {_intensity}", nameof(SsaoPassExecutor));
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

        // Resolve the SSAO inputs and the render target.
        var depthTexture = ctx.GetTexture(_depthTextureName);
        var normalTexture = ctx.GetTexture(_normalTextureName);
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

        // Bind the caller-supplied SSAO program.
        state.UseProgram(_program.Id);

        // Bind the depth and normal inputs to their fixed units.
        state.BindTexture2D((uint)DepthTextureUnit, depthTexture.Id);
        state.BindTexture2D((uint)NormalTextureUnit, normalTexture.Id);

        int location = _program.GetUniformLocation(_depthUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, DepthTextureUnit);
        }

        location = _program.GetUniformLocation(_normalUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, NormalTextureUnit);
        }

        // The sampling step follows the depth buffer the effect reads from.
        location = _program.GetUniformLocation(TexelSizeUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform2(location, 1.0f / depthTexture.Width, 1.0f / depthTexture.Height);
        }

        location = _program.GetUniformLocation(RadiusUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, _radius);
        }

        location = _program.GetUniformLocation(BiasUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, _bias);
        }

        location = _program.GetUniformLocation(IntensityUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, _intensity);
        }

        // Draw the screen-filling triangle (3 vertices, no VAO needed).
        state.BindVertexArray(0);
        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}
