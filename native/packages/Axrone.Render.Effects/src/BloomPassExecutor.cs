namespace Axrone.Render.Effects;

/// <summary>
/// Bloom effect pass with two interchangeable implementations over the same public
/// surface: a Karis soft-knee prefilter with a mip-pyramid blur (the default) and the
/// original separable Gaussian ping-pong blur (<see cref="UseLegacyGaussian"/>).
/// </summary>
/// <remarks>
/// <para>The default mip-pyramid path runs four stages:</para>
/// <list type="number">
///   <item><description>Prefilter: soft-knee bright-pass with Karis luminance weighting, rendered into pyramid level 0 at half resolution. The target is invalidated <c>DontCare</c> — every pixel is written.</description></item>
///   <item><description>Downsample: 13-tap tent per further level, invalidated <c>DontCare</c>. <c>u_karis</c> is <c>0</c> for the first halving (level 0 is already Karis-weighted by the prefilter) and <c>1</c> for the rest.</description></item>
///   <item><description>Upsample: 3x3 tent accumulated from the smallest level upward with an additive blend, so each level is loaded rather than cleared.</description></item>
///   <item><description>Composite: <c>scene + bloom * intensity</c> into the output target, alpha passed through.</description></item>
/// </list>
/// <para>The legacy path keeps the original three stages: bright-pass extraction,
/// <see cref="BlurIterations"/> horizontal + vertical Gaussian ping-pong pairs, and an
/// additive composite.</para>
/// <para>Program binding. The constructor is unchanged, so the pyramid path reuses
/// <c>brightPassShader</c> as the prefilter program and <c>blurShader</c> for both the
/// downsample and the upsample stage; that program must expose the union of the
/// <c>BloomShaders.DownsampleFragment</c> and <c>BloomShaders.UpsampleFragment</c>
/// uniforms, or either subset — every uniform is optional and re-probed per
/// <see cref="Execute"/>, so a missing one is simply not uploaded. The composite
/// program reads the intensity from <c>u_intensity</c> or, failing that, from the
/// legacy <c>u_bloomIntensity</c>; the value is uploaded to every name the program
/// exposes. See <see cref="BloomShaders"/> for the GLSL.</para>
/// <para>Resources. The mip pyramid is created lazily on the first
/// <see cref="Execute"/> and rebuilt only when the source resolution or the level count
/// changes. The pass owns the pyramid it creates and disposes it; a pyramid handed in
/// through <see cref="WithMipPyramid"/> stays owned by the caller and is never disposed
/// here.</para>
/// </remarks>
public sealed class BloomPassExecutor : IRenderPass, IDisposable
{
    /// <summary>Smallest mip-pyramid level count. A pyramid always has at least one halving step.</summary>
    public const int MinMipCount = 2;

    /// <summary>Largest mip-pyramid level count. Mirrors <see cref="BloomMath.MaxMipCount"/>.</summary>
    public const int MaxMipCount = 8;

    /// <summary>Upper bound for <see cref="Radius"/>, in texels of the level being upsampled.</summary>
    public const float MaxRadius = 4f;

    /// <summary>Upper bound for <see cref="Scatter"/>, the per-level contribution weight.</summary>
    public const float MaxScatter = 4f;

    private const int InvalidUniformLocation = -1;

    private const string SourceUniform = "u_source";
    private const string TexelSizeUniform = "u_texelSize";
    private const string CurveUniform = "u_curve";
    private const string KarisUniform = "u_karis";
    private const string RadiusUniform = "u_radius";
    private const string ScatterUniform = "u_scatter";
    private const string IntensityUniform = "u_intensity";
    private const string LegacyIntensityUniform = "u_bloomIntensity";
    private const string LegacyThresholdUniform = "u_threshold";
    private const string LegacyDirectionUniform = "u_direction";

