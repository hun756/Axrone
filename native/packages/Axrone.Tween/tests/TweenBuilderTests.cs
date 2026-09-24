namespace Axrone.Tween.Tests;

using System.Numerics;
using System.Runtime.Intrinsics;
using Axrone.Utility.Builders;

public class TweenBuilderTests
{
    [Fact]
    public void HappyPath_BuildsSpec()
    {
        var spec = new TweenBuilder()
            .From(0f)
            .To(10f)
            .DurationSeconds(1f)
            .Ease(EasingKind.EaseOutQuad)
            .Build();

        spec.ChannelCount.Should().Be(1);
        spec.Start.GetElement(0).Should().BeApproximately(0f, 1e-6f);
        spec.End.GetElement(0).Should().BeApproximately(10f, 1e-6f);
        spec.Duration.Should().Be(DurationNs.FromSeconds(1f));
        spec.Easing.Should().Be(EasingKind.EaseOutQuad);
        spec.Mode.Should().Be(PlaybackMode.Once);
        spec.LoopCount.Should().Be(1);
        spec.TimeScale.Should().BeApproximately(1f, 1e-6f);
    }

    [Fact]
    public void Vector3_SetsThreeLanes()
    {
        var spec = new TweenBuilder()
            .From(new Vector3(1f, 2f, 3f))
            .By(new Vector3(1f, 1f, 1f))
            .Build();

        spec.ChannelCount.Should().Be(3);
        spec.End.GetElement(0).Should().BeApproximately(2f, 1e-6f);
        spec.End.GetElement(2).Should().BeApproximately(4f, 1e-6f);
    }

    [Fact]
    public void ArityMismatch_FailsValidation()
    {
        var builder = new TweenBuilder()
            .From(0f)
            .To(new Vector2(1f, 2f));

        builder.TryBuild(out _, out BuilderDiagnostic diagnostic).Should().BeFalse();
        diagnostic.Code.Should().Be(BuilderStatusCode.ValidationFailed);
        diagnostic.Message.Should().Contain("must match");
    }

    [Fact]
    public void MissingTo_ReportsMissingField()
    {
        var builder = new TweenBuilder().From(0f);

        builder.TryBuild(out _, out BuilderDiagnostic diagnostic).Should().BeFalse();
        diagnostic.Code.Should().Be(BuilderStatusCode.MissingRequiredField);
    }

    [Fact]
    public void CustomWithoutFunction_FailsValidation()
    {
        var builder = new TweenBuilder()
            .From(0f)
            .To(1f)
            .Ease(EasingKind.Custom);

        builder.TryBuild(out _, out BuilderDiagnostic diagnostic).Should().BeFalse();
        diagnostic.Message.Should().Contain("Custom easing requires");
    }

    [Fact]
    public void NonPositiveTimeScale_FailsValidation()
    {
        var builder = new TweenBuilder()
            .From(0f)
            .To(1f)
            .TimeScale(0f);

        builder.TryBuild(out _, out BuilderDiagnostic diagnostic).Should().BeFalse();
        diagnostic.Message.Should().Contain("TimeScale");
    }

    [Fact]
    public void CustomEasing_BuildsWithFunction()
    {
        var spec = new TweenBuilder()
            .From(0f)
            .To(1f)
            .Ease(t => t * t)
            .Build();

        spec.Easing.Should().Be(EasingKind.Custom);
        spec.CustomEasing.Should().NotBeNull();
        spec.CustomEasing!(0.5f).Should().BeApproximately(0.25f, 1e-6f);
    }

    [Fact]
    public void Fork_DivergesIndependently()
    {
        var original = new TweenBuilder().From(0f).To(5f);
        var fork = original.Fork().To(10f);

        original.Build().End.GetElement(0).Should().BeApproximately(5f, 1e-6f);
        fork.Build().End.GetElement(0).Should().BeApproximately(10f, 1e-6f);
    }

    [Fact]
    public void Reset_ClearsToDefaults()
    {
        var builder = new TweenBuilder().From(0f).To(1f).TimeScale(2f);
        builder.Reset();

        builder.TryBuild(out _, out BuilderDiagnostic diagnostic).Should().BeFalse();
        diagnostic.Code.Should().Be(BuilderStatusCode.MissingRequiredField);
    }
}
