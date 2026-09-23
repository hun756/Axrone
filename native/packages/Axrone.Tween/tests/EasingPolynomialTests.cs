namespace Axrone.Tween.Tests;

public class EasingPolynomialTests
{
    public static IEnumerable<object[]> AllPolynomialKinds()
    {
        yield return new object[] { EasingKind.Linear };
        yield return new object[] { EasingKind.EaseInQuad };
        yield return new object[] { EasingKind.EaseOutQuad };
        yield return new object[] { EasingKind.EaseInOutQuad };
        yield return new object[] { EasingKind.EaseInCubic };
        yield return new object[] { EasingKind.EaseOutCubic };
        yield return new object[] { EasingKind.EaseInOutCubic };
        yield return new object[] { EasingKind.EaseInQuart };
        yield return new object[] { EasingKind.EaseOutQuart };
        yield return new object[] { EasingKind.EaseInOutQuart };
        yield return new object[] { EasingKind.EaseInQuint };
        yield return new object[] { EasingKind.EaseOutQuint };
        yield return new object[] { EasingKind.EaseInOutQuint };
    }

    [Theory]
    [MemberData(nameof(AllPolynomialKinds))]
    public void Boundaries_PinEndpoints(EasingKind kind)
    {
        EasingEvaluator.Evaluate(kind, 0f).Should().BeApproximately(0f, 1e-6f);
        EasingEvaluator.Evaluate(kind, 1f).Should().BeApproximately(1f, 1e-6f);
    }

    [Theory]
    [MemberData(nameof(AllPolynomialKinds))]
    public void Curves_AreMonotonic(EasingKind kind)
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
    [InlineData(EasingKind.EaseInOutQuad)]
    [InlineData(EasingKind.EaseInOutCubic)]
    [InlineData(EasingKind.EaseInOutQuart)]
    [InlineData(EasingKind.EaseInOutQuint)]
    public void InOutCurves_AreSymmetric(EasingKind kind)
    {
        for (int i = 0; i <= 100; i++)
        {
            float t = i / 100f;
            (EasingEvaluator.Evaluate(kind, t) + EasingEvaluator.Evaluate(kind, 1f - t))
                .Should().BeApproximately(1f, 1e-5f);
        }
    }

    [Fact]
    public void Midpoint_SpotValues()
    {
        EasingEvaluator.Evaluate(EasingKind.Linear, 0.5f).Should().BeApproximately(0.5f, 1e-6f);
        EasingEvaluator.Evaluate(EasingKind.EaseInQuad, 0.5f).Should().BeApproximately(0.25f, 1e-6f);
        EasingEvaluator.Evaluate(EasingKind.EaseOutQuad, 0.5f).Should().BeApproximately(0.75f, 1e-6f);
        EasingEvaluator.Evaluate(EasingKind.EaseInOutQuad, 0.5f).Should().BeApproximately(0.5f, 1e-6f);
    }

    [Fact]
    public void OutOfRange_Clamps()
    {
        EasingEvaluator.Evaluate(EasingKind.EaseInCubic, -2f).Should().BeApproximately(0f, 1e-6f);
        EasingEvaluator.Evaluate(EasingKind.EaseInCubic, 5f).Should().BeApproximately(1f, 1e-6f);
    }
}
