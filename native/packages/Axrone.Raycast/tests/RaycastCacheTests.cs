namespace Axrone.Raycast.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class RaycastCacheTests
{
    private static readonly RaycastQuery2D Query2D = new(
        new Ray2D(new Vec2(-5f, 0f), Vec2.UnitX),
        20f,
        (uint)RaycastLayer.Default,
        RaycastFlags.ClosestOnly,
        1);

    private static readonly RaycastQuery3D Query3D = new(
        new Ray3D(new Vec3(-5f, 0f, 0f), Vec3.UnitX),
        20f,
        (uint)RaycastLayer.Default,
        RaycastFlags.ClosestOnly,
        1);

    private static readonly RaycastHit2D Hit2D = new(
        new RaycastShapeId(3),
        new Vec2(-1f, 0f),
        new Vec2(-1f, 0f),
        4f,
        0.2f,
        (uint)RaycastLayer.Default);

    private static readonly RaycastHit3D Hit3D = new(
        new RaycastShapeId(4),
        new Vec3(-1f, 0f, 0f),
        new Vec3(-1f, 0f, 0f),
        4f,
        0.2f,
        -1,
        default,
        (uint)RaycastLayer.Default);

    [Fact]
    public void Cache2DStartsEmptyAndServesTheRecordedQuery()
    {
        var cache = new RaycastCache2D(8);
        cache.Capacity.Should().Be(8);
        cache.TryGet(in Query2D, out _).Should().BeFalse();

        cache.Set(in Query2D, in Hit2D);
        cache.TryGet(in Query2D, out RaycastHit2D cached).Should().BeTrue();
        cached.Should().Be(Hit2D);

        var otherDistance = Query2D with { MaxDistance = 21f };
        cache.TryGet(in otherDistance, out _).Should().BeFalse();

        var otherLayer = Query2D with { LayerMask = (uint)RaycastLayer.Water };
        cache.TryGet(in otherLayer, out _).Should().BeFalse();
    }

    [Fact]
    public void Cache2DAdvancesInvalidatesAndClears()
    {
        var cache = new RaycastCache2D(8);
        cache.Set(in Query2D, in Hit2D);

        cache.AdvanceFrame();
        cache.TryGet(in Query2D, out _).Should().BeFalse();

        cache.Set(in Query2D, in Hit2D);
        cache.Invalidate();
        cache.TryGet(in Query2D, out _).Should().BeFalse();

        cache.Set(in Query2D, in Hit2D);
        cache.Clear();
        cache.TryGet(in Query2D, out _).Should().BeFalse();
    }

    [Fact]
    public void Cache3DStartsEmptyAndTracksGenerations()
    {
        var cache = new RaycastCache3D(8);
        cache.Capacity.Should().Be(8);
        cache.TryGet(in Query3D, out _).Should().BeFalse();

        cache.Set(in Query3D, in Hit3D);
        cache.TryGet(in Query3D, out RaycastHit3D cached).Should().BeTrue();
        cached.Should().Be(Hit3D);

        cache.AdvanceFrame();
        cache.TryGet(in Query3D, out _).Should().BeFalse();

        cache.Set(in Query3D, in Hit3D);
        cache.Invalidate();
        cache.TryGet(in Query3D, out _).Should().BeFalse();

        cache.Set(in Query3D, in Hit3D);
        cache.Clear();
        cache.TryGet(in Query3D, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    [InlineData(3)]
    [InlineData(12)]
    public void CacheRejectsNonPowerOfTwoCapacity(int capacity)
    {
        Action create2D = () => _ = new RaycastCache2D(capacity);
        create2D.Should().Throw<ArgumentOutOfRangeException>();

        Action create3D = () => _ = new RaycastCache3D(capacity);
        create3D.Should().Throw<ArgumentOutOfRangeException>();
    }
}
