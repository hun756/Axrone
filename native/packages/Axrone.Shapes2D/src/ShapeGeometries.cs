namespace Axrone.Shapes2D;

/// <summary>Geometry role contract.</summary>
/// <typeparam name="TSelf">Geometry type.</typeparam>
public interface IShapeGeometry<TSelf>
    where TSelf : struct, IShapeGeometry<TSelf>
{
    /// <summary>Shape kind.</summary>
    static abstract ShapeKind Kind { get; }

    /// <summary>Bounds.</summary>
    ShapeBounds ComputeBounds();

    /// <summary>Area.</summary>
    float ComputeArea();

    /// <summary>Perimeter.</summary>
    float ComputePerimeter();

    /// <summary>Centroid.</summary>
    Vec2 ComputeCentroid();

    /// <summary>Containment.</summary>
    bool ContainsPoint(Vec2 point);
}

/// <summary>Rectangle geometry.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct RectangleGeometry(float X, float Y, float Width, float Height) : IShapeGeometry<RectangleGeometry>
{
    /// <inheritdoc/>
    public static ShapeKind Kind => ShapeKind.Rectangle;

    /// <inheritdoc/>
    public ShapeBounds ComputeBounds() => ShapeBounds.Create(X, Y, X + Width, Y + Height);

    /// <inheritdoc/>
    public float ComputeArea() => Width * Height;

    /// <inheritdoc/>
    public float ComputePerimeter() => 2f * (Width + Height);

    /// <inheritdoc/>
    public Vec2 ComputeCentroid() => new(X + Width * 0.5f, Y + Height * 0.5f);

    /// <inheritdoc/>
    public bool ContainsPoint(Vec2 point) => point.X >= X && point.X <= X + Width && point.Y >= Y && point.Y <= Y + Height;
}

/// <summary>Circle geometry.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CircleGeometry(float Cx, float Cy, float Radius) : IShapeGeometry<CircleGeometry>
{
    /// <inheritdoc/>
    public static ShapeKind Kind => ShapeKind.Circle;

    /// <inheritdoc/>
    public ShapeBounds ComputeBounds() => ShapeBounds.Create(Cx - Radius, Cy - Radius, Cx + Radius, Cy + Radius);

    /// <inheritdoc/>
    public float ComputeArea() => MathF.PI * Radius * Radius;

    /// <inheritdoc/>
    public float ComputePerimeter() => ShapeConstants.Tau * Radius;

    /// <inheritdoc/>
    public Vec2 ComputeCentroid() => new(Cx, Cy);

    /// <inheritdoc/>
    public bool ContainsPoint(Vec2 point)
    {
        float dx = point.X - Cx;
        float dy = point.Y - Cy;
        return (dx * dx + dy * dy) <= (Radius * Radius) + ShapeConstants.Epsilon;
    }
}

/// <summary>Ellipse geometry.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct EllipseGeometry(float Cx, float Cy, float RadiusX, float RadiusY) : IShapeGeometry<EllipseGeometry>
{
    /// <inheritdoc/>
    public static ShapeKind Kind => ShapeKind.Ellipse;

    /// <inheritdoc/>
    public ShapeBounds ComputeBounds() => ShapeBounds.Create(Cx - RadiusX, Cy - RadiusY, Cx + RadiusX, Cy + RadiusY);

    /// <inheritdoc/>
    public float ComputeArea() => MathF.PI * RadiusX * RadiusY;

    /// <inheritdoc/>
    public float ComputePerimeter()
    {
        float a = MathF.Max(RadiusX, RadiusY);
        float b = MathF.Min(RadiusX, RadiusY);
        return MathF.PI * (3f * (a + b) - MathF.Sqrt((3f * a + b) * (a + 3f * b)));
    }

    /// <inheritdoc/>
    public Vec2 ComputeCentroid() => new(Cx, Cy);

    /// <inheritdoc/>
    public bool ContainsPoint(Vec2 point)
    {
        float dx = (point.X - Cx) / RadiusX;
        float dy = (point.Y - Cy) / RadiusY;
        return (dx * dx + dy * dy) <= 1f + ShapeConstants.Epsilon;
    }
}

/// <summary>Triangle geometry.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct TriangleGeometry(Vec2 A, Vec2 B, Vec2 C) : IShapeGeometry<TriangleGeometry>
{
    /// <inheritdoc/>
    public static ShapeKind Kind => ShapeKind.Triangle;

    /// <inheritdoc/>
    public ShapeBounds ComputeBounds() => ShapeBounds.Create(
        MathF.Min(A.X, MathF.Min(B.X, C.X)),
        MathF.Min(A.Y, MathF.Min(B.Y, C.Y)),
        MathF.Max(A.X, MathF.Max(B.X, C.X)),
        MathF.Max(A.Y, MathF.Max(B.Y, C.Y)));

    /// <inheritdoc/>
    public float ComputeArea() => MathF.Abs(A.X * (B.Y - C.Y) + B.X * (C.Y - A.Y) + C.X * (A.Y - B.Y)) * 0.5f;

    /// <inheritdoc/>
    public float ComputePerimeter() => (B - A).Length() + (C - B).Length() + (A - C).Length();

    /// <inheritdoc/>
    public Vec2 ComputeCentroid() => new((A.X + B.X + C.X) / 3f, (A.Y + B.Y + C.Y) / 3f);

    /// <inheritdoc/>
    public bool ContainsPoint(Vec2 point) =>
        PolygonMath.PointInConvexPolygon([A, B, C], point);
}

/// <summary>Line geometry.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct LineGeometry(Vec2 Start, Vec2 End) : IShapeGeometry<LineGeometry>
{
    /// <inheritdoc/>
    public static ShapeKind Kind => ShapeKind.Line;

    /// <inheritdoc/>
    public ShapeBounds ComputeBounds() => ShapeBounds.Create(Start.X, Start.Y, End.X, End.Y);

    /// <inheritdoc/>
    public float ComputeArea() => 0f;

    /// <inheritdoc/>
    public float ComputePerimeter() => (End - Start).Length();

    /// <inheritdoc/>
    public Vec2 ComputeCentroid() => (Start + End) * 0.5f;

    /// <inheritdoc/>
    public bool ContainsPoint(Vec2 point) => false;
}
