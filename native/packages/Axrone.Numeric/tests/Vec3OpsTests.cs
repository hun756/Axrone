namespace Axrone.Numeric.Tests;

using System;
using System.Globalization;
using Xunit;
using FluentAssertions;
using Axrone.Numeric;

public class Vec3OpsTests
{

    private static void AssertComponents(Vec3 actual, float x, float y, float z)
    {
        actual.X.Should().Be(x);
        actual.Y.Should().Be(y);
        actual.Z.Should().Be(z);
    }

    private static void AssertBits(Vec3 actual, float x, float y, float z)
    {
        BitConverter.SingleToUInt32Bits(actual.X).Should().Be(BitConverter.SingleToUInt32Bits(x));
        BitConverter.SingleToUInt32Bits(actual.Y).Should().Be(BitConverter.SingleToUInt32Bits(y));
        BitConverter.SingleToUInt32Bits(actual.Z).Should().Be(BitConverter.SingleToUInt32Bits(z));
    }

    private static void AssertMask(Vec3 mask, bool x, bool y, bool z)
    {
        BitConverter.SingleToUInt32Bits(mask.X).Should().Be(x ? 0xFFFF_FFFFu : 0u);
        BitConverter.SingleToUInt32Bits(mask.Y).Should().Be(y ? 0xFFFF_FFFFu : 0u);
        BitConverter.SingleToUInt32Bits(mask.Z).Should().Be(z ? 0xFFFF_FFFFu : 0u);
    }

    private static float Bits(uint pattern) => BitConverter.UInt32BitsToSingle(pattern);

    [Fact]
    public void Operator_Add_IsComponentWise()
    {
        Vec3 a = new(1F, 2F, 3F);
        Vec3 b = new(4F, 5F, 6F);
        AssertComponents(a + b, 5F, 7F, 9F);
    }

    [Fact]
    public void Operator_Subtract_IsComponentWise()
    {
        Vec3 a = new(4F, 5F, 6F);
        Vec3 b = new(1F, 2F, 3F);
        AssertComponents(a - b, 3F, 3F, 3F);
    }

    [Fact]
    public void Operator_Multiply_IsComponentWise()
    {
        Vec3 a = new(1F, 2F, 3F);
        Vec3 b = new(4F, 5F, 6F);
        AssertComponents(a * b, 4F, 10F, 18F);
    }

    [Fact]
    public void Operator_Multiply_ScalarOnRight()
    {
        Vec3 a = new(1F, 2F, 3F);
        AssertComponents(a * 2F, 2F, 4F, 6F);
    }

    [Fact]
    public void Operator_Multiply_ScalarOnLeft()
    {
        Vec3 a = new(1F, 2F, 3F);
        AssertComponents(2F * a, 2F, 4F, 6F);
    }

    [Fact]
    public void Operator_Divide_IsComponentWise()
    {
        Vec3 a = new(4F, 9F, 16F);
        Vec3 b = new(2F, 3F, 4F);
        AssertComponents(a / b, 2F, 3F, 4F);
    }

    [Fact]
    public void Operator_Divide_ByScalar()
    {
        Vec3 a = new(2F, 4F, 8F);
        AssertComponents(a / 2F, 1F, 2F, 4F);
    }

    [Fact]
    public void Operator_Negate_FlipsSign()
    {
        Vec3 a = new(1F, -2F, 3F);
        AssertComponents(-a, -1F, 2F, -3F);
    }

    [Fact]
    public void Operator_UnaryPlus_ReturnsSameValue()
    {
        Vec3 a = new(1F, -2F, 3F);
        AssertComponents(+a, 1F, -2F, 3F);
    }

