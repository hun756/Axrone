namespace Axrone.Geometry.Tests;

using Axrone.Geometry;
using Axrone.Numeric;

using FluentAssertions;
using Xunit;

public class SpatialPartitioningTests
{
    private struct CollectQuerySink : ISpatialQuerySink<string>
    {
        public List<string> Payloads;

        public bool OnHit(SpatialHandle handle, in Aabb3D bounds, ref readonly string payload)
        {
            Payloads.Add(payload);
            return true;
        }
    }

    private struct StopAfterFirstSink : ISpatialQuerySink<string>
    {
        public int Calls;

        public bool OnHit(SpatialHandle handle, in Aabb3D bounds, ref readonly string payload)
        {
            Calls++;
            return false;
        }
    }

    private struct CollectRaySink : ISpatialRaySink<string>
    {
        public List<(string Payload, float Distance)> Hits;

        public bool OnHit(SpatialHandle handle, in Aabb3D bounds, ref readonly string payload, Distance distance)
        {
            Hits.Add((payload, distance.Value));
            return true;
        }
    }

    private struct CollectPairSink : ISpatialPairSink<string>
    {
        public int Pairs;

        public bool OnPair(SpatialHandle handleA, ref readonly string payloadA, SpatialHandle handleB, ref readonly string payloadB)
        {
            Pairs++;
            return true;
        }
    }

    private static Aabb3D Box(float minX, float minY, float minZ, float maxX, float maxY, float maxZ) =>
        new(new Vec3(minX, minY, minZ), new Vec3(maxX, maxY, maxZ));

    private static DynamicSpatialTree<string> CreateTree() => new(new TreeCapacity(16));

    [Fact]
    public void Insert_ReturnsValidDistinctHandles()
    {
        using var tree = CreateTree();

        SpatialHandle first = tree.Insert(Box(0, 0, 0, 1, 1, 1), "first");
        SpatialHandle second = tree.Insert(Box(2, 0, 0, 3, 1, 1), "second");

        first.IsValid.Should().BeTrue();
        second.IsValid.Should().BeTrue();
        first.Should().NotBe(second);
        tree.Count.Should().Be(2);
    }

    [Fact]
    public void StaleHandle_AfterRecycle_IsRejected()
    {
        using var tree = CreateTree();

        SpatialHandle old = tree.Insert(Box(0, 0, 0, 1, 1, 1), "old");
        tree.Remove(old).Should().BeTrue();
        SpatialHandle next = tree.Insert(Box(5, 5, 5, 6, 6, 6), "next");

        next.Should().NotBe(old);
        tree.TryGetBounds(old, out _).Should().BeFalse();
        tree.TryGetPayload(old, out _).Should().BeFalse();
        tree.Remove(old).Should().BeFalse();
        tree.UpdateEx(old, Box(0, 0, 0, 1, 1, 1)).Outcome.Should().Be(TreeUpdateOutcome.HandleNotFound);
        tree.TryGetPayload(next, out string? payload).Should().BeTrue();
        payload.Should().Be("next");
    }

    [Fact]
    public void Query_ReportsOverlaps_WithTightBounds()
    {
        using var tree = CreateTree();
        tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");
        tree.Insert(Box(0.5f, 0, 0, 1.5f, 1, 1), "b");
        tree.Insert(Box(10, 10, 10, 11, 11, 11), "far");

        var sink = new CollectQuerySink { Payloads = new() };
        int hits = tree.Query(Box(-1, -1, -1, 2, 2, 2), ref sink);

        hits.Should().Be(2);
        sink.Payloads.Should().BeEquivalentTo("a", "b");
    }

    [Fact]
    public void Query_EarlyOut_StopsTraversal()
    {
        using var tree = CreateTree();
        tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");
        tree.Insert(Box(0.5f, 0, 0, 1.5f, 1, 1), "b");

        var sink = new StopAfterFirstSink();
        int hits = tree.Query(Box(-1, -1, -1, 2, 2, 2), ref sink);

        hits.Should().Be(1);
        sink.Calls.Should().Be(1);
    }

    [Fact]
    public void QuerySphere_HitsOnlyOverlapping()
    {
        using var tree = CreateTree();
        tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");
        tree.Insert(Box(5, 5, 5, 6, 6, 6), "b");

        var sink = new CollectQuerySink { Payloads = new() };
        int hits = tree.QuerySphere(new BoundingSphere3(new Vec3(0.25f), 0.1f), ref sink);

        hits.Should().Be(1);
        sink.Payloads.Should().ContainSingle().Which.Should().Be("a");
    }

    [Fact]
    public void UpdateEx_InsideFatBox_IsUnchanged()
    {
        using var tree = CreateTree();
        SpatialHandle handle = tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");

        TreeUpdateResult result = tree.UpdateEx(handle, Box(0.1f, 0.1f, 0.1f, 0.9f, 0.9f, 0.9f));

        result.Outcome.Should().Be(TreeUpdateOutcome.BoundsUnchanged);
        result.IsSuccessful.Should().BeTrue();
        tree.Update(handle, Box(0.1f, 0.1f, 0.1f, 0.9f, 0.9f, 0.9f)).Should().BeFalse();
        tree.TryGetBounds(handle, out Aabb3D tight).Should().BeTrue();
        tight.Min.X.Should().BeApproximately(0.1f, 1e-6f);
    }

