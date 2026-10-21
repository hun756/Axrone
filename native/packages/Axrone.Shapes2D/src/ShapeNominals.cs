namespace Axrone.Shapes2D;

/// <summary>Shared shape constants.</summary>
public static class ShapeConstants
{
    /// <summary>Float epsilon.</summary>
    public const float Epsilon = 1e-7f;

    /// <summary>Double epsilon.</summary>
    public const double DoubleEpsilon = 1e-9;

    /// <summary>Two pi.</summary>
    public const float Tau = MathF.PI * 2f;

    /// <summary>Default curve tolerance.</summary>
    public const float DefaultCurveTolerance = 0.25f;

    /// <summary>Default minimum curve segments.</summary>
    public const int DefaultMinCurveSegments = 16;

    /// <summary>Default maximum curve segments.</summary>
    public const int DefaultMaxCurveSegments = 128;

    /// <summary>Default gradient lookup size.</summary>
    public const int DefaultGradientLookupSize = 256;

    /// <summary>Default registry shape capacity.</summary>
    public const int DefaultRegistryMaxShapes = 2048;

    /// <summary>Default registry compiled capacity.</summary>
    public const int DefaultRegistryMaxCompiled = 4096;

    /// <summary>Miter limit.</summary>
    public const float MiterLimit = 4.0f;
}

