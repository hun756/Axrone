namespace Axrone.Render.Effects;

/// <summary>
/// Tone mapping operators for HDR to LDR conversion. The numeric value of each member is
/// the mode id handed to the GLSL program through the <c>u_operator</c> uniform, so the ids
/// are a wire contract with caller-supplied shaders.
/// </summary>
/// <remarks>
/// <para><b>Web parity:</b> ids 0-6 match the mode ids emitted by the WebGL2 renderer's
/// tonemap executor (<c>resolveTonemapModeId</c>), letting a single GLSL port serve both
/// backends. Ids 7 and 8 are native-only extensions.</para>
/// <para><b>Renumbering note:</b> earlier revisions of this enum used
/// <c>Reinhard = 0, Aces = 1, Uncharted2 = 2, ExposureOnly = 3</c>. Ids were realigned to the
/// WebGL2 mapping; the affected member <em>names</em> are unchanged, but a GLSL program written
/// against the old numbering must be updated.</para>
/// </remarks>
public enum ToneMapOperator
{
    /// <summary>No tone curve: the exposed colour is clamped and passed through.</summary>
    None = 0,

    /// <summary>Classic Reinhard operator: <c>color / (1 + color)</c>.</summary>
    Reinhard = 1,

    /// <summary>ACES (Academy Color Encoding System) filmic curve.</summary>
    Aces = 2,

    /// <summary>ACES RRT+ODT fit: the input/output matrices plus the RRT and ODT approximation.</summary>
    AcesFitted = 3,

    /// <summary>Uncharted 2 filmic tone mapping by John Hable, with shoulder/toe shaping.</summary>
    Filmic = 4,

    /// <summary>AgX: log2 encode, a 7th-order contrast approximation, then the AgX output matrix.</summary>
    AgX = 5,

    /// <summary>Khronos PBR Neutral: hue-preserving compression with a single desaturation knee.</summary>
    Neutral = 6,

    /// <summary>Simple exposure-only scaling: <c>color * exposure</c> with no tone curve.</summary>
    ExposureOnly = 7,

    /// <summary>
    /// Retained legacy identifier for the John Hable Uncharted 2 curve. <see cref="Filmic"/> is
    /// the web-parity identifier for the same curve and should be preferred; a program switching
    /// on <c>u_operator</c> must map this id to the same branch as <see cref="Filmic"/>.
    /// </summary>
    Uncharted2 = 8,
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
/// <para><b>Exposure adaptation (optional).</b> Supplying both history texture names turns the
/// pass into a two-draw operation: an adaptation stage resolves a temporally smoothed exposure
/// into the history write target, then the tone map stage samples that target and ignores
/// <see cref="Exposure"/>. The adaptation stage needs a second, caller-supplied program
/// (see <see cref="WithExposureHistory"/>) that writes the adapted exposure to
/// <c>outColor.r</c>. Its optional uniforms are:</para>
/// <list type="bullet">
///   <item><description><c>u_source</c> (sampler2D) — receives the unit index 0 (the HDR input).</description></item>
///   <item><description><c>u_previousExposure</c> (sampler2D) — receives the unit index 1 (the history read target).</description></item>
///   <item><description><c>u_keyValue</c> (float) — see <see cref="KeyValue"/>.</description></item>
///   <item><description><c>u_minExposure</c> (float) — see <see cref="MinExposureEv"/> (EV, post <c>exp2</c>).</description></item>
///   <item><description><c>u_maxExposure</c> (float) — see <see cref="MaxExposureEv"/> (EV, post <c>exp2</c>).</description></item>
///   <item><description><c>u_adaptationSpeed</c> (float) — see <see cref="AdaptationSpeed"/>.</description></item>
///   <item><description><c>u_deltaTime</c> (float) — see <see cref="DeltaTime"/> (seconds).</description></item>
///   <item><description><c>u_hasPreviousExposure</c> (int) — 0 on the first frame, 1 afterwards.</description></item>
/// </list>
/// <para>The tone map stage additionally accepts two optional uniforms, both skipped when the
/// program does not reflect them (location <c>-1</c>):</para>
/// <list type="bullet">
///   <item><description><c>u_exposureHistory</c> (sampler2D) — receives the unit index 1.</description></item>
///   <item><description><c>u_useExposureHistory</c> (int) — 1 when history is enabled, else 0.</description></item>
/// </list>
/// <para><b>Web parity:</b> operator ids, the 16-tap log-average metering, the
/// <c>1 - exp(-rate * dt)</c> adaptation blend and the EV-clamped exposure range mirror
/// <c>Axrone/web/packages/render-webgl2</c>. Two deliberate differences: <see cref="Exposure"/>
/// is a linear multiplier here whereas the WebGL2 pass converts manual exposure as
/// <c>2^ev</c>; and <see cref="AdaptationSpeed"/> is normalised to (0, 1] whereas the WebGL2
/// pass drives the same blend with a raw <c>adaptationRate</c> (default 1.5).</para>
/// </remarks>
public sealed class ToneMapPassExecutor : IRenderPass
{
    private const int InvalidUniformLocation = -1;

