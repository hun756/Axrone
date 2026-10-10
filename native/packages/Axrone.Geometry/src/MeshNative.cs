namespace Axrone.Geometry;

/// <summary>
/// Sealed immutable mesh: vertex and index buffers frozen at seal time,
/// exposed read-only with allocation-free enumeration.
/// </summary>
/// <typeparam name="TVertex">The vertex type.</typeparam>
/// <typeparam name="TIndex">The index element type.</typeparam>
public sealed unsafe class NativeMesh<TVertex, TIndex> : IDisposable
    where TVertex : unmanaged, IVertex<TVertex>
    where TIndex : unmanaged, System.Numerics.IBinaryInteger<TIndex>
{
    private static int s_idGen;

    private readonly int _layoutId;
    private NativeBuffer<TVertex> _vertices;
    private NativeBuffer<TIndex> _indices;
    private int _disposed;

    /// <summary>Process-wide layout discriminator.</summary>
    public int LayoutId => _layoutId;

    /// <summary>Live vertices.</summary>
    public int VertexCount => checked((int)_vertices.Length);

    /// <summary>Live indices.</summary>
    public int IndexCount => checked((int)_indices.Length);

    /// <summary>Interleaved vertex stride in bytes.</summary>
    public int Stride => TVertex.ByteStride;

    /// <summary>Assembled topology; always triangles.</summary>
    public PrimitiveTopology Topology => PrimitiveTopology.Triangles;

    /// <summary>Attribute descriptors of the vertex layout.</summary>
    public ReadOnlySpan<VertexAttributeDescriptor> Attributes => TVertex.LayoutDescriptors;

    /// <summary>Read-only vertex view.</summary>
    public ReadOnlySpan<TVertex> Vertices
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return _vertices.AsReadOnlySpan();
        }
    }

    /// <summary>Read-only index view.</summary>
    public ReadOnlySpan<TIndex> Indices
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return _indices.AsReadOnlySpan();
        }
    }

    internal NativeMesh(ref NativeBuffer<TVertex> vertices, ref NativeBuffer<TIndex> indices)
    {
        _layoutId = Interlocked.Increment(ref s_idGen);
        _vertices = vertices;
        _indices = indices;
        vertices = default;
        indices = default;
        _disposed = 0;
    }

    /// <summary>Enumerates vertices without allocation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VertexEnumerator<TVertex> GetEnumerator()
    {
        ThrowIfDisposed();
        return new VertexEnumerator<TVertex>(_vertices.AsReadOnlySpan());
    }

    /// <summary>Pushes every vertex into <paramref name="consumer"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyVerticesTo<TConsumer>(ref TConsumer consumer)
        where TConsumer : IVertexConsumer<TVertex>, allows ref struct
    {
        ThrowIfDisposed();
        ReadOnlySpan<TVertex> span = _vertices.AsReadOnlySpan();
        for (int i = 0; i < span.Length; i++)
        {
            consumer.Accept(in span[i]);
        }
    }

    /// <summary>Pushes every triangle into <paramref name="consumer"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyIndicesTo<TConsumer>(ref TConsumer consumer)
        where TConsumer : ITriangleConsumer<TIndex>, allows ref struct
    {
        ThrowIfDisposed();
        ReadOnlySpan<TIndex> span = _indices.AsReadOnlySpan();
        for (int i = 0; i < span.Length; i += 3)
        {
            consumer.Accept(span[i], span[i + 1], span[i + 2]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(NativeMesh<TVertex, TIndex>));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _vertices.Dispose();
            _indices.Dispose();
        }
    }
}

/// <summary>By-value vertex enumerator; ref returns are illegal on mutable structs.</summary>
/// <typeparam name="TVertex">The vertex type.</typeparam>
public ref struct VertexEnumerator<TVertex> where TVertex : unmanaged
{
    private readonly ReadOnlySpan<TVertex> _span;
    private int _index;

    /// <summary>Creates an enumerator; prefer <c>GetEnumerator</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal VertexEnumerator(ReadOnlySpan<TVertex> span)
    {
        _span = span;
        _index = -1;
    }

    /// <summary>Advances to the next vertex, if any.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() => ++_index < _span.Length;

    /// <summary>The vertex at the current position.</summary>
    public TVertex Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _span[_index];
    }
}
