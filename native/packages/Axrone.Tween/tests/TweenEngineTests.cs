namespace Axrone.Tween.Tests;

public class TweenEngineTests
{
    private static TweenEngine Create(out ManualTweenClock clock)
    {
        clock = new ManualTweenClock();
        return new TweenEngine(16, clock);
    }

    private static TweenSpec Linear(float from, float to, float seconds, Action<float>? onUpdate = null) =>
        new TweenBuilder()
            .From(from)
            .To(to)
            .DurationSeconds(seconds)
            .OnUpdate(onUpdate ?? (_ => { }))
            .OnComplete(() => { })
            .Build();

    [Fact]
    public void PlayTick_CompletesThroughManualClock()
    {
        using var engine = Create(out ManualTweenClock clock);
        var seen = new List<float>();
        engine.Play(Linear(0f, 10f, 1f, v => seen.Add(v)));

        clock.Advance(DurationNs.FromSeconds(0.5f));
        engine.Update();
        clock.Advance(DurationNs.FromSeconds(0.5f));
        engine.Update();

        seen.Should().Equal(5f, 10f);
        engine.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void TryPlay_Full_ReturnsFalse()
    {
        using var engine = new TweenEngine(1);
        engine.TryPlay(Linear(0f, 1f, 10f), out _).Should().BeTrue();
        engine.TryPlay(Linear(0f, 1f, 10f), out TweenHandle handle).Should().BeFalse();
        handle.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Handle_RoutesLifecycle()
    {
        using var engine = Create(out _);
        var handle = engine.Play(Linear(0f, 10f, 10f));

        handle.IsValid.Should().BeTrue();
        handle.State.Should().Be(TweenState.Playing);
        handle.Pause().Should().BeTrue();
        handle.State.Should().Be(TweenState.Paused);
        handle.Resume().Should().BeTrue();
        handle.Cancel().Should().BeTrue();
        handle.State.Should().Be(TweenState.Inactive);
        handle.Cancel().Should().BeFalse();
    }

    [Fact]
    public void Complete_StopsScheduling()
    {
        using var engine = Create(out _);
        engine.Complete();

        engine.TryPlay(Linear(0f, 1f, 1f), out _).Should().BeFalse();
    }

    [Fact]
    public void Fault_Propagates()
    {
        using var engine = Create(out _);
        engine.Complete(new InvalidOperationException("engine fault"));

        var act = () => engine.Update();

        act.Should().Throw<InvalidOperationException>().WithMessage("engine fault");
    }

    [Fact]
    public void Restart_ReplaysFromZero()
    {
        using var engine = Create(out ManualTweenClock clock);
        var seen = new List<float>();
        var handle = engine.Play(Linear(0f, 10f, 1f, v => seen.Add(v)));

        clock.Advance(DurationNs.FromSeconds(1f));
        engine.Update();
        seen.Should().Equal(10f);

        handle.Restart().Should().BeFalse();

        var replay = engine.Play(Linear(0f, 10f, 1f, v => seen.Add(v)));
        clock.Advance(DurationNs.FromSeconds(0.5f));
        engine.Update();
        replay.Restart().Should().BeTrue();
        clock.Advance(DurationNs.FromSeconds(0.5f));
        engine.Update();
        seen.Should().Equal(10f, 5f, 5f);
    }

    [Fact]
    public void Goto_JumpsPlayhead()
    {
        using var engine = Create(out ManualTweenClock clock);
        var seen = new List<float>();
        var handle = engine.Play(Linear(0f, 10f, 1f, v => seen.Add(v)));

        handle.Goto(DurationNs.FromSeconds(0.75f)).Should().BeTrue();
        clock.Advance(DurationNs.FromSeconds(0.016f));
        engine.Update();

        seen.Should().ContainSingle().Which.Should().BeApproximately(7.66f, 1e-3f);
    }

    [Fact]
    public void Controls_RejectStaleIdentities()
    {
        using var engine = Create(out ManualTweenClock clock);
        var handle = engine.Play(Linear(0f, 10f, 1f));
        clock.Advance(DurationNs.FromSeconds(1f));
        engine.Update();

        handle.Restart().Should().BeFalse();
        handle.Goto(DurationNs.Zero).Should().BeFalse();
        handle.Rewind().Should().BeFalse();
    }
}
