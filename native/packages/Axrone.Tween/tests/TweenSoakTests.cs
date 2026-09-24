namespace Axrone.Tween.Tests;

public class TweenSoakTests
{
    [Fact]
    public void Churn_AccountsEveryTween()
    {
        var clock = new ManualTweenClock();
        using var engine = new TweenEngine(256, clock);
        var live = new List<TweenHandle>();
        int submitted = 0;
        int completed = 0;
        int canceled = 0;

        for (int round = 0; round < 2000; round++)
        {
            for (int k = 0; k < 3; k++)
            {
                float seconds = 0.016f * ((round + k) % 5 + 1);
                int step = 0;
                TweenSpec spec = new TweenBuilder()
                    .From(0.0f)
                    .To(1.0f)
                    .DurationSeconds(seconds)
                    .OnUpdate((float _) => step++)
                    .OnComplete(() => completed++)
                    .Build();
                if (engine.TryPlay(spec, out TweenHandle handle))
                {
                    submitted++;
                    live.Add(handle);
                }
            }

            if (round % 7 == 3 && live.Count > 0)
            {
                TweenHandle victim = live[^1];
                live.RemoveAt(live.Count - 1);
                if (victim.Cancel())
                {
                    canceled++;
                }
            }

            clock.Advance(DurationNs.FromMilliseconds(16.666f));
            engine.Update();

            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (!live[i].IsValid)
                {
                    live.RemoveAt(i);
                }
            }
        }

        foreach (TweenHandle handle in live)
        {
            if (handle.Cancel())
            {
                canceled++;
            }
        }

        engine.ActiveCount.Should().Be(0);
        (completed + canceled).Should().Be(submitted);

        TweenHandle after = engine.Play(
            new TweenBuilder().From(0.0f).To(1.0f).DurationSeconds(0.5f).Build());
        after.IsValid.Should().BeTrue();
        clock.Advance(DurationNs.FromSeconds(1.0f));
        engine.Update();
        engine.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void InfiniteLoops_SurviveLongRuns()
    {
        var clock = new ManualTweenClock();
        using var engine = new TweenEngine(64, clock);
        int ticks = 0;
        for (int i = 0; i < 16; i++)
        {
            engine.Play(
                new TweenBuilder().From(0.0f).To(1.0f).DurationSeconds(0.1f)
                    .Mode(PlaybackMode.Loop).Loops(-1)
                    .OnUpdate((float _) => ticks++)
                    .Build());
        }

        for (int i = 0; i < 5000; i++)
        {
            clock.Advance(DurationNs.FromMilliseconds(16.666f));
            engine.Update();
        }

        engine.ActiveCount.Should().Be(16);
        ticks.Should().Be(16 * 5000);
    }
}
