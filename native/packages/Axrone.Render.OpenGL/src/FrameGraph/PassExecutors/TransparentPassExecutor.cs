namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Renders transparent geometry with alpha blending, back-to-front sorting.
/// </summary>
public sealed class TransparentPassExecutor : RenderPass
{
    private readonly string? _targetFramebufferName;
    private readonly GLProgram _program;
    private readonly List<TransparentMeshEntry> _meshEntries = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TransparentPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="program">The shader program to use.</param>
    /// <param name="targetFramebufferName">The target framebuffer resource name, or null for the default framebuffer.</param>
    public TransparentPassExecutor(
        string name,
        GLProgram program,
        string? targetFramebufferName = null)
        : base(name, FramePassKind.Transparent)
    {
        ArgumentNullException.ThrowIfNull(program);
        _program = program;
        _targetFramebufferName = targetFramebufferName;

        if (targetFramebufferName != null)
        {
            Reads(targetFramebufferName);
        }
    }

    /// <summary>
    /// Adds a mesh with its world position for distance-based sorting.
    /// </summary>
    /// <param name="mesh">The mesh to add.</param>
    /// <param name="worldPosition">The world position for sorting.</param>
    /// <returns>This executor for fluent chaining.</returns>
    public TransparentPassExecutor AddMesh(GLMesh mesh, Vector3 worldPosition)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        _meshEntries.Add(new TransparentMeshEntry(mesh, worldPosition));
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (_program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Shader program has been disposed", nameof(TransparentPassExecutor));
        }
    }

    /// <inheritdoc/>
    public override void Execute(GLContext context, PassExecutionContext ctx)
    {
        context.AssertRenderThread();

        if (_meshEntries.Count == 0)
            return;

        var gl = context.GL;
        var state = context.State;

        // Bind target framebuffer
        uint fboId = 0;
        if (_targetFramebufferName != null && ctx.HasResource(_targetFramebufferName))
        {
            var fbo = ctx.GetFramebuffer(_targetFramebufferName);
            fboId = fbo.Id;
        }

        state.BindFramebuffer(GLConst.Framebuffer, fboId);

        // Configure state for transparent rendering
        state.SetDepthTest(true);
        state.SetDepthFunc(GLConst.Lequal);
        state.SetDepthMask(false); // Don't write depth for transparent objects
        state.SetBlend(true);
        state.SetBlendFuncSeparate(GLConst.SrcAlpha, GLConst.OneMinusSrcAlpha, GLConst.SrcAlpha, GLConst.OneMinusSrcAlpha);
        state.SetBlendEquationSeparate(GLConst.FuncAdd, GLConst.FuncAdd);
        state.SetCullFace(true);
        state.SetCullMode(GLConst.Back);
        state.SetColorMask(true, true, true, true);

        // Bind shader program
        state.UseProgram(_program.Id);

        // Sort back-to-front for correct alpha blending
        // Use a simple sort by distance (squared distance to avoid sqrt)
        var entries = CollectionsMarshal.AsSpan(_meshEntries);
        entries.Sort(static (a, b) =>
        {
            float distA = a.WorldPosition.LengthSquared();
            float distB = b.WorldPosition.LengthSquared();
            return distB.CompareTo(distA); // Back-to-front: larger distance first
        });

        // Draw sorted meshes
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i].Mesh.Draw();
        }
    }

    /// <summary>
    /// Represents a mesh entry with its world position for sorting.
    /// </summary>
    private readonly struct TransparentMeshEntry
    {
        /// <summary>
        /// Gets the mesh.
        /// </summary>
        public readonly GLMesh Mesh;

        /// <summary>
        /// Gets the world position.
        /// </summary>
        public readonly Vector3 WorldPosition;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransparentMeshEntry"/> struct.
        /// </summary>
        /// <param name="mesh">The mesh.</param>
        /// <param name="worldPosition">The world position.</param>
        public TransparentMeshEntry(GLMesh mesh, Vector3 worldPosition)
        {
            Mesh = mesh;
            WorldPosition = worldPosition;
        }
    }
}
