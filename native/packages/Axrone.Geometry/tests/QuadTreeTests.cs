namespace Axrone.Geometry.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using Axrone.Utility.Backoff.SpinPolicies;

using FluentAssertions;
using Xunit;

public class QuadTreeTests
{
    private static readonly Aabb2D s_root = new(0f, 0f, 16f, 16f);

    private static QuadTree<int, AdaptiveSpinBackoff, NullQuadMetricsSink> CreateTree() =>
        new(s_root, new QuadTreeConfig { MaxElementsPerNode = 4, MaxDepth = 8 });

    [Fact]
    public void Insert_QueryRange_FindsPointsInRange()
    {
        using var tree = CreateTree();
        tree.Insert(new Vec2(1, 1), 1).Should().BeTrue();
        tree.Insert(new Vec2(5, 5), 2).Should().BeTrue();
        tree.Insert(new Vec2(15, 15), 3).Should().BeTrue();
        tree.Count.Should().Be(3);

        Span<SpatialElement<int>> buffer = stackalloc SpatialElement<int>[8];
        var visitor = new ElementSpanVisitor<int>(buffer);
        tree.QueryRange(new Aabb2D(0f, 0f, 6f, 6f), ref visitor);

        visitor.Count.Should().Be(2);
        buffer[..visitor.Count].ToArray().Select(e => e.Value).OrderBy(v => v).ToArray().Should().Equal(1, 2);
    }

    [Fact]
    public void Insert_OutsideBounds_IsRejected()
    {
        using var tree = CreateTree();

        tree.TryInsert(new Vec2(100, 100), 1).Should().BeFalse();
        tree.Count.Should().Be(0);
    }

    [Fact]
    public void Remove_RoundTrips()
    {
        using var tree = CreateTree();
        tree.Insert(new Vec2(1, 1), 1);

        tree.Remove(new Vec2(1, 1), 1).Should().BeTrue();
        tree.Count.Should().Be(0);
        tree.Remove(new Vec2(1, 1), 1).Should().BeFalse();
        tree.Remove(new Vec2(1, 1), 2).Should().BeFalse();
    }

    [Fact]
    public void Split_Redistributes_And_GrowsPools()
    {
        using var tree = new QuadTree<int, AdaptiveSpinBackoff, NullQuadMetricsSink>(
            s_root,
            new QuadTreeConfig { MaxElementsPerNode = 2, MaxDepth = 8, InitialNodeCapacity = 4, InitialElementCapacity = 4 });

        for (int i = 0; i < 10; i++)
        {
            tree.Insert(new Vec2(i + 0.5f, (i * 3 % 15) + 0.5f), i).Should().BeTrue();
        }

        tree.Count.Should().Be(10);

        Span<SpatialElement<int>> buffer = stackalloc SpatialElement<int>[16];
        var visitor = new ElementSpanVisitor<int>(buffer);
        tree.QueryRange(s_root, ref visitor);

        visitor.Count.Should().Be(10);
    }

    [Fact]
    public void FindNearest_ReturnsClosest()
    {
        using var tree = CreateTree();
        tree.Insert(new Vec2(1, 1), 10);
        tree.Insert(new Vec2(5, 5), 20);
        tree.Insert(new Vec2(10, 10), 30);

        NearestQueryResult<int> result = tree.FindNearest(new Vec2(1.5f, 1.5f));

        result.IsFound.Should().BeTrue();
        result.Value.Should().Be(10);
        result.DistanceSquared.Should().BeApproximately(0.5f, 1e-5f);

        tree.FindNearest(new Vec2(1.5f, 1.5f), out Vec2 point, out int value, out float distSq).Should().BeTrue();
        point.Should().Be(new Vec2(1, 1));
        value.Should().Be(10);
        distSq.Should().BeApproximately(0.5f, 1e-5f);
    }

    [Fact]
    public void FindNearest_EmptyTree_ReportsEmpty()
    {
        using var tree = CreateTree();

        NearestQueryResult<int> result = tree.FindNearest(new Vec2(8, 8));

        result.IsFound.Should().BeFalse();
        result.Kind.Should().Be(NearestResultKind.Empty);
        tree.FindNearest(new Vec2(8, 8), out _, out _, out float distSq).Should().BeFalse();
        distSq.Should().Be(float.PositiveInfinity);
    }

    [Fact]
    public void Clear_DropsEverything()
    {
        using var tree = CreateTree();
        tree.Insert(new Vec2(1, 1), 1);
        tree.Insert(new Vec2(2, 2), 2);

        tree.Clear();

        tree.Count.Should().Be(0);
        Span<SpatialElement<int>> buffer = stackalloc SpatialElement<int>[8];
        var visitor = new ElementSpanVisitor<int>(buffer);
        tree.QueryRange(s_root, ref visitor);
        visitor.Count.Should().Be(0);
    }

