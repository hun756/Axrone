namespace Axrone.Render.OpenGL.Batch;

/// <summary>
/// Instance data for batched rendering.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 16)]
public record struct InstanceData(Matrix4x4 World, Vector4 Color, Vector4 Custom);

/// <summary>
/// High-performance batch renderer using GPU instancing.
/// </summary>
public sealed class BatchRenderer : IDisposable
{
    private readonly GLContext _context;
    private readonly GLBuffer _instanceBuffer;
    private readonly GLVertexArray _vao;
    private readonly InstanceData[] _instances;
    private int _instanceCount;
    private int _visibleCount;
    private bool _needsUpdate;
    private int _isDisposed;

    /// <summary>
    /// Gets the maximum batch size.
    /// </summary>
    public int MaxBatchSize { get; }

    /// <summary>
    /// Gets the visible instance count.
    /// </summary>
    public int VisibleCount => _visibleCount;

    /// <summary>
    /// Gets a value indicating whether the renderer has been disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchRenderer"/> class.
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="maxBatchSize">The maximum batch size.</param>
    public BatchRenderer(GLContext context, int maxBatchSize = 1024)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (maxBatchSize <= 0)
            ThrowHelper.ThrowInvalidArgument("Max batch size must be positive");

        _context = context;
        MaxBatchSize = maxBatchSize;
        _instances = GC.AllocateArray<InstanceData>(maxBatchSize, pinned: true);

        int byteSize = maxBatchSize * Unsafe.SizeOf<InstanceData>();
        _instanceBuffer = new GLBuffer(context, 0x8892, 0x88E8, byteSize, "BatchInstanceBuffer"); // ARRAY_BUFFER, DYNAMIC_DRAW
        _vao = new GLVertexArray(context);

        ConfigureInstanceAttributes();
    }

    private unsafe void ConfigureInstanceAttributes()
    {
        _context.State.BindVertexArray(_vao.Id);
        _context.State.BindArrayBuffer(_instanceBuffer.Id);

        uint stride = (uint)sizeof(InstanceData);

        // World matrix (4 vec4 attributes)
        for (uint i = 0; i < 4; i++)
        {
            uint loc = 7 + i;
            _context.GL.EnableVertexAttribArray(loc);
            _context.GL.VertexAttribPointer(loc, 4, 0x1406, false, stride, (void*)(i * 16)); // GL_FLOAT
            _context.GL.VertexAttribDivisor(loc, 1);
        }

        // Color (vec4)
        _context.GL.EnableVertexAttribArray(5);
        _context.GL.VertexAttribPointer(5, 4, 0x1406, false, stride, (void*)64); // GL_FLOAT
        _context.GL.VertexAttribDivisor(5, 1);

        // Custom (vec4)
        _context.GL.EnableVertexAttribArray(6);
        _context.GL.VertexAttribPointer(6, 4, 0x1406, false, stride, (void*)80); // GL_FLOAT
        _context.GL.VertexAttribDivisor(6, 1);

        _context.State.BindVertexArray(0);
    }

    /// <summary>
    /// Adds an instance.
    /// </summary>
    /// <param name="world">The world matrix.</param>
    /// <param name="color">The color.</param>
    /// <param name="custom">Custom data.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddInstance(in Matrix4x4 world, in Vector4 color, in Vector4 custom)
    {
        if (IsDisposed)
            ThrowHelper.ThrowInvalidOperation("BatchRenderer disposed");

        if (_instanceCount >= MaxBatchSize)
            ThrowHelper.ThrowBatchOverflow();

        ref var inst = ref _instances[_instanceCount++];
        inst.World = world;
        inst.Color = color;
        inst.Custom = custom;
        _visibleCount++;
        _needsUpdate = true;
    }

    /// <summary>
    /// Sorts instances by depth (back-to-front).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void SortByDepth()
    {
        if (IsDisposed)
            ThrowHelper.ThrowInvalidOperation("BatchRenderer disposed");

        // Sort by M43 (translation Z in row-major Matrix4x4)
        MemoryExtensions.Sort(_instances.AsSpan(0, _instanceCount), (a, b) => b.World.M43.CompareTo(a.World.M43));
        _needsUpdate = true;
    }

    /// <summary>
    /// Flushes the batch (uploads data and draws).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Flush()
    {
        if (IsDisposed)
            ThrowHelper.ThrowInvalidOperation("BatchRenderer disposed");

        if (_visibleCount == 0)
            return;

        if (_needsUpdate)
        {
            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(_instances.AsSpan(0, _visibleCount));
            _instanceBuffer.Update(bytes, 0);
            _needsUpdate = false;
        }

        _context.State.BindVertexArray(_vao.Id);
        _context.GL.DrawArraysInstanced(0x0004, 0, 6, (uint)_visibleCount); // GL_TRIANGLES
        _context.State.BindVertexArray(0);
    }

    /// <summary>
    /// Resets the batch (clears instances).
    /// </summary>
    public void Reset()
    {
        if (IsDisposed)
            ThrowHelper.ThrowInvalidOperation("BatchRenderer disposed");

        _instanceCount = 0;
        _visibleCount = 0;
        _needsUpdate = false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            _vao.Dispose();
            _instanceBuffer.Dispose();
        }
    }
}
