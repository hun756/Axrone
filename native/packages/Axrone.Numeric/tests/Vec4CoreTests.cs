using System.Runtime.InteropServices;
using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class Vec4CoreTests
{
    [Fact]
    public void Constants_AndLayout_AreCorrect()
    {
        Marshal.SizeOf<Vec4>().Should().Be(16);
        Vec4.Zero.Should().Be(new Vec4(0f, 0f, 0f, 0f));
        Vec4.UnitW.Should().Be(new Vec4(0f, 0f, 0f, 1f));
        Vec4.One.Should().Be(new Vec4(1f, 1f, 1f, 1f));
    }

    [Fact]
    public void CrossDimensional_Ctors_Work()
    {
        new Vec4(new Vec2(1f, 2f), 3f, 4f).Should().Be(new Vec4(1f, 2f, 3f, 4f));
        new Vec4(new Vec3(1f, 2f, 3f), 4f).Should().Be(new Vec4(1f, 2f, 3f, 4f));
        Vec4.Create(new Vec2(1f, 2f), 3f, 4f).Should().Be(new Vec4(1f, 2f, 3f, 4f));
    }

    [Fact]
    public void Indexer_GetSetAndBounds()
    {
        var v = new Vec4(1f, 2f, 3f, 4f);
        v[3].Should().Be(4f);
        v[0] = 9f;
        v.X.Should().Be(9f);
        Action get = () => { _ = v[4]; };
        get.Should().Throw<Exception>();
    }

    [Fact]
    public void Bridges_RoundTrip()
    {
        var v = new Vec4(1f, -2f, 3f, -4f);
        Vec4.FromSystemNumerics(v.ToSystemNumerics()).Should().Be(v);
        (((float, float, float, float))v).Should().Be((1f, -2f, 3f, -4f));
        float[] arr = new float[4];
        v.CopyTo(arr);
        arr.Should().Equal(1f, -2f, 3f, -4f);
        v.AsSpan().ToArray().Should().Equal(1f, -2f, 3f, -4f);
    }
}
