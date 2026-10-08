namespace Axrone.Geometry.Tests;

using Axrone.Geometry;
using Axrone.Numeric;

using FluentAssertions;
using Xunit;

public class DynamicTreeCoreTests
{
    private static readonly Aabb3D s_unitBox = new(new Vec3(0f, 0f, 0f), new Vec3(1f, 1f, 1f));

    private static DynamicAabbTree<string, SurfaceAreaHeuristicStrategy, NullSpatialMetricsSink> CreateTree() => new(new TreeOptions());

    [Fact]
    public void Insert_ReturnsValidIds_CountGrows()
    {
        var tree = CreateTree();

        var first = tree.Insert(s_unitBox, "first");
        var second = tree.Insert(new Aabb3D(new Vec3(2f, 0f, 0f), new Vec3(3f, 1f, 1f)), "second");
        var third = tree.Insert(new Aabb3D(new Vec3(4f, 0f, 0f), new Vec3(5f, 1f, 1f)), "third");

        first.IsValid.Should().BeTrue();
        second.IsValid.Should().BeTrue();
        third.IsValid.Should().BeTrue();
        first.Should().NotBe(second);
        second.Should().NotBe(third);
        first.Should().NotBe(third);
        tree.Count.Should().Be(3);
    }

    [Fact]
    public void Insert_EmptyBox_StillTracked()
    {
        var tree = CreateTree();

        var id = tree.Insert(Aabb3D.Empty, "empty");

        id.IsValid.Should().BeTrue();
        tree.Count.Should().Be(1);
    }

    [Fact]
    public void GetUserData_RoundTrips()
    {
        var tree = CreateTree();

        var id = tree.Insert(s_unitBox, "payload");

        tree.GetUserData(id).Should().Be("payload");
    }

    [Fact]
    public void GetFatAabb_CoversInsertedBox()
    {
        var tree = CreateTree();

        var id = tree.Insert(s_unitBox, "payload");

        tree.GetFatAabb(id).Contains(in s_unitBox).Should().BeTrue();
    }

    [Fact]
    public void Move_OutsideFatBox_ReinsertsTrue()
    {
        var tree = CreateTree();
        var id = tree.Insert(s_unitBox, "payload");

        var moved = tree.Move(id, new Aabb3D(new Vec3(10f, 10f, 10f), new Vec3(11f, 11f, 11f)), new Vec3(10f, 10f, 10f));

        moved.Should().BeTrue();
    }

    [Fact]
    public void Move_InsideFatBox_False()
    {
        var tree = CreateTree();
        var id = tree.Insert(s_unitBox, "payload");

        var moved = tree.Move(id, s_unitBox, Vec3.Zero);

        moved.Should().BeFalse();
    }

    [Fact]
    public void Update_OutsideFatBox_Refreshes()
    {
        var tree = CreateTree();
        var id = tree.Insert(s_unitBox, "payload");

        var farBox = new Aabb3D(new Vec3(10f, 10f, 10f), new Vec3(11f, 11f, 11f));
        tree.Update(id, farBox);

        tree.GetFatAabb(id).Contains(in farBox).Should().BeTrue();
    }

    [Fact]
    public void Update_InsideFatBox_NoOp()
    {
        var tree = CreateTree();
        var id = tree.Insert(s_unitBox, "payload");

        var before = tree.GetFatAabb(id);
        tree.Update(id, s_unitBox);
        var after = tree.GetFatAabb(id);

        after.Min.Should().Be(before.Min);
        after.Max.Should().Be(before.Max);
    }

    [Fact]
    public void Remove_Decrements_InvalidatesId()
    {
        var tree = CreateTree();
        var id = tree.Insert(s_unitBox, "payload");

        tree.Remove(id);

        tree.Count.Should().Be(0);
        var act = () => tree.GetUserData(id);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Remove_UnknownId_Throws()
    {
        var tree = CreateTree();

        var act = () => tree.Remove(new SpatialItemId(9999));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Capacity_GrowsOnDemand()
    {
        var tree = CreateTree();

        for (int i = 0; i < 200; ++i)
        {
            tree.Insert(new Aabb3D(new Vec3(i, 0f, 0f), new Vec3(i + 1f, 1f, 1f)), $"item-{i}");
        }

        tree.Count.Should().Be(200);
        tree.Capacity.Should().BeGreaterThanOrEqualTo(399);
    }

    [Fact]
    public void Complete_ThenInsert_ThrowsInvalidOperation()
    {
        var tree = CreateTree();

        tree.Complete();

        var act = () => tree.Insert(s_unitBox, "payload");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task DrainAsync_Completes()
    {
        var tree = CreateTree();
        tree.Insert(s_unitBox, "first");
        tree.Insert(s_unitBox, "second");
        tree.Complete();

        var act = () => tree.DrainAsync().AsTask();

        await act.Should().NotThrowAsync();
    }
}
