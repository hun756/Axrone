namespace Axrone.Physics2D.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class ShapeTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void Circle_SupportBoundsMass()
    {
        var circle = new Circle2(new Vec2(1, 2), 3f);

        Circle2.Support(in circle, new Vec2(1, 0)).Should().Be(new Vec2(4, 2));
        Circle2.Support(in circle, Vec2.Zero).Should().Be(new Vec2(1, 2));
        Circle2.ComputeCentroid(in circle).Should().Be(new Vec2(1, 2));
        Circle2.ComputeBounds(in circle).Should().Be(new Aabb2D(new Vec2(-2, -1), new Vec2(4, 5)));

        MassData mass = circle.ComputeMass(2f);
        mass.Mass.Should().BeApproximately(MathF.PI * 9f * 2f, Tolerance);
        mass.Inertia.Should().BeApproximately(0.5f * mass.Mass * 9f, Tolerance);
        mass.InverseMass.Should().BeApproximately(1f / mass.Mass, Tolerance);
        mass.Centroid.Should().Be(new Vec2(1, 2));

        Action negative = () => _ = new Circle2(new Vec2(0, 0), -1f);
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Capsule_SupportBoundsMass()
    {
        var capsule = new Capsule2D(new Vec2(-2, 0), new Vec2(2, 0), 1f);

        Capsule2D.Support(in capsule, new Vec2(1, 0)).Should().Be(new Vec2(3, 0));
        Capsule2D.Support(in capsule, new Vec2(0, 1)).Should().Be(new Vec2(-2, 1));
        Capsule2D.ComputeCentroid(in capsule).Should().Be(new Vec2(0, 0));
        Capsule2D.ComputeBounds(in capsule).Should().Be(new Aabb2D(new Vec2(-3, -1), new Vec2(3, 1)));

        MassData mass = capsule.ComputeMass(1f);
        float rectArea = 2f * 1f * 4f;
        float capArea = MathF.PI * 1f;
        mass.Mass.Should().BeApproximately(rectArea + capArea, Tolerance);
        float expected = (rectArea * (16f + 4f) / 12f) + (0.5f * capArea * 1f);
        mass.Inertia.Should().BeApproximately(expected, Tolerance);

        var degenerate = new Capsule2D(new Vec2(5, 5), new Vec2(5, 5), 2f);
        Capsule2D.Support(in degenerate, new Vec2(1, 0)).Should().Be(new Vec2(7, 5));
    }

    [Fact]
    public void Polygon_Box_FactorySupportMass()
    {
        Polygon2D box = Polygon2D.CreateBox(new Vec2(1, 2));

        box.Count.Should().Be(4);
        Vec2 supportX = Polygon2D.Support(in box, new Vec2(1, 0));
        Vec2.Dot(supportX, new Vec2(1, 0)).Should().BeApproximately(1f, Tolerance);
        Polygon2D.Support(in box, new Vec2(-1, -1)).Should().Be(new Vec2(-1, -2));
        Polygon2D.ComputeCentroid(in box).Should().Be(new Vec2(0, 0));
        Polygon2D.ComputeBounds(in box).Should().Be(new Aabb2D(new Vec2(-1, -2), new Vec2(1, 2)));

        MassData mass = box.ComputeMass(1f);
        mass.Mass.Should().BeApproximately(8f, Tolerance);
        mass.Inertia.Should().BeApproximately(8f * (4f + 16f) / 12f, Tolerance);
        mass.Centroid.Should().Be(new Vec2(0, 0));
    }

    [Fact]
    public void Polygon_ClockwiseInput_WindsToCcw()
    {
        Vec2[] clockwise =
        [
            new(0f, 0f),
            new(0f, 1f),
            new(1f, 1f),
            new(1f, 0f),
        ];

        Polygon2D polygon = Polygon2D.Create(clockwise);

        polygon.Vertices[0].Should().Be(new Vec2(1, 0));
        Polygon2D.Support(in polygon, new Vec2(1, 1)).Should().Be(new Vec2(1, 1));
    }

    [Fact]
    public void Polygon_InvalidInput_Throws()
    {
        Action tooFew = () => Polygon2D.Create(new Vec2[] { new(0, 0), new(1, 1) });
        Action concave = () => Polygon2D.Create(new Vec2[] { new(0, 0), new(2, 0), new(2, 2), new(1, 1), new(0, 2) });
        Action nan = () => Polygon2D.Create(new Vec2[] { new(0, 0), new(float.NaN, 0), new(0, 1) });

        tooFew.Should().Throw<ArgumentException>();
        concave.Should().Throw<ArgumentException>();
        nan.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Shapes_Equality_RoundTrips()
    {
        new Circle2(new Vec2(1, 2), 3f).Should().Be(new Circle2(new Vec2(1, 2), 3f));
        (new Capsule2D(new Vec2(0, 0), new Vec2(1, 0), 1f) == new Capsule2D(new Vec2(0, 0), new Vec2(1, 0), 1f)).Should().BeTrue();
        Polygon2D.CreateBox(new Vec2(1, 1)).Should().Be(Polygon2D.CreateBox(new Vec2(1, 1)));
    }
}