    [Fact]
    public void Named_Arithmetic_MatchesOperators()
    {
        Vec3 a = new(4F, 9F, 16F);
        Vec3 b = new(2F, 3F, 4F);

        AssertComponents(Vec3.Add(a, b), 6F, 12F, 20F);
        AssertComponents(Vec3.Subtract(a, b), 2F, 6F, 12F);
        AssertComponents(Vec3.Multiply(a, b), 8F, 27F, 64F);
        AssertComponents(Vec3.Multiply(a, 2F), 8F, 18F, 32F);
        AssertComponents(Vec3.Multiply(2F, a), 8F, 18F, 32F);
        AssertComponents(Vec3.Divide(a, b), 2F, 3F, 4F);
        AssertComponents(Vec3.Divide(a, 2F), 2F, 4.5F, 8F);
        AssertComponents(Vec3.Negate(a), -4F, -9F, -16F);
    }

    [Fact]
    public void Bitwise_TruthTable_OnTwoValues()
    {
        Vec3 a = new(Bits(0x0000_0000u), Bits(0xFFFF_FFFFu), Bits(0x0F0F_0F0Fu));
        Vec3 b = new(Bits(0xFFFF_FFFFu), Bits(0x0000_0000u), Bits(0xF0F0_F0F0u));

        AssertBits(a & b, Bits(0x0000_0000u), Bits(0x0000_0000u), Bits(0x0000_0000u));
        AssertBits(a | b, Bits(0xFFFF_FFFFu), Bits(0xFFFF_FFFFu), Bits(0xFFFF_FFFFu));
        AssertBits(a ^ b, Bits(0xFFFF_FFFFu), Bits(0xFFFF_FFFFu), Bits(0xFFFF_FFFFu));
        AssertBits(~a, Bits(0xFFFF_FFFFu), Bits(0x0000_0000u), Bits(0xF0F0_F0F0u));
        AssertBits(~b, Bits(0x0000_0000u), Bits(0xFFFF_FFFFu), Bits(0x0F0F_0F0Fu));
    }

    [Fact]
    public void Bitwise_AllBitsSetAndZero_AreIdentities()
    {
        Vec3 x = new(Bits(0x1234_5678u), Bits(0x9ABC_DEF0u), Bits(0x0F0F_F0F0u));

        AssertBits(x & Vec3.AllBitsSet, x.X, x.Y, x.Z);
        AssertBits(x | Vec3.AllBitsSet, Bits(0xFFFF_FFFFu), Bits(0xFFFF_FFFFu), Bits(0xFFFF_FFFFu));
        AssertBits(x ^ Vec3.AllBitsSet, (~x).X, (~x).Y, (~x).Z);
        AssertBits(x & Vec3.Zero, 0F, 0F, 0F);
        AssertBits(x | Vec3.Zero, x.X, x.Y, x.Z);
        AssertBits(x ^ Vec3.Zero, x.X, x.Y, x.Z);
    }

    [Fact]
    public void Named_Bitwise_MatchesOperators()
    {
        Vec3 a = new(Bits(0x0F00_FF00u), Bits(0xF0F0_0F0Fu), Bits(0x1234_5678u));
        Vec3 b = new(Bits(0x00FF_00FFu), Bits(0x0F0F_0F0Fu), Bits(0x8765_4321u));

        AssertBits(Vec3.BitwiseAnd(a, b), (a & b).X, (a & b).Y, (a & b).Z);
        AssertBits(Vec3.BitwiseOr(a, b), (a | b).X, (a | b).Y, (a | b).Z);
        AssertBits(Vec3.Xor(a, b), (a ^ b).X, (a ^ b).Y, (a ^ b).Z);
        AssertBits(Vec3.OnesComplement(a), (~a).X, (~a).Y, (~a).Z);
        AssertBits(Vec3.AndNot(a, b), (a & ~b).X, (a & ~b).Y, (a & ~b).Z);
    }

    [Fact]
    public void Classification_MaskVectorForm()
    {
        Vec3 v = new(float.NaN, 1F, 0F);

        AssertMask(Vec3.IsNaN(v), true, false, false);
        AssertMask(Vec3.IsFinite(v), false, true, true);
        AssertMask(Vec3.IsInfinity(v), false, false, false);
        AssertMask(Vec3.IsZero(v), false, false, true);
        AssertMask(Vec3.IsPositive(v), false, true, true);
        AssertMask(Vec3.IsNegative(v), true, false, false);
        AssertMask(Vec3.IsInteger(v), false, true, true);
    }