    /// <summary>Zero texture handle — the "no texture bound" sentinel for a sampler unit.</summary>
    private const uint NoTexture = 0;

    private const int InputTextureUnit = 0;
    private const int ExposureHistoryTextureUnit = 1;

    private const string ExposureUniform = "u_exposure";
    private const string GammaUniform = "u_gamma";
    private const string OperatorUniform = "u_operator";
    private const string ExposureHistoryUniform = "u_exposureHistory";
    private const string UseExposureHistoryUniform = "u_useExposureHistory";

    private const string ExposureSourceUniform = "u_source";
    private const string ExposurePreviousUniform = "u_previousExposure";
    private const string ExposureKeyValueUniform = "u_keyValue";
    private const string ExposureMinUniform = "u_minExposure";
    private const string ExposureMaxUniform = "u_maxExposure";
    private const string ExposureSpeedUniform = "u_adaptationSpeed";
    private const string ExposureDeltaTimeUniform = "u_deltaTime";
    private const string ExposureHasPreviousUniform = "u_hasPreviousExposure";

    // WebGL2 tonemap defaults: average grey target and a +/-6 EV clamp window.
    private const float DefaultKeyValue = 0.18f;
    private const float DefaultMinExposureEv = -6f;
    private const float DefaultMaxExposureEv = 6f;

    private const float MaxAdaptationSpeed = 1f;

    private readonly GLProgram _shader;
    private readonly string _inputTextureName;
    private readonly string _outputTextureName;
    private readonly string _exposureHistoryReadName;
    private readonly string _exposureHistoryWriteName;
    private readonly bool _exposureHistoryWired;

    private GLProgram? _exposureHistoryShader;

    private ToneMapOperator _operator = ToneMapOperator.Aces;
    private float _exposure = 1.0f;
    private float _gamma = 2.2f;
    private float _adaptationSpeed = MaxAdaptationSpeed;
    private float _keyValue = DefaultKeyValue;
    private float _minExposureEv = DefaultMinExposureEv;
    private float _maxExposureEv = DefaultMaxExposureEv;
    private float _deltaTime;

    private readonly List<string> _reads = new(4);
    private readonly List<string> _writes = new(4);
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

    /// <summary>
    /// Initializes a new instance of the <see cref="ToneMapPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="shader">The tone mapping shader program.</param>
    /// <param name="inputTextureName">Name of the input HDR texture in the pass context.</param>
    /// <param name="outputTextureName">Name of the output LDR texture in the pass context.</param>
    /// <param name="exposureHistoryReadName">
    /// Name of the previous frame's exposure history texture in the pass context, or an empty
    /// string to disable exposure adaptation. Defaults to disabled.
    /// </param>
    /// <param name="exposureHistoryWriteName">
    /// Name of the exposure history texture to write this frame, or an empty string to disable
    /// exposure adaptation. Its framebuffer must be registered in the pass context as
    /// <c>&lt;exposureHistoryWriteName&gt;_fbo</c>. Defaults to disabled.
    /// </param>
    public ToneMapPassExecutor(
        string name,
        GLProgram shader,
        string inputTextureName = "scene_hdr",
        string outputTextureName = "scene_ldr",
        string exposureHistoryReadName = "",
        string exposureHistoryWriteName = "")
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(exposureHistoryReadName);
        ArgumentNullException.ThrowIfNull(exposureHistoryWriteName);

