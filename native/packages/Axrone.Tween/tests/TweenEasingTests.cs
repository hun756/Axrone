namespace Axrone.Tween.Tests;

public class TweenEasingTests
{
    [Fact]
    public void Factories_HitExactEndpoints()
    {
        Func<float, float>[] easings =
        [
            TweenEasings.ElasticIn(), TweenEasings.ElasticOut(), TweenEasings.ElasticInOut(),
            TweenEasings.BackIn(), TweenEasings.BackOut(), TweenEasings.BackInOut(),
            TweenEasings.ElasticOut(2.5f, 0.5f), TweenEasings.BackOut(3.0f),
        ];
        foreach (Func<float, float> easing in easings)
        {
            easing(0.0f).Should().Be(0.0f);
            easing(1.0f).Should().Be(1.0f);
        }
    }

    [Fact]
    public void BackDefaults_ReproduceBuiltIns()
    {
        Func<float, float> backIn = TweenEasings.BackIn();
        Func<float, float> backOut = TweenEasings.BackOut();
        Func<float, float> backInOut = TweenEasings.BackInOut();
        for (int i = 0; i <= 64; i++)
        {
            float t = i / 64.0f;
            backIn(t).Should().Be(EaseInBackCurve.Evaluate(t));
            backOut(t).Should().Be(EaseOutBackCurve.Evaluate(t));
            backInOut(t).Should().Be(EaseInOutBackCurve.Evaluate(t));
        }
    }

    [Fact]
    public void ElasticOut_OvershootsAndSettles()
    {
        Func<float, float> easing = TweenEasings.ElasticOut();
        float max = 0.0f;
        for (int i = 0; i <= 400; i++)
        {
            max = Math.Max(max, easing(i / 400.0f));
        }

        max.Should().BeGreaterThan(1.0f);
    }

    [Fact]
    public void Parameters_ChangeTheCurve()
    {
        float standard = TweenEasings.ElasticOut()(0.3f);
        float wide = TweenEasings.ElasticOut(1.0f, 0.6f)(0.3f);
        wide.Should().NotBeApproximately(standard, 1e-4f);

        float mild = TweenEasings.BackOut(0.5f)(0.7f);
        float wild = TweenEasings.BackOut(4.0f)(0.7f);
        wild.Should().NotBeApproximately(mild, 1e-4f);
    }

    [Fact]
    public void Factories_RejectBadArguments()
    {
        Action amplitude = () => TweenEasings.ElasticOut(0.0f, 0.3f);
        amplitude.Should().Throw<ArgumentOutOfRangeException>();
        Action period = () => TweenEasings.ElasticIn(1.0f, 0.0f);
        period.Should().Throw<ArgumentOutOfRangeException>();
        Action overshoot = () => TweenEasings.BackOut(-1.0f);
        overshoot.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ParametricEase_PlaysThroughBuilder()
    {
        var clock = new ManualTweenClock();
        using var engine = new TweenEngine(16, clock);
        var seen = new List<float>();
        TweenSpec spec = new TweenBuilder()
            .From(0.0f)
            .To(10.0f)
            .DurationSeconds(1.0f)
            .Ease(TweenEasings.BackOut(2.0f))
            .OnUpdate(seen.Add)
            .Build();
        engine.Play(spec);

        clock.Advance(DurationNs.FromSeconds(0.5f));
        engine.Update();
        clock.Advance(DurationNs.FromSeconds(0.5f));
        engine.Update();

        seen.Should().HaveCount(2);
        seen[^1].Should().Be(10.0f);
        seen[0].Should().BeGreaterThan(5.0f);
    }
}
