namespace Axrone.Scene.Rendering.OpenGL;

/// <summary>
/// Renders particle systems as point sprites. Particles are expanded from the
/// scene bridge's dense storage into an interleaved vertex buffer and submitted
/// as one point draw per system. Buffers and scratch arrays are reused, so a
/// steady frame allocates nothing.
/// </summary>
public sealed class SceneGlParticleBatchRenderer : IDisposable
{
    private const int InitialParticleCapacity = 1024;
    private const string ViewProjectionUniform = "u_ViewProjection";

    private const string VertexShaderSource = """
        #version 330 core
        layout(location = 0) in vec3 a_Position;
        layout(location = 1) in vec4 a_Color;
        layout(location = 2) in float a_Size;

        uniform mat4 u_ViewProjection;

        out vec4 v_Color;

        void main()
        {
            v_Color = a_Color;
            gl_Position = vec4(a_Position, 1.0) * u_ViewProjection;
            gl_PointSize = max(a_Size, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in vec4 v_Color;

        out vec4 fragColor;

        void main()
        {
            vec2 offset = gl_PointCoord - vec2(0.5);
            float radius = length(offset);
            if (radius > 0.5)
            {
                discard;
            }

            float alpha = smoothstep(0.5, 0.25, radius);
            fragColor = vec4(v_Color.rgb, v_Color.a * alpha);
        }
        """;

    private static readonly int VertexStride = Unsafe.SizeOf<ParticleVertex>();

    private static readonly VertexAttribute[] s_layout =
    [
        new VertexAttribute(0, 3, GLConst.Float, false, VertexStride, 0, 0, false),
        new VertexAttribute(1, 4, GLConst.Float, false, VertexStride, 12, 0, false),
        new VertexAttribute(2, 1, GLConst.Float, false, VertexStride, 28, 0, false)
    ];

    private readonly GLContext _context;
    private readonly SceneGlShader _shader;
    private readonly GLVertexArray _vao;
    private readonly GLBuffer _vertexBuffer;
    private ParticleVertex[] _vertices;
    private bool _disposed;

    /// <summary>Creates a particle point renderer.</summary>
    public SceneGlParticleBatchRenderer(GLContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _shader = new SceneGlShader(
            context,
            "scene-particles",
            VertexShaderSource,
            FragmentShaderSource);

        _vertices = new ParticleVertex[InitialParticleCapacity];
        _vertexBuffer = new GLBuffer(
            context,
            GLConst.ArrayBuffer,
            GLConst.DynamicDraw,
            InitialParticleCapacity * VertexStride,
            "scene-particle-vertices");
        _vao = new GLVertexArray(context, "scene-particles");
        _vao.ConfigureLayout(_vertexBuffer, s_layout, InitialParticleCapacity);
    }

    /// <summary>Renders one particle system and returns the draw count.</summary>
    public int Render(
        in SceneParticleSnapshot snapshot,
        SceneCameraFrame camera,
        SceneRenderPassDefinition pass,
        SceneRenderFrameState frameState)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(pass);
        ArgumentNullException.ThrowIfNull(frameState);

        if (snapshot.Count == 0)
        {
            return 0;
        }

        int count = AvailableCount(snapshot);
        if (count == 0)
        {
            return 0;
        }

        _context.AssertRenderThread();
        EnsureCapacity(count);
        Fill(snapshot, count);

        _context.State.SetViewport(0, 0, camera.ViewportWidth, camera.ViewportHeight);
        ApplyState(pass);

        _shader.Bind();
        _shader.SetMatrix4(ViewProjectionUniform, camera.ViewProjectionMatrix);
        _vertexBuffer.Update(MemoryMarshal.AsBytes(_vertices.AsSpan(0, count)));
        _vao.Bind();

        unsafe
        {
            _context.GL.DrawArrays(GLConst.Points, 0, (uint)count);
        }

        frameState.RecordDraw(triangleCount: 0);
        return 1;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _vao.Dispose();
        _vertexBuffer.Dispose();
        _shader.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Clamps the requested count to the shortest stream so a malformed source
    /// can never read past its storage.
    /// </summary>
    private static int AvailableCount(in SceneParticleSnapshot snapshot)
    {
        int count = snapshot.Count;
        count = Math.Min(count, snapshot.PosX.Length);
        count = Math.Min(count, snapshot.PosY.Length);
        count = Math.Min(count, snapshot.PosZ.Length);
        count = Math.Min(count, snapshot.ColR.Length);
        count = Math.Min(count, snapshot.ColG.Length);
        count = Math.Min(count, snapshot.ColB.Length);
        count = Math.Min(count, snapshot.ColA.Length);
        return Math.Min(count, snapshot.Size.Length);
    }

    private void EnsureCapacity(int count)
    {
        if (count <= _vertices.Length)
        {
            return;
        }

        int capacity = _vertices.Length;
        while (capacity < count)
        {
            capacity *= 2;
        }

        Array.Resize(ref _vertices, capacity);
        _vertexBuffer.Resize(capacity * VertexStride);
    }

    private void Fill(in SceneParticleSnapshot snapshot, int count)
    {
        ReadOnlySpan<float> posX = snapshot.PosX;
        ReadOnlySpan<float> posY = snapshot.PosY;
        ReadOnlySpan<float> posZ = snapshot.PosZ;
        ReadOnlySpan<float> colR = snapshot.ColR;
        ReadOnlySpan<float> colG = snapshot.ColG;
        ReadOnlySpan<float> colB = snapshot.ColB;
        ReadOnlySpan<float> colA = snapshot.ColA;
        ReadOnlySpan<float> size = snapshot.Size;

        for (int i = 0; i < count; i++)
        {
            _vertices[i] = new ParticleVertex(
                new Vec3(posX[i], posY[i], posZ[i]),
                new Vec4(colR[i], colG[i], colB[i], colA[i]),
                size[i]);
        }
    }

    private void ApplyState(SceneRenderPassDefinition pass)
    {
        bool blend = pass.Blend ?? true;

        _context.State.SetDepthTest(pass.DepthTest ?? true);
        _context.State.SetDepthMask(false);
        _context.State.SetCullFace(pass.Cull ?? false);
        _context.State.SetCullMode(GLConst.Back);
        _context.State.SetBlend(blend);

        if (blend)
        {
            _context.State.SetBlendFuncSeparate(
                GLConst.SrcAlpha,
                GLConst.OneMinusSrcAlpha,
                GLConst.One,
                GLConst.OneMinusSrcAlpha);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new SceneDisposedException(nameof(SceneGlParticleBatchRenderer));
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private readonly struct ParticleVertex
    {
        public ParticleVertex(in Vec3 position, in Vec4 color, float size)
        {
            Position = position;
            Color = color;
            Size = size;
        }

        public readonly Vec3 Position;

        public readonly Vec4 Color;

        public readonly float Size;
    }
}
