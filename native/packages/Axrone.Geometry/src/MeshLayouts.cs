namespace Axrone.Geometry;

/// <summary>
/// Monomorphic vertex contract: byte stride, attribute descriptors and the
/// full-attribute factory, closed over the concrete vertex type so generators
/// stay allocation-free with no virtual dispatch.
/// </summary>
/// <typeparam name="TSelf">The concrete vertex type.</typeparam>
public interface IVertex<TSelf> where TSelf : unmanaged, IVertex<TSelf>
{
    /// <summary>Interleaved stride in bytes.</summary>
    static abstract int ByteStride { get; }

    /// <summary>Attribute descriptors in layout order.</summary>
    static abstract ReadOnlySpan<VertexAttributeDescriptor> LayoutDescriptors { get; }

    /// <summary>Builds a vertex from all four attributes.</summary>
    static abstract TSelf Create(in Position3D position, in Normal3D normal, in TexCoord uv, in Tangent4D tangent);

    /// <summary>Position attribute.</summary>
    Position3D Position { get; }

    /// <summary>Normal attribute.</summary>
    Normal3D Normal { get; }

    /// <summary>Texture-coordinate attribute.</summary>
    TexCoord UV { get; }

    /// <summary>Tangent attribute.</summary>
    Tangent4D Tangent { get; }

    /// <summary>Returns a copy with a replaced normal.</summary>
    TSelf WithNormal(in Normal3D normal);

    /// <summary>Returns a copy with a replaced tangent.</summary>
    TSelf WithTangent(in Tangent4D tangent);
}

/// <summary>Position + normal + UV vertex, 32 bytes.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct VertexP3N3T2 : IVertex<VertexP3N3T2>
{
    /// <summary>Position attribute.</summary>
    public readonly Position3D Position;

    /// <summary>Normal attribute.</summary>
    public readonly Normal3D Normal;

    /// <summary>Texture-coordinate attribute.</summary>
    public readonly TexCoord UV;

    /// <inheritdoc/>
    public static int ByteStride => Unsafe.SizeOf<VertexP3N3T2>();

    Position3D IVertex<VertexP3N3T2>.Position => Position;
    Normal3D IVertex<VertexP3N3T2>.Normal => Normal;
    TexCoord IVertex<VertexP3N3T2>.UV => UV;
    Tangent4D IVertex<VertexP3N3T2>.Tangent => Tangent4D.Default;

    /// <summary>Creates a vertex.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VertexP3N3T2(in Position3D position, in Normal3D normal, in TexCoord uv)
    {
        Position = position;
        Normal = normal;
        UV = uv;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VertexP3N3T2 Create(in Position3D position, in Normal3D normal, in TexCoord uv, in Tangent4D tangent) =>
        new(position, normal, uv);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VertexP3N3T2 WithNormal(in Normal3D normal) => new(Position, normal, UV);

    /// <summary>No tangent storage; returns this unchanged.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VertexP3N3T2 WithTangent(in Tangent4D tangent) => this;

    private static readonly VertexAttributeDescriptor[] Descriptors =
    [
        new(AttributeSemantic.Position, 3, GLAttributeType.Float, false, 0),
        new(AttributeSemantic.Normal, 3, GLAttributeType.Float, false, 12),
        new(AttributeSemantic.TexCoord, 2, GLAttributeType.Float, false, 24)
    ];

    /// <inheritdoc/>
    public static ReadOnlySpan<VertexAttributeDescriptor> LayoutDescriptors => Descriptors;
}

/// <summary>Position + normal + UV + tangent vertex, 48 bytes.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct VertexP3N3T2T4 : IVertex<VertexP3N3T2T4>
{
    /// <summary>Position attribute.</summary>
    public readonly Position3D Position;

    /// <summary>Normal attribute.</summary>
    public readonly Normal3D Normal;

    /// <summary>Texture-coordinate attribute.</summary>
    public readonly TexCoord UV;

    /// <summary>Tangent attribute.</summary>
    public readonly Tangent4D Tangent;

    /// <inheritdoc/>
    public static int ByteStride => Unsafe.SizeOf<VertexP3N3T2T4>();

    Position3D IVertex<VertexP3N3T2T4>.Position => Position;
    Normal3D IVertex<VertexP3N3T2T4>.Normal => Normal;
    TexCoord IVertex<VertexP3N3T2T4>.UV => UV;
    Tangent4D IVertex<VertexP3N3T2T4>.Tangent => Tangent;

    /// <summary>Creates a vertex.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VertexP3N3T2T4(in Position3D position, in Normal3D normal, in TexCoord uv, in Tangent4D tangent)
    {
        Position = position;
        Normal = normal;
        UV = uv;
        Tangent = tangent;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VertexP3N3T2T4 Create(in Position3D position, in Normal3D normal, in TexCoord uv, in Tangent4D tangent) =>
        new(position, normal, uv, tangent);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VertexP3N3T2T4 WithNormal(in Normal3D normal) => new(Position, normal, UV, Tangent);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VertexP3N3T2T4 WithTangent(in Tangent4D tangent) => new(Position, Normal, UV, tangent);

    private static readonly VertexAttributeDescriptor[] Descriptors =
    [
        new(AttributeSemantic.Position, 3, GLAttributeType.Float, false, 0),
        new(AttributeSemantic.Normal, 3, GLAttributeType.Float, false, 12),
        new(AttributeSemantic.TexCoord, 2, GLAttributeType.Float, false, 24),
        new(AttributeSemantic.Tangent, 4, GLAttributeType.Float, false, 32)
    ];

    /// <inheritdoc/>
    public static ReadOnlySpan<VertexAttributeDescriptor> LayoutDescriptors => Descriptors;
}