    [Fact]
    public void UpdateEx_OutsideFatBox_Reinserts()
    {
        using var tree = CreateTree();
        SpatialHandle handle = tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");

        TreeUpdateResult result = tree.UpdateEx(handle, Box(5, 5, 5, 6, 6, 6), new Vec3(5, 5, 5));

        result.Outcome.Should().Be(TreeUpdateOutcome.Reinserted);
        result.Handle.Should().Be(handle);
        tree.Update(handle, Box(5, 5, 5, 6, 6, 6)).Should().BeFalse();
        tree.TryGetBounds(handle, out Aabb3D tight).Should().BeTrue();
        tight.Min.X.Should().BeApproximately(5f, 1e-6f);
    }

    [Fact]
    public void UpdateEx_UnknownHandle_ReportsNotFound()
    {
        using var tree = CreateTree();

        tree.UpdateEx(SpatialHandle.Invalid, Box(0, 0, 0, 1, 1, 1)).Outcome.Should().Be(TreeUpdateOutcome.HandleNotFound);
        tree.UpdateEx(SpatialHandle.Create(new NodeIndex(5000), new NodeGeneration(1)), Box(0, 0, 0, 1, 1, 1)).Outcome
            .Should().Be(TreeUpdateOutcome.HandleNotFound);
    }

    [Fact]
    public void InsertRange_WritesHandles()
    {
        using var tree = CreateTree();
        Span<SpatialEntry<string>> entries = [new(Box(0, 0, 0, 1, 1, 1), "a"), new(Box(2, 2, 2, 3, 3, 3), "b")];
        Span<SpatialHandle> handles = stackalloc SpatialHandle[2];

        int added = tree.InsertRange(entries, handles);

        added.Should().Be(2);
        handles[0].IsValid.Should().BeTrue();
        handles[1].IsValid.Should().BeTrue();
        tree.Count.Should().Be(2);
    }