    /// <summary>Debug labels for the internally created pyramid level textures.</summary>
    private static readonly string[] LevelTextureLabels =
    [
        "bloom_mip0", "bloom_mip1", "bloom_mip2", "bloom_mip3",
        "bloom_mip4", "bloom_mip5", "bloom_mip6", "bloom_mip7"
    ];

    /// <summary>Debug labels for the internally created pyramid level framebuffers.</summary>
    private static readonly string[] LevelFramebufferLabels =
    [
        "bloom_mip0_fbo", "bloom_mip1_fbo", "bloom_mip2_fbo", "bloom_mip3_fbo",
        "bloom_mip4_fbo", "bloom_mip5_fbo", "bloom_mip6_fbo", "bloom_mip7_fbo"
    ];

    private readonly GLProgram _brightPassShader;
    private readonly GLProgram _blurShader;
    private readonly GLProgram _compositeShader;

    private readonly List<string> _reads = new(2);
    private readonly List<string> _writes = new(2);
    private string[]? _readsSnapshot;
    private string[]? _writesSnapshot;

    /// <summary>Gets the pass name.</summary>
    public string Name { get; }

    /// <summary>Gets the pass kind for scheduling classification.</summary>
    public FramePassKind Kind { get; }

    /// <summary>Gets or sets a value indicating whether this pass is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Gets the hardware-agnostic descriptor describing targets and attachments.</summary>
    public RenderPassDescriptor Descriptor { get; }

    /// <summary>Gets how this pass's render target attachments must be loaded.</summary>
    public AttachmentLoadAction LoadAction => AttachmentLoadAction.Load;

    /// <summary>Gets how this pass's render target attachments must be stored.</summary>
    public AttachmentStoreAction StoreAction => AttachmentStoreAction.Store;

    private readonly string _inputTextureName;
    private readonly string _outputTextureName;

    /// <summary>Framebuffer companion name of <see cref="_outputTextureName"/>, composed once.</summary>
    private readonly string _outputFramebufferName;

    // One value-dedup cache shared by all four pyramid stages: the cache is keyed by
    // (program, location), so sharing it cannot alias two distinct uniforms.
    private readonly UniformCache _uniformCache = new();

    // The four pyramid stages. The public constructor is program-only, so the
    // instances — which need a GL context — are bound on the first pyramid execution
    // and reused from then on.
    private ShaderInstance? _prefilter;
    private ShaderInstance? _downsample;
    private ShaderInstance? _upsample;
    private ShaderInstance? _composite;

    private readonly GLTexture?[] _pyramidTextures = new GLTexture?[MaxMipCount];
    private readonly GLFramebuffer?[] _pyramidFramebuffers = new GLFramebuffer?[MaxMipCount];
    private readonly BloomMipLevel[] _suppliedLevels = new BloomMipLevel[MaxMipCount];

    private float _threshold = 1.0f;
    private int _blurIterations = 5;
    private float _bloomIntensity = 0.5f;

    // Pyramid configuration.
    private float _softKnee = BloomMath.DefaultSoftKnee;
    private float _radius = 1f;
    private float _scatter = 1f;
    private int _mipCount;
    private bool _useLegacyGaussian;
    private int _suppliedLevelCount;
    private bool _hasSuppliedLevels;

    // Lazily-initialized internal ping-pong resources.
    private GLFramebuffer? _brightFbo;
    private GLFramebuffer? _pingFbo;
    private GLFramebuffer? _pongFbo;
    private GLTexture? _brightTexture;
    private GLTexture? _pingTexture;
    private GLTexture? _pongTexture;
    private int _internalWidth;
    private int _internalHeight;

    // Lazily-initialized internal mip pyramid.
    private int _pyramidLevelCount;
    private int _pyramidWidth;
    private int _pyramidHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="BloomPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="brightPassShader">Shader for bright-pass extraction (scene → bright pixels). Doubles as the pyramid prefilter program.</param>
    /// <param name="blurShader">Shader for Gaussian blur (direction set via <c>u_direction</c> uniform). Doubles as the pyramid downsample and upsample program.</param>
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
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(brightPassShader);
        ArgumentNullException.ThrowIfNull(blurShader);
        ArgumentNullException.ThrowIfNull(compositeShader);

