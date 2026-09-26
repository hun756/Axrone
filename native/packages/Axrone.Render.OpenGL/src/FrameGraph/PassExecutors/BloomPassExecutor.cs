namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Multi-stage bloom effect pass. Extracts bright pixels above a luminance threshold,
/// applies a separable Gaussian blur via ping-pong rendering, and composites the bloom
/// result back onto the original scene with additive blending.
/// </summary>
/// <remarks>
/// <para>The pass operates in three stages:</para>
/// <list type="number">
///   <item><description>Bright-pass extraction: pixels above <see cref="Threshold"/> are written to a half-resolution bright texture.</description></item>
///   <item><description>Separable Gaussian blur: <see cref="BlurIterations"/> horizontal + vertical blur passes using ping-pong framebuffers.</description></item>
///   <item><description>Composite: the blurred bloom is additively blended onto the scene at <see cref="BloomIntensity"/>.</description></item>
/// </list>
/// <para>Internal ping-pong resources are lazily created on first <see cref="Execute"/> and
/// cached for the lifetime of the pass. They are disposed when the pass is no longer needed.</para>
/// </remarks>
public sealed class BloomPassExecutor : RenderPass, IDisposable
{
    private readonly GLProgram _brightPassShader;
    private readonly GLProgram _blurShader;
    private readonly GLProgram _compositeShader;

    private readonly string _inputTextureName;
    private readonly string _outputTextureName;

    private float _threshold = 1.0f;
    private int _blurIterations = 5;
    private float _bloomIntensity = 0.5f;

    // Lazily-initialized internal ping-pong resources.
    private GLFramebuffer? _brightFbo;
    private GLFramebuffer? _pingFbo;
    private GLFramebuffer? _pongFbo;
    private GLTexture? _brightTexture;
    private GLTexture? _pingTexture;
    private GLTexture? _pongTexture;
    private int _internalWidth;
    private int _internalHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="BloomPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="brightPassShader">Shader for bright-pass extraction (scene → bright pixels).</param>
    /// <param name="blurShader">Shader for Gaussian blur (direction set via <c>u_direction</c> uniform).</param>
    /// <param name="compositeShader">Shader for additive composite (scene + bloom → output).</param>
    /// <param name="inputTextureName">Name of the input HDR scene texture in the pass context.</param>
    /// <param name="outputTextureName">Name of the output bloom texture in the pass context.</param>
    public BloomPassExecutor(
        string name,
        GLProgram brightPassShader,
        GLProgram blurShader,
        GLProgram compositeShader,
        string inputTextureName = "scene_hdr",
        string outputTextureName = "scene_bloom")
        : base(name, FramePassKind.Bloom)
    {
        ArgumentNullException.ThrowIfNull(brightPassShader);
        ArgumentNullException.ThrowIfNull(blurShader);
        ArgumentNullException.ThrowIfNull(compositeShader);

        _brightPassShader = brightPassShader;
        _blurShader = blurShader;
        _compositeShader = compositeShader;
        _inputTextureName = inputTextureName;
        _outputTextureName = outputTextureName;

        Reads(inputTextureName);
        Writes(outputTextureName);
    }

