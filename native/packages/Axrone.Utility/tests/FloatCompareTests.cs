using Axrone.Utility.Comparer;

namespace Axrone.Utility.Tests.Comparer;

public class FloatCompareTests
{
    [Theory]
    [InlineData(1.0f, 1.0f, true)]
    [InlineData(1.0f, 1.0000001f, true)]
    [InlineData(1.0f, 2.0f, false)]
    [InlineData(0.0f, 0.0000001f, true)]
    public void AlmostEqual_Float(float a, float b, bool expected)
    {
        FloatCompare.AlmostEqual(a, b).Should().Be(expected);
    }

    [Theory]
    [InlineData(1.0, 1.0, true)]
    [InlineData(1.0, 1.0000000001, true)]
    [InlineData(1.0, 2.0, false)]
    public void AlmostEqual_Double(double a, double b, bool expected)
    {
        FloatCompare.AlmostEqual(a, b).Should().Be(expected);
    }

    [Theory]
    [InlineData(0.0f, true)]
    [InlineData(1e-6f, true)]
    [InlineData(1.0f, false)]
    public void IsZero_Float(float value, bool expected)
    {
        FloatCompare.IsZero(value).Should().Be(expected);
    }

    [Fact]
    public void Compare_EqualValues_ReturnsZero()
    {
        FloatCompare.Compare(1.0f, 1.0f).Should().Be(0);
    }

    [Fact]
    public void Compare_LessThan_ReturnsNegative()
    {
        FloatCompare.Compare(1.0f, 2.0f).Should().Be(-1);
    }

    [Fact]
    public void Compare_GreaterThan_ReturnsPositive()
    {
        FloatCompare.Compare(2.0f, 1.0f).Should().Be(1);
    }

    [Fact]
    public void AlmostEqualRelative_HandlesNearZero()
    {
        FloatCompare.AlmostEqualRelative(0.0f, 1e-8f).Should().BeTrue();
        FloatCompare.AlmostEqualRelative(1e10f, 1e10f + 1.0f).Should().BeTrue();
    }
}
