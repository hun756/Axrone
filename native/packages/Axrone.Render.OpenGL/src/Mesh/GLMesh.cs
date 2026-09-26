
namespace Axrone.Render.OpenGL.Mesh;

/// <summary>
/// High-level mesh resource that owns vertex/index buffers and a VAO.
/// Provides draw methods and bounds computation.
/// Implements <see cref="IGLResource"/> for context-loss recovery.
/// </summary>
public sealed class GLMesh : IGLResource, IDisposable
{
    private readonly GLContext _context;
    private readonly byte[] _vertexDataSnapshot;
    private readonly byte[]? _indexDataSnapshot;
    private int _disposed;

    /// <summary>
    /// Gets the vertex array object for this mesh.
    /// </summary>
    public GLVertexArray VertexArray { get; private set; }

    /// <summary>
    /// Gets the vertex buffer.
    /// </summary>
    public GLBuffer VertexBuffer { get; private set; }

    /// <summary>
    /// Gets the index buffer, or <c>null</c> if this mesh is not indexed.
    /// </summary>
    public GLBuffer? IndexBuffer { get; private set; }

    /// <summary>
    /// Gets the vertex layout descriptor.
    /// </summary>
    public VertexLayout Layout { get; }

    /// <summary>
    /// Gets the vertex count.
    /// </summary>
    public int VertexCount { get; }

    /// <summary>
    /// Gets the index count (0 if not indexed).
    /// </summary>
    public int IndexCount { get; }

    /// <summary>
    /// Gets a value indicating whether this mesh uses an index buffer.
    /// </summary>
    public bool IsIndexed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IndexBuffer != null;
    }

    /// <summary>
    /// Gets the axis-aligned bounding box of this mesh.
    /// </summary>
    public Bounds3D Bounds { get; }

    /// <summary>
    /// Gets the debug label.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the primitive topology used for drawing.
    /// </summary>
    public uint Topology { get; }

    /// <inheritdoc/>
    public int RegistrySequence { get; set; }

    /// <inheritdoc/>
    public int RebuildPriority => 20;

    /// <summary>
    /// Gets a value indicating whether this mesh has been disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLMesh"/> class.
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="layout">The vertex layout descriptor.</param>
    /// <param name="vertexData">The raw vertex data to upload.</param>
    /// <param name="indexData">Optional raw index data to upload.</param>
    /// <param name="bounds">The axis-aligned bounding box.</param>
    /// <param name="topology">The primitive topology (default: <see cref="GLConst.Triangles"/>).</param>
    /// <param name="label">The debug label.</param>
    public GLMesh(
        GLContext context,
        VertexLayout layout,
        ReadOnlySpan<byte> vertexData,
        ReadOnlySpan<byte> indexData,
        Bounds3D bounds,
        uint topology = GLConst.Triangles,
        string label = "mesh")
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(layout);

        if (vertexData.Length == 0)
            ThrowHelper.ThrowInvalidArgument("Vertex data cannot be empty");

        if (vertexData.Length % layout.VertexStride != 0)
            ThrowHelper.ThrowInvalidArgument(
                $"Vertex data length ({vertexData.Length}) is not a multiple of vertex stride ({layout.VertexStride})");

        _context = context;
        Layout = layout;
        VertexCount = vertexData.Length / layout.VertexStride;
        Bounds = bounds;
        Label = label;
        Topology = topology;

        // Snapshot for context-loss recovery
        _vertexDataSnapshot = vertexData.ToArray();

        // Create vertex buffer and upload data
        VertexBuffer = new GLBuffer(context, GLConst.ArrayBuffer, GLConst.StaticDraw, vertexData.Length, $"{label}_vbo");
        VertexBuffer.Update(vertexData);

        // Create index buffer if index data provided
        if (!indexData.IsEmpty)
        {
            IndexCount = indexData.Length / sizeof(uint);
            _indexDataSnapshot = indexData.ToArray();
            IndexBuffer = new GLBuffer(context, GLConst.ElementArrayBuffer, GLConst.StaticDraw, indexData.Length, $"{label}_ibo");
            IndexBuffer.Update(indexData);
        }

        // Create VAO and configure layout
        VertexArray = IndexBuffer != null
            ? layout.BuildVertexArray(context, VertexBuffer, IndexBuffer, VertexCount, IndexCount)
            : layout.BuildVertexArray(context, VertexBuffer, VertexCount);

        // Register this mesh with the resource registry
        context.Registry.Register(this);
    }

    /// <summary>
    /// Draws this mesh by binding its VAO and issuing the draw call.
    /// </summary>
    public void Draw()
    {
        EnsureAlive();
        VertexArray.Draw(Topology);
    }

    /// <summary>
    /// Draws this mesh with instancing.
    /// </summary>
    /// <param name="instanceCount">The number of instances to draw.</param>
    public void DrawInstanced(int instanceCount)
    {
        EnsureAlive();

        if (instanceCount <= 0)
            ThrowHelper.ThrowInvalidArgument("Instance count must be positive");

        VertexArray.DrawInstanced(instanceCount, Topology);
    }

    /// <inheritdoc/>
    public void Invalidate()
    {
        VertexArray.Invalidate();
        VertexBuffer.Invalidate();
        IndexBuffer?.Invalidate();
    }

    /// <inheritdoc/>
    public void OnContextLost()
    {
        VertexArray.OnContextLost();
        VertexBuffer.OnContextLost();
        IndexBuffer?.OnContextLost();
    }

    /// <inheritdoc/>
    public void Rebuild()
    {
        if (IsDisposed)
            return;

        // Rebuild buffers first (they have lower priority = 10)
        VertexBuffer.Rebuild();
        VertexBuffer.Update(_vertexDataSnapshot);

        if (IndexBuffer != null && _indexDataSnapshot != null)
        {
            IndexBuffer.Rebuild();
            IndexBuffer.Update(_indexDataSnapshot);
        }

        // Rebuild VAO (priority = 20)
        VertexArray.OnContextRestored();
        VertexArray.Rebuild();

        // Reconfigure the VAO layout
        if (IndexBuffer != null)
        {
            VertexArray.ConfigureLayout(VertexBuffer, IndexBuffer, Layout.Attributes, VertexCount, IndexCount);
        }
        else
        {
            VertexArray.ConfigureLayout(VertexBuffer, Layout.Attributes, VertexCount);
        }
    }

    /// <inheritdoc/>
    public void OnContextRestored() => Rebuild();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            VertexArray.Dispose();
            VertexBuffer.Dispose();
            IndexBuffer?.Dispose();

            _context.Registry.Unregister(this);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureAlive()
    {
        if (IsDisposed)
            ThrowHelper.ThrowObjectDisposed(nameof(GLMesh));

        if (_context.IsLost)
            ThrowHelper.ThrowContextLost();
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"GLMesh: \"{Label}\", Vertices={VertexCount}, Indices={IndexCount}, " +
        $"Indexed={IsIndexed}, Topology=0x{Topology:X4}, Bounds={Bounds}";
}
