namespace Axrone.Tween.Tests;

public class TweenTimeTests
{
    [Fact]
    public void Timestamp_ConversionsRoundTrip()
    {
        TimestampNs.FromSeconds(1.5).ToSeconds().Should().BeApproximately(1.5, 1e-9);
        TimestampNs.FromMilliseconds(250).ToSeconds().Should().BeApproximately(0.25, 1e-9);
    }

    [Fact]
    public void Timestamp_RejectsInvalid()
    {
        var nan = () => TimestampNs.FromSeconds(double.NaN);
        nan.Should().Throw<ArgumentOutOfRangeException>();

        var negative = () => TimestampNs.FromMilliseconds(-1);
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Timestamp_Arithmetic()
    {
        var start = TimestampNs.FromSeconds(10);
        var end = start + DurationNs.FromSeconds(2.5f);

        (end - start).Should().Be(DurationNs.FromSeconds(2.5f));
        end.CompareTo(start).Should().BePositive();
    }

    [Fact]
    public void Duration_ConversionsRoundTrip()
    {
        DurationNs.FromSeconds(2f).ToSeconds().Should().BeApproximately(2f, 1e-6f);
        DurationNs.FromMilliseconds(500f).ToSeconds().Should().BeApproximately(0.5f, 1e-6f);
        DurationNs.Zero.Value.Should().Be(0);
    }

    [Fact]
    public void Duration_RejectsInvalid()
    {
        var nan = () => DurationNs.FromSeconds(float.NaN);
        nan.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TweenId_PacksIndexAndGeneration()
    {
        var id = new TweenId(7, 3);

        id.Index.Should().Be(7u);
        id.Generation.Should().Be(3u);
        id.IsValid.Should().BeTrue();
        id.RawValue.Should().Be(((ulong)3 << 32) | 7ul);
        TweenId.Invalid.IsValid.Should().BeFalse();
        id.ToString().Should().Be("TweenId(7:3)");
    }

    [Fact]
    public void TweenId_EqualityIsStructural()
    {
        new TweenId(1, 1).Should().Be(new TweenId(1, 1));
        new TweenId(1, 1).Should().NotBe(new TweenId(1, 2));
        new TweenId(1, 1).Should().NotBe(new TweenId(2, 1));
    }
}
