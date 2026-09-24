namespace Axrone.Tween.Tests;

public class TweenTimingTests
{
    private static TweenEngine Create(out ManualTweenClock clock)
    {
        clock = new ManualTweenClock();
        return new TweenEngine(16, clock);
    }

    [Fact]
    public void RepeatDelay_InsertsGapBetweenPlaythroughs()
    {
        using var engine = Create(out ManualTweenClock clock);
        var seen = new List<float>();
        int steps = 0;
        TweenSpec spec = new TweenBuilder()
            .From(0.0f)
            .To(1.0f)
            .DurationSeconds(1.0f)
            .Loops(2)
            .RepeatDelaySeconds(0.5f)
            .OnUpdate(seen.Add)
            .OnStepComplete(() => steps++)
            .Build();
        _ = engine.Play(spec);

        clock.Advance(DurationNs.FromSeconds(1.0f));
        engine.Update();
        seen.Should().Equal(1.0f);
        steps.Should().Be(1);

        clock.Advance(DurationNs.FromSeconds(0.25f));
        engine.Update();
        seen.Should().HaveCount(1);

        clock.Advance(DurationNs.FromSeconds(0.25f));
        engine.Update();
        seen.Should().Equal(1.0f, 0.0f);

        clock.Advance(DurationNs.FromSeconds(1.0f));
        engine.Update();
        seen.Should().Equal(1.0f, 0.0f, 1.0f);
        steps.Should().Be(2);
        engine.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void RepeatDelay_NegativeFailsValidation()
    {
        Action negative = () => new TweenBuilder()
            .From(0.0f)
            .To(1.0f)
            .DurationSeconds(1.0f)
            .RepeatDelay(DurationNs.FromMilliseconds(1.0f).Subtract(DurationNs.FromMilliseconds(2.0f)))
            .Build();
        negative.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MaxDelta_ClampsStallJump()
    {
        using var engine = Create(out ManualTweenClock clock);
        engine.MaxDelta = DurationNs.FromSeconds(0.1f);
        var seen = new List<float>();
        _ = engine.Play(new TweenBuilder().From(0.0f).To(1.0f).DurationSeconds(1.0f).OnUpdate(seen.Add).Build());

        engine.Update(DurationNs.FromSeconds(5.0f));
        seen.Should().Equal(0.1f);
        engine.ActiveCount.Should().Be(1);

        engine.MaxDelta = null;
        engine.Update(DurationNs.FromSeconds(0.9f));
        seen.Should().Equal(0.1f, 1.0f);
        engine.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void MaxDelta_NullAdvancesFully()
    {
        using var engine = Create(out _);
        engine.MaxDelta.Should().BeNull();
        var seen = new List<float>();
        _ = engine.Play(new TweenBuilder().From(0.0f).To(1.0f).DurationSeconds(1.0f).OnUpdate(seen.Add).Build());

        engine.Update(DurationNs.FromSeconds(5.0f));
        seen.Should().Equal(1.0f);
        engine.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void TotalDuration_CoversDelayLoopsAndGaps()
    {
        TweenSpec spec = new TweenBuilder()
            .From(0.0f)
            .To(1.0f)
            .DurationSeconds(1.0f)
            .DelaySeconds(0.5f)
            .Loops(3)
            .RepeatDelaySeconds(0.25f)
            .Build();
        spec.TotalDuration.Should().Be(DurationNs.FromSeconds(4.0f));
    }

    [Fact]
    public void TotalDuration_NullForInfiniteLoops()
    {
        TweenSpec infinite = new TweenBuilder()
            .From(0.0f)
            .To(1.0f)
            .DurationSeconds(1.0f)
            .Loops(-1)
            .Build();
        infinite.TotalDuration.Should().BeNull();

        TweenSpec single = new TweenBuilder()
            .From(0.0f)
            .To(1.0f)
            .DurationSeconds(1.0f)
            .DelaySeconds(0.5f)
            .Build();
        single.TotalDuration.Should().Be(DurationNs.FromSeconds(1.5f));
    }
}
