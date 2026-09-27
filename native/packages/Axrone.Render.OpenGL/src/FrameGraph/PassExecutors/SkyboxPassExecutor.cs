namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Renders a cubemap skybox background with a fullscreen triangle drawn at the far plane.
/// Depth testing uses LESS-EQUAL with depth writes disabled so scene geometry always wins
/// and the skybox only fills pixels no opaque pass claimed (parity with the web
/// <c>builtin-skybox</c> executor: color-mask on, depth-mask off, depth-func LEQUAL,
/// cull-face off).
/// </summary>
/// <remarks>
/// <para>Uses the overdrawn-triangle technique (3 VAO-less vertices, positions generated in
/// the vertex shader from <c>gl_VertexID</c>), so no <c>MeshGenerators</c> helper or vertex
/// buffer is needed. The shader is expected to reconstruct the view direction from the
/// uploaded view-projection matrix and sample the cubemap with it, emitting depth equal
/// to the far plane.</para>
/// <para>The view-projection matrix varies per frame, so it is supplied via the mutable
/// <see cref="ViewProjection"/> property (or <see cref="SetViewProjection"/>) before execution,
/// and uploaded to the <see cref="ViewProjectionUniform"/> uniform when the program exposes it.</para>
/// <para>The cubemap is a direct GPU handle (like <c>OpaquePassExecutor</c>'s program), not a
/// frame-graph resource, so no <c>Reads</c> entry is declared for it. Output target handling
/// mirrors <see cref="FullscreenQuadPassExecutor"/> exactly: rendering goes to the framebuffer
/// configured via <see cref="WithOutput"/>, and when no output is configured the current
/// binding (typically the scene target already bound by the graph) is left untouched instead
/// of forcing the default framebuffer, keeping offscreen HDR pipelines intact.</para>
/// </remarks>
public sealed class SkyboxPassExecutor : RenderPass
{
    private const int InvalidUniformLocation = -1;
    private const uint CubemapTextureUnit = 0;
    private const uint FullscreenTriangleVertexCount = 3;

    private readonly GLProgram _skyboxProgram;
    private readonly GLTexture _cubemap;
    private readonly string _viewProjectionUniform;
    private readonly string _cubemapUniform;

    private string? _outputFramebufferName;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkyboxPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="skyboxProgram">The caller-supplied skybox shader program. Never compiled here.</param>
    /// <param name="cubemap">The caller-supplied cubemap texture sampled by view direction.</param>
    /// <param name="viewProjectionUniform">The view-projection matrix uniform name in the skybox program.</param>
    /// <param name="cubemapUniform">The cubemap sampler uniform name in the skybox program.</param>
    public SkyboxPassExecutor(
        string name,
        GLProgram skyboxProgram,
        GLTexture cubemap,
        string viewProjectionUniform,
        string cubemapUniform = "u_skybox")
        : base(name, FramePassKind.Skybox)
    {
        ArgumentNullException.ThrowIfNull(skyboxProgram);
        ArgumentNullException.ThrowIfNull(cubemap);
        ArgumentNullException.ThrowIfNull(viewProjectionUniform);
        ArgumentNullException.ThrowIfNull(cubemapUniform);

        if (string.IsNullOrWhiteSpace(viewProjectionUniform))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "View-projection uniform name must not be empty", nameof(SkyboxPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(cubemapUniform))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Cubemap uniform name must not be empty", nameof(SkyboxPassExecutor));
        }

