namespace Axrone.Tween.Tests;

public class EasingTranscendentTests
{
    public static IEnumerable<object[]> TranscendentKinds()
    {
        yield return new object[] { EasingKind.EaseInExpo };
        yield return new object[] { EasingKind.EaseOutExpo };
        yield return new object[] { EasingKind.EaseInOutExpo };
        yield return new object[] { EasingKind.EaseInCirc };
        yield return new object[] { EasingKind.EaseOutCirc };
        yield return new object[] { EasingKind.EaseInOutCirc };
        yield return new object[] { EasingKind.EaseInElastic };
        yield return new object[] { EasingKind.EaseOutElastic };
        yield return new object[] { EasingKind.EaseInOutElastic };
        yield return new object[] { EasingKind.EaseInBack };
        yield return new object[] { EasingKind.EaseOutBack };
        yield return new object[] { EasingKind.EaseInOutBack };
        yield return new object[] { EasingKind.EaseInBounce };
        yield return new object[] { EasingKind.EaseOutBounce };
        yield return new object[] { EasingKind.EaseInOutBounce };
    }

    [Theory]
    [MemberData(nameof(TranscendentKinds))]
    public void Boundaries_PinEndpoints(EasingKind kind)
    {
        EasingEvaluator.Evaluate(kind, 0f).Should().BeApproximately(0f, 1e-5f);
        EasingEvaluator.Evaluate(kind, 1f).Should().BeApproximately(1f, 1e-5f);
    }

    [Theory]
    [InlineData(EasingKind.EaseInExpo)]
    [InlineData(EasingKind.EaseOutExpo)]
    [InlineData(EasingKind.EaseInOutExpo)]
    [InlineData(EasingKind.EaseInCirc)]
    [InlineData(EasingKind.EaseOutCirc)]
    [InlineData(EasingKind.EaseInOutCirc)]
    public void SmoothCurves_AreMonotonic(EasingKind kind)
    {
        float previous = 0f;
        for (int i = 1; i <= 100; i++)
        {
            float current = EasingEvaluator.Evaluate(kind, i / 100f);
            current.Should().BeGreaterThanOrEqualTo(previous);
            previous = current;
        }
    }

    [Theory]
    [InlineData(EasingKind.EaseInOutExpo)]
    [InlineData(EasingKind.EaseInOutCirc)]
    public void InOutCurves_AreSymmetric(EasingKind kind)
    {
        for (int i = 0; i <= 100; i++)
        {
            float t = i / 100f;
            (EasingEvaluator.Evaluate(kind, t) + EasingEvaluator.Evaluate(kind, 1f - t))
                .Should().BeApproximately(1f, 1e-4f);
        }
    }

    [Fact]
    public void Midpoint_SpotValues()
    {
        EasingEvaluator.Evaluate(EasingKind.EaseInExpo, 0.5f).Should().BeApproximately(0.03125f, 1e-5f);
        EasingEvaluator.Evaluate(EasingKind.EaseOutExpo, 0.5f).Should().BeApproximately(0.96875f, 1e-5f);
        EasingEvaluator.Evaluate(EasingKind.EaseInCirc, 0.5f).Should().BeApproximately(0.13397f, 1e-4f);
        EasingEvaluator.Evaluate(EasingKind.EaseOutBounce, 0.5f).Should().BeApproximately(0.76563f, 1e-4f);
    }

    [Fact]
    public void ElasticBack_OvershootByDesign()
    {
        float max = 0f;
        for (int i = 0; i <= 100; i++)
        {
            max = Math.Max(max, EasingEvaluator.Evaluate(EasingKind.EaseOutElastic, i / 100f));
        }

        max.Should().BeGreaterThan(1f);

        float min = 1f;
        for (int i = 0; i <= 100; i++)
        {
            min = Math.Min(min, EasingEvaluator.Evaluate(EasingKind.EaseInBack, i / 100f));
        }

        min.Should().BeLessThan(0f);
    }
}
