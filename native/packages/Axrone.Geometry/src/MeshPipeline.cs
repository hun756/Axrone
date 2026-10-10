namespace Axrone.Geometry;

/// <summary>Marker for the compile-time stage of a mesh pipeline.</summary>
public interface IPipelineStage { }

/// <summary>Stage in which capacity is reserved.</summary>
public readonly struct ConfigurationStage : IPipelineStage { }

/// <summary>Stage in which vertices and triangles are appended.</summary>
public readonly struct AssemblyStage : IPipelineStage { }

/// <summary>Terminal stage: the mesh is sealed and immutable.</summary>
public readonly struct SealedStage : IPipelineStage { }

/// <summary>
/// Typestate mesh assembly: configure, then assemble, then seal. Stage
/// violations throw; sealing hands buffer ownership to the mesh. Capacity is
/// fixed at reservation time, so overruns fail fast instead of growing.
/// </summary>
/// <typeparam name="TVertex">The vertex type.</typeparam>
/// <typeparam name="TIndex">The index element type.</typeparam>
/// <typeparam name="TWinding">The triangle winding policy.</typeparam>
/// <typeparam name="TStage">The compile-time stage marker.</typeparam>
public sealed class MeshPipeline<TVertex, TIndex, TWinding, TStage> : IDisposable
    where TVertex : unmanaged, IVertex<TVertex>
    where TIndex : unmanaged, System.Numerics.IBinaryInteger<TIndex>
    where TWinding : struct, IWindingPolicy
    where TStage : struct, IPipelineStage
{
    private readonly NativeBuffer<TVertex, AlignedNativeAllocator>? _vertices;
    private readonly NativeBuffer<TIndex, AlignedNativeAllocator>? _indices;
    private uint _vertexCount;
    private uint _indexCount;
    private int _disposed;

    private MeshPipeline(
        NativeBuffer<TVertex, AlignedNativeAllocator>? vertices,
        NativeBuffer<TIndex, AlignedNativeAllocator>? indices)
    {
        _vertices = vertices;
        _indices = indices;
        _vertexCount = 0;
        _indexCount = 0;
        _disposed = 0;
    }

    /// <summary>Starts a configuration-stage pipeline.</summary>
    public static MeshPipeline<TVertex, TIndex, TWinding, ConfigurationStage> Create()
    {
        return new MeshPipeline<TVertex, TIndex, TWinding, ConfigurationStage>(null, null);
    }

    /// <summary>Assembled vertices so far.</summary>
    public uint VertexCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _vertexCount;
    }

    /// <summary>Assembled indices so far.</summary>
    public uint IndexCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _indexCount;
    }

    /// <summary>Reserves fixed capacity, moving to the assembly stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MeshPipeline<TVertex, TIndex, TWinding, AssemblyStage> Allocate(CapacityAllocation allocation)
    {
        ThrowIfDisposed();
        if (typeof(TStage) != typeof(ConfigurationStage))
        {
            ThrowHelper.ThrowInvalidOperation("Allocation can only occur during Configuration stage.");
        }

        var vBuf = new NativeBuffer<TVertex, AlignedNativeAllocator>(
            ElementCount.From(allocation.VertexCapacity), MemoryAlignment.CacheLine);
        var iBuf = new NativeBuffer<TIndex, AlignedNativeAllocator>(
            ElementCount.From(allocation.IndexCapacity), MemoryAlignment.CacheLine);
        _disposed = 1;
        return new MeshPipeline<TVertex, TIndex, TWinding, AssemblyStage>(vBuf, iBuf);
    }

    /// <summary>Appends one vertex, returning its identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public VertexId AddVertex(in Position3D pos, in Normal3D norm, in TexCoord uv)
    {
        ThrowIfDisposed();
        if (typeof(TStage) != typeof(AssemblyStage))
        {
            ThrowHelper.ThrowInvalidOperation("Vertices can only be appended during Assembly stage.");
        }

        uint idx = _vertexCount;
        _vertices![BufferIndex.From(idx)] = TVertex.Create(pos, norm, uv, Tangent4D.Default);
        _vertexCount++;
        return new VertexId(idx);
    }

    /// <summary>Appends one triangle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void AddTriangle(uint i0, uint i1, uint i2)
    {
        ThrowIfDisposed();
        if (typeof(TStage) != typeof(AssemblyStage))
        {
            ThrowHelper.ThrowInvalidOperation("Triangles can only be added during Assembly stage.");
        }

        uint offset = _indexCount;
        TIndex t0 = TIndex.CreateChecked(i0);
        TIndex t1 = TIndex.CreateChecked(i1);
        TIndex t2 = TIndex.CreateChecked(i2);
        _indices![BufferIndex.From(offset)] = t0;
        _indices![BufferIndex.From(offset + 1)] = t1;
        _indices![BufferIndex.From(offset + 2)] = t2;
        _indexCount += 3;

        TWinding.EmitTriangle(_indices.Span, offset, t0, t1, t2);
    }

    /// <summary>Appends one quad as the (a,b,d) and (b,c,d) triangles.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void AddQuad(uint a, uint b, uint c, uint d)
    {
        AddTriangle(a, b, d);
        AddTriangle(b, c, d);
    }

    /// <summary>Recomputes smooth normals from face areas.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void RecalculateNormals()
    {
        ThrowIfDisposed();
        uint vCount = _vertexCount;
        uint iCount = _indexCount;
        if (vCount == 0 || iCount == 0) return;

        using var normalAccum = new NativeBuffer<Vec3, AlignedNativeAllocator>(
            ElementCount.From(vCount), MemoryAlignment.CacheLine);
        Span<Vec3> normSpan = normalAccum.Span;
        normSpan.Clear();

        Span<TVertex> vertSpan = _vertices!.Span;
        ReadOnlySpan<TIndex> idxSpan = _indices!.ReadOnlySpan;
        uint triCount = iCount / 3;

        for (uint i = 0; i < triCount; i++)
        {
            uint i0 = uint.CreateChecked(idxSpan[(int)(i * 3)]);
            uint i1 = uint.CreateChecked(idxSpan[(int)(i * 3 + 1)]);
            uint i2 = uint.CreateChecked(idxSpan[(int)(i * 3 + 2)]);

            Vec3 v0 = vertSpan[(int)i0].Position.ToVec3();
            Vec3 v1 = vertSpan[(int)i1].Position.ToVec3();
            Vec3 v2 = vertSpan[(int)i2].Position.ToVec3();

            Vec3 fn = Vec3.Cross(v1 - v0, v2 - v0);
            normSpan[(int)i0] += fn;
            normSpan[(int)i1] += fn;
            normSpan[(int)i2] += fn;
        }

        for (uint i = 0; i < vCount; i++)
        {
            Vec3 accumulated = normSpan[(int)i];
            Normal3D normalized = Normal3D.FromVec3(accumulated);
            vertSpan[(int)i] = vertSpan[(int)i].WithNormal(normalized);
        }
    }

    /// <summary>Recomputes tangents with handedness from UV gradients.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void RecalculateTangents()
    {
        ThrowIfDisposed();
        uint vCount = _vertexCount;
        uint iCount = _indexCount;
        if (vCount == 0 || iCount == 0) return;

        using var tan1 = new NativeBuffer<Vec3, AlignedNativeAllocator>(
            ElementCount.From(vCount), MemoryAlignment.CacheLine);
        using var tan2 = new NativeBuffer<Vec3, AlignedNativeAllocator>(
            ElementCount.From(vCount), MemoryAlignment.CacheLine);

        Span<Vec3> s1 = tan1.Span;
        Span<Vec3> s2 = tan2.Span;
        s1.Clear();
        s2.Clear();

        Span<TVertex> vertSpan = _vertices!.Span;
        ReadOnlySpan<TIndex> idxSpan = _indices!.ReadOnlySpan;
        uint triCount = iCount / 3;

        for (uint i = 0; i < triCount; i++)
        {
            uint i0 = uint.CreateChecked(idxSpan[(int)(i * 3)]);
            uint i1 = uint.CreateChecked(idxSpan[(int)(i * 3 + 1)]);
            uint i2 = uint.CreateChecked(idxSpan[(int)(i * 3 + 2)]);

            Vec3 v0 = vertSpan[(int)i0].Position.ToVec3();
            Vec3 v1 = vertSpan[(int)i1].Position.ToVec3();
            Vec3 v2 = vertSpan[(int)i2].Position.ToVec3();

            Vec2 w0 = vertSpan[(int)i0].UV.ToVec2();
            Vec2 w1 = vertSpan[(int)i1].UV.ToVec2();
            Vec2 w2 = vertSpan[(int)i2].UV.ToVec2();

            Vec3 e1 = v1 - v0;
            Vec3 e2 = v2 - v0;

            float x1 = w1.X - w0.X;
            float x2 = w2.X - w0.X;
            float y1 = w1.Y - w0.Y;
            float y2 = w2.Y - w0.Y;

            float r = (x1 * y2 - x2 * y1);
            float invR = MathF.Abs(r) > 1e-12f ? 1.0f / r : 0.0f;

            Vec3 sdir = new(
                (y2 * e1.X - y1 * e2.X) * invR,
                (y2 * e1.Y - y1 * e2.Y) * invR,
                (y2 * e1.Z - y1 * e2.Z) * invR
            );

            Vec3 tdir = new(
                (x1 * e2.X - x2 * e1.X) * invR,
                (x1 * e2.Y - x2 * e1.Y) * invR,
                (x1 * e2.Z - x2 * e1.Z) * invR
            );

            s1[(int)i0] += sdir;
            s1[(int)i1] += sdir;
            s1[(int)i2] += sdir;

            s2[(int)i0] += tdir;
            s2[(int)i1] += tdir;
            s2[(int)i2] += tdir;
        }

        for (uint i = 0; i < vCount; i++)
        {
            Vec3 n = vertSpan[(int)i].Normal.ToVec3();
            Vec3 t = s1[(int)i];

            if (t.LengthSquared() < 1e-12f)
            {
                vertSpan[(int)i] = vertSpan[(int)i].WithTangent(new Tangent4D(1, 0, 0, 1));
                continue;
            }

            Vec3 tOrth = Vec3.Normalize(t - n * Vec3.Dot(n, t));
            float w = (Vec3.Dot(Vec3.Cross(n, t), s2[(int)i]) < 0.0f) ? -1.0f : 1.0f;
            vertSpan[(int)i] = vertSpan[(int)i].WithTangent(new Tangent4D(tOrth.X, tOrth.Y, tOrth.Z, w));
        }
    }

    /// <summary>Seals the pipeline into an immutable mesh.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public NativeMesh<TVertex, TIndex> Seal()
    {
        ThrowIfDisposed();
        if (typeof(TStage) != typeof(AssemblyStage))
        {
            ThrowHelper.ThrowInvalidOperation("Only an active Assembly pipeline can be sealed.");
        }

        NativeMesh<TVertex, TIndex> mesh = new(_vertices!, (int)_vertexCount, _indices!, (int)_indexCount);
        _disposed = 1;
        return mesh;
    }

    /// <summary>Exposes this pipeline as an append sink.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal AssemblySink AsSink()
    {
        return new AssemblySink(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(MeshPipeline<TVertex, TIndex, TWinding, TStage>));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _vertices?.Dispose();
            _indices?.Dispose();
        }
    }

    /// <summary>Append sink over a live pipeline.</summary>
    internal readonly ref struct AssemblySink : IMeshSink<TVertex, TIndex>
    {
        private readonly MeshPipeline<TVertex, TIndex, TWinding, TStage> _pipeline;

        /// <summary>Creates a sink; prefer <c>AsSink</c>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AssemblySink(MeshPipeline<TVertex, TIndex, TWinding, TStage> pipeline)
        {
            _pipeline = pipeline;
        }

        /// <inheritdoc/>
        public uint CurrentVertexCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pipeline.VertexCount;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendVertex(in TVertex vertex)
        {
            uint idx = _pipeline._vertexCount;
            _pipeline._vertices![BufferIndex.From(idx)] = vertex;
            _pipeline._vertexCount++;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendTriangle(TIndex i0, TIndex i1, TIndex i2)
        {
            uint offset = _pipeline._indexCount;
            _pipeline._indices![BufferIndex.From(offset)] = i0;
            _pipeline._indices![BufferIndex.From(offset + 1)] = i1;
            _pipeline._indices![BufferIndex.From(offset + 2)] = i2;
            _pipeline._indexCount += 3;

            TWinding.EmitTriangle(_pipeline._indices.Span, offset, i0, i1, i2);
        }
    }
}