    /// <summary>Gets the luminance threshold for bright-pass extraction.</summary>
    public float Threshold
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _threshold;
    }

    /// <summary>Gets the number of horizontal+vertical blur iteration pairs.</summary>
    public int BlurIterations
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _blurIterations;
    }

    /// <summary>Gets the bloom composite intensity multiplier.</summary>
    public float BloomIntensity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bloomIntensity;
    }

    /// <summary>
    /// Sets the luminance threshold for bright-pass extraction.
    /// Pixels with luminance above this value are included in the bloom.
    /// </summary>
    /// <param name="threshold">The threshold value. Must be non-negative.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public BloomPassExecutor WithThreshold(float threshold)
    {
        _threshold = threshold;
        return this;
    }

    /// <summary>
    /// Sets the number of separable blur iteration pairs (horizontal + vertical per iteration).
    /// </summary>
    /// <param name="iterations">The iteration count. Must be at least 1.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public BloomPassExecutor WithBlurIterations(int iterations)
    {
        _blurIterations = iterations;
        return this;
    }

    /// <summary>
    /// Sets the bloom intensity multiplier used during compositing.
    /// </summary>
    /// <param name="intensity">The intensity. Must be non-negative.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public BloomPassExecutor WithBloomIntensity(float intensity)
    {
        _bloomIntensity = intensity;
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (_threshold < 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Bloom threshold must be non-negative, got {_threshold}", nameof(BloomPassExecutor));
        }

        if (_blurIterations < 1)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Bloom blur iterations must be at least 1, got {_blurIterations}", nameof(BloomPassExecutor));
        }

        if (_bloomIntensity < 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Bloom intensity must be non-negative, got {_bloomIntensity}", nameof(BloomPassExecutor));
        }
    }

    /// <inheritdoc/>
    public override void Execute(GLContext context, PassExecutionContext ctx)
    {
        context.AssertRenderThread();

        var sourceTexture = ctx.GetTexture(_inputTextureName);
        var outputTexture = ctx.GetTexture(_outputTextureName);

        int halfWidth = Math.Max(1, sourceTexture.Width / 2);
        int halfHeight = Math.Max(1, sourceTexture.Height / 2);

        EnsureInternalResources(context, halfWidth, halfHeight);

        var gl = context.GL;

        // ── Stage 1: Bright-pass extraction ──────────────────────────────
        context.State.BindFramebuffer(GLConst.Framebuffer, _brightFbo!.Id);
        context.State.SetViewport(0, 0, halfWidth, halfHeight);
        context.State.SetDepthTest(false);
        context.State.SetCullFace(false);

        context.State.UseProgram(_brightPassShader.Id);
        context.State.BindTexture2D(0, sourceTexture.Id);
        gl.Uniform1(_brightPassShader.GetUniformLocation("u_threshold"), _threshold);

        gl.DrawArrays(GLConst.Triangles, 0, 3);

        // ── Stage 2: Separable Gaussian blur (ping-pong) ─────────────────
        context.State.UseProgram(_blurShader.Id);

        uint readTex = _brightTexture!.Id;

        for (int i = 0; i < _blurIterations; i++)
        {
            // Horizontal pass: read → ping, write → pong
            context.State.BindFramebuffer(GLConst.Framebuffer, _pongFbo!.Id);
            context.State.BindTexture2D(0, readTex);
            gl.Uniform2(_blurShader.GetUniformLocation("u_direction"), 1.0f, 0.0f);
            gl.DrawArrays(GLConst.Triangles, 0, 3);

            // Vertical pass: read → pong, write → ping
            context.State.BindFramebuffer(GLConst.Framebuffer, _pingFbo!.Id);
            context.State.BindTexture2D(0, _pongTexture!.Id);
            gl.Uniform2(_blurShader.GetUniformLocation("u_direction"), 0.0f, 1.0f);
            gl.DrawArrays(GLConst.Triangles, 0, 3);

            readTex = _pingTexture!.Id;
        }

        // ── Stage 3: Additive composite ──────────────────────────────────
        var outputFbo = ctx.HasResource(_outputTextureName + "_fbo")
            ? ctx.GetFramebuffer(_outputTextureName + "_fbo")
            : null;

        if (outputFbo is not null)
        {
            context.State.BindFramebuffer(GLConst.Framebuffer, outputFbo.Id);
        }

        context.State.SetViewport(0, 0, outputTexture.Width, outputTexture.Height);
        context.State.UseProgram(_compositeShader.Id);
        context.State.BindTexture2D(0, sourceTexture.Id);
        context.State.BindTexture2D(1, _pingTexture!.Id);

        gl.Uniform1(_compositeShader.GetUniformLocation("u_bloomIntensity"), _bloomIntensity);

        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }

    /// <summary>
    /// Disposes internal ping-pong framebuffers and textures.
    /// </summary>
    public void Dispose() => DisposeInternalResources();

    /// <summary>
    /// Disposes internal ping-pong framebuffers and textures.
    /// </summary>
    public void DisposeInternalResources()
    {
        _brightFbo?.Dispose();
        _pingFbo?.Dispose();
        _pongFbo?.Dispose();
        _brightTexture?.Dispose();
        _pingTexture?.Dispose();
        _pongTexture?.Dispose();

        _brightFbo = null;
        _pingFbo = null;
        _pongFbo = null;
        _brightTexture = null;
        _pingTexture = null;
        _pongTexture = null;
    }

    private void EnsureInternalResources(GLContext context, int width, int height)
    {
        if (_internalWidth == width && _internalHeight == height && _brightFbo is not null)
        {
            return;
        }

        DisposeInternalResources();

        _internalWidth = width;
        _internalHeight = height;

        var gl = context.GL;

        // Bright-pass texture
        _brightTexture = new GLTexture(context, GLConst.Texture2D,
            TextureFormat.Rgba16f, width, height, label: "bloom_bright");
        _brightTexture.SetParameter(GLConst.TextureMinFilter, (int)GLConst.Linear);
        _brightTexture.SetParameter(GLConst.TextureMagFilter, (int)GLConst.Linear);
        _brightTexture.SetParameter(GLConst.TextureWrapS, (int)GLConst.ClampToEdge);
        _brightTexture.SetParameter(GLConst.TextureWrapT, (int)GLConst.ClampToEdge);

        // Ping texture
        _pingTexture = new GLTexture(context, GLConst.Texture2D,
            TextureFormat.Rgba16f, width, height, label: "bloom_ping");
        _pingTexture.SetParameter(GLConst.TextureMinFilter, (int)GLConst.Linear);
        _pingTexture.SetParameter(GLConst.TextureMagFilter, (int)GLConst.Linear);
        _pingTexture.SetParameter(GLConst.TextureWrapS, (int)GLConst.ClampToEdge);
        _pingTexture.SetParameter(GLConst.TextureWrapT, (int)GLConst.ClampToEdge);

        // Pong texture
        _pongTexture = new GLTexture(context, GLConst.Texture2D,
            TextureFormat.Rgba16f, width, height, label: "bloom_pong");
        _pongTexture.SetParameter(GLConst.TextureMinFilter, (int)GLConst.Linear);
        _pongTexture.SetParameter(GLConst.TextureMagFilter, (int)GLConst.Linear);
        _pongTexture.SetParameter(GLConst.TextureWrapS, (int)GLConst.ClampToEdge);
        _pongTexture.SetParameter(GLConst.TextureWrapT, (int)GLConst.ClampToEdge);

        // Framebuffers
        _brightFbo = new GLFramebuffer(context, width, height, "bloom_bright_fbo");
        _brightFbo.AttachColor(_brightTexture, 0);

        _pingFbo = new GLFramebuffer(context, width, height, "bloom_ping_fbo");
        _pingFbo.AttachColor(_pingTexture, 0);

        _pongFbo = new GLFramebuffer(context, width, height, "bloom_pong_fbo");
        _pongFbo.AttachColor(_pongTexture, 0);
    }
}
