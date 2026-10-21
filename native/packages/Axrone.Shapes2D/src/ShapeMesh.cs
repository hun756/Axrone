namespace Axrone.Shapes2D;

/// <summary>Index buffer with 16/32-bit selection.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ShapeIndexBuffer
{
    private readonly ushort[]? _indices16;
    private readonly uint[]? _indices32;

    /// <summary>Whether 16-bit.</summary>
    public bool Is16Bit => _indices16 is not null;

    /// <summary>Index count.</summary>
    public int Length => _indices16?.Length ?? _indices32?.Length ?? 0;

    /// <summary>Index accessor.</summary>
    public uint this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _indices16 is not null ? _indices16[index] : _indices32![index];
    }

    /// <summary>Creates a 16-bit buffer.</summary>
    public ShapeIndexBuffer(ushort[] indices)
    {
        _indices16 = indices;
        _indices32 = null;
    }

    /// <summary>Creates a 32-bit buffer.</summary>
    public ShapeIndexBuffer(uint[] indices)
    {
        _indices32 = indices;
        _indices16 = null;
    }

    /// <summary>Selects width by vertex count.</summary>
    public static ShapeIndexBuffer FromIndices(ReadOnlySpan<uint> indices, int vertexCount)
    {
        if (vertexCount <= 65535)
        {
            ushort[] idx16 = GC.AllocateUninitializedArray<ushort>(indices.Length);
            for (int i = 0; i < indices.Length; i++)
            {
                idx16[i] = (ushort)indices[i];
            }

            return new ShapeIndexBuffer(idx16);
        }

        return new ShapeIndexBuffer(indices.ToArray());
    }

    /// <summary>Copies to a destination.</summary>
    public void CopyTo(Span<uint> destination)
    {
        if (_indices16 is not null)
        {
            for (int i = 0; i < _indices16.Length; i++)
            {
                destination[i] = _indices16[i];
            }
        }
        else if (_indices32 is not null)
        {
            _indices32.CopyTo(destination);
        }
    }

    /// <summary>Enumerates indices.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndexEnumerator GetEnumerator() => new(this);

    /// <summary>Zero-allocation index enumerator.</summary>
    public ref struct IndexEnumerator
    {
        private readonly ShapeIndexBuffer _buffer;
        private int _index;

        /// <summary>Creates an enumerator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IndexEnumerator(ShapeIndexBuffer buffer)
        {
            _buffer = buffer;
            _index = -1;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int next = _index + 1;
            if (next < _buffer.Length)
            {
                _index = next;
                return true;
            }

            return false;
        }

        /// <summary>Current index.</summary>
        public readonly uint Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer[_index];
        }
    }
}

/// <summary>CPU-side 2D mesh.</summary>
public sealed class ShapeMesh2D
{
    /// <summary>Interleaved XY positions.</summary>
    public float[] Positions { get; }

    /// <summary>Indices.</summary>
    public ShapeIndexBuffer Indices { get; }

    /// <summary>Vertex count.</summary>
    public int VertexCount => Positions.Length / 2;

    /// <summary>Index count.</summary>
    public int IndexCount => Indices.Length;

    /// <summary>Bounds.</summary>
    public ShapeBounds Bounds { get; }

    /// <summary>Creates a mesh.</summary>
    public ShapeMesh2D(float[] positions, ShapeIndexBuffer indices, ShapeBounds bounds)
    {
        Positions = positions;
        Indices = indices;
        Bounds = bounds;
    }
}

/// <summary>Contour approximation options.</summary>
public readonly record struct ShapeApproximationOptions(
    float CurveTolerance = ShapeConstants.DefaultCurveTolerance,
    int MinCurveSegments = ShapeConstants.DefaultMinCurveSegments,
    int MaxCurveSegments = ShapeConstants.DefaultMaxCurveSegments)
{
    /// <summary>Default options.</summary>
    public static readonly ShapeApproximationOptions Default = new(
        ShapeConstants.DefaultCurveTolerance,
        ShapeConstants.DefaultMinCurveSegments,
        ShapeConstants.DefaultMaxCurveSegments);
}

/// <summary>Compile options.</summary>
public readonly record struct ShapeCompileOptions(
    bool IncludeFillMesh = true,
    bool IncludeStrokeMesh = true,
    ShapeApproximationOptions Approximation = default);

/// <summary>Compiled shape: cached geometry, meshes and metrics.</summary>
public sealed class CompiledShape2D
{
    /// <summary>Source shape.</summary>
    public Shape2D Shape { get; }

    /// <summary>Fingerprint.</summary>
    public string Fingerprint { get; }

    /// <summary>Stroke-free bounds.</summary>
    public ShapeBounds GeometryBounds { get; }

    /// <summary>Full bounds.</summary>
    public ShapeBounds Bounds { get; }

    /// <summary>Area.</summary>
    public float Area { get; }

    /// <summary>Perimeter.</summary>
    public float Perimeter { get; }

    /// <summary>Approximated contour.</summary>
    public float[] Contour { get; }

    /// <summary>Fill mesh.</summary>
    public ShapeMesh2D? FillMesh { get; }

    /// <summary>Stroke mesh.</summary>
    public ShapeMesh2D? StrokeMesh { get; }

    /// <summary>Creates a compiled shape.</summary>
    public CompiledShape2D(
        Shape2D shape,
        string fingerprint,
        ShapeBounds geometryBounds,
        ShapeBounds bounds,
        float area,
        float perimeter,
        float[] contour,
        ShapeMesh2D? fillMesh,
        ShapeMesh2D? strokeMesh)
    {
        Shape = shape;
        Fingerprint = fingerprint;
        GeometryBounds = geometryBounds;
        Bounds = bounds;
        Area = area;
        Perimeter = perimeter;
        Contour = contour;
        FillMesh = fillMesh;
        StrokeMesh = strokeMesh;
    }
}
