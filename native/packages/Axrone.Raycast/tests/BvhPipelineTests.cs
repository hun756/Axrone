namespace Axrone.Raycast.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class BvhPipelineTests
{
    private readonly struct BoxPrimitive : IPrimitiveGeometry<BoxPrimitive>
    {
        public readonly Aabb3D Bounds;
        public readonly PrimitiveId Id;

        public BoxPrimitive(Aabb3D bounds, PrimitiveId id)
        {
            Bounds = bounds;
            Id = id;
        }

        public static Aabb3D ComputeBounds(in BoxPrimitive primitive) => primitive.Bounds;

        public static Vec3 ComputeCentroid(in BoxPrimitive primitive) => primitive.Bounds.Center;

        public static PrimitiveId GetId(in BoxPrimitive primitive) => primitive.Id;

        public static bool TryIntersect(in Ray ray, in BoxPrimitive primitive, out PrimitiveHit hit)
        {
            hit = PrimitiveHit.None;
            Vec3 min = primitive.Bounds.Min;
            Vec3 max = primitive.Bounds.Max;

            float t1x = (min.X - ray.Origin.X) * ray.InvDirection.X;
            float t2x = (max.X - ray.Origin.X) * ray.InvDirection.X;
            float tminx = MathF.Min(t1x, t2x);
            float tmaxx = MathF.Max(t1x, t2x);

            float t1y = (min.Y - ray.Origin.Y) * ray.InvDirection.Y;
            float t2y = (max.Y - ray.Origin.Y) * ray.InvDirection.Y;
            float tminy = MathF.Min(t1y, t2y);
            float tmaxy = MathF.Max(t1y, t2y);

            float t1z = (min.Z - ray.Origin.Z) * ray.InvDirection.Z;
            float t2z = (max.Z - ray.Origin.Z) * ray.InvDirection.Z;
            float tminz = MathF.Min(t1z, t2z);
            float tmaxz = MathF.Max(t1z, t2z);

            float tmin = MathF.Max(tminx, MathF.Max(tminy, tminz));
            float tmax = MathF.Min(tmaxx, MathF.Min(tmaxy, tmaxz));

            if (tmax < tmin || tmax < ray.MinDistance.Value || tmin > ray.MaxDistance.Value)
            {
                return false;
            }

            float enter = MathF.Max(tmin, ray.MinDistance.Value);
            Vec3 normal = tminx >= tminy && tminx >= tminz
                ? new Vec3(MathF.Sign(ray.Direction.X), 0f, 0f)
                : tminy >= tminz
                    ? new Vec3(0f, MathF.Sign(ray.Direction.Y), 0f)
                    : new Vec3(0f, 0f, MathF.Sign(ray.Direction.Z));
            hit = new PrimitiveHit(ray.PointAt(enter), normal, new Distance(enter), primitive.Id);
            return true;
        }
    }

    private struct CountObserver : ITraversalObserver<CountObserver>
    {
        public int Nodes;
        public int Prims;
        public bool Hit;

        public void OnTraversalComplete(int nodesVisited, int primitivesTested, bool hitOccurred)
        {
            Nodes = nodesVisited;
            Prims = primitivesTested;
            Hit = hitOccurred;
        }
    }

    private static readonly Triangle s_near = new(
        new Vec3(0, 0, 5), new Vec3(1, 0, 5), new Vec3(0, 1, 5), 7);

    private static readonly Triangle s_far = new(
        new Vec3(0, 0, 8), new Vec3(1, 0, 8), new Vec3(0, 1, 8), 8);

    private static Ray UnitRay(Vec3 origin, Vec3 direction) =>
        new(origin, Vec3.Normalize(direction));

    private static BvhPipeline<Triangle> TwoTrianglePipeline() =>
        BvhPipelineBuilder<Triangle, UnconfiguredPhase>.Create()
            .Configure()
            .BuildHierarchy(new Triangle[] { s_near, s_far });

    [Fact]
    public void Raycast_ReturnsNearest()
    {
        using var pipeline = TwoTrianglePipeline();
        Ray ray = UnitRay(new Vec3(0.1f, 0.1f, 0), new Vec3(0, 0, 1));

        RayQueryResult result = pipeline.Raycast(in ray);

        result.IsHit.Should().BeTrue();
        result.Hit.PrimitiveId.Should().Be(new PrimitiveId(7));
        result.Hit.Distance.Value.Should().BeApproximately(5f, 1e-4f);
        result.Hit.Point.Z.Should().BeApproximately(5f, 1e-4f);
        pipeline.TotalRays.Should().Be(1);
        pipeline.TotalHits.Should().Be(1);
    }

    [Fact]
    public void Raycast_MissesCleanly()
    {
        using var pipeline = TwoTrianglePipeline();
        Ray ray = UnitRay(new Vec3(0, 0, 0), new Vec3(0, 1, 0));

        RayQueryResult result = pipeline.Raycast(in ray);

        result.IsHit.Should().BeFalse();
        result.Kind.Should().Be(RayQueryResultKind.Miss);
    }

    [Fact]
    public void RaycastAny_ProbesOcclusion()
    {
        using var pipeline = TwoTrianglePipeline();

        pipeline.RaycastAny(UnitRay(new Vec3(0.1f, 0.1f, 0), new Vec3(0, 0, 1))).Should().BeTrue();
        pipeline.RaycastAny(UnitRay(new Vec3(0, 0, 0), new Vec3(0, 1, 0))).Should().BeFalse();
    }

    [Fact]
    public void RaycastBatch_MapsOneToOne()
    {
        using var pipeline = TwoTrianglePipeline();
        Ray[] rays =
        [
            UnitRay(new Vec3(0.1f, 0.1f, 0), new Vec3(0, 0, 1)),
            UnitRay(new Vec3(0, 0, 0), new Vec3(0, 1, 0)),
        ];
        var results = new RayQueryResult[2];
        var anys = new bool[2];

        pipeline.RaycastBatch(rays, results);
        pipeline.RaycastBatchAny(rays, anys);

        results[0].IsHit.Should().BeTrue();
        results[1].IsHit.Should().BeFalse();
        anys.Should().Equal(true, false);

        Action shortResults = () => pipeline.RaycastBatch(rays, results.AsSpan(0, 1));
        shortResults.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EnumerateHits_WalksContacts()
    {
        using var pipeline = TwoTrianglePipeline();
        Ray ray = UnitRay(new Vec3(0.1f, 0.1f, 0), new Vec3(0, 0, 1));

        var ids = new List<int>();
        foreach (PrimitiveHit hit in pipeline.EnumerateHits(in ray))
        {
            ids.Add(hit.PrimitiveId.Value);
        }

        ids.Should().Equal(7, 8);
    }

    [Fact]
    public void TraverseWithCollector_BuffersHits()
    {
        using var pipeline = TwoTrianglePipeline();
        Ray ray = UnitRay(new Vec3(0.1f, 0.1f, 0), new Vec3(0, 0, 1));

        Span<PrimitiveHit> buffer = stackalloc PrimitiveHit[4];
        var collector = new HitBufferCollector(buffer);
        var observer = new CountObserver();

        pipeline.TraverseWithCollector(in ray, ref collector, ref observer).Should().BeTrue();

        collector.Count.Should().Be(2);
        observer.Hit.Should().BeTrue();
        observer.Nodes.Should().BeGreaterThan(0);
        observer.Prims.Should().Be(2);
    }

    [Fact]
    public void SahSplit_BuildsInternalNodes()
    {
        var triangles = new Triangle[20];
        for (int i = 0; i < triangles.Length; i++)
        {
            float x = i * 2f;
            triangles[i] = new Triangle(
                new Vec3(x, 0, 5), new Vec3(x + 1, 0, 5), new Vec3(x, 1, 5), i);
        }

        using var pipeline = BvhPipelineBuilder<Triangle, UnconfiguredPhase>.Create()
            .Configure()
            .BuildHierarchy(triangles);

        pipeline.NodeCount.Should().BeGreaterThan(1);
        pipeline.PrimitiveCount.Should().Be(20);

        Ray ray = UnitRay(new Vec3(0.1f, 0.1f, 0), new Vec3(0, 0, 1));
        RayQueryResult result = pipeline.Raycast(in ray);

        result.IsHit.Should().BeTrue();
        result.Hit.PrimitiveId.Should().Be(new PrimitiveId(0));
    }

    [Fact]
    public void DegenerateCentroids_FallBackToLeaf()
    {
        var triangles = new Triangle[5];
        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i] = new Triangle(
                new Vec3(0, 0, 5), new Vec3(1, 0, 5), new Vec3(0, 1, 5), i);
        }

        using var pipeline = BvhPipelineBuilder<Triangle, UnconfiguredPhase>.Create()
            .Configure()
            .BuildHierarchy(triangles);

        Ray ray = UnitRay(new Vec3(0.1f, 0.1f, 0), new Vec3(0, 0, 1));
        pipeline.Raycast(in ray).IsHit.Should().BeTrue();
    }

    [Fact]
    public void SphereScene_Hits()
    {
        var spheres = new Sphere[] { new(new Vec3(0, 0, 5), 1f, 3) };
        using var pipeline = BvhPipelineBuilder<Sphere, UnconfiguredPhase>.Create()
            .Configure()
            .BuildHierarchy(spheres);

        Ray ray = UnitRay(new Vec3(0, 0, 0), new Vec3(0, 0, 1));
        RayQueryResult result = pipeline.Raycast(in ray);

        result.IsHit.Should().BeTrue();
        result.Hit.Distance.Value.Should().BeApproximately(4f, 1e-4f);
        result.Hit.Normal.Should().Be(new Vec3(0, 0, -1));
    }

    [Fact]
    public void CustomPrimitive_GenericPath()
    {
        var boxes = new BoxPrimitive[]
        {
            new(new Aabb3D(new Vec3(2, -1, -1), new Vec3(3, 1, 1)), new PrimitiveId(11)),
        };
        using var pipeline = BvhPipelineBuilder<BoxPrimitive, UnconfiguredPhase>.Create()
            .Configure()
            .BuildHierarchy(boxes);

        Ray ray = UnitRay(new Vec3(0, 0, 0), new Vec3(1, 0, 0));
        RayQueryResult result = pipeline.Raycast(in ray);

        result.IsHit.Should().BeTrue();
        result.Hit.PrimitiveId.Should().Be(new PrimitiveId(11));
        result.Hit.Distance.Value.Should().BeApproximately(2f, 1e-4f);
    }

    [Fact]
    public void Window_ClipsHits()
    {
        using var pipeline = TwoTrianglePipeline();
        var windowed = new Ray(new Vec3(0.1f, 0.1f, 0), new Vec3(0, 0, 1), Distance.DefaultEpsilon, new Distance(1f));

        pipeline.Raycast(in windowed).IsHit.Should().BeFalse();
    }

    [Fact]
    public void EmptySpan_Throws()
    {
        Action act = () => BvhPipelineBuilder<Triangle, UnconfiguredPhase>.Create()
            .Configure()
            .BuildHierarchy(ReadOnlySpan<Triangle>.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BadConfig_Throws()
    {
        Action leaf = () => BvhPipelineBuilder<Triangle, UnconfiguredPhase>.Create().Configure(0, 16);
        Action bins = () => BvhPipelineBuilder<Triangle, UnconfiguredPhase>.Create().Configure(4, 2);

        leaf.Should().Throw<ArgumentOutOfRangeException>();
        bins.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Disposed_Traverse_Throws_And_MetricsSettle()
    {
        var pipeline = TwoTrianglePipeline();
        pipeline.NodeCount.Should().BeGreaterThan(0);
        pipeline.PrimitiveCount.Should().Be(2);
        pipeline.IsOperational.Should().BeTrue();

        pipeline.Dispose();

        pipeline.IsOperational.Should().BeFalse();
        Ray ray = UnitRay(new Vec3(0.1f, 0.1f, 0), new Vec3(0, 0, 1));
        Action traverse = () => pipeline.Raycast(in ray);
        traverse.Should().Throw<ObjectDisposedException>();

        Action again = () => pipeline.Dispose();
        again.Should().NotThrow();
    }

    [Fact]
    public void Complete_Fault_Rethrows()
    {
        var pipeline = TwoTrianglePipeline();
        pipeline.Complete(new InvalidOperationException("boom"));

        Ray ray = UnitRay(new Vec3(0.1f, 0.1f, 0), new Vec3(0, 0, 1));
        Action traverse = () => pipeline.Raycast(in ray);

        traverse.Should().Throw<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public async Task DrainAsync_Settles()
    {
        using var pipeline = TwoTrianglePipeline();

        await pipeline.DrainAsync();

        pipeline.IsOperational.Should().BeFalse();
    }
}