    [Fact]
    public void Classification_InfinityAndSubnormal()
    {
        Vec3 v = new(float.PositiveInfinity, float.NegativeInfinity, float.Epsilon);

        AssertMask(Vec3.IsInfinity(v), true, true, false);
        AssertMask(Vec3.IsPositiveInfinity(v), true, false, false);
        AssertMask(Vec3.IsNegativeInfinity(v), false, true, false);
        AssertMask(Vec3.IsSubnormal(v), false, false, true);
        AssertMask(Vec3.IsNormal(v), false, false, false);
    }

    [Fact]
    public void Classification_EvenAndOddIntegers()
    {
        Vec3 v = new(2F, 3F, 4F);

        AssertMask(Vec3.IsInteger(v), true, true, true);
        AssertMask(Vec3.IsEvenInteger(v), true, false, true);
        AssertMask(Vec3.IsOddInteger(v), false, true, false);
    }

    [Fact]
    public void Classification_NegativeFlag()
    {
        Vec3 v = new(-1F, 1F, -0F);

        AssertMask(Vec3.IsNegative(v), true, false, true);
        AssertMask(Vec3.IsPositive(v), false, true, false);
    }

    [Fact]
    public void Comparison_MaskAndBoolForms()
    {
        Vec3 a = new(1F, 2F, 3F);
        Vec3 b = new(2F, 2F, 1F);

        AssertMask(Vec3.GreaterThan(a, b), false, false, true);
        Vec3.GreaterThanAll(a, b).Should().BeFalse();
        Vec3.GreaterThanAny(a, b).Should().BeTrue();

        AssertMask(Vec3.GreaterThanOrEqual(a, b), false, true, true);
        Vec3.GreaterThanOrEqualAll(a, b).Should().BeFalse();
        Vec3.GreaterThanOrEqualAny(a, b).Should().BeTrue();

        AssertMask(Vec3.LessThan(a, b), true, false, false);
        Vec3.LessThanAll(a, b).Should().BeFalse();
        Vec3.LessThanAny(a, b).Should().BeTrue();

        AssertMask(Vec3.LessThanOrEqual(a, b), true, true, false);
        Vec3.LessThanOrEqualAll(a, b).Should().BeFalse();
        Vec3.LessThanOrEqualAny(a, b).Should().BeTrue();
    }

    [Fact]
    public void Comparison_AllAndAny_WithEqualVectors()
    {
        Vec3 a = new(1F, 2F, 3F);
        Vec3 b = new(1F, 2F, 3F);

        Vec3.GreaterThanAll(a, b).Should().BeFalse();
        Vec3.GreaterThanOrEqualAll(a, b).Should().BeTrue();
        Vec3.LessThanAll(a, b).Should().BeFalse();
        Vec3.LessThanOrEqualAll(a, b).Should().BeTrue();
        Vec3.GreaterThanAny(a, b).Should().BeFalse();
        Vec3.GreaterThanOrEqualAny(a, b).Should().BeTrue();
        Vec3.LessThanAny(a, b).Should().BeFalse();
        Vec3.LessThanOrEqualAny(a, b).Should().BeTrue();
    }

    [Fact]
    public void ConditionalSelect_AllTrueTakesTrueValue()
    {
        Vec3 condition = Vec3.AllBitsSet;
        Vec3 trueValue = new(1F, 2F, 3F);
        Vec3 falseValue = new(4F, 5F, 6F);
        AssertComponents(Vec3.ConditionalSelect(condition, trueValue, falseValue), 1F, 2F, 3F);
    }

    [Fact]
    public void ConditionalSelect_AllFalseTakesFalseValue()
    {
        Vec3 condition = Vec3.Zero;
        Vec3 trueValue = new(1F, 2F, 3F);
        Vec3 falseValue = new(4F, 5F, 6F);
        AssertComponents(Vec3.ConditionalSelect(condition, trueValue, falseValue), 4F, 5F, 6F);
    }

