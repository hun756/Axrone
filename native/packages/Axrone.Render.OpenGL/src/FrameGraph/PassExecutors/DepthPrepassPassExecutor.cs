namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Depth-only pre-pass that primes the depth buffer so later opaque passes benefit from early-Z.
/// Renders caller-supplied meshes with a caller-supplied depth-only program into the depth
/// target framebuffer resolved via the pass execution context. Color writes are disabled,
/// depth writes are enabled, depth func is LESS, and back faces are culled (parity with the
/// web <c>builtin-depth-prepass</c> executor: color-mask off, depth-mask on).
/// </summary>
/// <remarks>
/// <para>The view-projection matrix varies per frame, so it is supplied via the mutable
/// <see cref="ViewProjection"/> property (or <see cref="SetViewProjection"/>) before execution,
/// and uploaded to the <see cref="ViewProjectionUniform"/> uniform when the program exposes it.</para>
/// </remarks>
public sealed class DepthPrepassPassExecutor : RenderPass
{
    private const int InvalidUniformLocation = -1;

    private readonly GLProgram _depthProgram;
    private readonly string _viewProjectionUniform;
    private readonly List<GLMesh> _meshes;
    private readonly string _depthTargetName;

    /// <summary>
    /// Initializes a new instance of the <see cref="DepthPrepassPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="depthProgram">The caller-supplied depth-only shader program. Never compiled here.</param>
    /// <param name="viewProjectionUniform">The view-projection matrix uniform name in the depth program.</param>
    /// <param name="meshes">The meshes to render into the depth target.</param>
    /// <param name="depthTargetName">The depth target framebuffer resource name in the pass context.</param>
    public DepthPrepassPassExecutor(
        string name,
        GLProgram depthProgram,
        string viewProjectionUniform,
        IReadOnlyList<GLMesh> meshes,
        string depthTargetName)
        : base(name, FramePassKind.DepthPrepass)
    {
        ArgumentNullException.ThrowIfNull(depthProgram);
        ArgumentNullException.ThrowIfNull(viewProjectionUniform);
        ArgumentNullException.ThrowIfNull(meshes);
        ArgumentNullException.ThrowIfNull(depthTargetName);

        if (string.IsNullOrWhiteSpace(viewProjectionUniform))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "View-projection uniform name must not be empty", nameof(DepthPrepassPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(depthTargetName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Depth target name must not be empty", nameof(DepthPrepassPassExecutor));
        }

        _depthProgram = depthProgram;
        _viewProjectionUniform = viewProjectionUniform;
        _depthTargetName = depthTargetName;

        _meshes = new List<GLMesh>(meshes.Count);
        for (int i = 0; i < meshes.Count; i++)
        {
            GLMesh mesh = meshes[i];
            ArgumentNullException.ThrowIfNull(mesh);
            _meshes.Add(mesh);
        }

        Writes(depthTargetName);
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

    /// <summary>Gets the depth target framebuffer resource name.</summary>
    public string DepthTargetName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _depthTargetName;
    }

    /// <summary>Gets the meshes rendered in this pass.</summary>
    public IReadOnlyList<GLMesh> Meshes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _meshes;
    }

    /// <summary>Gets the number of meshes in this pass.</summary>
    public int MeshCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _meshes.Count;
    }

    /// <summary>
    /// Adds a mesh to be rendered in the depth pre-pass.
    /// </summary>
    /// <param name="mesh">The mesh to add.</param>
    /// <returns>This executor for fluent chaining.</returns>
    public DepthPrepassPassExecutor AddMesh(GLMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        _meshes.Add(mesh);
        return this;
    }

    /// <summary>
    /// Sets the view-projection matrix for the next execution.
    /// </summary>
    /// <param name="viewProjection">The camera view-projection matrix.</param>
    /// <returns>This executor for fluent chaining.</returns>
    public DepthPrepassPassExecutor SetViewProjection(Matrix4x4 viewProjection)
    {
        ViewProjection = viewProjection;
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (_depthProgram.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Depth shader program has been disposed", nameof(DepthPrepassPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_viewProjectionUniform))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "View-projection uniform name must not be empty", nameof(DepthPrepassPassExecutor));
        }

        if (string.IsNullOrWhiteSpace(_depthTargetName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Depth target name must not be empty", nameof(DepthPrepassPassExecutor));
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override void Execute(GLContext context, PassExecutionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(ctx);

        context.AssertRenderThread();

        if (_meshes.Count == 0)
        {
            return;
        }

        var gl = context.GL;
        var state = context.State;

        // Bind the depth target framebuffer (default framebuffer when unresolved).
        uint fboId = 0;
        GLFramebuffer? target = null;
        if (ctx.HasResource(_depthTargetName))
        {
            target = ctx.GetFramebuffer(_depthTargetName);
            fboId = target.Id;
        }

        state.BindFramebuffer(GLConst.Framebuffer, fboId);
        if (target is not null)
        {
            state.SetViewport(0, 0, target.Width, target.Height);
        }

        // Early-Z setup: no color writes, depth writes on, LESS, back-face culling.
        state.SetDepthTest(true);
        state.SetDepthFunc(GLConst.Less);
        state.SetDepthMask(true);
        state.SetBlend(false);
        state.SetCullFace(true);
        state.SetCullMode(GLConst.Back);
        state.SetColorMask(false, false, false, false);

        // Bind the caller-supplied depth-only program.
        state.UseProgram(_depthProgram.Id);

        // Upload the view-projection matrix when the program exposes the uniform.
        int location = _depthProgram.GetUniformLocation(_viewProjectionUniform);
        if (location != InvalidUniformLocation)
        {
            unsafe
            {
                Matrix4x4 matrix = ViewProjection;
                gl.UniformMatrix4(location, 1, false, &matrix.M11);
            }
        }

        // Draw all meshes.
        var meshSpan = CollectionsMarshal.AsSpan(_meshes);
        for (int i = 0; i < meshSpan.Length; i++)
        {
            ref var mesh = ref meshSpan[i];
            mesh.Draw();
        }

        // Restore color writes so later passes are unaffected.
        state.SetColorMask(true, true, true, true);
    }
}
