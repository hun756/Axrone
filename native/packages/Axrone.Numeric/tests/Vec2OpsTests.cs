using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class Vec2OpsTests
{
    [Fact]
    public void Operators_ArithmeticAndBitwise()
    {
        (new Vec2(1f, 2f) + new Vec2(3f, 4f)).Should().Be(new Vec2(4f, 6f));
        (new Vec2(4f, 6f) - new Vec2(1f, 2f)).Should().Be(new Vec2(3f, 4f));
        (new Vec2(2f, 3f) * new Vec2(4f, 5f)).Should().Be(new Vec2(8f, 15f));
        (new Vec2(2f, 4f) / new Vec2(2f, 2f)).Should().Be(new Vec2(1f, 2f));
        (new Vec2(2f, 3f) * 2f).Should().Be(new Vec2(4f, 6f));
        (2f * new Vec2(2f, 3f)).Should().Be(new Vec2(4f, 6f));
        (new Vec2(4f, 6f) / 2f).Should().Be(new Vec2(2f, 3f));
        (-new Vec2(1f, -2f)).Should().Be(new Vec2(-1f, 2f));
        (+new Vec2(1f, 2f)).Should().Be(new Vec2(1f, 2f));
        (new Vec2(1f, 2f) == new Vec2(1f, 2f)).Should().BeTrue();
        (new Vec2(1f, 2f) != new Vec2(1f, 3f)).Should().BeTrue();
    }

    [Fact]
    public void Named_Arithmetic_MatchOperators()
    {
        var a = new Vec2(6f, 8f);
        var b = new Vec2(2f, 4f);
        Vec2.Add(a, b).Should().Be(a + b);
        Vec2.Subtract(a, b).Should().Be(a - b);
        Vec2.Multiply(a, b).Should().Be(a * b);
        Vec2.Divide(a, b).Should().Be(a / b);
        Vec2.Multiply(a, 2f).Should().Be(a * 2f);
        Vec2.Divide(a, 2f).Should().Be(a / 2f);
        Vec2.Negate(a).Should().Be(-a);
    }

    [Fact]
    public void Comparisons_MaskAndBool()
    {
        Vec2 mask = Vec2.GreaterThan(new Vec2(2f, 3f), Vec2.One);
        Vec2.CountWhereAllBitsSet(mask).Should().Be(2);
        Vec2.CountWhereAllBitsSet(Vec2.GreaterThan(Vec2.Zero, Vec2.One)).Should().Be(0);
        Vec2.GreaterThanAll(new Vec2(2f, 3f), Vec2.One).Should().BeTrue();
        Vec2.GreaterThanAny(new Vec2(0f, 3f), Vec2.One).Should().BeTrue();
        Vec2.LessThanAll(Vec2.Zero, Vec2.One).Should().BeTrue();
        Vec2.LessThanOrEqualAny(Vec2.Zero, Vec2.Zero).Should().BeTrue();
        Vec2.CountWhereAllBitsSet(Vec2.GreaterThanOrEqual(new Vec2(1f, 0f), Vec2.One)).Should().Be(1);
    }

    [Fact]
    public void Select_Index_Shuffle()
    {
        var allSet = new Vec2(
            BitConverter.UInt32BitsToSingle(0xFFFF_FFFF),
            BitConverter.UInt32BitsToSingle(0xFFFF_FFFF));
        Vec2.ConditionalSelect(allSet, new Vec2(10f, 20f), new Vec2(30f, 40f)).Should().Be(new Vec2(10f, 20f));
        Vec2.ConditionalSelect(Vec2.Zero, new Vec2(10f, 20f), new Vec2(30f, 40f)).Should().Be(new Vec2(30f, 40f));
        Vec2.IndexOf(new Vec2(5f, 7f), 7f).Should().Be(1);
        Vec2.IndexOf(new Vec2(5f, 7f), 9f).Should().Be(-1);
        Vec2.LastIndexOf(new Vec2(5f, 5f), 5f).Should().Be(1);
        Vec2.Shuffle(new Vec2(1f, 2f), 1, 0).Should().Be(new Vec2(2f, 1f));
        Vec2.Count(new Vec2(1f, 0f)).Should().Be(1);
        Vec2.All(new Vec2(1f, 2f)).Should().BeTrue();
        Vec2.None(Vec2.Zero).Should().BeTrue();
    }

    [Fact]
    public void Classifiers_MaskVectors()
    {
        Vec2.CountWhereAllBitsSet(Vec2.IsNaN(new Vec2(float.NaN, 0f))).Should().Be(1);
        Vec2.CountWhereAllBitsSet(Vec2.IsFinite(Vec2.One)).Should().Be(2);
        Vec2.CountWhereAllBitsSet(Vec2.IsZero(Vec2.Zero)).Should().Be(2);
        Vec2.CountWhereAllBitsSet(Vec2.IsNegative(new Vec2(-1f, 1f))).Should().Be(1);
        Vec2.CountWhereAllBitsSet(Vec2.IsInteger(new Vec2(2f, 2.5f))).Should().Be(1);
    }

    [Fact]
    public void Parse_RoundTrips()
    {
        var v = new Vec2(1.5f, -2.25f);
        Vec2.Parse(v.ToString(), null).Should().Be(v);
        Vec2.TryParse("<3, 4>", null, out Vec2 parsed).Should().BeTrue();
        parsed.Should().Be(new Vec2(3f, 4f));
        Vec2.TryParse("nope", null, out _).Should().BeFalse();
        Action bad = () => Vec2.Parse("nope", null);
        bad.Should().Throw<Exception>();
    }

    [Fact]
    public void Equality_Hash_BitEquals()
    {
        var a = new Vec2(1f, 2f);
        a.Equals(new Vec2(1f, 2f)).Should().BeTrue();
        a.Equals(new Vec2(1f, 2.0001f), 0.001f).Should().BeTrue();
        a.BitEquals(new Vec2(1f, 2f)).Should().BeTrue();
        a.GetHashCode().Should().Be(new Vec2(1f, 2f).GetHashCode());
        Vec2.EqualsAll(a, new Vec2(1f, 2f)).Should().BeTrue();
        Vec2.EqualsAny(a, new Vec2(9f, 2f)).Should().BeTrue();
    }
}