    [Fact]
    public void ConditionalSelect_MixedConditionSelectsPerLane()
    {
        Vec3 condition = new(Bits(0xFFFF_FFFFu), 0F, Bits(0xFFFF_FFFFu));
        Vec3 trueValue = new(1F, 2F, 3F);
        Vec3 falseValue = new(4F, 5F, 6F);
        AssertComponents(Vec3.ConditionalSelect(condition, trueValue, falseValue), 1F, 5F, 3F);
    }

    [Fact]
    public void IndexOf_ReturnsFirstMatchingLane()
    {
        Vec3 v = new(1F, 2F, 3F);
        Vec3 target = new(9F, 2F, 9F);
        Vec3.IndexOf(v, target).Should().Be(1);
    }

    [Fact]
    public void IndexOf_ReturnsMinusOneWhenNoLaneMatches()
    {
        Vec3 v = new(1F, 2F, 3F);
        Vec3 target = new(9F, 9F, 9F);
        Vec3.IndexOf(v, target).Should().Be(-1);
    }

    [Fact]
    public void LastIndexOf_ReturnsLastMatchingLane()
    {
        Vec3 v = new(1F, 2F, 1F);
        Vec3 target = new(1F, 9F, 1F);
        Vec3.LastIndexOf(v, target).Should().Be(2);
    }

    [Fact]
    public void LastIndexOf_ReturnsMinusOneWhenNoLaneMatches()
    {
        Vec3 v = new(1F, 2F, 3F);
        Vec3 target = new(9F, 9F, 9F);
        Vec3.LastIndexOf(v, target).Should().Be(-1);
    }

    [Fact]
    public void IndexOfWhereAllBitsSet_ReturnsFirstAllBitsSetLane()
    {
        Vec3 v = new(0F, Bits(0xFFFF_FFFFu), Bits(0xFFFF_FFFFu));
        Vec3.IndexOfWhereAllBitsSet(v).Should().Be(1);
    }

    [Fact]
    public void IndexOfWhereAllBitsSet_ReturnsMinusOneWhenNone()
    {
        Vec3 v = new(1F, 2F, 3F);
        Vec3.IndexOfWhereAllBitsSet(v).Should().Be(-1);
    }

    [Fact]
    public void LastIndexOfWhereAllBitsSet_ReturnsLastAllBitsSetLane()
    {
        Vec3 v = new(Bits(0xFFFF_FFFFu), Bits(0xFFFF_FFFFu), 0F);
        Vec3.LastIndexOfWhereAllBitsSet(v).Should().Be(1);
    }

    [Fact]
    public void LastIndexOfWhereAllBitsSet_ReturnsMinusOneWhenNone()
    {
        Vec3 v = new(1F, 2F, 3F);
        Vec3.LastIndexOfWhereAllBitsSet(v).Should().Be(-1);
    }

    [Fact]
    public void Shuffle_IdentityReorder()
    {
        Vec3 v = new(1F, 2F, 3F);
        AssertComponents(Vec3.Shuffle(v, 0, 1, 2), 1F, 2F, 3F);
    }

    [Fact]
    public void Shuffle_ReverseLanes()
    {
        Vec3 v = new(1F, 2F, 3F);
        AssertComponents(Vec3.Shuffle(v, 2, 1, 0), 3F, 2F, 1F);
    }

    [Fact]
    public void Shuffle_BroadcastSingleLane()
    {
        Vec3 v = new(1F, 2F, 3F);
        AssertComponents(Vec3.Shuffle(v, 1, 1, 1), 2F, 2F, 2F);
    }

    [Fact]
    public void Shuffle_OutOfRangeIndexYieldsZero()
    {
        Vec3 v = new(1F, 2F, 3F);
        AssertComponents(Vec3.Shuffle(v, 3, 0, 1), 0F, 1F, 2F);
        AssertComponents(Vec3.Shuffle(v, 0, 255, 2), 1F, 0F, 3F);
    }