    [Fact]
    public void Config_Guards_RejectInvalid()
    {
        Action elements = () => _ = new QuadTreeConfig { MaxElementsPerNode = 0 };
        Action elementsHigh = () => _ = new QuadTreeConfig { MaxElementsPerNode = 257 };
        Action depth = () => _ = new QuadTreeConfig { MaxDepth = 0 };
        Action depthHigh = () => _ = new QuadTreeConfig { MaxDepth = 33 };
        Action nodes = () => _ = new QuadTreeConfig { InitialNodeCapacity = 3 };
        Action elems = () => _ = new QuadTreeConfig { InitialElementCapacity = 3 };

        elements.Should().Throw<ArgumentOutOfRangeException>();
        elementsHigh.Should().Throw<ArgumentOutOfRangeException>();
        depth.Should().Throw<ArgumentOutOfRangeException>();
        depthHigh.Should().Throw<ArgumentOutOfRangeException>();
        nodes.Should().Throw<ArgumentOutOfRangeException>();
        elems.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Ctor_InvertedRoot_Throws()
    {
        Action act = () => _ = new QuadTree<int, AdaptiveSpinBackoff, NullQuadMetricsSink>(
            new Aabb2D(new Vec2(5, 5), new Vec2(1, 1)));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Builder_BuildsConfiguredTree()
    {
        using var tree = QuadTreeBuilder<int, AdaptiveSpinBackoff, NullQuadMetricsSink, QuadUnconfigured>
            .Create()
            .WithBounds(s_root)
            .WithConfig(new QuadTreeConfig { MaxElementsPerNode = 4, MaxDepth = 8 })
            .Build();

        tree.Insert(new Vec2(3, 3), 42).Should().BeTrue();
        tree.Count.Should().Be(1);
        tree.Bounds.Should().Be(s_root);
    }

    [Fact]
    public void Builder_PolicyAndMetrics_Switch()
    {
        using var tree = QuadTreeBuilder<int, AdaptiveSpinBackoff, NullQuadMetricsSink, QuadUnconfigured>
            .Create()
            .WithBackoff<YieldingBackoff>()
            .WithMetrics<NullQuadMetricsSink>()
            .WithBounds(s_root)
            .Build();

        tree.Insert(new Vec2(3, 3), 1).Should().BeTrue();
    }

    [Fact]
    public void EnumerateRange_WalksPoints()
    {
        using var tree = CreateTree();
        tree.Insert(new Vec2(1, 1), 1);
        tree.Insert(new Vec2(5, 5), 2);
        tree.Insert(new Vec2(15, 15), 3);

        var seen = new List<int>();
        foreach (SpatialElement<int> element in tree.EnumerateRange(new Aabb2D(0f, 0f, 6f, 6f)))
        {
            seen.Add(element.Value);
        }

        seen.OrderBy(v => v).ToArray().Should().Equal(1, 2);
    }

    [Fact]
    public void SpanBufferVisitor_StopsWhenFull()
    {
        using var tree = CreateTree();
        tree.Insert(new Vec2(1, 1), 1);
        tree.Insert(new Vec2(2, 2), 2);
        tree.Insert(new Vec2(3, 3), 3);

        Span<int> buffer = stackalloc int[1];
        var visitor = new SpanBufferVisitor<int>(buffer);
        tree.QueryRange(s_root, ref visitor);

        visitor.Count.Should().Be(1);
    }

    [Fact]
    public void Complete_RejectsMutations()
    {
        using var tree = CreateTree();
        tree.Insert(new Vec2(1, 1), 1);

        tree.Complete();

        Action insert = () => tree.Insert(new Vec2(2, 2), 2);
        insert.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task DrainAsync_Settles()
    {
        using var tree = CreateTree();
        tree.Insert(new Vec2(1, 1), 1);

        await tree.DrainAsync();

        tree.Count.Should().Be(1);
    }

    [Fact]
    public void Dispose_PoisonsOperations()
    {
        var tree = CreateTree();
        tree.Insert(new Vec2(1, 1), 1);
        tree.Dispose();

        Action insert = () => tree.Insert(new Vec2(2, 2), 2);
        insert.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_WaitsForLiveEnumeration()
    {
        var tree = CreateTree();
        tree.Insert(new Vec2(1, 1), 1);

        var enumerator = tree.EnumerateRange(s_root).GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();

        Task disposeTask = Task.Run(() => tree.Dispose());
        Thread.Sleep(50);
        disposeTask.IsCompleted.Should().BeFalse();

        enumerator.Dispose();

        long deadline = Environment.TickCount64 + 5000;
        while (!disposeTask.IsCompleted)
        {
            if (Environment.TickCount64 > deadline)
            {
                Assert.Fail("Dispose did not complete after the enumerator was released.");
            }

            Thread.Sleep(0);
        }

        disposeTask.Exception.Should().BeNull();
    }

    [Fact]
    public void ElementIndex_InvalidIsNotValid()
    {
        ElementIndex.Invalid.IsValid.Should().BeFalse();
        ElementIndex.Zero.IsValid.Should().BeTrue();
    }
}
