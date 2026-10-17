namespace Axrone.GameLoop.Tests;

public class GameLoopNominalTests
{
    [Fact]
    public void FrameIndex_Advances_AndCompares()
    {
        FrameIndex.Zero.Next().Should().Be(new FrameIndex(1UL));
        (FrameIndex.Zero + 5UL).Should().Be(new FrameIndex(5UL));
        (new FrameIndex(2UL) > new FrameIndex(1UL)).Should().BeTrue();
    }

    [Fact]
    public void GameTime_Arithmetic_Works()
    {
        GameTime.FromSeconds(1.0).Should().Be(GameTime.FromMilliseconds(1000.0));
        (GameTime.FromMilliseconds(300.0) - GameTime.FromMilliseconds(100.0)).Should().Be(GameTime.FromMilliseconds(200.0));
        (GameTime.FromMilliseconds(100.0) * 2.0).Should().Be(GameTime.FromMilliseconds(200.0));
        (GameTime.FromMilliseconds(300.0) / GameTime.FromMilliseconds(100.0)).Should().Be(3.0);
        GameTime.Zero.ToString().Should().Be("0.000 ms");
    }

    [Fact]
    public void FixedStep_Rejects_NonPositive()
    {
        Action zero = () => _ = new FixedStep(0.0);
        Action negative = () => _ = new FixedStep(-16.0);
        Action nan = () => _ = new FixedStep(double.NaN);
        zero.Should().Throw<GameLoopConfigurationException>().WithMessage("*Fixed step*");
        negative.Should().Throw<GameLoopConfigurationException>();
        nan.Should().Throw<GameLoopConfigurationException>();
        FixedStep.SixtyFps.TotalMilliseconds.Should().BeApproximately(16.666, 0.001);
    }

    [Fact]
    public void TimeScale_Rejects_Negative()
    {
        Action negative = () => _ = new TimeScale(-1.0);
        negative.Should().Throw<GameLoopConfigurationException>();
        TimeScale.Identity.Value.Should().Be(1.0);
        TimeScale.Paused.Value.Should().Be(0.0);
        ((double)TimeScale.Identity).Should().Be(1.0);
    }

    [Fact]
    public void InterpolationAlpha_Clamps()
    {
        new InterpolationAlpha(-1.0).Value.Should().Be(0.0);
        new InterpolationAlpha(2.0).Value.Should().Be(1.0);
        ((double)InterpolationAlpha.One).Should().Be(1.0);
    }

    [Fact]
    public void SystemId_Hashes_Names()
    {
        SystemId.FromString("render").Should().Be(SystemId.FromSpan("render".AsSpan()));
        SystemId.FromString("render").Should().NotBe(SystemId.FromString("physics"));
        SystemId.FromString("x").ToString().Should().StartWith("0x");
    }

    [Fact]
    public void SystemPriority_Orders_ByValue()
    {
        (SystemPriority.Critical > SystemPriority.Default).Should().BeTrue();
        (SystemPriority.Low < SystemPriority.Default).Should().BeTrue();
        SystemPriority.Critical.CompareTo(SystemPriority.Default).Should().BeGreaterThan(0);
    }

    [Fact]
    public void SnapshotVersion_Increments()
    {
        SnapshotVersion.Initial.Next().Should().Be(new SnapshotVersion(2U));
    }
}
