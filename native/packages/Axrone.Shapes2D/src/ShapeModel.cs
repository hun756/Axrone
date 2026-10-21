namespace Axrone.Shapes2D;

/// <summary>Base 2D shape with paint, stroke, opacity and visibility.</summary>
public abstract class Shape2D
{
    private string? _cachedFingerprint;

    /// <summary>Shape kind.</summary>
    public abstract ShapeKind Kind { get; }

    /// <summary>Fill paint.</summary>
    public ShapePaint? Fill { get; init; }

    /// <summary>Stroke.</summary>
    public ShapeStroke? Stroke { get; init; }

    /// <summary>Opacity.</summary>
    public float Opacity
    {
        get => field;
        init => field = Math.Clamp(ThrowHelper.AssertFinite(value, nameof(Opacity)), 0f, 1f);
    } = 1.0f;

    /// <summary>Visibility.</summary>
    public bool Visible { get; init; } = true;

    /// <summary>Name.</summary>
    public string? Name { get; init; }

    /// <summary>Memoized fingerprint.</summary>
    public string Fingerprint => _cachedFingerprint ??= ShapeSerializer.CreateFingerprint(this);
}

/// <summary>Rectangle shape.</summary>
public sealed class RectangleShape : Shape2D
{
    /// <summary>X.</summary>
    public float X { get; }

    /// <summary>Y.</summary>
    public float Y { get; }

    /// <summary>Width.</summary>
    public float Width
    {
        get => field;
        init => field = ThrowHelper.AssertPositive(value, nameof(Width));
    }

    /// <summary>Height.</summary>
    public float Height
    {
        get => field;
        init => field = ThrowHelper.AssertPositive(value, nameof(Height));
    }

    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.Rectangle;

    /// <summary>Creates a rectangle.</summary>
    public RectangleShape(float x, float y, float width, float height)
    {
        X = ThrowHelper.AssertFinite(x, "rectangle.x");
        Y = ThrowHelper.AssertFinite(y, "rectangle.y");
        Width = ThrowHelper.AssertPositive(width, "rectangle.width");
        Height = ThrowHelper.AssertPositive(height, "rectangle.height");
    }

    /// <summary>Geometry view.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RectangleGeometry AsGeometry() => new(X, Y, Width, Height);
}

/// <summary>Circle shape.</summary>
public sealed class CircleShape : Shape2D
{
    /// <summary>Center X.</summary>
    public float Cx { get; }

    /// <summary>Center Y.</summary>
    public float Cy { get; }

    /// <summary>Radius.</summary>
    public float Radius
    {
        get => field;
        init => field = ThrowHelper.AssertPositive(value, nameof(Radius));
    }

    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.Circle;

    /// <summary>Creates a circle.</summary>
    public CircleShape(float cx, float cy, float radius)
    {
        Cx = ThrowHelper.AssertFinite(cx, "circle.cx");
        Cy = ThrowHelper.AssertFinite(cy, "circle.cy");
        Radius = ThrowHelper.AssertPositive(radius, "circle.radius");
    }

    /// <summary>Geometry view.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CircleGeometry AsGeometry() => new(Cx, Cy, Radius);
}

/// <summary>Ellipse shape.</summary>
public sealed class EllipseShape : Shape2D
{
    /// <summary>Center X.</summary>
    public float Cx { get; }

    /// <summary>Center Y.</summary>
    public float Cy { get; }

    /// <summary>X radius.</summary>
    public float RadiusX
    {
        get => field;
        init => field = ThrowHelper.AssertPositive(value, nameof(RadiusX));
    }

    /// <summary>Y radius.</summary>
    public float RadiusY
    {
        get => field;
        init => field = ThrowHelper.AssertPositive(value, nameof(RadiusY));
    }

    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.Ellipse;

    /// <summary>Creates an ellipse.</summary>
    public EllipseShape(float cx, float cy, float radiusX, float radiusY)
    {
        Cx = ThrowHelper.AssertFinite(cx, "ellipse.cx");
        Cy = ThrowHelper.AssertFinite(cy, "ellipse.cy");
        RadiusX = ThrowHelper.AssertPositive(radiusX, "ellipse.radiusX");
        RadiusY = ThrowHelper.AssertPositive(radiusY, "ellipse.radiusY");
    }