/// <summary>
/// Nominal shape identity (4 bytes, register promoted).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ShapeId(uint Value) :
    IComparable<ShapeId>,
    IEquatable<ShapeId>,
    IComparisonOperators<ShapeId, ShapeId, bool>
{
    /// <summary>Invalid identity.</summary>
    public static readonly ShapeId Invalid = new(0);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(ShapeId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => FormattableString.Invariant($"shape_{Value}");

    /// <inheritdoc/>
    public static bool operator >(ShapeId left, ShapeId right) => left.Value > right.Value;

    /// <inheritdoc/>
    public static bool operator >=(ShapeId left, ShapeId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    public static bool operator <(ShapeId left, ShapeId right) => left.Value < right.Value;

    /// <inheritdoc/>
    public static bool operator <=(ShapeId left, ShapeId right) => left.Value <= right.Value;
}

/// <summary>
/// Capacity limit nominal (4 bytes, register promoted).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CapacityLimit
{
    /// <summary>Limit value.</summary>
    public int Value { get; }

    /// <summary>Default shape capacity.</summary>
    public static readonly CapacityLimit DefaultShapes = new(ShapeConstants.DefaultRegistryMaxShapes);

    /// <summary>Default compiled capacity.</summary>
    public static readonly CapacityLimit DefaultCompiled = new(ShapeConstants.DefaultRegistryMaxCompiled);

    /// <summary>Creates a limit.</summary>
    public CapacityLimit(int value)
    {
        if (value <= 0)
        {
            ThrowHelper.ThrowRegistry(Shapes2DErrorCode.CapacityExceeded, "Capacity must be positive.");
        }

        Value = value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(CapacityLimit limit) => limit.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator CapacityLimit(int value) => new(value);
}

/// <summary>
/// Nominal clamped unit scalar (4 bytes, register promoted).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct NormalizedScalar :
    IComparable<NormalizedScalar>,
    IEquatable<NormalizedScalar>,
    IComparisonOperators<NormalizedScalar, NormalizedScalar, bool>
{
    /// <summary>Clamped value.</summary>
    public float Value
    {
        get => field;
        init => field = Math.Clamp(ThrowHelper.AssertFinite(value, nameof(NormalizedScalar)), 0f, 1f);
    }

    /// <summary>Creates a scalar.</summary>
    public NormalizedScalar(float value)
    {
        Value = value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float(NormalizedScalar scalar) => scalar.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator NormalizedScalar(float value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(NormalizedScalar other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    public static bool operator >(NormalizedScalar left, NormalizedScalar right) => left.Value > right.Value;

    /// <inheritdoc/>
    public static bool operator >=(NormalizedScalar left, NormalizedScalar right) => left.Value >= right.Value;

    /// <inheritdoc/>
    public static bool operator <(NormalizedScalar left, NormalizedScalar right) => left.Value < right.Value;

    /// <inheritdoc/>
    public static bool operator <=(NormalizedScalar left, NormalizedScalar right) => left.Value <= right.Value;
}

/// <summary>
/// RGBA color (16 bytes, register promoted).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ResolvedColor(float R, float G, float B, float A)
{
    /// <summary>Transparent black.</summary>
    public static readonly ResolvedColor Transparent = new(0f, 0f, 0f, 0f);

    /// <summary>Opaque black.</summary>
    public static readonly ResolvedColor Black = new(0f, 0f, 0f, 1f);

    /// <summary>Opaque white.</summary>
    public static readonly ResolvedColor White = new(1f, 1f, 1f, 1f);

    /// <summary>Clamps every channel.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ResolvedColor Clamp() => new(
        Math.Clamp(R, 0f, 1f),
        Math.Clamp(G, 0f, 1f),
        Math.Clamp(B, 0f, 1f),
        Math.Clamp(A, 0f, 1f));

    /// <summary>Multiplies alpha by opacity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ResolvedColor ModulateAlpha(float opacity) => new(
        R, G, B, Math.Clamp(A * Math.Clamp(opacity, 0f, 1f), 0f, 1f));

    /// <summary>Converts to the shared linear color.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Rgba128Float(ResolvedColor color) => new(color.R, color.G, color.B, color.A);

    /// <summary>Converts from the shared linear color.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ResolvedColor(Rgba128Float color) => new(color.R, color.G, color.B, color.A);
}

/// <summary>
/// Axis-aligned 2D pixel bounding box (16 bytes, dual-register promoted).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ShapeBounds(float MinX, float MinY, float MaxX, float MaxY)
{
    /// <summary>Zero bounds.</summary>
    public static readonly ShapeBounds Empty = new(0f, 0f, 0f, 0f);

    /// <summary>Width.</summary>
    public float Width => MaxX - MinX;

    /// <summary>Height.</summary>
    public float Height => MaxY - MinY;

    /// <summary>Center X.</summary>
    public float CenterX => (MinX + MaxX) * 0.5f;

    /// <summary>Center Y.</summary>
    public float CenterY => (MinY + MaxY) * 0.5f;

    /// <summary>Creates normalized bounds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ShapeBounds Create(float minX, float minY, float maxX, float maxY)
    {
        float sMinX = MathF.Min(minX, maxX);
        float sMaxX = MathF.Max(minX, maxX);
        float sMinY = MathF.Min(minY, maxY);
        float sMaxY = MathF.Max(minY, maxY);
        return new ShapeBounds(sMinX, sMinY, sMaxX, sMaxY);
    }

    /// <summary>Expands by amount on every side.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ShapeBounds Expand(float amount) => new(MinX - amount, MinY - amount, MaxX + amount, MaxY + amount);

    /// <summary>Containment with tolerance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(Vec2 point, float epsilon = ShapeConstants.Epsilon) =>
        point.X >= MinX - epsilon && point.X <= MaxX + epsilon &&
        point.Y >= MinY - epsilon && point.Y <= MaxY + epsilon;
}

/// <summary>Gradient color stop.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct GradientStop(float Offset, ResolvedColor Color);

/// <summary>Stroke alignment.</summary>
public enum ShapeStrokeAlignment : byte
{
    /// <summary>Centered on the edge.</summary>
    Center = 0,

    /// <summary>Inside the edge.</summary>
    Inside = 1,

    /// <summary>Outside the edge.</summary>
    Outside = 2,
}

/// <summary>Gradient spread.</summary>
public enum GradientSpread : byte
{
    /// <summary>Clamp.</summary>
    Pad = 0,

    /// <summary>Repeat.</summary>
    Repeat = 1,

    /// <summary>Reflect.</summary>
    Reflect = 2,
}

/// <summary>Gradient color space.</summary>
public enum GradientColorSpace : byte
{
    /// <summary>sRGB.</summary>
    Srgb = 0,

    /// <summary>Linear sRGB.</summary>
    LinearSrgb = 1,

    /// <summary>HSL.</summary>
    Hsl = 2,

    /// <summary>Lab.</summary>
    Lab = 3,
}

/// <summary>Gradient units.</summary>
public enum GradientUnits : byte
{
    /// <summary>Shape bounds.</summary>
    ShapeBounds = 0,

    /// <summary>Local space.</summary>
    Local = 1,
}

/// <summary>Polygon winding.</summary>
public enum PolygonWinding : byte
{
    /// <summary>Counter-clockwise.</summary>
    Ccw = 0,

    /// <summary>Clockwise.</summary>
    Cw = 1,
}

/// <summary>Hit target.</summary>
public enum ShapeHitTarget : byte
{
    /// <summary>No hit.</summary>
    None = 0,

    /// <summary>Fill hit.</summary>
    Fill = 1,

    /// <summary>Stroke hit.</summary>
    Stroke = 2,
}

/// <summary>Shape kind.</summary>
public enum ShapeKind : byte
{
    /// <summary>Rectangle.</summary>
    Rectangle = 0,

    /// <summary>Circle.</summary>
    Circle = 1,

    /// <summary>Ellipse.</summary>
    Ellipse = 2,

    /// <summary>Triangle.</summary>
    Triangle = 3,

    /// <summary>Line.</summary>
    Line = 4,

    /// <summary>Polygon.</summary>
    Polygon = 5,
}

/// <summary>Paint kind.</summary>
public enum PaintKind : byte
{
    /// <summary>Solid.</summary>
    Solid = 0,

    /// <summary>Linear gradient.</summary>
    LinearGradient = 1,

    /// <summary>Radial gradient.</summary>
    RadialGradient = 2,
}
