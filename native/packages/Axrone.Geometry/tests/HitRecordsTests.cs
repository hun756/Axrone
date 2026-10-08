namespace Axrone.Geometry.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Numeric;

public class RayHitTests
{
    [Fact]
    public void CompareTo_OrdersByDistance()
    {
        var near = new RayHit(new SpatialItemId(1U), 1.5F);
        var far = new RayHit(new SpatialItemId(2U), 9F);

        near.CompareTo(far).Should().BeNegative();
        far.CompareTo(near).Should().BePositive();
        near.CompareTo(near).Should().Be(0);
    }

    [Fact]
    public void Sort_OrdersByDistance_RegardlessOfItemId()
    {
        RayHit[] hits =
        [
            new(new SpatialItemId(9U), 4F),
            new(new SpatialItemId(1U), 0.25F),
            new(new SpatialItemId(5U), 2F),
        ];

        Array.Sort(hits);

        hits.Select(hit => hit.Distance).Should().Equal(0.25F, 2F, 4F);
        hits[0].ItemId.Should().Be(new SpatialItemId(1U));
    }

    [Fact]
    public void Fields_RoundTrip()
    {
        var hit = new RayHit(new SpatialItemId(7U), 2.25F);

        hit.ItemId.Value.Should().Be(7U);
        hit.Distance.Should().Be(2.25F);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        new RayHit(new SpatialItemId(3U), 1F).Should().Be(new RayHit(new SpatialItemId(3U), 1F));
        (new RayHit(new SpatialItemId(3U), 1F) == new RayHit(new SpatialItemId(3U), 1F)).Should().BeTrue();
    }

    [Fact]
    public void ToString_UsesInvariantNamedForm()
    {
        new RayHit(new SpatialItemId(42U), 1.5F).ToString().Should().Be("Hit(Id=42, Distance=1.5000)");
    }
}

public class RayIntersectionTests
{
    [Fact]
    public void None_IsAMiss_WithInvalidPayload()
    {
        RayIntersection none = RayIntersection.None;

        none.IsHit.Should().BeFalse();
        none.Kind.Should().Be(IntersectionKind.None);
        none.ItemId.Should().Be(SpatialItemId.Invalid);
        none.Distance.Should().Be(0F);
    }

    [Fact]
    public void FromHit_KeepsItemIdAndDistance()
    {
        var id = new SpatialItemId(11U);

        RayIntersection hit = RayIntersection.FromHit(id, 3.5F);

        hit.IsHit.Should().BeTrue();
        hit.Kind.Should().Be(IntersectionKind.Hit);
        hit.ItemId.Should().Be(id);
        hit.Distance.Should().Be(3.5F);
    }

    [Fact]
    public void Default_IsNotAHit()
    {
        default(RayIntersection).IsHit.Should().BeFalse();
    }

    [Fact]
    public void Layout_IsSixteenBytes()
    {
        System.Runtime.InteropServices.Marshal.SizeOf<RayIntersection>().Should().Be(16);
    }
}

public class TriangleHit3Tests
{
    [Fact]
    public void Fields_RoundTrip()
    {
        var normal = new Vec3(0F, 1F, 0F);
        var point = new Vec3(1F, 2F, 3F);

        var hit = new TriangleHit3(4.5F, 0.25F, 0.75F, normal, point);

        hit.Distance.Should().Be(4.5F);
        hit.U.Should().Be(0.25F);
        hit.V.Should().Be(0.75F);
        hit.Normal.Should().Be(normal);
        hit.Point.Should().Be(point);
    }

    [Fact]
    public void ToString_IsNonEmpty_AndMentionsUvAndPoint()
    {
        var hit = new TriangleHit3(4.5F, 0.25F, 0.75F, new Vec3(0F, 1F, 0F), new Vec3(1F, 2F, 3F));

        string text = hit.ToString();

        text.Should().Be("TriangleHit(Dist=4.5000, UV=<0.250,0.750>, Pt=1,2,3)");
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var normal = new Vec3(0F, 1F, 0F);
        var point = new Vec3(1F, 2F, 3F);

        new TriangleHit3(4.5F, 0.25F, 0.75F, normal, point)
            .Should().Be(new TriangleHit3(4.5F, 0.25F, 0.75F, normal, point));
    }
}

