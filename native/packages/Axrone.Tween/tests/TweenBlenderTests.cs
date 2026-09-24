namespace Axrone.Tween.Tests;

using System.Numerics;

public class TweenBlenderTests
{
    private static (TweenEngine Engine, ManualTweenClock Clock, TweenBlender Blender) Create()
    {
        var clock = new ManualTweenClock();
        var engine = new TweenEngine(16, clock);
        return (engine, clock, new TweenBlender(engine));
    }

    private static TweenSpec Linear(float to) =>
        new TweenBuilder().From(0.0f).To(to).DurationSeconds(1.0f).Build();

    [Fact]
    public void Blendables_SumOnOneTarget()
    {
        var (engine, clock, blender) = Create();
        using (engine)
        {
            float target = 10.0f;
            blender.PlayBlendable(Linear(4.0f), d => target += d);
            blender.PlayBlendable(Linear(6.0f), d => target += d);

            clock.Advance(DurationNs.FromSeconds(0.5f));
            engine.Update();
            target.Should().BeApproximately(15.0f, 1e-5f);

            clock.Advance(DurationNs.FromSeconds(0.5f));
            engine.Update();
            target.Should().BeApproximately(20.0f, 1e-5f);
            engine.ActiveCount.Should().Be(0);
        }
    }

    [Fact]
    public void OriginalUpdate_StillSeesAbsolutes()
    {
        var (engine, clock, blender) = Create();
        using (engine)
        {
            var absolutes = new List<float>();
            var deltas = new List<float>();
            TweenSpec spec = Linear(8.0f) with { OnUpdateFloat = absolutes.Add };
            blender.PlayBlendable(spec, deltas.Add);

            clock.Advance(DurationNs.FromSeconds(1.0f));
            engine.Update();

            absolutes.Should().Equal(8.0f);
            deltas.Should().Equal(8.0f);
        }
    }

    [Fact]
    public void Kill_KeepsAppliedPortionAndStops()
    {
        var (engine, clock, blender) = Create();
        using (engine)
        {
            float target = 0.0f;
            int calls = 0;
            TweenHandle handle = blender.PlayBlendable(
                Linear(10.0f),
                d => { target += d; calls++; });

            clock.Advance(DurationNs.FromSeconds(0.5f));
            engine.Update();
            handle.Cancel();
            clock.Advance(DurationNs.FromSeconds(0.5f));
            engine.Update();

            target.Should().BeApproximately(5.0f, 1e-5f);
            calls.Should().Be(1);
            engine.ActiveCount.Should().Be(0);
        }
    }

    [Fact]
    public void Restart_ResetsBaselineWithoutSpike()
    {
        var (engine, clock, blender) = Create();
        using (engine)
        {
            float target = 10.0f;
            TweenHandle handle = blender.PlayBlendable(Linear(10.0f), d => target += d);

            clock.Advance(DurationNs.FromSeconds(0.5f));
            engine.Update();
            blender.Restart(handle).Should().BeTrue();

            clock.Advance(DurationNs.FromSeconds(0.5f));
            engine.Update();
            clock.Advance(DurationNs.FromSeconds(0.5f));
            engine.Update();

            target.Should().BeApproximately(25.0f, 1e-4f);
        }
    }

    [Fact]
    public void VectorBlendable_DeliversLaneDeltas()
    {
        var (engine, clock, blender) = Create();
        using (engine)
        {
            Vector2 target = new(1.0f, 1.0f);
            TweenSpec spec = new TweenBuilder()
                .From(Vector2.Zero).To(new Vector2(3.0f, 5.0f)).DurationSeconds(1.0f).Build();
            blender.PlayBlendable(spec, d => target += d);

            clock.Advance(DurationNs.FromSeconds(1.0f));
            engine.Update();

            target.X.Should().BeApproximately(4.0f, 1e-5f);
            target.Y.Should().BeApproximately(6.0f, 1e-5f);
        }
    }

    [Fact]
    public void ArityMismatch_Throws()
    {
        var (engine, _, blender) = Create();
        using (engine)
        {
            Action mismatch = () => blender.PlayBlendable(Linear(1.0f), (Vector2 _) => { });
            mismatch.Should().Throw<ArgumentOutOfRangeException>();
            blender.Restart(default).Should().BeFalse();
        }
    }
}
