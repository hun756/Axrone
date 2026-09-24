namespace Axrone.Animation.Tests;

public class ClipTests
{
    private static AnimationChannel Translation(float[] times, float[] values) =>
        new(1, ChannelTarget.Translation, InterpolationMode.Linear, times, values);

    [Fact]
    public void Channel_RejectsStrideMismatch()
    {
        Action build = () => { _ = new AnimationChannel(0, ChannelTarget.Translation, InterpolationMode.Linear, [0.0f, 1.0f], [0.0f]); };
        build.Should().Throw<ValidationException>()
            .Where(e => e.Code == AnimationErrorCode.ValidationClipMismatch);
    }

    [Fact]
    public void LinearSampling_LerpsAndClamps()
    {
        var channel = Translation([0.0f, 1.0f], [0.0f, 0.0f, 0.0f, 10.0f, 0.0f, 0.0f]);
        Span<float> output = stackalloc float[3];

        channel.Sample(0.5f, output);
        output[0].Should().BeApproximately(5.0f, 1e-6f);

        channel.Sample(-1.0f, output);
        output[0].Should().Be(0.0f);

        channel.Sample(5.0f, output);
        output[0].Should().Be(10.0f);
    }

    [Fact]
    public void StepSampling_Holds()
    {
        var channel = new AnimationChannel(0, ChannelTarget.Translation, InterpolationMode.Step, [0.0f, 1.0f], [3.0f, 0.0f, 0.0f, 9.0f, 0.0f, 0.0f]);
        Span<float> output = stackalloc float[3];
        channel.Sample(0.9f, output);
        output[0].Should().Be(3.0f);
    }

    [Fact]
    public void RotationLinearSampling_Slerps()
    {
        var channel = new AnimationChannel(
            0, ChannelTarget.Rotation, InterpolationMode.Linear,
            [0.0f, 1.0f],
            [0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f]);
        Span<float> output = stackalloc float[4];
        channel.Sample(0.5f, output);
        float length = MathF.Sqrt((output[0] * output[0]) + (output[1] * output[1]) + (output[2] * output[2]) + (output[3] * output[3]));
        length.Should().BeApproximately(1.0f, 1e-5f);
        output[1].Should().BeApproximately(0.7071f, 1e-4f);
    }

    [Fact]
    public void CubicSampling_HermiteMidpoint()
    {
        var channel = new AnimationChannel(
            0, ChannelTarget.Translation, InterpolationMode.CubicSpline,
            [0.0f, 1.0f],
            [0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f]);
        Span<float> output = stackalloc float[3];
        channel.Sample(0.5f, output);
        output[0].Should().BeApproximately(0.5f, 1e-5f);
    }

    [Fact]
    public void Clip_SanitizesAndSamplesFrame()
    {
        var clip = new AnimationClip(
            new ClipId("walk"), 1.0f,
            [Translation([0.0f, 1.0f], [0.0f, 0.0f, 0.0f, 0.0f, 2.0f, 0.0f])],
            events:
            [
                new ClipEvent(0.5f, "step", ""),
                new ClipEvent(float.NaN, "bad", ""),
                new ClipEvent(0.2f, "  ", ""),
            ],
            tags: ["locomotion", " ", "locomotion"]);
        clip.Duration.Should().Be(1.0f);
        clip.Events.Length.Should().Be(1);
        clip.Tags.Should().BeEquivalentTo("locomotion");

        var frame = new AnimationFrame(2, new Dictionary<CurveId, int>());
        clip.Sample(0.5f, frame);
        frame.ReadTranslations()[1].Y.Should().BeApproximately(1.0f, 1e-6f);
        frame.ReadTranslations()[0].Should().Be(Vector3.Zero);
    }

    [Fact]
    public void Events_CollectAcrossWrap()
    {
        var clip = new AnimationClip(
            new ClipId("e"), 1.0f, [],
            events: [new ClipEvent(0.2f, "a", ""), new ClipEvent(0.8f, "b", "")]);
        var found = new List<ClipEvent>();
        clip.CollectEvents(0.7f, 0.3f, found);
        found.Should().HaveCount(2);
    }

    [Fact]
    public void FootContact_WeightRamps()
    {
        var contact = new FootContact(0.0f, 1.0f, 3);
        contact.EvaluateWeight(-0.1f).Should().Be(0.0f);
        contact.EvaluateWeight(0.5f).Should().Be(1.0f);
        contact.EvaluateWeight(0.05f).Should().BeInRange(0.25f, 1.0f);
    }
}
