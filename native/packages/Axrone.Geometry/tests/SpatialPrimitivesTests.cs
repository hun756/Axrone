namespace Axrone.Geometry.Tests;

using Xunit;
using FluentAssertions;

public class SpatialItemIdTests
{
    [Fact]
    public void Invalid_Sentinel_HasZeroValue_AndIsNotValid()
    {
        SpatialItemId.Invalid.Value.Should().Be(0U);
        SpatialItemId.Invalid.IsValid.Should().BeFalse();
        default(SpatialItemId).Should().Be(SpatialItemId.Invalid);
    }

    [Fact]
    public void NonZeroValue_IsValid()
    {
        new SpatialItemId(1U).IsValid.Should().BeTrue();
        new SpatialItemId(uint.MaxValue).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ComparisonOperators_FollowNumericOrder()
    {
        var low = new SpatialItemId(3U);
        var high = new SpatialItemId(9U);
        var sameAsLow = new SpatialItemId(3U);

        (low < high).Should().BeTrue();
        (high > low).Should().BeTrue();
        (low <= high).Should().BeTrue();
        (low >= high).Should().BeFalse();
        (low <= sameAsLow).Should().BeTrue();
        (low >= sameAsLow).Should().BeTrue();
    }

    [Fact]
    public void CompareTo_OrdersByValue()
    {
        var low = new SpatialItemId(2U);
        var high = new SpatialItemId(5U);

        low.CompareTo(high).Should().BeNegative();
        high.CompareTo(low).Should().BePositive();
        low.CompareTo(low).Should().Be(0);
    }

    [Fact]
    public void ImplicitAndExplicitCasts_RoundTrip()
    {
        SpatialItemId id = new(42U);

        uint raw = id;
        raw.Should().Be(42U);

        ((SpatialItemId)raw).Should().Be(id);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        new SpatialItemId(7U).Should().Be(new SpatialItemId(7U));
        (new SpatialItemId(7U) == new SpatialItemId(7U)).Should().BeTrue();
        (new SpatialItemId(7U) != new SpatialItemId(8U)).Should().BeTrue();
        new SpatialItemId(7U).GetHashCode().Should().Be(new SpatialItemId(7U).GetHashCode());
    }

    [Fact]
    public void ToString_UsesNamedForm()
    {
        new SpatialItemId(42U).ToString().Should().Be("Item(42)");
        SpatialItemId.Invalid.ToString().Should().Be("Item(Invalid)");
    }
}

public class NodeIndexTests
{
    [Fact]
    public void Null_Sentinel_IsNegative_AndNotValid()
    {
        NodeIndex.Null.Value.Should().Be(-1);
        NodeIndex.Null.IsNull.Should().BeTrue();
        NodeIndex.Null.IsValid.Should().BeFalse();
    }