    /// <summary>Geometry view.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EllipseGeometry AsGeometry() => new(Cx, Cy, RadiusX, RadiusY);
}

/// <summary>Triangle shape.</summary>
public sealed class TriangleShape : Shape2D
{
    /// <summary>First vertex.</summary>
    public Vec2 A { get; }

    /// <summary>Second vertex.</summary>
    public Vec2 B { get; }

    /// <summary>Third vertex.</summary>
    public Vec2 C { get; }

    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.Triangle;

    /// <summary>Creates a triangle.</summary>
    public TriangleShape(Vec2 a, Vec2 b, Vec2 c)
    {
        A = a;
        B = b;
        C = c;
        float doubledArea = a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y);
        if (MathF.Abs(doubledArea) <= ShapeConstants.Epsilon)
        {
            ThrowHelper.ThrowShapeValidation("Triangle points must not be collinear.");
        }
    }

    /// <summary>Geometry view.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TriangleGeometry AsGeometry() => new(A, B, C);
}

/// <summary>Line shape.</summary>
public sealed class LineShape : Shape2D
{
    /// <summary>Start.</summary>
    public Vec2 Start { get; }

    /// <summary>End.</summary>
    public Vec2 End { get; }

    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.Line;

    /// <summary>Creates a line.</summary>
    public LineShape(Vec2 start, Vec2 end)
    {
        Start = start;
        End = end;
    }

    /// <summary>Geometry view.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LineGeometry AsGeometry() => new(Start, End);
}

/// <summary>Polygon shape.</summary>
public sealed class PolygonShape : Shape2D
{
    /// <summary>Outer ring, normalized CCW.</summary>
    public PolygonRing Outer { get; }

    /// <summary>Holes, normalized CW.</summary>
    public ImmutableArray<PolygonRing> Holes { get; }

    /// <summary>Always closed.</summary>
    public bool Closed { get; }

    /// <summary>Convexity.</summary>
    public bool Convex { get; }

    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.Polygon;