public class SweepHit3Tests
{
    [Fact]
    public void Fields_RoundTrip()
    {
        var normal = new Vec3(0F, 0F, -1F);
        var point = new Vec3(-1F, 0.5F, 2F);

        var hit = new SweepHit3(0.75F, normal, point);

        hit.Time.Should().Be(0.75F);
        hit.Normal.Should().Be(normal);
        hit.Point.Should().Be(point);
    }

    [Fact]
    public void ToString_IsNonEmpty_AndMentionsNormalAndPoint()
    {
        var hit = new SweepHit3(0.75F, new Vec3(0F, 0F, -1F), new Vec3(-1F, 0.5F, 2F));

        string text = hit.ToString();

        text.Should().Be("SweepHit(Time=0.7500, Normal=0,0,-1, Pt=-1,0.5,2)");
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        new SweepHit3(0.75F, new Vec3(0F, 0F, -1F), new Vec3(-1F, 0.5F, 2F))
            .Should().Be(new SweepHit3(0.75F, new Vec3(0F, 0F, -1F), new Vec3(-1F, 0.5F, 2F)));
    }
}

public class SweepResultTests
{
    [Fact]
    public void Miss_IsNotAHit()
    {
        SweepResult miss = SweepResult.Miss;

        miss.IsHit.Should().BeFalse();
        miss.Kind.Should().Be(SweepKind.Miss);
        miss.Hit.Should().Be(default(SweepHit3));
    }

    [Fact]
    public void FromHit_KeepsKindAndPayload()
    {
        var payload = new SweepHit3(0.5F, new Vec3(0F, 1F, 0F), new Vec3(2F, 3F, 4F));

        SweepResult hit = SweepResult.FromHit(in payload);

        hit.IsHit.Should().BeTrue();
        hit.Kind.Should().Be(SweepKind.Hit);
        hit.Hit.Should().Be(payload);
    }

    [Fact]
    public void FromHit_DoesNotAliasTheCallersValue()
    {
        var payload = new SweepHit3(0.5F, new Vec3(0F, 1F, 0F), new Vec3(2F, 3F, 4F));

        SweepResult hit = SweepResult.FromHit(in payload);
        payload = new SweepHit3(9F, new Vec3(1F, 0F, 0F), new Vec3(9F, 9F, 9F));

        hit.Hit.Time.Should().Be(0.5F);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        SweepResult.Miss.Should().Be(SweepResult.Miss);
    }
}

public class KnnResultTests
{
    [Fact]
    public void Fields_RoundTrip()
    {
        var result = new KnnResult<string>(new SpatialItemId(4U), "alpha", new DistanceSquared(16F));

        result.ItemId.Should().Be(new SpatialItemId(4U));
        result.UserData.Should().Be("alpha");
        result.DistanceSq.Should().Be(new DistanceSquared(16F));
    }

    [Fact]
    public void CompareTo_OrdersByDistanceSq()
    {
        var near = new KnnResult<string>(new SpatialItemId(1U), "near", new DistanceSquared(1F));
        var far = new KnnResult<string>(new SpatialItemId(2U), "far", new DistanceSquared(25F));

        near.CompareTo(far).Should().BeNegative();
        far.CompareTo(near).Should().BePositive();
        near.CompareTo(near).Should().Be(0);
    }

    [Fact]
    public void Sort_OrdersByDistanceSq_AndKeepsUserData()
    {
        KnnResult<string>[] results =
        [
            new(new SpatialItemId(1U), "third", new DistanceSquared(9F)),
            new(new SpatialItemId(2U), "first", new DistanceSquared(1F)),
            new(new SpatialItemId(3U), "second", new DistanceSquared(4F)),
        ];

        Array.Sort(results);

        results.Select(result => result.DistanceSq).Should().Equal(new DistanceSquared(1F), new DistanceSquared(4F), new DistanceSquared(9F));
        results.Select(result => result.UserData).Should().Equal("first", "second", "third");
    }

    [Fact]
    public void ToString_IsNonEmpty_AndMentionsIdAndData()
    {
        string text = new KnnResult<string>(new SpatialItemId(4U), "alpha", new DistanceSquared(16F)).ToString();

        text.Should().NotBeNullOrEmpty();
        text.Should().Contain("Id=4");
        text.Should().Be("Knn(Id=4, DistSq=16.0000)");
    }
}