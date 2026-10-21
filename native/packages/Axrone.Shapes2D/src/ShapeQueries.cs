namespace Axrone.Shapes2D;

/// <summary>Geometry queries, hit testing, paint sampling and compilation.</summary>
public static class ShapeQueries
{
    /// <summary>Bounds with optional stroke.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShapeBounds GetBounds(Shape2D shape, bool includeStroke = true, ShapeApproximationOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return includeStroke ? GeometryEngine.GetShapeBounds(shape, options) : GeometryEngine.GetGeometryBounds(shape);
    }

    /// <summary>Area.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetArea(Shape2D shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape switch
        {
            RectangleShape r => r.AsGeometry().ComputeArea(),
            CircleShape c => c.AsGeometry().ComputeArea(),
            EllipseShape e => e.AsGeometry().ComputeArea(),
            TriangleShape t => t.AsGeometry().ComputeArea(),
            LineShape => 0f,
            PolygonShape p => ComputePolygonArea(p),
            _ => 0f,
        };
    }

    private static float ComputePolygonArea(PolygonShape p)
    {
        double area = PolygonMath.AbsoluteArea(p.Outer.Points.AsSpan());
        foreach (PolygonRing hole in p.Holes)
        {
            area -= PolygonMath.AbsoluteArea(hole.Points.AsSpan());
        }

        return (float)Math.Max(0.0, area);
    }

    /// <summary>Perimeter.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetPerimeter(Shape2D shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape switch
        {
            RectangleShape r => r.AsGeometry().ComputePerimeter(),
            CircleShape c => c.AsGeometry().ComputePerimeter(),
            EllipseShape e => e.AsGeometry().ComputePerimeter(),
            TriangleShape t => t.AsGeometry().ComputePerimeter(),
            LineShape l => l.AsGeometry().ComputePerimeter(),
            PolygonShape p => ComputePolygonPerimeter(p),
            _ => 0f,
        };
    }

    private static float ComputePolygonPerimeter(PolygonShape p)
    {
        float perim = PolygonMath.Perimeter(p.Outer.Points.AsSpan());
        foreach (PolygonRing hole in p.Holes)
        {
            perim += PolygonMath.Perimeter(hole.Points.AsSpan());
        }

        return perim;
    }

    /// <summary>Centroid.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 GetCentroid(Shape2D shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape switch
        {
            RectangleShape r => r.AsGeometry().ComputeCentroid(),
            CircleShape c => c.AsGeometry().ComputeCentroid(),
            EllipseShape e => e.AsGeometry().ComputeCentroid(),
            TriangleShape t => t.AsGeometry().ComputeCentroid(),
            LineShape l => l.AsGeometry().ComputeCentroid(),
            PolygonShape p => PolygonMath.Centroid(p.Outer.Points.AsSpan()),
            _ => Vec2.Zero,
        };
    }

    /// <summary>Fill containment.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool ContainsPoint(Shape2D shape, Vec2 point)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape switch
        {
            RectangleShape r => r.AsGeometry().ContainsPoint(point),
            CircleShape c => c.AsGeometry().ContainsPoint(point),
            EllipseShape e => e.AsGeometry().ContainsPoint(point),
            TriangleShape t => t.AsGeometry().ContainsPoint(point),
            LineShape => false,
            PolygonShape p => PolygonMath.PointInPolygonWithHoles(p.Outer.Points.AsSpan(), p.Holes.AsSpan(), point),
            _ => false,
        };
    }

    /// <summary>Stroke containment.</summary>
    public static bool ContainsStrokePoint(Shape2D shape, Vec2 point, ShapeApproximationOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (!shape.Stroke.HasValue)
        {
            return false;
        }

        ShapeStroke stroke = shape.Stroke.Value;
        if (shape is LineShape line)
        {
            float halfWidth = stroke.Width * 0.5f;
            return PolygonMath.DistanceToSegmentSquared(point, line.Start, line.End) <= (halfWidth * halfWidth) + ShapeConstants.Epsilon;
        }

        float[] contour = GeometryEngine.GetContour(shape, options);
        ReadOnlySpan<Vec2> span = MemoryMarshal.Cast<float, Vec2>(contour);
        GeometryEngine.GetStrokeOffsets(stroke, out float outerOffset, out float innerOffset);

        bool isConcave = !PolygonMath.IsConvexPolygon(span);
        if (isConcave)
        {
            float edgeDist = PolygonMath.PointToPolygonEdgeDistance(span, point);
            float halfWidth = stroke.Width * 0.5f;
            return stroke.Alignment switch
            {
                ShapeStrokeAlignment.Inside => PolygonMath.PointInPolygon(span, point) && edgeDist <= stroke.Width + ShapeConstants.Epsilon,
                ShapeStrokeAlignment.Outside => !PolygonMath.PointInPolygon(span, point) && edgeDist <= stroke.Width + ShapeConstants.Epsilon,
                _ => edgeDist <= halfWidth + ShapeConstants.Epsilon,
            };
        }

        float[] outerC = outerOffset <= ShapeConstants.Epsilon ? contour : GeometryEngine.OffsetConvexContour(contour, outerOffset);
        if (!PolygonMath.PointInConvexPolygon(MemoryMarshal.Cast<float, Vec2>(outerC), point))
        {
            return false;
        }

        if (innerOffset <= ShapeConstants.Epsilon)
        {
            return true;
        }

        float[] innerC = GeometryEngine.OffsetConvexContour(contour, -innerOffset);
        return !PolygonMath.PointInConvexPolygon(MemoryMarshal.Cast<float, Vec2>(innerC), point);
    }

    /// <summary>Hit test: stroke first, then fill.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShapeHitTarget HitTest(Shape2D shape, Vec2 point, ShapeApproximationOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (!shape.Visible || shape.Opacity <= 0f)
        {
            return ShapeHitTarget.None;
        }

        if (shape.Stroke.HasValue && ContainsStrokePoint(shape, point, options))
        {
            return ShapeHitTarget.Stroke;
        }

        if (shape.Fill is not null && ContainsPoint(shape, point))
        {
            return ShapeHitTarget.Fill;
        }

        return ShapeHitTarget.None;
    }

    /// <summary>Samples paint at a point for the hit target.</summary>
    public static ResolvedColor? SamplePaint(Shape2D shape, ShapeHitTarget target, Vec2 point, ShapeApproximationOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (!shape.Visible || shape.Opacity <= 0f)
        {
            return null;
        }

        if (target == ShapeHitTarget.Fill)
        {
            if (shape.Fill is null || !ContainsPoint(shape, point))
            {
                return null;
            }

            ResolvedColor sampled = PaintSampler.Sample(shape.Fill, point, GeometryEngine.GetGeometryBounds(shape));
            return sampled.ModulateAlpha(shape.Opacity);
        }

        if (target == ShapeHitTarget.Stroke)
        {
            if (!shape.Stroke.HasValue || !ContainsStrokePoint(shape, point, options))
            {
                return null;
            }

            ResolvedColor sampled = PaintSampler.Sample(shape.Stroke.Value.Paint, point, GeometryEngine.GetShapeBounds(shape, options));
            return sampled.ModulateAlpha(shape.Opacity);
        }

        return null;
    }

    /// <summary>Compiles cached geometry, meshes and metrics.</summary>
    public static CompiledShape2D Compile(Shape2D shape, ShapeCompileOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(shape);
        string fp = shape.Fingerprint;
        ShapeBounds geomBounds = GeometryEngine.GetGeometryBounds(shape);
        ShapeBounds fullBounds = GeometryEngine.GetShapeBounds(shape, options.Approximation);
        float area = GetArea(shape);
        float perimeter = GetPerimeter(shape);
        float[] contour = GeometryEngine.GetContour(shape, options.Approximation);

        ShapeMesh2D? fillMesh = options.IncludeFillMesh ? GeometryEngine.BuildFillMesh(shape, options.Approximation) : null;
        ShapeMesh2D? strokeMesh = options.IncludeStrokeMesh ? GeometryEngine.BuildStrokeMesh(shape, options.Approximation) : null;

        return new CompiledShape2D(shape, fp, geomBounds, fullBounds, area, perimeter, contour, fillMesh, strokeMesh);
    }
}

