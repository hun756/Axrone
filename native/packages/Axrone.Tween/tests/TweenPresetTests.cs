namespace Axrone.Tween.Tests;

using System.Numerics;

public class TweenPresetTests
{
    private static TweenEngine Create(out ManualTweenClock clock)
    {
        clock = new ManualTweenClock();
        return new TweenEngine(16, clock);
    }

    [Fact]
    public void Punch_RestsAtBothEnds()
    {
        TweenSpec spec = TweenPresets.Punch(1.0f, 5.0f);
        spec.CustomEasing.Should().NotBeNull();
        spec.CustomEasing!(0.0f).Should().Be(0.0f);
        spec.CustomEasing(1.0f).Should().BeApproximately(0.0f, 1e-6f);
    }

    [Fact]
    public void Punch_NeverExceedsUnitMagnitude()
    {
        TweenSpec spec = TweenPresets.Punch(0.5f, 5.0f, vibrato: 7, elasticity: 0.5f);
        for (int i = 0; i <= 200; i++)
        {
            Math.Abs(spec.CustomEasing!(i / 200.0f)).Should().BeLessThanOrEqualTo(1.0f);
        }
    }

    [Fact]
    public void Punch_RejectsBadArguments()
    {
        Action zero = () => TweenPresets.Punch(0.0f, 1.0f);
        zero.Should().Throw<ArgumentOutOfRangeException>();
        Action vibrato = () => TweenPresets.Punch(1.0f, 1.0f, vibrato: 0);
        vibrato.Should().Throw<ArgumentOutOfRangeException>();
        Action elasticity = () => TweenPresets.Punch(1.0f, 1.0f, elasticity: -1.0f);
        elasticity.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Shake_SettlesToRestAndStaysBounded()
    {
        TweenSpec spec = TweenPresets.Shake(1.0f, 3.0f, vibrato: 12, seed: 42);
        spec.CustomEasing.Should().NotBeNull();
        spec.CustomEasing!(1.0f).Should().BeApproximately(0.0f, 1e-5f);
        for (int i = 0; i <= 200; i++)
        {
            Math.Abs(spec.CustomEasing(i / 200.0f)).Should().BeLessThanOrEqualTo(1.0f);
        }
    }

    [Fact]
    public void Shake_SameSeedReplaysIdentically()
    {
        TweenSpec first = TweenPresets.Shake(1.0f, 3.0f, seed: 7);
        TweenSpec second = TweenPresets.Shake(1.0f, 3.0f, seed: 7);
        TweenSpec other = TweenPresets.Shake(1.0f, 3.0f, seed: 8);
        for (int i = 0; i <= 64; i++)
        {
            float t = i / 64.0f;
            second.CustomEasing!(t).Should().Be(first.CustomEasing!(t));
        }

        bool differs = false;
        for (int i = 1; i < 64; i++)
        {
            if (other.CustomEasing!(i / 64.0f) != first.CustomEasing!(i / 64.0f))
            {
                differs = true;
                break;
            }
        }

        differs.Should().BeTrue();
    }

    [Fact]
    public void Punch_PlaysThroughEngineToCompletion()
    {
        using var engine = Create(out ManualTweenClock clock);
        var seen = new List<float>();
        bool done = false;
        TweenSpec spec = TweenPresets.Punch(1.0f, 4.0f, vibrato: 6) with
        {
            OnUpdateFloat = seen.Add,
            OnComplete = () => done = true,
        };
        engine.Play(spec);

        for (int i = 0; i < 20; i++)
        {
            clock.Advance(DurationNs.FromSeconds(0.05f));
            engine.Update();
        }

        done.Should().BeTrue();
        seen.Should().HaveCount(20);
        seen[^1].Should().BeApproximately(0.0f, 1e-5f);
        seen.Should().Contain(v => Math.Abs(v) > 0.5f);
    }

    [Fact]
    public void Directional_CoversVectorArities()
    {
        TweenPresets.Punch(1.0f, new Vector2(1.0f, 2.0f)).ChannelCount.Should().Be(2);
        TweenPresets.Punch(1.0f, new Vector3(1.0f, 2.0f, 3.0f)).ChannelCount.Should().Be(3);
        TweenPresets.Punch(1.0f, new Vector4(1.0f, 2.0f, 3.0f, 4.0f)).ChannelCount.Should().Be(4);
        TweenPresets.Shake(1.0f, new Vector2(1.0f, 2.0f)).ChannelCount.Should().Be(2);
        TweenPresets.Shake(1.0f, new Vector3(1.0f, 2.0f, 3.0f)).ChannelCount.Should().Be(3);
        TweenPresets.Shake(1.0f, new Vector4(1.0f, 2.0f, 3.0f, 4.0f)).ChannelCount.Should().Be(4);
    }
}
