using System.Runtime.InteropServices;
using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class Vec2CoreTests
{
    [Fact]
    public void Constants_AndLayout_AreCorrect()
    {
        Marshal.SizeOf<Vec2>().Should().Be(8);
        Vec2.Zero.Should().Be(new Vec2(0f, 0f));
        Vec2.UnitX.Should().Be(new Vec2(1f, 0f));
        Vec2.UnitY.Should().Be(new Vec2(0f, 1f));
        Vec2.One.Should().Be(new Vec2(1f, 1f));
    }

    [Fact]
    public void Indexer_GetSetAndBounds()
    {
        var v = new Vec2(1f, 2f);
        v[0].Should().Be(1f);
        v[1].Should().Be(2f);
        v[0] = 5f;
        v.X.Should().Be(5f);
        Action get = () => { _ = v[2]; };
        get.Should().Throw<Exception>();
        Action set = () => { v[2] = 0f; };
        set.Should().Throw<Exception>();
    }

    [Fact]
    public void Bridges_RoundTrip()
    {
        var v = new Vec2(1f, -2f);
        Vec2.FromSystemNumerics(v.ToSystemNumerics()).Should().Be(new Vec2(1f, -2f));
        (((float, float))v).Should().Be((1f, -2f));
        Vec2 fromTuple = (3f, 4f);
        fromTuple.Should().Be(new Vec2(3f, 4f));
        float[] arr = new float[2];
        v.CopyTo(arr);
        arr.Should().Equal(1f, -2f);
        v.AsSpan().ToArray().Should().Equal(1f, -2f);
        v.TryCopyTo(arr).Should().BeTrue();
    }

    [Fact]
    public void SpanCtor_RejectsShortSpan()
    {
        Action create = () =>
        {
            Span<float> one = stackalloc float[1];
            one[0] = 1f;
            _ = new Vec2(one);
        };
        create.Should().Throw<Exception>();
    }
}