        _skyboxProgram = skyboxProgram;
        _cubemap = cubemap;
        _viewProjectionUniform = viewProjectionUniform;
        _cubemapUniform = cubemapUniform;
    }

    /// <summary>
    /// Gets or sets the view-projection matrix uploaded on each execution.
    /// Update per frame before <see cref="Execute"/> runs.
    /// </summary>
    public Matrix4x4 ViewProjection { get; set; } = Matrix4x4.Identity;

    /// <summary>Gets the view-projection uniform name.</summary>
    public string ViewProjectionUniform
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _viewProjectionUniform;
    }

    /// <summary>Gets the cubemap sampler uniform name.</summary>
    public string CubemapUniform
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _cubemapUniform;
    }

    /// <summary>Gets the output framebuffer name, or <see langword="null"/> for the current binding.</summary>
    public string? OutputFramebufferName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _outputFramebufferName;
    }

    /// <summary>
    /// Sets the output framebuffer name. If <see langword="null"/> or not set,
    /// rendering targets the currently bound framebuffer.
    /// </summary>
    /// <param name="framebufferName">The framebuffer resource name in the pass context.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public SkyboxPassExecutor WithOutput(string framebufferName)
    {
        _outputFramebufferName = framebufferName;
        Writes(framebufferName);
        return this;
    }

    /// <summary>
    /// Sets the view-projection matrix for the next execution.
    /// </summary>
    /// <param name="viewProjection">The camera view-projection matrix.</param>
    /// <returns>This executor for fluent chaining.</returns>
    public SkyboxPassExecutor SetViewProjection(Matrix4x4 viewProjection)
    {
        ViewProjection = viewProjection;
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (_skyboxProgram.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Skybox shader program has been disposed", nameof(SkyboxPassExecutor));
        }

        if (_cubemap.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Skybox cubemap texture has been disposed", nameof(SkyboxPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_viewProjectionUniform))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "View-projection uniform name must not be empty", nameof(SkyboxPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_cubemapUniform))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Cubemap uniform name must not be empty", nameof(SkyboxPassExecutor));
        }

        if (_cubemap.Target != GLConst.TextureCubeMap)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Skybox texture must be a cube map", nameof(SkyboxPassExecutor));
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

        // Bind the output framebuffer when one is configured (mirrors FullscreenQuadPassExecutor:
        // only rebind on an explicit, resolvable output; otherwise keep the current binding).
        if (_outputFramebufferName is not null && ctx.HasResource(_outputFramebufferName))
        {
            var fbo = ctx.GetFramebuffer(_outputFramebufferName);
            state.BindFramebuffer(GLConst.Framebuffer, fbo.Id);
            state.SetViewport(0, 0, fbo.Width, fbo.Height);
        }

        // Far-plane background setup: depth test on with LESS-EQUAL, no depth writes,
        // no blending, no culling; color writes on.
        state.SetDepthTest(true);
        state.SetDepthFunc(GLConst.Lequal);
        state.SetDepthMask(false);
        state.SetBlend(false);
        state.SetCullFace(false);
        state.SetColorMask(true, true, true, true);

        // Bind the caller-supplied skybox program.
        state.UseProgram(_skyboxProgram.Id);

        // Bind the cubemap at unit 0.
        state.ActiveTexture(CubemapTextureUnit);
        state.BindTexture(GLConst.TextureCubeMap, _cubemap.Id);

        // Point the sampler at the cubemap unit when the program exposes the uniform.
        int cubemapLocation = _skyboxProgram.GetUniformLocation(_cubemapUniform);
        if (cubemapLocation != InvalidUniformLocation)
        {
            gl.Uniform1(cubemapLocation, (int)CubemapTextureUnit);
        }

        // Upload the view-projection matrix when the program exposes the uniform.
        int viewProjectionLocation = _skyboxProgram.GetUniformLocation(_viewProjectionUniform);
        if (viewProjectionLocation != InvalidUniformLocation)
        {
            unsafe
            {
                Matrix4x4 matrix = ViewProjection;
                gl.UniformMatrix4(viewProjectionLocation, 1, false, &matrix.M11);
            }
        }

        // Draw the fullscreen triangle (3 VAO-less vertices, generated in the vertex shader).
        state.BindVertexArray(0);
        gl.DrawArrays(GLConst.Triangles, 0, FullscreenTriangleVertexCount);
    }
}
