namespace Axrone.Shapes2D.Tests;

public sealed class ShapeTriangulationTests
{
    private static readonly Vec2[] Square = [new(0f, 0f), new(4f, 0f), new(4f, 4f), new(0f, 4f)];

    [Fact]
    public void EarClipping_Triangulates_Convex()
    {
        uint[] indices = PolygonTriangulator.TriangulateEarClipping(Square);
        indices.Should().HaveCount(6);
        indices.Should().OnlyContain(i => i < 4u);
    }

    [Fact]
    public void EarClipping_Handles_Concave()
    {
        Vec2[] concave = [new(0f, 0f), new(4f, 0f), new(4f, 4f), new(2f, 1f), new(0f, 4f)];
        uint[] indices = PolygonTriangulator.TriangulateEarClipping(concave);
        indices.Should().HaveCount((concave.Length - 2) * 3);
    }

    [Fact]
    public void EarClipping_Rejects_Degenerate()
    {
        Action flat = () => PolygonTriangulator.TriangulateEarClipping([new Vec2(0f, 0f), new Vec2(1f, 1f), new Vec2(2f, 2f)]);
        flat.Should().Throw<ShapeValidationException>();
        PolygonTriangulator.TriangulateEarClipping([new Vec2(0f, 0f), new Vec2(1f, 0f)]).Should().BeEmpty();
    }

    [Fact]
    public void Holes_Bridge_And_Triangulate()
    {
        var holes = new PolygonRing[] { new(new Vec2[] { new(1f, 1f), new(2f, 1f), new(2f, 2f), new(1f, 2f) }, PolygonWinding.Cw) };
        (Vec2[] positions, uint[] indices) = PolygonTriangulator.TriangulateWithHoles(Square, holes);
        positions.Should().HaveCount(8);
        indices.Should().HaveCount(24);
        indices.Should().OnlyContain(i => i < 8u);
    }

    [Fact]
    public void Holes_Reject_Bad_Rings()
    {
        Action noOuter = () => PolygonTriangulator.TriangulateWithHoles([new Vec2(0f, 0f)], []);
        noOuter.Should().Throw<ShapeValidationException>();

        Action badHole = () => PolygonTriangulator.TriangulateWithHoles(
            Square, new PolygonRing[] { new(new Vec2[] { new(1f, 1f), new(2f, 1f) }, PolygonWinding.Cw) });
        badHole.Should().Throw<ShapeValidationException>();
    }
}
