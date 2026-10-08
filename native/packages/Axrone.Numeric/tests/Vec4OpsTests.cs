using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class Vec4OpsTests
{
    [Fact]
    public void Operators_Arithmetic()
    {
        (new Vec4(1f, 2f, 3f, 4f) + new Vec4(4f, 3f, 2f, 1f)).Should().Be(new Vec4(5f, 5f, 5f, 5f));
        (new Vec4(2f, 2f, 2f, 2f) * 3f).Should().Be(new Vec4(6f, 6f, 6f, 6f));
        (3f * new Vec4(1f, 1f, 1f, 1f)).Should().Be(new Vec4(3f, 3f, 3f, 3f));
        (new Vec4(4f, 6f, 8f, 10f) / 2f).Should().Be(new Vec4(2f, 3f, 4f, 5f));
        (-new Vec4(1f, -2f, 3f, -4f)).Should().Be(new Vec4(-1f, 2f, -3f, 4f));
        (new Vec4(1f, 2f, 3f, 4f) == new Vec4(1f, 2f, 3f, 4f)).Should().BeTrue();
        (new Vec4(1f, 2f, 3f, 4f) != new Vec4(1f, 2f, 3f, 5f)).Should().BeTrue();
        Vec4.Add(new Vec4(1f, 1f, 1f, 1f), new Vec4(2f, 2f, 2f, 2f)).Should().Be(new Vec4(3f, 3f, 3f, 3f));
        Vec4.Negate(new Vec4(1f, -1f, 1f, -1f)).Should().Be(new Vec4(-1f, 1f, -1f, 1f));
    }

    [Fact]
    public void Comparisons_MaskSemantics()
    {
        Vec4.CountWhereAllBitsSet(Vec4.GreaterThan(new Vec4(2f, 3f, 4f, 5f), Vec4.One)).Should().Be(4);
        Vec4.CountWhereAllBitsSet(Vec4.GreaterThan(Vec4.Zero, Vec4.One)).Should().Be(0);
        Vec4.GreaterThanAll(new Vec4(2f, 2f, 2f, 2f), Vec4.One).Should().BeTrue();
        Vec4.LessThanAny(Vec4.Zero, Vec4.One).Should().BeTrue();
        Vec4.CountWhereAllBitsSet(Vec4.LessThanOrEqual(Vec4.One, Vec4.One)).Should().Be(4);
        Vec4.IndexOf(new Vec4(5f, 7f, 9f, 11f), 9f).Should().Be(2);
        Vec4.LastIndexOf(new Vec4(5f, 5f, 5f, 5f), 5f).Should().Be(3);
        Vec4.IndexOf(new Vec4(1f, 2f, 3f, 4f), 9f).Should().Be(-1);
        Vec4.Shuffle(new Vec4(1f, 2f, 3f, 4f), 3, 2, 1, 0).Should().Be(new Vec4(4f, 3f, 2f, 1f));
        Vec4.Count(new Vec4(1f, 0f, 3f, 0f)).Should().Be(2);
    }

    [Fact]
    public void Classifiers_MaskSemantics()
    {
        Vec4.CountWhereAllBitsSet(Vec4.IsNaN(new Vec4(float.NaN, 0f, 0f, 0f))).Should().Be(1);
        Vec4.CountWhereAllBitsSet(Vec4.IsFinite(Vec4.One)).Should().Be(4);
        Vec4.CountWhereAllBitsSet(Vec4.IsZero(Vec4.Zero)).Should().Be(4);
        Vec4.CountWhereAllBitsSet(Vec4.IsNegative(new Vec4(-1f, 1f, -1f, 1f))).Should().Be(2);
        Vec4.CountWhereAllBitsSet(Vec4.IsInteger(new Vec4(2f, 2.5f, 3f, 3.5f))).Should().Be(2);
    }

    [Fact]
    public void ConditionalSelect_PicksLanes()
    {
        var allSet = new Vec4(
            BitConverter.UInt32BitsToSingle(0xFFFF_FFFF),
            BitConverter.UInt32BitsToSingle(0xFFFF_FFFF),
            BitConverter.UInt32BitsToSingle(0xFFFF_FFFF),
            BitConverter.UInt32BitsToSingle(0xFFFF_FFFF));
        Vec4.ConditionalSelect(allSet, new Vec4(1f, 2f, 3f, 4f), new Vec4(5f, 6f, 7f, 8f)).Should().Be(new Vec4(1f, 2f, 3f, 4f));
        Vec4.ConditionalSelect(Vec4.Zero, new Vec4(1f, 2f, 3f, 4f), new Vec4(5f, 6f, 7f, 8f)).Should().Be(new Vec4(5f, 6f, 7f, 8f));
    }

    [Fact]
    public void Parse_RoundTrips()
    {
        var v = new Vec4(1.5f, -2.25f, 3f, 4.75f);
        Vec4.Parse(v.ToString(), null).Should().Be(v);
        Vec4.TryParse("<3, 4, 5, 6>", null, out Vec4 parsed).Should().BeTrue();
        parsed.Should().Be(new Vec4(3f, 4f, 5f, 6f));
        Vec4.TryParse("nope", null, out _).Should().BeFalse();
        Vec4.TryParse("<1, 2, 3>", null, out _).Should().BeFalse();
        Action bad = () => Vec4.Parse("nope", null);
        bad.Should().Throw<Exception>();
    }

    [Fact]
    public void Equality_Hash_Tolerance()
    {
        var a = new Vec4(1f, 2f, 3f, 4f);
        a.Equals(new Vec4(1f, 2f, 3f, 4f)).Should().BeTrue();
        a.Equals(new Vec4(1f, 2f, 3f, 4.0001f), 0.001f).Should().BeTrue();
        a.BitEquals(new Vec4(1f, 2f, 3f, 4f)).Should().BeTrue();
        a.GetHashCode().Should().Be(new Vec4(1f, 2f, 3f, 4f).GetHashCode());
        Vec4.EqualsAll(a, new Vec4(1f, 2f, 3f, 4f)).Should().BeTrue();
        Vec4.EqualsAny(a, new Vec4(9f, 9f, 9f, 4f)).Should().BeTrue();
    }
}