    /// <summary>Creates a polygon.</summary>
    public PolygonShape(
        ReadOnlySpan<Vec2> outerPoints,
        ReadOnlySpan<PolygonRing> holes = default,
        PolygonWinding? declaredOuterWinding = null,
        bool closed = true)
    {
        if (!closed)
        {
            ThrowHelper.ThrowShapeValidation("Open polygons are not supported by PolygonShape.");
        }

        if (outerPoints.Length < 3)
        {
            ThrowHelper.ThrowShapeValidation("polygon.outer must contain at least 3 points.");
        }

        double outerArea = PolygonMath.SignedArea(outerPoints);
        if (Math.Abs(outerArea) <= ShapeConstants.DoubleEpsilon)
        {
            ThrowHelper.ThrowShapeValidation("polygon.outer has zero signed area; ring is degenerate.");
        }

        if (!PolygonMath.IsSimplePolygon(outerPoints))
        {
            ThrowHelper.ThrowShapeValidation("polygon.outer is not a simple polygon (edges self-intersect).");
        }

        PolygonWinding detectedWinding = outerArea > 0.0 ? PolygonWinding.Ccw : PolygonWinding.Cw;
        if (declaredOuterWinding.HasValue && declaredOuterWinding.Value != detectedWinding)
        {
            ThrowHelper.ThrowShapeValidation($"polygon.outer declares winding '{declaredOuterWinding.Value}' but geometry is wound '{detectedWinding}'.");
        }

        Vec2[] normalizedOuter = detectedWinding == PolygonWinding.Ccw
            ? outerPoints.ToArray()
            : ReversePoints(outerPoints);

        Outer = new PolygonRing(normalizedOuter, PolygonWinding.Ccw);
        Convex = PolygonMath.IsConvexPolygon(normalizedOuter);

        if (holes.Length > 0)
        {
            var holeList = new List<PolygonRing>(holes.Length);
            for (int i = 0; i < holes.Length; i++)
            {
                PolygonRing h = holes[i];
                if (h.Points.Length < 3)
                {
                    ThrowHelper.ThrowShapeValidation($"polygon.holes[{i}] must contain at least 3 points.");
                }

                double hArea = PolygonMath.SignedArea(h.Points.AsSpan());
                if (Math.Abs(hArea) <= ShapeConstants.DoubleEpsilon)
                {
                    ThrowHelper.ThrowShapeValidation($"polygon.holes[{i}] is degenerate (zero area).");
                }

                PolygonWinding detectedHoleWinding = hArea > 0.0 ? PolygonWinding.Ccw : PolygonWinding.Cw;
                if (h.Winding != detectedHoleWinding)
                {
                    ThrowHelper.ThrowShapeValidation($"polygon.holes[{i}] declares winding '{h.Winding}' but geometry is wound '{detectedHoleWinding}'.");
                }

                Vec2[] normHole = detectedHoleWinding == PolygonWinding.Cw
                    ? h.Points.AsSpan().ToArray()
                    : ReversePoints(h.Points.AsSpan());

                if (!PolygonMath.PointInPolygon(normalizedOuter, normHole[0]))
                {
                    ThrowHelper.ThrowShapeValidation($"polygon.holes[{i}] is not contained within the outer ring.");
                }

                holeList.Add(new PolygonRing(normHole, PolygonWinding.Cw));
            }

            for (int i = 0; i < holeList.Count; i++)
            {
                for (int j = 0; j < holeList.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    if (PolygonMath.PointInPolygon(holeList[j].Points.AsSpan(), holeList[i].Points[0]))
                    {
                        ThrowHelper.ThrowShapeValidation($"polygon.holes[{i}] overlaps with polygon.holes[{j}].");
                    }
                }
            }

            Holes = holeList.Count == 0
                ? ImmutableArray<PolygonRing>.Empty
                : ImmutableCollectionsMarshal.AsImmutableArray(holeList.ToArray());
        }

        Closed = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vec2[] ReversePoints(ReadOnlySpan<Vec2> points)
    {
        Vec2[] rev = GC.AllocateUninitializedArray<Vec2>(points.Length);
        for (int i = 0; i < points.Length; i++)
        {
            rev[i] = points[points.Length - 1 - i];
        }

        return rev;
    }
}

/// <summary>Shape factory.</summary>
public static class ShapeFactory
{
    /// <summary>Creates a rectangle.</summary>
    public static RectangleShape CreateRectangle(float x, float y, float width, float height, ShapePaint? fill = null, ShapeStroke? stroke = null, float opacity = 1.0f, bool visible = true, string? name = null) =>
        new(x, y, width, height)
        {
            Fill = fill,
            Stroke = stroke,
            Opacity = opacity,
            Visible = visible,
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
        };

    /// <summary>Creates a circle.</summary>
    public static CircleShape CreateCircle(float cx, float cy, float radius, ShapePaint? fill = null, ShapeStroke? stroke = null, float opacity = 1.0f, bool visible = true, string? name = null) =>
        new(cx, cy, radius)
        {
            Fill = fill,
            Stroke = stroke,
            Opacity = opacity,
            Visible = visible,
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
        };

    /// <summary>Creates an ellipse.</summary>
    public static EllipseShape CreateEllipse(float cx, float cy, float radiusX, float radiusY, ShapePaint? fill = null, ShapeStroke? stroke = null, float opacity = 1.0f, bool visible = true, string? name = null) =>
        new(cx, cy, radiusX, radiusY)
        {
            Fill = fill,
            Stroke = stroke,
            Opacity = opacity,
            Visible = visible,
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
        };

    /// <summary>Creates a triangle.</summary>
    public static TriangleShape CreateTriangle(Vec2 a, Vec2 b, Vec2 c, ShapePaint? fill = null, ShapeStroke? stroke = null, float opacity = 1.0f, bool visible = true, string? name = null) =>
        new(a, b, c)
        {
            Fill = fill,
            Stroke = stroke,
            Opacity = opacity,
            Visible = visible,
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
        };