    [Fact]
    public void InsertRange_ShortHandlesSpan_Throws()
    {
        using var tree = CreateTree();
        SpatialEntry<string>[] entries = [new(Box(0, 0, 0, 1, 1, 1), "a"), new(Box(2, 2, 2, 3, 3, 3), "b")];
        SpatialHandle[] handles = new SpatialHandle[1];

        Action act = () => tree.InsertRange(entries, handles);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Remove_And_Clear()
    {
        using var tree = CreateTree();
        SpatialHandle handle = tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");
        tree.Insert(Box(2, 2, 2, 3, 3, 3), "b");

        tree.Remove(handle).Should().BeTrue();
        tree.Remove(handle).Should().BeFalse();
        tree.Count.Should().Be(1);

        tree.Clear();
        tree.Count.Should().Be(0);
    }

    [Fact]
    public void RaycastClosest_ReturnsNearest()
    {
        using var tree = CreateTree();
        tree.Insert(Box(5, -1, -1, 6, 1, 1), "far");
        tree.Insert(Box(2, -1, -1, 3, 1, 1), "near");

        var ray = new Ray3D(new Vec3(0, 0, 0), new Vec3(1, 0, 0));
        RaycastResult<string> hit = tree.RaycastClosest(ray, Distance.PositiveInfinity);

        hit.HasHit.Should().BeTrue();
        hit.Payload.Should().Be("near");
        hit.Distance.Value.Should().BeApproximately(2f, 1e-4f);
    }

    [Fact]
    public void RaycastClosest_MissesCleanly()
    {
        using var tree = CreateTree();
        tree.Insert(Box(2, -1, -1, 3, 1, 1), "a");

        var ray = new Ray3D(new Vec3(0, 0, 0), new Vec3(0, 1, 0));
        RaycastResult<string> miss = tree.RaycastClosest(ray, Distance.PositiveInfinity);

        miss.HasHit.Should().BeFalse();
        miss.Status.Should().Be(RaycastStatus.Miss);
        miss.Distance.Should().Be(Distance.PositiveInfinity);
    }

    [Fact]
    public void Raycast_Sink_CollectsHits()
    {
        using var tree = CreateTree();
        tree.Insert(Box(2, -1, -1, 3, 1, 1), "a");
        tree.Insert(Box(5, -1, -1, 6, 1, 1), "b");
        tree.Insert(Box(2, 5, 5, 3, 6, 6), "off-axis");

        var sink = new CollectRaySink { Hits = new() };
        var ray = new Ray3D(new Vec3(0, 0, 0), new Vec3(1, 0, 0));
        int hits = tree.Raycast(ray, Distance.PositiveInfinity, ref sink);

        hits.Should().Be(2);
        sink.Hits.Should().HaveCount(2);
    }

    [Fact]
    public void QueryOverlaps_ReportsEachPairOnce()
    {
        using var tree = CreateTree();
        tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");
        tree.Insert(Box(0.5f, 0.5f, 0.5f, 1.5f, 1.5f, 1.5f), "b");
        tree.Insert(Box(10, 10, 10, 11, 11, 11), "far");

        var sink = new CollectPairSink();
        tree.QueryOverlaps(ref sink);

        sink.Pairs.Should().Be(1);
    }

    [Fact]
    public void Capacity_GrowsBeyondInitial()
    {
        using var tree = new DynamicSpatialTree<string>(new TreeCapacity(16));
        var handles = new SpatialHandle[20];
        for (int i = 0; i < handles.Length; i++)
        {
            float x = i * 10f;
            handles[i] = tree.Insert(Box(x, 0, 0, x + 1, 1, 1), $"item{i}");
        }

        tree.Count.Should().Be(20);
        tree.Capacity.Value.Should().BeGreaterThan(16);
        for (int i = 0; i < handles.Length; i++)
        {
            tree.TryGetPayload(handles[i], out string? payload).Should().BeTrue();
            payload.Should().Be($"item{i}");
        }
    }

    [Fact]
    public void Enumerator_WalksLiveLeaves()
    {
        using var tree = CreateTree();
        tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");
        tree.Insert(Box(2, 2, 2, 3, 3, 3), "b");
        tree.Insert(Box(4, 4, 4, 5, 5, 5), "c");

        int leaves = 0;
        foreach (ref readonly DynamicTreeNode<string> node in tree)
        {
            if (node.IsLeaf)
            {
                leaves++;
            }
        }

        leaves.Should().Be(3);
    }

    [Fact]
    public void Builder_BuildsConfiguredTree()
    {
        using var tree = SpatialTreeBuilder.Create<string>()
            .WithCapacity(new TreeCapacity(16))
            .WithMargins(0.1f, 2.0f)
            .Build();

        SpatialHandle handle = tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");

        handle.IsValid.Should().BeTrue();
        tree.Count.Should().Be(1);
    }

    [Fact]
    public void Builder_DefaultMargins_Works()
    {
        using var tree = SpatialTreeBuilder.Create<string>()
            .WithCapacity(new TreeCapacity(16))
            .WithDefaultMargins()
            .Build();

        tree.Capacity.Value.Should().Be(16);
    }

    [Fact]
    public void Options_Guards_RejectInvalid()
    {
        Action margin = () => _ = new SpatialTreeOptions { FatMargin = -1f };
        Action velocity = () => _ = new SpatialTreeOptions { VelocityMultiplier = 0.5f };
        Action capacity = () => _ = new TreeCapacity(8);
        Action ctorMargin = () => _ = new DynamicSpatialTree<string>(new TreeCapacity(16), -0.1f);
        Action ctorVelocity = () => _ = new DynamicSpatialTree<string>(new TreeCapacity(16), 0.2f, 0.5f);

        margin.Should().Throw<ArgumentOutOfRangeException>();
        velocity.Should().Throw<ArgumentOutOfRangeException>();
        capacity.Should().Throw<ArgumentOutOfRangeException>();
        ctorMargin.Should().Throw<ArgumentOutOfRangeException>();
        ctorVelocity.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Dispose_PoisonsMutations()
    {
        var tree = CreateTree();
        tree.Dispose();

        Action insert = () => tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");
        insert.Should().Throw<ObjectDisposedException>();
        tree.UpdateEx(SpatialHandle.Create(new NodeIndex(0), new NodeGeneration(1)), Box(0, 0, 0, 1, 1, 1)).Outcome
            .Should().Be(TreeUpdateOutcome.Disposed);
    }

    [Fact]
    public void Clear_InvalidatesOldHandles()
    {
        using var tree = CreateTree();
        SpatialHandle old = tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");

        tree.Clear();
        tree.Count.Should().Be(0);

        SpatialHandle next = tree.Insert(Box(5, 5, 5, 6, 6, 6), "b");
        next.Should().NotBe(old);
        tree.TryGetBounds(old, out _).Should().BeFalse();
        tree.TryGetPayload(old, out _).Should().BeFalse();
        tree.Remove(old).Should().BeFalse();
        tree.UpdateEx(old, Box(0, 0, 0, 1, 1, 1)).Outcome.Should().Be(TreeUpdateOutcome.HandleNotFound);
        tree.TryGetPayload(next, out string? payload).Should().BeTrue();
        payload.Should().Be("b");
    }

    [Fact]
    public void Nominals_RoundTrip()
    {
        SpatialHandle handle = SpatialHandle.Create(new NodeIndex(7), new NodeGeneration(3));

        handle.IsValid.Should().BeTrue();
        handle.Index.Value.Should().Be(7);
        handle.Generation.Value.Should().Be(3);
        SpatialHandle.Invalid.IsValid.Should().BeFalse();

        new NodeGeneration(0xFFF).Next().Value.Should().Be(0);
        Distance.Zero.Value.Should().Be(0f);
        (Distance.Zero < Distance.PositiveInfinity).Should().BeTrue();
    }
}
