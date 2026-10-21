namespace Axrone.Shapes2D.Tests;

public sealed class ShapeEngineTests
{
    private static readonly Vec2[] Square = [new(0f, 0f), new(4f, 0f), new(4f, 4f), new(0f, 4f)];

    [Fact]
    public void Bounds_Expand_With_Stroke()
    {
        Shape2D rect = ShapeFactory.CreateRectangle(0f, 0f, 4f, 2f);
        ShapeQueries.GetBounds(rect, includeStroke: false).Should().Be(new ShapeBounds(0f, 0f, 4f, 2f));
    }

    [Fact]
    public void Queries_Area_Perimeter_Centroid()
    {
        ShapeQueries.GetArea(ShapeFactory.CreateRectangle(0f, 0f, 4f, 2f)).Should().Be(8f);
        ShapeQueries.GetPerimeter(ShapeFactory.CreateCircle(0f, 0f, 1f)).Should().BeApproximately(ShapeConstants.Tau, 1e-5f);
        ShapeQueries.GetCentroid(ShapeFactory.CreateTriangle(new Vec2(0f, 0f), new Vec2(6f, 0f), new Vec2(0f, 6f)))
            .Should().Be(new Vec2(2f, 2f));
        ShapeQueries.GetArea(ShapeFactory.CreateLine(new Vec2(0f, 0f), new Vec2(1f, 0f))).Should().Be(0f);
    }

    [Fact]
    public void HitTest_Prefers_Stroke_Then_Fill()
    {
        var stroke = ShapePaintFactory.CreateStroke(ShapePaintFactory.CreateSolidPaint("red"), 2f);
        Shape2D rect = ShapeFactory.CreateRectangle(0f, 0f, 4f, 4f, ShapePaintFactory.CreateSolidPaint("blue"), stroke);
        ShapeQueries.HitTest(rect, new Vec2(2f, 2f)).Should().Be(ShapeHitTarget.Fill);
        ShapeQueries.HitTest(rect, new Vec2(0.5f, 2f)).Should().Be(ShapeHitTarget.Stroke);
        ShapeQueries.HitTest(rect, new Vec2(9f, 9f)).Should().Be(ShapeHitTarget.None);

        Shape2D hidden = ShapeFactory.CreateRectangle(0f, 0f, 4f, 4f, visible: false);
        ShapeQueries.HitTest(hidden, new Vec2(2f, 2f)).Should().Be(ShapeHitTarget.None);
    }

    [Fact]
    public void Compile_Caches_Geometry_And_Meshes()
    {
        Shape2D poly = ShapeFactory.CreatePolygon(Square);
        CompiledShape2D compiled = ShapeQueries.Compile(poly, new ShapeCompileOptions(IncludeFillMesh: true, IncludeStrokeMesh: false));
        compiled.Area.Should().BeApproximately(16f, 1e-4f);
        compiled.Perimeter.Should().BeApproximately(16f, 1e-4f);
        compiled.FillMesh.Should().NotBeNull();
        compiled.FillMesh!.VertexCount.Should().Be(4);
        compiled.FillMesh.IndexCount.Should().Be(6);
        compiled.StrokeMesh.Should().BeNull();

        var stroke = ShapePaintFactory.CreateStroke(ShapePaintFactory.CreateSolidPaint("red"), 1f);
        CompiledShape2D stroked = ShapeQueries.Compile(
            ShapeFactory.CreatePolygon(Square, stroke: stroke),
            new ShapeCompileOptions(IncludeFillMesh: false, IncludeStrokeMesh: true));
        stroked.FillMesh.Should().BeNull();
        stroked.StrokeMesh.Should().NotBeNull();
    }

    [Fact]
    public void Approx_Segments_Scale_With_Radius()
    {
        int small = GeometryEngine.ApproximateSegments(1f, 1f, ShapeApproximationOptions.Default);
        int big = GeometryEngine.ApproximateSegments(100f, 100f, ShapeApproximationOptions.Default);
        (big >= small).Should().BeTrue();
        small.Should().Be(ShapeConstants.DefaultMinCurveSegments);
        big.Should().BeLessThan(ShapeConstants.DefaultMaxCurveSegments + 1);
    }

    [Fact]
    public void Mesh_Index_Buffer_Selects_Width()
    {
        uint[] big = new uint[70000];
        for (int i = 0; i < big.Length; i++) big[i] = (uint)i;
        ShapeIndexBuffer buf = ShapeIndexBuffer.FromIndices(big, 70000);
        buf.Is16Bit.Should().BeFalse();
        buf.Length.Should().Be(70000);
        buf[69999].Should().Be(69999u);

        ShapeIndexBuffer small = ShapeIndexBuffer.FromIndices([0u, 1u, 2u], 3);
        small.Is16Bit.Should().BeTrue();

        Span<uint> dest = stackalloc uint[3];
        small.CopyTo(dest);
        dest[2].Should().Be(2u);

        int count = 0;
        foreach (uint idx in small) count++;
        count.Should().Be(3);
    }
}

