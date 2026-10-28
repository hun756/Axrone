namespace Axrone.Raycast.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class RaycastEngine3DTests
{
    private static readonly Ray3D AlongPositiveX = new(new Vec3(-5f, 0f, 0f), Vec3.UnitX);

    [Fact]
    public void ClosestReturnsNearestSphere()
    {
        using var engine = new RaycastEngine3D();
        RaycastShapeId near = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        RaycastShapeId far = engine.Register(RaycastShape3D.CreateSphere(new Vec3(5f, 0f, 0f), 1f));

        var query = new RaycastQuery3D(AlongPositiveX, 30f);
        engine.Raycast(in query, out RaycastHit3D hit).Should().BeTrue();

        hit.Shape.Should().Be(near);
        hit.Shape.Should().NotBe(far);
        hit.Distance.Should().BeApproximately(4f, 1e-4f);
        hit.Normal.X.Should().BeApproximately(-1f, 1e-4f);
        engine.Statistics.TotalQueries.Should().Be(1);
        engine.Statistics.TotalHits.Should().Be(1);
    }

    [Fact]
    public void AllHitsCollectAndSort()
    {
        using var engine = new RaycastEngine3D();
        RaycastShapeId near = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        RaycastShapeId far = engine.Register(RaycastShape3D.CreateSphere(new Vec3(5f, 0f, 0f), 1f));
        var query = new RaycastQuery3D(
            AlongPositiveX,
            30f,
            uint.MaxValue,
            RaycastFlags.AllHits | RaycastFlags.SortByDistance,
            8);

        Span<RaycastHit3D> results = stackalloc RaycastHit3D[8];
        int count = engine.Raycast(in query, results);

        count.Should().Be(2);
        results[0].Shape.Should().Be(near);
        results[0].Distance.Should().BeApproximately(4f, 1e-4f);
        results[1].Shape.Should().Be(far);
        results[1].Distance.Should().BeApproximately(9f, 1e-4f);
    }

    [Fact]
    public void TriangleCarriesBarycentricsAndWindingNormal()
    {
        using var engine = new RaycastEngine3D();
        RaycastShapeId id = engine.Register(RaycastShape3D.CreateTriangle(
            Vec3.Zero,
            new Vec3(1f, 0f, 0f),
            new Vec3(0f, 1f, 0f)));
        var query = new RaycastQuery3D(new Ray3D(new Vec3(0.25f, 0.25f, 1f), Vec3.NegativeUnitZ), 10f);

        engine.Raycast(in query, out RaycastHit3D hit).Should().BeTrue();

        hit.Shape.Should().Be(id);
        hit.TriangleIndex.Should().Be(0);
        hit.HasBarycentric.Should().BeTrue();
        hit.Barycentric.X.Should().BeApproximately(0.25f, 1e-4f);
        hit.Barycentric.Y.Should().BeApproximately(0.25f, 1e-4f);
        hit.Normal.Z.Should().BeApproximately(1f, 1e-4f);
    }

    [Fact]
    public void PlaneIsTestedOutsideTheBroadphaseAndRespectsBackfaceCulling()
    {
        using var engine = new RaycastEngine3D();
        RaycastShapeId id = engine.Register(RaycastShape3D.CreatePlane(Vec3.Zero, Vec3.UnitY));
        engine.ShapeCount.Should().Be(1);

        var fromAbove = new RaycastQuery3D(new Ray3D(new Vec3(0f, 5f, 0f), Vec3.NegativeUnitY), 20f);
        engine.Raycast(in fromAbove, out RaycastHit3D hit).Should().BeTrue();
        hit.Shape.Should().Be(id);
        hit.Distance.Should().BeApproximately(5f, 1e-4f);
        hit.Normal.Y.Should().BeApproximately(1f, 1e-4f);

        var fromBelow = new RaycastQuery3D(new Ray3D(new Vec3(0f, -5f, 0f), Vec3.UnitY), 20f);
        engine.Raycast(in fromBelow, out _).Should().BeTrue();
        var culled = fromBelow with { Flags = fromBelow.Flags | RaycastFlags.IgnoreBackfaces };
        engine.Raycast(in culled, out _).Should().BeFalse();

        engine.Unregister(id).Should().BeTrue();
        engine.Raycast(in fromAbove, out _).Should().BeFalse();
    }

    [Fact]
    public void CapsuleAndOrientedBoxAreHitThroughTheBroadphase()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateCapsule(new Vec3(0f, -1f, 0f), new Vec3(0f, 1f, 0f), 0.5f));
        var down = new RaycastQuery3D(new Ray3D(new Vec3(0f, 5f, 0f), Vec3.NegativeUnitY), 20f);
        engine.Raycast(in down, out RaycastHit3D capsuleHit).Should().BeTrue();
        capsuleHit.Distance.Should().BeApproximately(3.5f, 1e-4f);

        Quat rotation = Quat.CreateFromAxisAngle(Vec3.UnitZ, MathF.PI * 0.5f);
        _ = engine.Register(RaycastShape3D.CreateBox(new Vec3(5f, 0f, 0f), new Vec3(2f, 1f, 1f), rotation));
        var boxRay = new RaycastQuery3D(new Ray3D(new Vec3(5f, 5f, 0f), Vec3.NegativeUnitY), 20f);
        engine.Raycast(in boxRay, out RaycastHit3D boxHit).Should().BeTrue();
        boxHit.Distance.Should().BeApproximately(3f, 1e-4f);
    }

    [Fact]
    public void OctreeBroadphaseMatchesSpatialHash()
    {
        using var spatial = new RaycastEngine3D();
        using var octree = new RaycastEngine3D(new Aabb3D(new Vec3(-100f), new Vec3(100f)));
        octree.Broadphase.Should().Be(RaycastBroadphase3D.Octree);

        RaycastShapeId spatialId = spatial.Register(RaycastShape3D.CreateSphere(new Vec3(0f, 0f, 0f), 1f));
        RaycastShapeId octreeId = octree.Register(RaycastShape3D.CreateSphere(new Vec3(0f, 0f, 0f), 1f));
        var query = new RaycastQuery3D(AlongPositiveX, 30f);

        spatial.Raycast(in query, out RaycastHit3D spatialHit).Should().BeTrue();
        octree.Raycast(in query, out RaycastHit3D octreeHit).Should().BeTrue();

        spatialHit.Shape.Should().Be(spatialId);
        octreeHit.Shape.Should().Be(octreeId);
        octreeHit.Distance.Should().BeApproximately(spatialHit.Distance, 1e-4f);
        octreeHit.Normal.X.Should().BeApproximately(spatialHit.Normal.X, 1e-4f);
    }

    [Fact]
    public void OctreeRejectsOutOfDomainShapesAndRollsBack()
    {
        using var engine = new RaycastEngine3D(new Aabb3D(new Vec3(-10f), new Vec3(10f)));
        _ = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        engine.ShapeCount.Should().Be(1);

        Action outside = () => engine.Register(RaycastShape3D.CreateSphere(new Vec3(50f, 0f, 0f), 1f));
        outside.Should().Throw<ArgumentOutOfRangeException>();
        engine.ShapeCount.Should().Be(1);

        var query = new RaycastQuery3D(AlongPositiveX, 30f);
        engine.Raycast(in query, out RaycastHit3D hit).Should().BeTrue();
        hit.Distance.Should().BeApproximately(4f, 1e-4f);
    }

    [Fact]
    public void ShapeCastFindsInflatedContactAndRespectsLayers()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f, (uint)RaycastLayer.Dynamic));
        RaycastShape3D volume = RaycastShape3D.CreateSphere(new Vec3(-5f, 0f, 0f), 1f);

        engine.ShapeCast(in AlongPositiveX, in volume, 30f, out RaycastHit3D hit).Should().BeTrue();
        hit.Distance.Should().BeApproximately(3f, 1e-4f);
        hit.Point.X.Should().BeApproximately(-1f, 1e-4f);

        engine.ShapeCast(
            in AlongPositiveX,
            in volume,
            30f,
            out _,
            (uint)RaycastLayer.Static).Should().BeFalse();
    }

    [Fact]
    public void ShapeCastAgainstBoxAndCapsule()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateAabb(new Aabb3D(-1f, -1f, -1f, 1f, 1f, 1f)));
        RaycastShape3D boxVolume = RaycastShape3D.CreateAabb(new Aabb3D(-0.5f, -0.5f, -0.5f, 0.5f, 0.5f, 0.5f));

        engine.ShapeCast(in AlongPositiveX, in boxVolume, 30f, out RaycastHit3D boxHit).Should().BeTrue();
        boxHit.Distance.Should().BeApproximately(3.5f, 1e-4f);
        boxHit.Point.X.Should().BeApproximately(-1f, 1e-4f);

        var offset = new Ray3D(new Vec3(-5f, 1.6f, 0f), Vec3.UnitX);
        engine.ShapeCast(in offset, in boxVolume, 30f, out _).Should().BeFalse();

        using var capsuleEngine = new RaycastEngine3D();
        _ = capsuleEngine.Register(RaycastShape3D.CreateCapsule(new Vec3(0f, -1f, 0f), new Vec3(0f, 1f, 0f), 0.5f));
        RaycastShape3D sphereVolume = RaycastShape3D.CreateSphere(new Vec3(-5f, 0f, 0f), 0.5f);
        capsuleEngine.ShapeCast(in AlongPositiveX, in sphereVolume, 30f, out RaycastHit3D capsuleHit).Should().BeTrue();
        capsuleHit.Distance.Should().BeApproximately(4f, 1e-4f);
    }

    [Fact]
    public void ShapeCastRejectsUnsupportedVolumes()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        RaycastShape3D capsule = RaycastShape3D.CreateCapsule(new Vec3(-5f, -1f, 0f), new Vec3(-5f, 1f, 0f), 0.5f);

        Action act = () => engine.ShapeCast(in AlongPositiveX, in capsule, 30f, out _);
        act.Should().Throw<RaycastException>()
            .Which.Code.Should().Be(RaycastErrorCode.InvalidQuery);
    }

    [Fact]
    public void CachingAndStatisticsBehaveLike2D()
    {
        using var engine = new RaycastEngine3D { EnableCache = true };
        RaycastShapeId id = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        var query = new RaycastQuery3D(AlongPositiveX, 30f);

        engine.Raycast(in query, out _).Should().BeTrue();
        engine.Raycast(in query, out RaycastHit3D cached).Should().BeTrue();
        cached.Shape.Should().Be(id);
        engine.Statistics.TotalQueries.Should().Be(1);
        engine.Statistics.CacheHits.Should().Be(1);

        engine.AdvanceFrame();
        engine.Raycast(in query, out _).Should().BeTrue();
        engine.Statistics.TotalQueries.Should().Be(2);

        engine.SetActive(id, false);
        engine.Raycast(in query, out _).Should().BeFalse();
    }

    [Fact]
    public void BatchWritesOneSlotPerQuery()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));

        RaycastQuery3D[] queries =
        [
            new RaycastQuery3D(AlongPositiveX, 30f),
            new RaycastQuery3D(new Ray3D(new Vec3(-5f, 0f, 0f), Vec3.UnitY), 30f),
        ];
        Span<RaycastHit3D> results = stackalloc RaycastHit3D[2];
        results.Clear();
        engine.RaycastBatch(queries, results);
        results[0].Shape.IsValid.Should().BeTrue();
        results[1].Shape.IsValid.Should().BeFalse();

        Span<bool> flags = stackalloc bool[2];
        engine.RaycastBatchAny(queries, flags);
        flags[0].Should().BeTrue();
        flags[1].Should().BeFalse();
    }

    [Fact]
    public void DisposeRejectsFurtherQueries()
    {
        var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        engine.Dispose();

        var query = new RaycastQuery3D(AlongPositiveX, 30f);
        Action act = () => engine.Raycast(in query, out _);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void DisposingOctreeEngineReleasesTheTree()
    {
        var engine = new RaycastEngine3D(new Aabb3D(new Vec3(-10f), new Vec3(10f)));
        _ = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        engine.Dispose();

        var query = new RaycastQuery3D(AlongPositiveX, 30f);
        Action act = () => engine.Raycast(in query, out _);
        act.Should().Throw<ObjectDisposedException>();
    }
}