        Name = name;
        Kind = FramePassKind.Bloom;
        _brightPassShader = brightPassShader;
        _blurShader = blurShader;
        _compositeShader = compositeShader;
        _inputTextureName = inputTextureName;
        _outputTextureName = outputTextureName;
        _outputFramebufferName = outputTextureName + "_fbo";

        _reads.Add(inputTextureName);
        _writes.Add(outputTextureName);
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

    /// <summary>Gets the soft-knee factor of the pyramid prefilter. Must be in <c>[0, 1]</c>.</summary>
    public float SoftKnee
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _softKnee;
    }

    /// <summary>Gets the pyramid upsample tent radius, in texels. Must be in <c>(0, <see cref="MaxRadius"/>]</c>.</summary>
    public float Radius
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _radius;
    }

    /// <summary>Gets the per-level contribution weight applied during the upsample accumulation. Must be in <c>[0, <see cref="MaxScatter"/>]</c>.</summary>
    public float Scatter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _scatter;
    }

    /// <summary>Gets the requested pyramid level count. <c>0</c> derives it from the source resolution.</summary>
    public int MipCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _mipCount;
    }

    /// <summary>Gets a value indicating whether the legacy separable Gaussian path is used instead of the mip pyramid.</summary>
    public bool UseLegacyGaussian
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _useLegacyGaussian;
    }

    /// <summary>Gets the width of pyramid level 0, or <c>0</c> before the first pyramid execution.</summary>
    public int PyramidWidth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _pyramidWidth;
    }

    /// <summary>Gets the height of pyramid level 0, or <c>0</c> before the first pyramid execution.</summary>
    public int PyramidHeight
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _pyramidHeight;
    }

    /// <summary>Gets the live pyramid level count, or <c>0</c> before the first pyramid execution.</summary>
    public int LevelCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _pyramidLevelCount;
    }

    /// <summary>
    /// Gets a value indicating whether the pass owns the live pyramid and will dispose it.
    /// <c>false</c> for a chain supplied through <see cref="WithMipPyramid"/>.
    /// </summary>
    public bool OwnsMipPyramid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !_hasSuppliedLevels;
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
    /// Only the legacy Gaussian path uses this value.
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

    /// <summary>
    /// Sets the soft-knee factor of the pyramid prefilter. <c>0</c> degenerates the
    /// prefilter to a hard luminance threshold.
    /// </summary>
    /// <param name="softKnee">The soft-knee factor. Must be in <c>[0, 1]</c>.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public BloomPassExecutor WithSoftKnee(float softKnee)
    {
        _softKnee = softKnee;
        return this;
    }

    /// <summary>
    /// Sets the pyramid upsample tent radius.
    /// </summary>
    /// <param name="radius">The radius in texels. Must be positive and at most <see cref="MaxRadius"/>.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public BloomPassExecutor WithRadius(float radius)
    {
        _radius = radius;
        return this;
    }

    /// <summary>
    /// Sets the per-level contribution weight applied while accumulating the pyramid.
    /// </summary>
    /// <param name="scatter">The weight. Must be in <c>[0, <see cref="MaxScatter"/>]</c>.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public BloomPassExecutor WithScatter(float scatter)
    {
        _scatter = scatter;
        return this;
    }

    /// <summary>
    /// Sets the requested pyramid level count. <c>0</c> derives the count from the
    /// source resolution, capped at <see cref="MaxMipCount"/>. A count that differs from
    /// the live chain invalidates the derived pyramid, which is rebuilt on the next
    /// execution.
    /// </summary>
    /// <param name="mipCount">The level count. Either <c>0</c> or in <c>[<see cref="MinMipCount"/>, <see cref="MaxMipCount"/>]</c>.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public BloomPassExecutor WithMipCount(int mipCount)
    {
        _mipCount = mipCount;
        return this;
    }

    /// <summary>
    /// Selects the legacy separable Gaussian ping-pong path instead of the mip pyramid.
    /// </summary>
    /// <param name="useLegacyGaussian"><c>true</c> to use the Gaussian path.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public BloomPassExecutor WithLegacyGaussian(bool useLegacyGaussian)
    {
        _useLegacyGaussian = useLegacyGaussian;
        return this;
    }

    /// <summary>
    /// Supplies a pre-allocated mip pyramid. The chain is copied into a fixed
    /// eight-slot buffer, so the caller's span does not have to outlive the call, and
    /// the pass never disposes the supplied levels. An empty span drops the supplied
    /// chain and restores the lazily created internal pyramid.
    /// </summary>
    /// <param name="levels">The levels, level 0 first. Chains longer than <see cref="MaxMipCount"/> are truncated.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public BloomPassExecutor WithMipPyramid(ReadOnlySpan<BloomMipLevel> levels)
    {
        Array.Clear(_suppliedLevels);
        _suppliedLevelCount = 0;
        _hasSuppliedLevels = false;

        if (!levels.IsEmpty)
        {
            int count = Math.Min(levels.Length, MaxMipCount);
            levels[..count].CopyTo(_suppliedLevels);
            _suppliedLevelCount = count;
            _hasSuppliedLevels = true;
        }

        // The chain is now authoritative: drop the derived level count and any
        // internal pyramid built for the previous configuration.
        DisposePyramid();
        return this;
    }

    /// <summary>
    /// Gets one level of the live mip pyramid.
    /// </summary>
    /// <param name="index">The zero-based level index, <c>0</c> being the half-resolution prefilter level.</param>
    /// <returns>The level's framebuffer and texture pair.</returns>
    /// <exception cref="RenderException">The pyramid has not been created yet, or the index is out of range.</exception>
    public BloomMipLevel GetPyramidLevel(int index)
    {
        if (_pyramidLevelCount == 0)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation,
                "Bloom mip pyramid has not been created yet", nameof(BloomPassExecutor));
        }

        if (index < 0 || index >= _pyramidLevelCount)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidValue,
                $"Bloom mip level index {index} is outside [0, {_pyramidLevelCount})", nameof(BloomPassExecutor));
        }

        if (_hasSuppliedLevels)
        {
            return _suppliedLevels[index];
        }

        return new BloomMipLevel(_pyramidFramebuffers[index]!, _pyramidTextures[index]!);
    }

    /// <inheritdoc/>
    public ReadOnlySpan<string> GetReadResources()
    {
        _readsSnapshot ??= _reads.ToArray();
        return _readsSnapshot;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<string> GetWrittenResources()
    {
        _writesSnapshot ??= _writes.ToArray();
        return _writesSnapshot;
    }

    /// <inheritdoc/>
    public void Validate()
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

        if (_softKnee < 0f || _softKnee > 1f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Bloom soft knee must be in [0, 1], got {_softKnee}", nameof(BloomPassExecutor));
        }

        if (_radius <= 0f || _radius > MaxRadius)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Bloom radius must be in (0, {MaxRadius}], got {_radius}", nameof(BloomPassExecutor));
        }

        if (_scatter < 0f || _scatter > MaxScatter)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Bloom scatter must be in [0, {MaxScatter}], got {_scatter}", nameof(BloomPassExecutor));
        }

        if (_mipCount != 0 && (_mipCount < MinMipCount || _mipCount > MaxMipCount))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Bloom mip count must be 0 or in [{MinMipCount}, {MaxMipCount}], got {_mipCount}", nameof(BloomPassExecutor));
        }

        if (_hasSuppliedLevels)
        {
            ValidateSuppliedPyramid();
        }
    }

    private void ValidateSuppliedPyramid()
    {
        if (_suppliedLevelCount < MinMipCount)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Bloom mip pyramid must have at least {MinMipCount} levels, got {_suppliedLevelCount}", nameof(BloomPassExecutor));
        }

        for (int i = 0; i < _suppliedLevelCount; i++)
        {
            BloomMipLevel level = _suppliedLevels[i];
            if (level.Framebuffer is null || level.Texture is null)
            {
                ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                    $"Bloom mip level {i} is not initialized", nameof(BloomPassExecutor));
            }

            if (level.Texture.Width != level.Framebuffer.Width || level.Texture.Height != level.Framebuffer.Height)
            {
                ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                    $"Bloom mip level {i} texture size ({level.Texture.Width}x{level.Texture.Height}) does not match its framebuffer ({level.Framebuffer.Width}x{level.Framebuffer.Height})",
                    nameof(BloomPassExecutor));
            }
        }
    }

    /// <inheritdoc/>
    void IRenderPass.Execute(IRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context is GLRenderContext glCtx && glCtx.PassContext is not null)
        {
            Execute(glCtx.GLContext, glCtx.PassContext);
            return;
        }

        ThrowHelper.Throw(RenderErrorCode.InvalidOperation,
            $"{Name} requires a GLRenderContext with a configured PassContext.",
            nameof(BloomPassExecutor));
    }

    /// <summary>
    /// Executes the pass on the graph-owned render context.
    /// </summary>
    /// <param name="context">The render context (must be a <see cref="GLRenderContext"/> with a configured pass context).</param>
    /// <param name="ctx">The pass execution context for resource resolution.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Execute(GLContext context, PassExecutionContext ctx)
    {
        context.AssertRenderThread();

        if (_useLegacyGaussian)
        {
            ExecuteLegacyGaussian(context, ctx);
            return;
        }

        ExecutePyramid(context, ctx);
    }

    private void ExecuteLegacyGaussian(GLContext context, PassExecutionContext ctx)
    {
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
        context.State.BindVertexArray(0);

        context.State.UseProgram(_brightPassShader.Id);
        context.State.BindTexture2D(0, sourceTexture.Id);

        int location = _brightPassShader.GetUniformLocation(LegacyThresholdUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, _threshold);
        }

        gl.DrawArrays(GLConst.Triangles, 0, 3);

        // ── Stage 2: Separable Gaussian blur (ping-pong) ─────────────────
        context.State.UseProgram(_blurShader.Id);

        uint readTex = _brightTexture!.Id;

        for (int i = 0; i < _blurIterations; i++)
        {
            // Horizontal pass: read → ping, write → pong
            context.State.BindFramebuffer(GLConst.Framebuffer, _pongFbo!.Id);
            context.State.BindTexture2D(0, readTex);
            location = _blurShader.GetUniformLocation(LegacyDirectionUniform);
            if (location != InvalidUniformLocation)
            {
                gl.Uniform2(location, 1.0f, 0.0f);
            }

            gl.DrawArrays(GLConst.Triangles, 0, 3);

            // Vertical pass: read → pong, write → ping
            context.State.BindFramebuffer(GLConst.Framebuffer, _pingFbo!.Id);
            context.State.BindTexture2D(0, _pongTexture!.Id);
            location = _blurShader.GetUniformLocation(LegacyDirectionUniform);
            if (location != InvalidUniformLocation)
            {
                gl.Uniform2(location, 0.0f, 1.0f);
            }

            gl.DrawArrays(GLConst.Triangles, 0, 3);

            readTex = _pingTexture!.Id;
        }

        // ── Stage 3: Additive composite ──────────────────────────────────
        var outputFbo = ctx.HasResource(_outputFramebufferName)
            ? ctx.GetFramebuffer(_outputFramebufferName)
            : null;

        if (outputFbo is not null)
        {
            context.State.BindFramebuffer(GLConst.Framebuffer, outputFbo.Id);
        }

        context.State.SetViewport(0, 0, outputTexture.Width, outputTexture.Height);
        context.State.UseProgram(_compositeShader.Id);
        context.State.BindTexture2D(0, sourceTexture.Id);
        context.State.BindTexture2D(1, _pingTexture!.Id);

        location = _compositeShader.GetUniformLocation(LegacyIntensityUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, _bloomIntensity);
        }

        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }

    private void ExecutePyramid(GLContext context, PassExecutionContext ctx)
    {
        var gl = context.GL;
        var state = context.State;

        var sourceTexture = ctx.GetTexture(_inputTextureName);
        var outputTexture = ctx.GetTexture(_outputTextureName);

        ShaderInstance prefilter = EnsureShaderInstances(context);
        ShaderInstance downsample = _downsample!;
        ShaderInstance upsample = _upsample!;
        ShaderInstance composite = _composite!;

        int levelCount = EnsurePyramid(context, sourceTexture.Width, sourceTexture.Height);

        // Locations are re-read per execution: a program rebuild recycles the driver's
        // location allocation, so a location cached in a field could point at another
        // uniform (or nowhere) after a context-loss restore.
        int prefilterSource = prefilter.Program.GetUniformLocation(SourceUniform);
        int prefilterTexelSize = prefilter.Program.GetUniformLocation(TexelSizeUniform);
        int prefilterCurve = prefilter.Program.GetUniformLocation(CurveUniform);
        int downsampleSource = downsample.Program.GetUniformLocation(SourceUniform);
        int downsampleTexelSize = downsample.Program.GetUniformLocation(TexelSizeUniform);
        int downsampleKaris = downsample.Program.GetUniformLocation(KarisUniform);
        int upsampleSource = upsample.Program.GetUniformLocation(SourceUniform);
        int upsampleTexelSize = upsample.Program.GetUniformLocation(TexelSizeUniform);
        int upsampleRadius = upsample.Program.GetUniformLocation(RadiusUniform);
        int upsampleScatter = upsample.Program.GetUniformLocation(ScatterUniform);
        int compositeIntensity = composite.Program.GetUniformLocation(IntensityUniform);
        int compositeLegacyIntensity = composite.Program.GetUniformLocation(LegacyIntensityUniform);

        state.SetDepthTest(false);
        state.SetCullFace(false);
        state.SetBlend(false);
        state.SetColorMask(true, true, true, true);
        state.BindVertexArray(0);

        Span<uint> attachment = stackalloc uint[1];
        attachment[0] = GLConst.ColorAttachment0;
        Span<uint> noAttachments = stackalloc uint[0];

        // ── Stage 1: Soft-knee prefilter into level 0 ────────────────────
        {
            GLFramebuffer target = LevelFramebuffer(0);
            GLTexture source = sourceTexture;

            state.BindFramebuffer(GLConst.Framebuffer, target.Id);
            state.SetViewport(0, 0, target.Width, target.Height);

            // Every pixel of level 0 is written, so its previous contents are undefined.
            gl.InvalidateFramebuffer(GLConst.Framebuffer, attachment);

            prefilter.Bind();
            state.BindTexture2D(0, source.Id);
            prefilter.SetInt(prefilterSource, 0);
            prefilter.SetVec2(prefilterTexelSize, 1f / source.Width, 1f / source.Height);

            // Routed through the instance so identical curves skip the upload; the
            // negative-location guard it carries keeps a program that omits the
            // uniform working.
            prefilter.SetVec4(prefilterCurve,
                _threshold,
                BloomMath.ComputeKneeWidth(_threshold, _softKnee),
                1f,
                1f);

            gl.DrawArrays(GLConst.Triangles, 0, 3);
        }

        // ── Stage 2: 13-tap tent downsample for every further level ──────
        for (int i = 1; i < levelCount; i++)
        {
            GLFramebuffer target = LevelFramebuffer(i);
            GLTexture source = LevelTexture(i - 1);

            state.BindFramebuffer(GLConst.Framebuffer, target.Id);
            state.SetViewport(0, 0, target.Width, target.Height);
            gl.InvalidateFramebuffer(GLConst.Framebuffer, attachment);

            downsample.Bind();
            state.BindTexture2D(0, source.Id);
            downsample.SetInt(downsampleSource, 0);
            downsample.SetVec2(downsampleTexelSize, 1f / source.Width, 1f / source.Height);

            // Level 0 is already Karis-weighted by the prefilter, so the first halving
            // must not weight it a second time.
            downsample.SetFloat(downsampleKaris, i == 1 ? 0f : 1f);

            gl.DrawArrays(GLConst.Triangles, 0, 3);
        }

        // ── Stage 3: Tent upsample accumulated from the smallest level up ─
        bool savedBlendEnabled = state.BlendEnabled;
        uint savedBlendSrcRGB = state.BlendSrcRGB;
        uint savedBlendDstRGB = state.BlendDstRGB;
        uint savedBlendSrcAlpha = state.BlendSrcAlpha;
        uint savedBlendDstAlpha = state.BlendDstAlpha;
        uint savedBlendEquationRGB = state.BlendEquationRGB;
        uint savedBlendEquationAlpha = state.BlendEquationAlpha;

        try
        {
            for (int i = levelCount - 2; i >= 0; i--)
            {
                GLFramebuffer target = LevelFramebuffer(i);
                GLTexture source = LevelTexture(i + 1);

                state.BindFramebuffer(GLConst.Framebuffer, target.Id);
                state.SetViewport(0, 0, target.Width, target.Height);

                // The destination level is the accumulation base and must be loaded:
                // invalidating it would discard the level the pass just downsampled.
                gl.InvalidateFramebuffer(GLConst.Framebuffer, noAttachments);

                state.SetBlend(true);
                state.SetBlendEquationSeparate(GLConst.FuncAdd, GLConst.FuncAdd);
                state.SetBlendFuncSeparate(GLConst.One, GLConst.One, GLConst.One, GLConst.OneMinusSrcAlpha);

                upsample.Bind();
                state.BindTexture2D(0, source.Id);
                upsample.SetInt(upsampleSource, 0);
                upsample.SetVec2(upsampleTexelSize, 1f / source.Width, 1f / source.Height);
                upsample.SetFloat(upsampleRadius, _radius);
                upsample.SetFloat(upsampleScatter, _scatter);

                gl.DrawArrays(GLConst.Triangles, 0, 3);
            }
        }
        finally
        {
            // Restoring through the setters re-issues exactly the calls whose cached
            // values changed, which keeps the shadow state and GL in agreement.
            state.SetBlend(savedBlendEnabled);
            state.SetBlendEquationSeparate(savedBlendEquationRGB, savedBlendEquationAlpha);
            state.SetBlendFuncSeparate(savedBlendSrcRGB, savedBlendDstRGB, savedBlendSrcAlpha, savedBlendDstAlpha);
        }

        // ── Stage 4: Composite ───────────────────────────────────────────
        var outputFbo = ctx.HasResource(_outputFramebufferName)
            ? ctx.GetFramebuffer(_outputFramebufferName)
            : null;

        if (outputFbo is not null)
        {
            state.BindFramebuffer(GLConst.Framebuffer, outputFbo.Id);
        }

        state.SetViewport(0, 0, outputTexture.Width, outputTexture.Height);
        state.SetBlend(false);

        composite.Bind();
        state.BindTexture2D(0, sourceTexture.Id);
        state.BindTexture2D(1, LevelTexture(0).Id);

        // Both names are probed and both receive the same value, so a composite shader
        // that declares either one — or both — reads the configured intensity.
        composite.SetFloat(compositeIntensity, _bloomIntensity);
        composite.SetFloat(compositeLegacyIntensity, _bloomIntensity);

        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }

    /// <summary>
    /// Disposes internal ping-pong framebuffers and textures.
    /// </summary>
    public void Dispose() => DisposeInternalResources();

    /// <summary>
    /// Disposes internal ping-pong framebuffers, textures and mip-pyramid levels.
    /// A pyramid supplied through <see cref="WithMipPyramid"/> is left untouched.
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
        _internalWidth = 0;
        _internalHeight = 0;

        DisposePyramid();
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

    private int EnsurePyramid(GLContext context, int sourceWidth, int sourceHeight)
    {
        if (_hasSuppliedLevels)
        {
            _pyramidLevelCount = _suppliedLevelCount;
            _pyramidWidth = _suppliedLevels[0].Texture.Width;
            _pyramidHeight = _suppliedLevels[0].Texture.Height;
            return _pyramidLevelCount;
        }

        int width = Math.Max(1, sourceWidth / 2);
        int height = Math.Max(1, sourceHeight / 2);
        int levelCount = BloomMath.ComputeMipCount(
            sourceWidth, sourceHeight, _mipCount == 0 ? BloomMath.MaxMipCount : _mipCount);

        if (_pyramidLevelCount == levelCount &&
            _pyramidWidth == width &&
            _pyramidHeight == height &&
            _pyramidFramebuffers[0] is not null)
        {
            return levelCount;
        }

        DisposePyramid();

        _pyramidWidth = width;
        _pyramidHeight = height;
        _pyramidLevelCount = levelCount;

        for (int i = 0; i < levelCount; i++)
        {
            int levelWidth = i == 0 ? width : Math.Max(1, width >> i);
            int levelHeight = i == 0 ? height : Math.Max(1, height >> i);

            var texture = new GLTexture(context, GLConst.Texture2D,
                TextureFormat.Rgba16f, levelWidth, levelHeight, label: LevelTextureLabels[i]);
            texture.SetParameter(GLConst.TextureMinFilter, (int)GLConst.Linear);
            texture.SetParameter(GLConst.TextureMagFilter, (int)GLConst.Linear);
            texture.SetParameter(GLConst.TextureWrapS, (int)GLConst.ClampToEdge);
            texture.SetParameter(GLConst.TextureWrapT, (int)GLConst.ClampToEdge);

            var framebuffer = new GLFramebuffer(context, levelWidth, levelHeight, LevelFramebufferLabels[i]);
            framebuffer.AttachColor(texture, 0);

            _pyramidTextures[i] = texture;
            _pyramidFramebuffers[i] = framebuffer;
        }

        return levelCount;
    }

    /// <summary>
    /// Binds the four pyramid <see cref="ShaderInstance"/>s to the executing context on
    /// first use. All four share one <see cref="UniformCache"/>; the prefilter and the
    /// composite use their own programs, the downsample and the upsample share
    /// <c>blurShader</c>, whose cache entries are kept apart by location.
    /// </summary>
    private ShaderInstance EnsureShaderInstances(GLContext context)
    {
        ShaderInstance? prefilter = _prefilter;
        if (prefilter is not null)
        {
            return prefilter;
        }

        prefilter = new ShaderInstance(context, _brightPassShader, _uniformCache);
        _prefilter = prefilter;
        _downsample = new ShaderInstance(context, _blurShader, _uniformCache);
        _upsample = new ShaderInstance(context, _blurShader, _uniformCache);
        _composite = new ShaderInstance(context, _compositeShader, _uniformCache);
        return prefilter;
    }

    private void DisposePyramid()
    {
        for (int i = 0; i < MaxMipCount; i++)
        {
            _pyramidFramebuffers[i]?.Dispose();
            _pyramidTextures[i]?.Dispose();
            _pyramidFramebuffers[i] = null;
            _pyramidTextures[i] = null;
        }

        _pyramidLevelCount = 0;
        _pyramidWidth = 0;
        _pyramidHeight = 0;
    }

    private GLFramebuffer LevelFramebuffer(int index) =>
        _hasSuppliedLevels ? _suppliedLevels[index].Framebuffer : _pyramidFramebuffers[index]!;

    private GLTexture LevelTexture(int index) =>
        _hasSuppliedLevels ? _suppliedLevels[index].Texture : _pyramidTextures[index]!;
}