    [Theory]
    [InlineData(1.5F, -2.25F, 3.125F)]
    [InlineData(-1F, 0F, 1F)]
    [InlineData(1E10F, -1E-5F, 42F)]
    [InlineData(3.4028235E38F, -3.4028235E38F, 1E-45F)]
    [InlineData(-0F, 0F, 123.456F)]
    public void ToString_RoundTrip_IsBitEqual(float x, float y, float z)
    {
        Vec3 original = new(x, y, z);
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            string text = original.ToString();
            Vec3 parsed = Vec3.Parse(text);
            AssertBits(parsed, x, y, z);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void TryFormat_TooSmallCharDestination_ReturnsFalse()
    {
        Vec3 value = new(1.5F, 2.5F, 3.5F);
        Span<char> destination = stackalloc char[3];
        bool ok = value.TryFormat(destination, out int charsWritten);
        ok.Should().BeFalse();
        charsWritten.Should().Be(0);
    }

    [Fact]
    public void TryFormat_TooSmallUtf8Destination_ReturnsFalse()
    {
        Vec3 value = new(1.5F, 2.5F, 3.5F);
        Span<byte> destination = stackalloc byte[2];
        bool ok = value.TryFormat(destination, out int bytesWritten);
        ok.Should().BeFalse();
        bytesWritten.Should().Be(0);
    }

    [Fact]
    public void TryFormat_AdequateDestination_ReturnsTrue()
    {
        Vec3 value = new(1.5F, 2.5F, 3.5F);
        Span<char> destination = stackalloc char[96];
        bool ok = value.TryFormat(destination, out int charsWritten);
        ok.Should().BeTrue();
        charsWritten.Should().BeGreaterThan(0);
        destination[..charsWritten].ToString().Should().Be("1.5,2.5,3.5");
    }

    [Theory]
    [InlineData("")]
    [InlineData("1, 2")]
    [InlineData("garbage")]
    [InlineData("1, 2, 3, 4")]
    [InlineData("1, 2, 3,")]
    public void TryParse_InvalidInput_ReturnsFalse(string input)
    {
        Vec3.TryParse(input, CultureInfo.InvariantCulture, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("1, 2")]
    [InlineData("garbage")]
    public void Parse_InvalidInput_ThrowsFormatException(string input)
    {
        var act = () => Vec3.Parse(input, CultureInfo.InvariantCulture);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Utf8_Parse_RoundTrips()
    {
        Vec3 original = new(1.5F, -2.25F, 3F);
        CultureInfo culture = CultureInfo.InvariantCulture;

        Span<byte> utf8 = stackalloc byte[64];
        original.TryFormat(utf8, out int bytesWritten, "R", culture).Should().BeTrue();

        Vec3 parsed = Vec3.Parse(utf8.Slice(0, bytesWritten), culture);
        AssertComponents(parsed, 1.5F, -2.25F, 3F);
    }

    [Fact]
    public void Utf8_TryParse_RoundTrips()
    {
        Vec3 original = new(1.5F, -2.25F, 3F);
        CultureInfo culture = CultureInfo.InvariantCulture;

        Span<byte> utf8 = stackalloc byte[64];
        original.TryFormat(utf8, out int bytesWritten, "R", culture).Should().BeTrue();

        bool ok = Vec3.TryParse(utf8.Slice(0, bytesWritten), culture, out Vec3 parsed);
        ok.Should().BeTrue();
        AssertComponents(parsed, 1.5F, -2.25F, 3F);
    }

    [Fact]
    public void Parse_CommaSeparator_IsAccepted()
    {
        Vec3 parsed = Vec3.Parse("1, 2, 3", CultureInfo.InvariantCulture);
        AssertComponents(parsed, 1F, 2F, 3F);
    }

    [Fact]
    public void Parse_SemicolonSeparator_CurrentBehavior()
    {
        Vec3.TryParse("1; 2; 3", CultureInfo.InvariantCulture, out _).Should().BeFalse();
        var act = () => Vec3.Parse("1; 2; 3", CultureInfo.InvariantCulture);
        act.Should().Throw<FormatException>();
    }
}
