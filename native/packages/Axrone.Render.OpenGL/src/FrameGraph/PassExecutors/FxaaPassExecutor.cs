namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// FXAA (Fast Approximate Anti-Aliasing) post-process pass. Applies edge-detection
/// based anti-aliasing as a fullscreen post-process, smoothing jagged edges without
/// the cost of multisample anti-aliasing.
/// </summary>
/// <remarks>
/// <para>The shader is expected to expose the following uniforms:</para>
/// <list type="bullet">
///   <item><description><c>u_texture</c> (sampler2D) — the input LDR texture bound at unit 0.</description></item>
///   <item><description><c>u_texelSize</c> (vec2) — <c>1.0 / textureSize</c>.</description></item>
///   <item><description><c>u_subpixelQuality</c> (float) — sub-pixel aliasing removal strength [0..1].</description></item>
///   <item><description><c>u_edgeThreshold</c> (float) — minimum luminance contrast to trigger FXAA.</description></item>
///   <item><description><c>u_edgeThresholdMin</c> (float) — absolute minimum luminance to apply FXAA (dark-area cutoff).</description></item>
/// </list>
/// </remarks>
public sealed class FxaaPassExecutor : RenderPass
{
    private readonly GLProgram _shader;
    private readonly string _inputTextureName;
    private readonly string _outputTextureName;

    private float _subpixelQuality = 0.75f;
    private float _edgeThreshold = 0.125f;
    private float _edgeThresholdMin = 0.0312f;

    /// <summary>
    /// Initializes a new instance of the <see cref="FxaaPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="shader">The FXAA shader program.</param>
    /// <param name="inputTextureName">Name of the input LDR texture in the pass context.</param>
    /// <param name="outputTextureName">Name of the output anti-aliased texture in the pass context.</param>
    public FxaaPassExecutor(
        string name,
        GLProgram shader,
        string inputTextureName = "scene_ldr",
        string outputTextureName = "scene_aa")
        : base(name, FramePassKind.Fxaa)
    {
        ArgumentNullException.ThrowIfNull(shader);

        _shader = shader;
        _inputTextureName = inputTextureName;
        _outputTextureName = outputTextureName;

        Reads(inputTextureName);
        Writes(outputTextureName);
    }

    /// <summary>Gets the sub-pixel aliasing removal quality [0..1].</summary>
    public float SubpixelQuality
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _subpixelQuality;
    }

    /// <summary>Gets the edge detection luminance threshold.</summary>
    public float EdgeThreshold
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _edgeThreshold;
    }

    /// <summary>Gets the absolute minimum luminance for FXAA application.</summary>
    public float EdgeThresholdMin
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _edgeThresholdMin;
    }

    /// <summary>
    /// Sets the sub-pixel aliasing removal quality.
    /// Higher values produce smoother results but may blur fine detail.
    /// </summary>
    /// <param name="quality">Quality value in [0..1].</param>
    /// <returns>This instance for fluent chaining.</returns>
    public FxaaPassExecutor WithSubpixelQuality(float quality)
    {
        _subpixelQuality = quality;
        return this;
    }

    /// <summary>
    /// Sets the edge detection luminance threshold.
    /// Lower values detect more edges for anti-aliasing.
    /// </summary>
    /// <param name="threshold">The threshold value. Must be non-negative.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public FxaaPassExecutor WithEdgeThreshold(float threshold)
    {
        _edgeThreshold = threshold;
        return this;
    }

    /// <summary>
    /// Sets the absolute minimum luminance below which FXAA is not applied.
    /// Prevents FXAA from operating on very dark areas where noise is imperceptible.
    /// </summary>
    /// <param name="thresholdMin">The minimum luminance. Must be non-negative.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public FxaaPassExecutor WithEdgeThresholdMin(float thresholdMin)
    {
        _edgeThresholdMin = thresholdMin;
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (_subpixelQuality < 0f || _subpixelQuality > 1f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Subpixel quality must be in [0, 1], got {_subpixelQuality}", nameof(FxaaPassExecutor));
        }

        if (_edgeThreshold < 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Edge threshold must be non-negative, got {_edgeThreshold}", nameof(FxaaPassExecutor));
        }

        if (_edgeThresholdMin < 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Edge threshold min must be non-negative, got {_edgeThresholdMin}", nameof(FxaaPassExecutor));
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

        // Bind FXAA shader.
        context.State.UseProgram(_shader.Id);

        // Bind input texture at unit 0.
        context.State.BindTexture2D(0, inputTexture.Id);

        // Set FXAA uniforms.
        var gl = context.GL;
        gl.Uniform2(_shader.GetUniformLocation("u_texelSize"),
            1.0f / inputTexture.Width, 1.0f / inputTexture.Height);
        gl.Uniform1(_shader.GetUniformLocation("u_subpixelQuality"), _subpixelQuality);
        gl.Uniform1(_shader.GetUniformLocation("u_edgeThreshold"), _edgeThreshold);
        gl.Uniform1(_shader.GetUniformLocation("u_edgeThresholdMin"), _edgeThresholdMin);

        // Draw fullscreen triangle.
        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}
