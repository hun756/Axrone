namespace Axrone.Render.OpenGL.Resources;

/// <summary>
/// Vertex Array Object (VAO) resource with layout configuration and draw methods.
/// Zero-allocation hot path, context-loss recovery enabled.
/// </summary>
public sealed class GLVertexArray : IGLResource, IDisposable
{
    private readonly GLContext _context;
    private readonly IGLApi _gl;
    private int _disposed;

    /// <summary>Gets the VAO handle.</summary>
    public uint Id { get; private set; }

    /// <summary>Gets the vertex count.</summary>
    public int VertexCount { get; private set; }

    /// <summary>Gets the index count (0 if not indexed).</summary>
    public int IndexCount { get; private set; }

    /// <summary>Gets the debug label.</summary>
    public string Label { get; private set; }

    /// <inheritdoc/>
    public int RegistrySequence { get; set; }

    /// <inheritdoc/>
    public int RebuildPriority => 20;

    /// <summary>Gets a value indicating whether this VAO has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLVertexArray"/> class.
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="label">The debug label.</param>
    public GLVertexArray(GLContext context, string label = "vao")
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _gl = context.GL;
        Label = label;

        Id = _gl.GenVertexArray();
        context.Registry.Register(this);

        if (context.DebugLabelsEnabled)
        {
            _gl.ObjectLabel(GLConst.VertexArray, Id, label);
        }
    }

    /// <summary>
    /// Configures the vertex layout with multiple attributes.
    /// </summary>
    /// <param name="vertexBuffer">The vertex buffer.</param>
    /// <param name="attributes">The vertex attributes.</param>
    /// <param name="vertexCount">The vertex count.</param>
    /// <returns>This VAO for fluent chaining.</returns>
    public unsafe GLVertexArray ConfigureLayout(
        GLBuffer vertexBuffer,
        ReadOnlySpan<VertexAttribute> attributes,
        int vertexCount)
    {
        ArgumentNullException.ThrowIfNull(vertexBuffer);
        EnsureAlive();
        _context.AssertRenderThread();

        VertexCount = vertexCount;
        IndexCount = 0;

        _context.State.BindVertexArray(Id);
        _context.State.BindBuffer(GLConst.ArrayBuffer, vertexBuffer.Id);

        for (int i = 0; i < attributes.Length; i++)
        {
            ref readonly var attr = ref attributes[i];
            _gl.EnableVertexAttribArray((uint)attr.Location);

            if (attr.IsInteger)
            {
                _gl.VertexAttribIPointer(
                    (uint)attr.Location,
                    attr.ComponentCount,
                    attr.Type,
                    (uint)attr.Stride,
                    (void*)attr.Offset);
            }
            else
            {
                _gl.VertexAttribPointer(
                    (uint)attr.Location,
                    attr.ComponentCount,
                    attr.Type,
                    attr.Normalized,
                    (uint)attr.Stride,
                    (void*)attr.Offset);
            }

            if (attr.Divisor > 0)
            {
                _gl.VertexAttribDivisor((uint)attr.Location, (uint)attr.Divisor);
            }
        }

        _context.State.BindVertexArray(0);
        return this;
    }

    /// <summary>
    /// Configures the vertex layout with an index buffer.
    /// </summary>
    /// <param name="vertexBuffer">The vertex buffer.</param>
    /// <param name="indexBuffer">The index buffer.</param>
    /// <param name="attributes">The vertex attributes.</param>
    /// <param name="vertexCount">The vertex count.</param>
    /// <param name="indexCount">The index count.</param>
    /// <returns>This VAO for fluent chaining.</returns>
    public GLVertexArray ConfigureLayout(
        GLBuffer vertexBuffer,
        GLBuffer indexBuffer,
        ReadOnlySpan<VertexAttribute> attributes,
        int vertexCount,
        int indexCount)
    {
        ArgumentNullException.ThrowIfNull(indexBuffer);
        ConfigureLayout(vertexBuffer, attributes, vertexCount);
        IndexCount = indexCount;

        _context.State.BindVertexArray(Id);
        _context.State.BindBuffer(GLConst.ElementArrayBuffer, indexBuffer.Id);
        _context.State.BindVertexArray(0);

        return this;
    }

    /// <summary>
    /// Draws the vertex array.
    /// </summary>
    /// <param name="topology">The primitive topology.</param>
    /// <returns>This VAO for fluent chaining.</returns>
    public unsafe GLVertexArray Draw(uint topology = GLConst.Triangles)
    {
        EnsureAlive();
        _context.AssertRenderThread();

        _context.State.BindVertexArray(Id);

        if (IndexCount > 0)
        {
            _gl.DrawElements(topology, (uint)IndexCount, GLConst.UnsignedInt, null);
        }
        else
        {
            _gl.DrawArrays(topology, 0, (uint)VertexCount);
        }

        return this;
    }

    /// <summary>
    /// Draws the vertex array with instancing.
    /// </summary>
    /// <param name="instanceCount">The instance count.</param>
    /// <param name="topology">The primitive topology.</param>
    /// <returns>This VAO for fluent chaining.</returns>
    public unsafe GLVertexArray DrawInstanced(int instanceCount, uint topology = GLConst.Triangles)
    {
        EnsureAlive();
        _context.AssertRenderThread();

        _context.State.BindVertexArray(Id);

        if (IndexCount > 0)
        {
            _gl.DrawElementsInstanced(topology, (uint)IndexCount, GLConst.UnsignedInt, null, (uint)instanceCount);
        }
        else
        {
            _gl.DrawArraysInstanced(topology, 0, (uint)VertexCount, (uint)instanceCount);
        }

        return this;
    }

    /// <summary>
    /// Binds this VAO.
    /// </summary>
    /// <returns>This VAO for fluent chaining.</returns>
    public GLVertexArray Bind()
    {
        EnsureAlive();
        _context.State.BindVertexArray(Id);
        return this;
    }

    /// <summary>
    /// Unbinds this VAO.
    /// </summary>
    /// <returns>This VAO for fluent chaining.</returns>
    public GLVertexArray Unbind()
    {
        _context.State.BindVertexArray(0);
        return this;
    }

    /// <inheritdoc/>
    public void OnContextLost()
    {
        Id = 0;
    }

    /// <inheritdoc/>
    public void Invalidate() => Id = 0;

    /// <inheritdoc/>
    public void Rebuild() => OnContextRestored();

    /// <inheritdoc/>
    public void OnContextRestored()
    {
        if (IsDisposed) return;

        Id = _gl.GenVertexArray();

        if (_context.DebugLabelsEnabled && !string.IsNullOrEmpty(Label))
        {
            _gl.ObjectLabel(GLConst.VertexArray, Id, Label);
        }
    }

    private void EnsureAlive()
    {
        if (IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "VAO has been disposed", nameof(GLVertexArray));
        }

        if (_context.Lifecycle.IsLost)
        {
            ThrowHelper.Throw(RenderErrorCode.ContextLost, "GL context is lost", nameof(GLVertexArray));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        if (Id != 0)
        {
            _gl.DeleteVertexArray(Id);
            Id = 0;
        }

        _context.Registry.Unregister(this);
    }
}

/// <summary>
/// Vertex attribute descriptor.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct VertexAttribute(
    int Location,
    int ComponentCount,
    uint Type,
    bool Normalized,
    int Stride,
    int Offset,
    int Divisor = 0,
    bool IsInteger = false)
{
    /// <summary>
    /// Creates a float attribute.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VertexAttribute Float(int location, int componentCount, int stride, int offset, bool normalized = false, int divisor = 0) =>
        new(location, componentCount, GLConst.Float, normalized, stride, offset, divisor, false);

    /// <summary>
    /// Creates an integer attribute.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VertexAttribute Int(int location, int componentCount, int stride, int offset, int divisor = 0) =>
        new(location, componentCount, GLConst.Int, false, stride, offset, divisor, true);

    /// <summary>
    /// Creates an unsigned integer attribute.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VertexAttribute UInt(int location, int componentCount, int stride, int offset, int divisor = 0) =>
        new(location, componentCount, GLConst.UnsignedInt, false, stride, offset, divisor, true);
}
