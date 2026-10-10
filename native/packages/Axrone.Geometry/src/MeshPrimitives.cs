namespace Axrone.Geometry;

/// <summary>OpenGL vertex attribute component types, as raw values.</summary>
public enum GLAttributeType : uint
{
    /// <summary>Signed 8-bit.</summary>
    Byte = 0x1400,

    /// <summary>Unsigned 8-bit.</summary>
    UnsignedByte = 0x1401,

    /// <summary>Signed 16-bit.</summary>
    Short = 0x1402,

    /// <summary>Unsigned 16-bit.</summary>
    UnsignedShort = 0x1403,

    /// <summary>Signed 32-bit.</summary>
    Int = 0x1404,

    /// <summary>Unsigned 32-bit.</summary>
    UnsignedInt = 0x1405,

    /// <summary>32-bit float.</summary>
    Float = 0x1406
}

/// <summary>Assembled primitive topology of an index stream.</summary>
public enum PrimitiveTopology : byte
{
    /// <summary>Triangle list.</summary>
    Triangles = 0,

    /// <summary>Line list.</summary>
    Lines = 1,

    /// <summary>Point list.</summary>
    Points = 2
}

/// <summary>Semantic role of a vertex attribute.</summary>
public enum AttributeSemantic : byte
{
    /// <summary>Position.</summary>
    Position = 0,

    /// <summary>Normal.</summary>
    Normal = 1,

    /// <summary>Texture coordinates.</summary>
    TexCoord = 2,

    /// <summary>Tangent frame.</summary>
    Tangent = 3
}

/// <summary>
/// Nominal world-space length. Keeps radii, widths and heights distinct from
/// bare floats at the call site.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Metric(float Value) :
    IComparable<Metric>,
    IEquatable<Metric>
{
    /// <summary>Zero length.</summary>
    public static Metric Zero => new(0.0f);

    /// <summary>Unit length.</summary>
    public static Metric One => new(1.0f);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Metric other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Metric operator +(Metric left, Metric right) => new(left.Value + right.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Metric operator -(Metric left, Metric right) => new(left.Value - right.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Metric operator *(Metric left, float scalar) => new(left.Value * scalar);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Metric operator /(Metric left, float scalar) => new(left.Value / scalar);
}

/// <summary>Tessellation density along one parametric axis, floored by callers.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SegmentResolution(uint Value) :
    IComparable<SegmentResolution>,
    IEquatable<SegmentResolution>
{
    /// <summary>Clamps <paramref name="value"/> up to <paramref name="minimum"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SegmentResolution Clamp(uint value, uint minimum) => new(Math.Max(value, minimum));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SegmentResolution other) => Value.CompareTo(other.Value);
}

/// <summary>Strongly-typed vertex slot identity within one mesh.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct VertexId(uint Value) :
    IComparable<VertexId>,
    IEquatable<VertexId>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(VertexId other) => Value.CompareTo(other.Value);
}

/// <summary>Up-front vertex and index reservations for one mesh build.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CapacityAllocation(uint VertexCapacity, uint IndexCapacity);

/// <summary>
/// Nominal vertex position. Distinct from <see cref="Vec3"/> so positions,
/// normals and directions never mix at the call site; converts freely.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Position3D(float X, float Y, float Z)
{
    /// <summary>The origin.</summary>
    public static Position3D Zero => new(0.0f, 0.0f, 0.0f);

    /// <summary>Unwraps to a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec3 ToVec3() => new(X, Y, Z);

    /// <summary>Wraps a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Position3D FromVec3(Vec3 v) => new(v.X, v.Y, v.Z);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Position3D operator +(Position3D a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Position3D operator -(Position3D a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}

/// <summary>
/// Nominal unit normal. Construction normalizes, so every instance is safe to
/// use directly in lighting math; degenerate input falls back to +Y.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Normal3D(float X, float Y, float Z)
{
    /// <summary>+X.</summary>
    public static Normal3D UnitX => new(1.0f, 0.0f, 0.0f);

    /// <summary>+Y.</summary>
    public static Normal3D UnitY => new(0.0f, 1.0f, 0.0f);

    /// <summary>+Z.</summary>
    public static Normal3D UnitZ => new(0.0f, 0.0f, 1.0f);

    /// <summary>-Y.</summary>
    public static Normal3D NegativeUnitY => new(0.0f, -1.0f, 0.0f);

    /// <summary>-Z.</summary>
    public static Normal3D NegativeUnitZ => new(0.0f, 0.0f, -1.0f);

    /// <summary>Unwraps to a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec3 ToVec3() => new(X, Y, Z);

    /// <summary>Normalizes <paramref name="v"/>; degenerate input yields +Y.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Normal3D FromVec3(Vec3 v)
    {
        float lenSq = v.LengthSquared();
        if (lenSq < 1e-12f) return UnitY;
        Vec3 norm = Vec3.Normalize(v);
        return new(norm.X, norm.Y, norm.Z);
    }
}

/// <summary>Nominal texture coordinate.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct TexCoord(float U, float V)
{
    /// <summary>Origin of UV space.</summary>
    public static TexCoord Zero => new(0.0f, 0.0f);

    /// <summary>Center of UV space.</summary>
    public static TexCoord Center => new(0.5f, 0.5f);

    /// <summary>Unwraps to a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec2 ToVec2() => new(U, V);

    /// <summary>Wraps a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TexCoord FromVec2(Vec2 v) => new(v.X, v.Y);
}

/// <summary>Nominal tangent frame with a handedness sign.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Tangent4D(float X, float Y, float Z, float W)
{
    /// <summary>+X tangent with positive handedness.</summary>
    public static Tangent4D Default => new(1.0f, 0.0f, 0.0f, 1.0f);

    /// <summary>Unwraps to a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec4 ToVec4() => new(X, Y, Z, W);

    /// <summary>Wraps a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Tangent4D FromVec4(Vec4 v) => new(v.X, v.Y, v.Z, v.W);
}

/// <summary>One attribute entry of an interleaved vertex layout.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct VertexAttributeDescriptor(
    AttributeSemantic Semantic,
    int ComponentCount,
    GLAttributeType Type,
    bool Normalized,
    int Offset
);