        Name = name;
        Kind = FramePassKind.ToneMap;
        _shader = shader;
        _inputTextureName = inputTextureName;
        _outputTextureName = outputTextureName;
        _exposureHistoryReadName = exposureHistoryReadName;
        _exposureHistoryWriteName = exposureHistoryWriteName;
        _exposureHistoryWired = exposureHistoryReadName.Length != 0 && exposureHistoryWriteName.Length != 0;

        _reads.Add(inputTextureName);
        _writes.Add(outputTextureName);

        if (_exposureHistoryWired)
        {
            _reads.Add(exposureHistoryReadName);
            _writes.Add(exposureHistoryWriteName);
        }
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

    /// <summary>Gets or sets the active tone mapping operator.</summary>
    public ToneMapOperator Operator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _operator;
        set => _operator = value;
    }

    /// <summary>Gets or sets the exposure multiplier.</summary>
    public float Exposure
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _exposure;
        set => _exposure = value;
    }

    /// <summary>Gets or sets the gamma correction exponent.</summary>
    public float Gamma
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _gamma;
        set => _gamma = value;
    }

    /// <summary>Gets the <see cref="ToneMapOperator"/> value handed to the <c>u_operator</c> uniform.</summary>
    public int OperatorId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (int)_operator;
    }

    /// <summary>Gets or sets the adaptation program that resolves exposure history. Null disables the adaptation stage.</summary>
    public GLProgram? ExposureHistoryShader
    {
        get => _exposureHistoryShader;
        set => _exposureHistoryShader = value;
    }

    /// <summary>
    /// Gets a value indicating whether both exposure history texture names were supplied, i.e.
    /// whether the pass is graph-wired for exposure adaptation.
    /// </summary>
    public bool HasExposureHistory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _exposureHistoryWired;
    }

    /// <summary>Gets the previous frame's exposure history texture name, or an empty string when disabled.</summary>
    public string ExposureHistoryReadName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _exposureHistoryReadName;
    }

    /// <summary>Gets the exposure history texture name written this frame, or an empty string when disabled.</summary>
    public string ExposureHistoryWriteName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _exposureHistoryWriteName;
    }

    /// <summary>
    /// Gets or sets the adaptation speed in (0, 1], where 1 snaps to the metered exposure within a
    /// single frame and small values converge slowly. Consumed as <c>u_adaptationSpeed</c>.
    /// </summary>
    public float AdaptationSpeed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _adaptationSpeed;
        set => _adaptationSpeed = value;
    }

    /// <summary>
    /// Gets or sets the mid-grey key the metered exposure targets, as a linear luminance.
    /// Consumed as <c>u_keyValue</c>.
    /// </summary>
    public float KeyValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _keyValue;
        set => _keyValue = value;
    }

    /// <summary>
    /// Gets or sets the lower clamp on the metered exposure, in EV. The shader applies
    /// <c>exp2(minExposure)</c> to obtain a linear multiplier. Consumed as <c>u_minExposure</c>.
    /// </summary>
    public float MinExposureEv
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _minExposureEv;
        set => _minExposureEv = value;
    }

    /// <summary>
    /// Gets or sets the upper clamp on the metered exposure, in EV. The shader applies
    /// <c>exp2(maxExposure)</c> to obtain a linear multiplier. Consumed as <c>u_maxExposure</c>.
    /// </summary>
    public float MaxExposureEv
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _maxExposureEv;
        set => _maxExposureEv = value;
    }

    /// <summary>
    /// Gets or sets the frame duration in seconds fed to the adaptation blend. The pass has no
    /// clock of its own, so the caller sets this per frame before execution.
    /// Consumed as <c>u_deltaTime</c>.
    /// </summary>
    public float DeltaTime
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _deltaTime;
        set => _deltaTime = value;
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

    /// <summary>
    /// Supplies the exposure adaptation program. The pass must also have been constructed with
    /// non-empty history read/write names, otherwise the program is never used.
    /// </summary>
    /// <param name="exposureHistoryShader">
    /// The adaptation program. It renders the adapted exposure into the red channel of the
    /// history write target. Never compiled here.
    /// </param>
    /// <returns>This instance for fluent chaining.</returns>
    public ToneMapPassExecutor WithExposureHistory(GLProgram exposureHistoryShader)
    {
        ArgumentNullException.ThrowIfNull(exposureHistoryShader);

        _exposureHistoryShader = exposureHistoryShader;
        return this;
    }

    /// <summary>
    /// Sets the adaptation speed.
    /// </summary>
    /// <param name="adaptationSpeed">The adaptation speed. Must be in (0, 1].</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ToneMapPassExecutor WithAdaptationSpeed(float adaptationSpeed)
    {
        _adaptationSpeed = adaptationSpeed;
        return this;
    }

    /// <summary>
    /// Sets the mid-grey key the metered exposure targets.
    /// </summary>
    /// <param name="keyValue">The key value as a linear luminance. Must be positive.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ToneMapPassExecutor WithKeyValue(float keyValue)
    {
        _keyValue = keyValue;
        return this;
    }

    /// <summary>
    /// Sets the lower clamp on the metered exposure.
    /// </summary>
    /// <param name="minExposureEv">The lower clamp in EV. Must not exceed <see cref="MaxExposureEv"/>.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ToneMapPassExecutor WithMinExposureEv(float minExposureEv)
    {
        _minExposureEv = minExposureEv;
        return this;
    }

    /// <summary>
    /// Sets the upper clamp on the metered exposure.
    /// </summary>
    /// <param name="maxExposureEv">The upper clamp in EV. Must not be below <see cref="MinExposureEv"/>.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ToneMapPassExecutor WithMaxExposureEv(float maxExposureEv)
    {
        _maxExposureEv = maxExposureEv;
        return this;
    }

    /// <summary>
    /// Sets the frame duration in seconds used by the adaptation blend.
    /// </summary>
    /// <param name="deltaTime">The frame duration in seconds. Must be non-negative.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ToneMapPassExecutor WithDeltaTime(float deltaTime)
    {
        _deltaTime = deltaTime;
        return this;
    }

    /// <inheritdoc/>
    public void Validate()
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

        if (_adaptationSpeed <= 0f || _adaptationSpeed > MaxAdaptationSpeed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Adaptation speed must be in (0, 1], got {_adaptationSpeed}", nameof(ToneMapPassExecutor));
        }

        if (_keyValue <= 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Exposure key value must be positive, got {_keyValue}", nameof(ToneMapPassExecutor));
        }

        if (_minExposureEv > _maxExposureEv)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Minimum exposure EV must not exceed maximum exposure EV, got min {_minExposureEv} and max {_maxExposureEv}",
                nameof(ToneMapPassExecutor));
        }

        if (_deltaTime < 0f)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Delta time must be non-negative, got {_deltaTime}", nameof(ToneMapPassExecutor));
        }

        if (_exposureHistoryWired && _exposureHistoryShader is null)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                "Exposure history textures are wired but no adaptation program was supplied; call WithExposureHistory",
                nameof(ToneMapPassExecutor));
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
            nameof(ToneMapPassExecutor));
    }

    /// <summary>
    /// Executes the tone mapping (plus optional exposure adaptation) on the given GL context.
    /// </summary>
    /// <param name="context">The GL context for issuing draw calls.</param>
    /// <param name="ctx">The pass execution context for resource resolution.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Execute(GLContext context, PassExecutionContext ctx)
    {
        context.AssertRenderThread();

        var gl = context.GL;
        var state = context.State;

        var inputTexture = ctx.GetTexture(_inputTextureName);
        var outputTexture = ctx.GetTexture(_outputTextureName);

        // The adaptation stage needs both a wired ping-pong pair and a caller-supplied program.
        bool useExposureHistory = _exposureHistoryWired && _exposureHistoryShader is not null;

        if (useExposureHistory)
        {
            ExecuteExposureAdaptation(gl, state, ctx, inputTexture);
        }

        // Bind output framebuffer.
        if (ctx.HasResource(_outputTextureName + "_fbo"))
        {
            state.BindFramebuffer(GLConst.Framebuffer, ctx.GetFramebuffer(_outputTextureName + "_fbo").Id);
        }

        state.SetViewport(0, 0, outputTexture.Width, outputTexture.Height);
        state.SetDepthTest(false);
        state.SetCullFace(false);

        // Bind tone mapping shader.
        state.UseProgram(_shader.Id);

        // Bind input HDR texture at unit 0.
        state.BindTexture2D(InputTextureUnit, inputTexture.Id);

        if (useExposureHistory)
        {
            // The freshly resolved exposure lives in the history write target.
            state.BindTexture2D(ExposureHistoryTextureUnit, ctx.GetTexture(_exposureHistoryWriteName).Id);
        }

        // Set uniforms.
        gl.Uniform1(_shader.GetUniformLocation(ExposureUniform), _exposure);
        gl.Uniform1(_shader.GetUniformLocation(GammaUniform), _gamma);
        gl.Uniform1(_shader.GetUniformLocation(OperatorUniform), OperatorId);

        int location = _shader.GetUniformLocation(ExposureHistoryUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, ExposureHistoryTextureUnit);
        }

        location = _shader.GetUniformLocation(UseExposureHistoryUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, useExposureHistory ? 1 : 0);
        }

        // Draw fullscreen triangle.
        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }

    /// <summary>
    /// Resolves a temporally smoothed exposure from the HDR input and the previous frame's
    /// history into the history write target, so the tone map stage can sample it at unit 1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void ExecuteExposureAdaptation(
        IGLApi gl,
        GLStateCache state,
        PassExecutionContext ctx,
        GLTexture inputTexture)
    {
        GLProgram program = _exposureHistoryShader!;

        var historyWriteTexture = ctx.GetTexture(_exposureHistoryWriteName);
        string historyFramebufferName = _exposureHistoryWriteName + "_fbo";

        if (!ctx.HasResource(historyFramebufferName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Exposure history framebuffer '{historyFramebufferName}' is not registered in the pass execution context",
                nameof(ToneMapPassExecutor));
        }

        // The native analogue of the WebGL2 texture-snapshot 'reused' flag: a history read
        // resource only exists once a previous frame has published one.
        bool hasPreviousExposure = ctx.HasResource(_exposureHistoryReadName);

        state.BindFramebuffer(GLConst.Framebuffer, ctx.GetFramebuffer(historyFramebufferName).Id);
        state.SetViewport(0, 0, historyWriteTexture.Width, historyWriteTexture.Height);
        state.SetDepthTest(false);
        state.SetCullFace(false);
        state.SetBlend(false);
        state.SetColorMask(true, true, true, true);

        state.UseProgram(program.Id);

        state.BindTexture2D(InputTextureUnit, inputTexture.Id);
        state.BindTexture2D(ExposureHistoryTextureUnit,
            hasPreviousExposure ? ctx.GetTexture(_exposureHistoryReadName).Id : NoTexture);

        int location = program.GetUniformLocation(ExposureSourceUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, InputTextureUnit);
        }

        location = program.GetUniformLocation(ExposurePreviousUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, ExposureHistoryTextureUnit);
        }

        location = program.GetUniformLocation(ExposureKeyValueUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, _keyValue);
        }

        location = program.GetUniformLocation(ExposureMinUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, _minExposureEv);
        }

        location = program.GetUniformLocation(ExposureMaxUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, _maxExposureEv);
        }

        location = program.GetUniformLocation(ExposureSpeedUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, _adaptationSpeed);
        }

        location = program.GetUniformLocation(ExposureDeltaTimeUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, _deltaTime);
        }

        location = program.GetUniformLocation(ExposureHasPreviousUniform);
        if (location != InvalidUniformLocation)
        {
            gl.Uniform1(location, hasPreviousExposure ? 1 : 0);
        }

        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}
