namespace Axrone.Tween.Tests;

using System.Numerics;

public class TweenSpringTests
{
    private static TweenSpring Underdamped(byte channels = 1) =>
        new(new SpringConfig(Stiffness: 100.0f, Damping: 10.0f, Mass: 1.0f), channels);

    private static int Drain(TweenSpring spring, DurationNs step, int cap = 10000)
    {
        int ticks = 0;
        while (spring.Update(step) && ticks < cap)
        {
            ticks++;
        }

        return ticks;
    }

    [Fact]
    public void Underdamped_SettlesExactlyOnTarget()
    {
        var spring = Underdamped();
        int completions = 0;
        spring.OnComplete = () => completions++;
        spring.Reset(0.0f, 10.0f);

        Drain(spring, DurationNs.FromMilliseconds(16.666f)).Should().BeLessThan(10000);
        spring.Position.Should().Be(10.0f);
        spring.Velocity.Should().Be(0.0f);
        completions.Should().Be(1);
        spring.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Overdamped_NeverOvershoots()
    {
        var spring = new TweenSpring(new SpringConfig(Stiffness: 10.0f, Damping: 20.0f, Mass: 1.0f));
        float peak = 0.0f;
        spring.OnUpdateFloat = v => peak = Math.Max(peak, v);
        spring.Reset(0.0f, 10.0f);

        Drain(spring, DurationNs.FromMilliseconds(16.666f));
        peak.Should().BeLessThanOrEqualTo(10.0f);
        spring.Position.Should().Be(10.0f);
    }

    [Fact]
    public void Retarget_ChasesNewGoalAndRearms()
    {
        var spring = Underdamped();
        int completions = 0;
        spring.OnComplete = () => completions++;
        spring.Reset(0.0f, 5.0f);

        Drain(spring, DurationNs.FromMilliseconds(16.666f));
        completions.Should().Be(1);

        spring.Retarget(15.0f);
        spring.IsRunning.Should().BeTrue();
        Drain(spring, DurationNs.FromMilliseconds(16.666f));
        spring.Position.Should().Be(15.0f);
        completions.Should().Be(2);
    }

    [Fact]
    public void StallStep_StaysStableAndSettles()
    {
        var spring = new TweenSpring(new SpringConfig(Stiffness: 2000.0f, Damping: 20.0f, Mass: 1.0f));
        float extreme = 0.0f;
        spring.OnUpdateFloat = v => extreme = Math.Max(extreme, Math.Abs(v));
        spring.Reset(0.0f, 1.0f);

        spring.Update(DurationNs.FromSeconds(1.0f));
        extreme.Should().BeLessThan(1e6f);
        Drain(spring, DurationNs.FromMilliseconds(16.666f));
        spring.Position.Should().Be(1.0f);
    }

    [Fact]
    public void Undamped_KeepsOscillating()
    {
        var spring = new TweenSpring(new SpringConfig(Stiffness: 50.0f, Damping: 0.0f, Mass: 1.0f));
        spring.Reset(0.0f, 5.0f);

        for (int i = 0; i < 200; i++)
        {
            spring.Update(DurationNs.FromMilliseconds(16.666f)).Should().BeTrue();
        }
    }

    [Fact]
    public void SameDeltas_ReplayIdentically()
    {
        var first = Underdamped();
        var second = Underdamped();
        first.Reset(0.0f, 10.0f);
        second.Reset(0.0f, 10.0f);

        for (int i = 0; i < 60; i++)
        {
            var step = DurationNs.FromMilliseconds(16.666f + (i % 7));
            first.Update(step);
            second.Update(step);
            first.Position.Should().Be(second.Position);
        }
    }

    [Fact]
    public void PauseHolds_StopSuppressesCompletion()
    {
        var spring = Underdamped();
        int completions = 0;
        spring.OnComplete = () => completions++;
        spring.Reset(0.0f, 10.0f);

        spring.Update(DurationNs.FromMilliseconds(16.666f));
        spring.Pause();
        float held = spring.Position;
        spring.Update(DurationNs.FromSeconds(1.0f));
        spring.Position.Should().Be(held);
        spring.IsRunning.Should().BeFalse();

        spring.Resume();
        spring.IsRunning.Should().BeTrue();
        spring.Stop();
        spring.IsRunning.Should().BeFalse();
        spring.Update(DurationNs.FromSeconds(5.0f));
        completions.Should().Be(0);
    }

    [Fact]
    public void Vector3_ConvergesPerLane()
    {
        var spring = Underdamped(3);
        spring.Reset(Vector3.Zero, new Vector3(1.0f, 2.0f, 3.0f));
        Vector3 last = default;
        spring.OnUpdateVector3 = v => last = v;

        Drain(spring, DurationNs.FromMilliseconds(16.666f));
        last.Should().Be(new Vector3(1.0f, 2.0f, 3.0f));
    }

    [Fact]
    public void BadConfig_Throws()
    {
        Action stiffness = () => { _ = new TweenSpring(new SpringConfig(0.0f, 1.0f, 1.0f)); };
        stiffness.Should().Throw<ArgumentOutOfRangeException>();
        Action mass = () => { _ = new TweenSpring(new SpringConfig(1.0f, 1.0f, -1.0f)); };
        mass.Should().Throw<ArgumentOutOfRangeException>();
        Action damping = () => { _ = new TweenSpring(new SpringConfig(1.0f, -1.0f, 1.0f)); };
        damping.Should().Throw<ArgumentOutOfRangeException>();
        Action channels = () => { _ = new TweenSpring(new SpringConfig(1.0f, 1.0f, 1.0f), 5); };
        channels.Should().Throw<ArgumentOutOfRangeException>();
    }
}