    [Fact]
    public void NonNegativeValue_IsValid_AndNotNull()
    {
        new NodeIndex(0).IsValid.Should().BeTrue();
        new NodeIndex(0).IsNull.Should().BeFalse();
        new NodeIndex(1234).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ComparisonOperators_FollowNumericOrder()
    {
        var low = new NodeIndex(2);
        var high = new NodeIndex(8);
        var sameAsLow = new NodeIndex(2);

        (low < high).Should().BeTrue();
        (high > low).Should().BeTrue();
        (low <= high).Should().BeTrue();
        (low >= high).Should().BeFalse();
        (low <= sameAsLow).Should().BeTrue();
        (low >= sameAsLow).Should().BeTrue();
    }

    [Fact]
    public void CompareTo_OrdersByValue()
    {
        var low = new NodeIndex(1);
        var high = new NodeIndex(4);

        low.CompareTo(high).Should().BeNegative();
        high.CompareTo(low).Should().BePositive();
        low.CompareTo(low).Should().Be(0);
    }

    [Fact]
    public void ImplicitAndExplicitCasts_RoundTrip()
    {
        NodeIndex index = new(11);

        int raw = index;
        raw.Should().Be(11);

        ((NodeIndex)raw).Should().Be(index);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        new NodeIndex(5).Should().Be(new NodeIndex(5));
        (new NodeIndex(5) == new NodeIndex(5)).Should().BeTrue();
        (new NodeIndex(5) != new NodeIndex(6)).Should().BeTrue();
        new NodeIndex(5).GetHashCode().Should().Be(new NodeIndex(5).GetHashCode());
    }

    [Fact]
    public void ToString_UsesNamedForm()
    {
        new NodeIndex(17).ToString().Should().Be("Node(17)");
        NodeIndex.Null.ToString().Should().Be("Node(Null)");
    }
}

public class TreeCapacityTests
{
    [Fact]
    public void MinimumCapacity_IsSixteen()
    {
        TreeCapacity.MinimumCapacity.Should().Be(16);
        new TreeCapacity(16).Value.Should().Be(16);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    public void Constructor_RejectsCapacityBelowMinimum(int value)
    {
        Action act = () => _ = new TreeCapacity(value);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_ReportsInitialCapacity_AsTheOffendingParameter()
    {
        Action act = () => _ = new TreeCapacity(15);

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithParameterName("InitialCapacity")
            .WithMessage("Capacity must be at least 16. (Parameter 'InitialCapacity')");
    }

    [Fact]
    public void ExplicitCast_AppliesTheGuard()
    {
        Action act = () => _ = (TreeCapacity)15;

        act.Should().Throw<ArgumentOutOfRangeException>();
        ((TreeCapacity)16).Value.Should().Be(16);
    }

    [Fact]
    public void ComparisonOperators_FollowNumericOrder()
    {
        var small = new TreeCapacity(16);
        var large = new TreeCapacity(64);
        var sameAsSmall = new TreeCapacity(16);

        (small < large).Should().BeTrue();
        (large > small).Should().BeTrue();
        (small <= large).Should().BeTrue();
        (small >= large).Should().BeFalse();
        (small <= sameAsSmall).Should().BeTrue();
        (small >= sameAsSmall).Should().BeTrue();
    }

    [Fact]
    public void CompareTo_OrdersByValue()
    {
        var small = new TreeCapacity(16);
        var large = new TreeCapacity(32);

        small.CompareTo(large).Should().BeNegative();
        large.CompareTo(small).Should().BePositive();
        small.CompareTo(small).Should().Be(0);
    }

    [Fact]
    public void ImplicitAndExplicitCasts_RoundTrip()
    {
        TreeCapacity capacity = new(32);

        int raw = capacity;
        raw.Should().Be(32);

        ((TreeCapacity)raw).Should().Be(capacity);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        new TreeCapacity(16).Should().Be(new TreeCapacity(16));
        (new TreeCapacity(16) == new TreeCapacity(16)).Should().BeTrue();
        (new TreeCapacity(16) != new TreeCapacity(17)).Should().BeTrue();
        new TreeCapacity(16).GetHashCode().Should().Be(new TreeCapacity(16).GetHashCode());
    }

    [Fact]
    public void ToString_UsesNamedForm()
    {
        new TreeCapacity(24).ToString().Should().Be("TreeCapacity(24)");
    }
}

public class DistanceSquaredTests
{
    [Fact]
    public void PositiveInfinity_IsUnreachable_AndSortsAboveEveryFiniteValue()
    {
        DistanceSquared.PositiveInfinity.Value.Should().Be(float.PositiveInfinity);

        (DistanceSquared.PositiveInfinity > new DistanceSquared(float.MaxValue)).Should().BeTrue();
    }

    [Fact]
    public void Zero_IsTheCoincidentDistance()
    {
        DistanceSquared.Zero.Value.Should().Be(0F);
        (DistanceSquared.Zero < new DistanceSquared(1F)).Should().BeTrue();
    }

    [Fact]
    public void ComparisonOperators_FollowNumericOrder()
    {
        var near = new DistanceSquared(1F);
        var far = new DistanceSquared(9F);
        var sameAsNear = new DistanceSquared(1F);

        (near < far).Should().BeTrue();
        (far > near).Should().BeTrue();
        (near <= far).Should().BeTrue();
        (near >= far).Should().BeFalse();
        (near <= sameAsNear).Should().BeTrue();
        (near >= sameAsNear).Should().BeTrue();
    }

    [Fact]
    public void CompareTo_OrdersByValue()
    {
        var near = new DistanceSquared(1F);
        var far = new DistanceSquared(4F);

        near.CompareTo(far).Should().BeNegative();
        far.CompareTo(near).Should().BePositive();
        near.CompareTo(near).Should().Be(0);
    }

    [Fact]
    public void ImplicitAndExplicitCasts_RoundTrip()
    {
        DistanceSquared distance = new(2.5F);

        float raw = distance;
        raw.Should().Be(2.5F);

        ((DistanceSquared)raw).Value.Should().Be(2.5F);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        new DistanceSquared(3F).Should().Be(new DistanceSquared(3F));
        (new DistanceSquared(3F) == new DistanceSquared(3F)).Should().BeTrue();
        (new DistanceSquared(3F) != new DistanceSquared(4F)).Should().BeTrue();
        new DistanceSquared(3F).GetHashCode().Should().Be(new DistanceSquared(3F).GetHashCode());
    }

    [Fact]
    public void ToString_UsesNamedForm()
    {
        new DistanceSquared(2.5F).ToString().Should().Be("DistanceSquared(2.5)");
        DistanceSquared.Zero.ToString().Should().Be("DistanceSquared(0)");
    }
}

public class SpatialEnumTests
{
    [Fact]
    public void ContainmentType_IsByteBacked_AndStartsAtDisjoint()
    {
        ((byte)ContainmentType.Disjoint).Should().Be(0);
        ((byte)ContainmentType.Contains).Should().Be(1);
        ((byte)ContainmentType.Intersects).Should().Be(2);
        Enum.GetUnderlyingType(typeof(ContainmentType)).Should().Be<byte>();
    }

    [Fact]
    public void Axis_IsByteBacked_AndOrdersXyz()
    {
        ((byte)Axis.X).Should().Be(0);
        ((byte)Axis.Y).Should().Be(1);
        ((byte)Axis.Z).Should().Be(2);
        Enum.GetUnderlyingType(typeof(Axis)).Should().Be<byte>();
    }

    [Fact]
    public void IntersectionKind_IsByteBacked_AndStartsAtNone()
    {
        ((byte)IntersectionKind.None).Should().Be(0);
        ((byte)IntersectionKind.Hit).Should().Be(1);
        Enum.GetUnderlyingType(typeof(IntersectionKind)).Should().Be<byte>();
    }

    [Fact]
    public void SweepKind_IsByteBacked_AndStartsAtMiss()
    {
        ((byte)SweepKind.Miss).Should().Be(0);
        ((byte)SweepKind.Hit).Should().Be(1);
        Enum.GetUnderlyingType(typeof(SweepKind)).Should().Be<byte>();
    }
}