    /// <summary>Creates a line.</summary>
    public static LineShape CreateLine(Vec2 start, Vec2 end, ShapeStroke? stroke = null, float opacity = 1.0f, bool visible = true, string? name = null)
    {
        if ((end - start).LengthSquared() <= ShapeConstants.Epsilon && stroke is null)
        {
            ThrowHelper.ThrowShapeValidation("Zero-length lines require a stroke.");
        }

        if (stroke.HasValue && stroke.Value.Alignment != ShapeStrokeAlignment.Center)
        {
            ThrowHelper.ThrowShapeValidation($"line.stroke.alignment must be 'center', got '{stroke.Value.Alignment}'.");
        }

        return new LineShape(start, end)
        {
            Fill = null,
            Stroke = stroke,
            Opacity = opacity,
            Visible = visible,
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
        };
    }

    /// <summary>Creates a polygon.</summary>
    public static PolygonShape CreatePolygon(
        ReadOnlySpan<Vec2> outer,
        ReadOnlySpan<PolygonRing> holes = default,
        PolygonWinding? winding = null,
        ShapePaint? fill = null,
        ShapeStroke? stroke = null,
        float opacity = 1.0f,
        bool visible = true,
        string? name = null) =>
        new(outer, holes, winding)
        {
            Fill = fill,
            Stroke = stroke,
            Opacity = opacity,
            Visible = visible,
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
        };

    /// <summary>Dispatches on shape kind.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult Match<TResult>(
        this Shape2D shape,
        Func<RectangleShape, TResult> rectangle,
        Func<CircleShape, TResult> circle,
        Func<EllipseShape, TResult> ellipse,
        Func<TriangleShape, TResult> triangle,
        Func<LineShape, TResult> line,
        Func<PolygonShape, TResult> polygon)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(rectangle);
        ArgumentNullException.ThrowIfNull(circle);
        ArgumentNullException.ThrowIfNull(ellipse);
        ArgumentNullException.ThrowIfNull(triangle);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(polygon);

        return shape switch
        {
            RectangleShape r => rectangle(r),
            CircleShape c => circle(c),
            EllipseShape e => ellipse(e),
            TriangleShape t => triangle(t),
            LineShape l => line(l),
            PolygonShape p => polygon(p),
            _ => throw new UnreachableException(),
        };
    }
}

/// <summary>Typestate marker for polygon builder phases.</summary>
public interface IPolygonBuilderPhase
{
}

/// <summary>Empty builder phase.</summary>
public readonly struct PhaseEmpty : IPolygonBuilderPhase
{
}

/// <summary>Outer ring configured phase.</summary>
public readonly struct PhaseOuterConfigured : IPolygonBuilderPhase
{
}

/// <summary>Typestate polygon builder.</summary>
public ref struct PolygonBuilder<TPhase>
    where TPhase : struct, IPolygonBuilderPhase
{
    private ReadOnlySpan<Vec2> _outer;
    private PolygonWinding? _outerWinding;
    private readonly List<PolygonRing> _holes;

    private PolygonBuilder(ReadOnlySpan<Vec2> outer, PolygonWinding? winding, List<PolygonRing> holes)
    {
        _outer = outer;
        _outerWinding = winding;
        _holes = holes;
    }

    /// <summary>Creates an empty builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PolygonBuilder<PhaseEmpty> Create() => new(default, null, new List<PolygonRing>());

    /// <summary>Configures the outer ring.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PolygonBuilder<PhaseOuterConfigured> WithOuter(ReadOnlySpan<Vec2> outerPoints, PolygonWinding? winding = null) =>
        new(outerPoints, winding, _holes);

    /// <summary>Adds a hole.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PolygonBuilder<PhaseOuterConfigured> AddHole(ReadOnlySpan<Vec2> holePoints, PolygonWinding winding = PolygonWinding.Cw)
    {
        _holes.Add(new PolygonRing(holePoints, winding));
        return new PolygonBuilder<PhaseOuterConfigured>(_outer, _outerWinding, _holes);
    }

    /// <summary>Builds a polygon.</summary>
    public PolygonShape Build(ShapePaint? fill = null, ShapeStroke? stroke = null, float opacity = 1.0f, bool visible = true, string? name = null)
    {
        if (typeof(TPhase) != typeof(PhaseOuterConfigured))
        {
            ThrowHelper.ThrowShapeValidation("Cannot build a polygon without configuring the outer ring.");
        }

        return ShapeFactory.CreatePolygon(_outer, CollectionsMarshal.AsSpan(_holes), _outerWinding, fill, stroke, opacity, visible, name);
    }
}
