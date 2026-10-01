namespace Axrone.Animation.Tests;

public class TrackDescriptorTests
{
    private static AnimationChannel BoneChannel(float[] times, float[] values) =>
        new(0, ChannelTarget.Translation, InterpolationMode.Linear, times, values);

    private static AnimationChannel CurveChannel(float[] times, float[] values, string curve) =>
        new(-1, ChannelTarget.Curve, InterpolationMode.Linear, times, values, new CurveId(curve));

    [Fact]
    public void Validate_AcceptsChannelProjectedDescriptor()
    {
        TrackDescriptor descriptor = TrackDescriptor.FromChannel(
            BoneChannel([0.0f, 0.5f, 2.0f], [0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f]));

        descriptor.KeyCount.Should().Be(3);
        descriptor.StartTime.Should().Be(0.0f);
        descriptor.EndTime.Should().Be(2.0f);
        descriptor.Duration.Should().Be(2.0f);
        descriptor.IsEmpty.Should().BeFalse();

        Action validate = () => TrackDescriptor.Validate(in descriptor);
        validate.Should().NotThrow();
    }

    [Fact]
    public void Validate_AcceptsCurveTrackProjection()
    {
        TrackDescriptor descriptor = TrackDescriptor.FromChannel(
            CurveChannel([0.25f, 1.75f], [0.0f, 1.0f], "grip"));

        descriptor.IsCurveTrack.Should().BeTrue();
        descriptor.CurveId.Should().Be(new CurveId("grip"));
        descriptor.BoneIndex.Should().Be(-1);

        Action validate = () => TrackDescriptor.Validate(in descriptor);
        validate.Should().NotThrow();
    }

    [Fact]
    public void Validate_AcceptsEmptyProjection()
    {
        TrackDescriptor descriptor = TrackDescriptor.FromChannel(BoneChannel([], []));

        descriptor.IsEmpty.Should().BeTrue();
        descriptor.KeyCount.Should().Be(0);
        descriptor.StartTime.Should().Be(0.0f);
        descriptor.EndTime.Should().Be(0.0f);
        descriptor.Duration.Should().Be(0.0f);

        Action validate = () => TrackDescriptor.Validate(in descriptor);
        validate.Should().NotThrow();
    }

    [Fact]
    public void Validate_RejectsNegativeKeyCount()
    {
        TrackDescriptor descriptor = new TrackDescriptor { KeyCount = -1 };

        Action validate = () => TrackDescriptor.Validate(in descriptor);
        validate.Should().Throw<ValidationException>()
            .Where(e => e.Code == AnimationErrorCode.ValidationClipDegenerateData);
    }

    [Fact]
    public void Validate_RejectsEmptyTrackWithNonZeroTiming()
    {
        TrackDescriptor startOnly = new TrackDescriptor { StartTime = 1.0f };
        Action validateStart = () => TrackDescriptor.Validate(in startOnly);
        validateStart.Should().Throw<ValidationException>()
            .Where(e => e.Code == AnimationErrorCode.ValidationClipDegenerateData);

        TrackDescriptor endOnly = new TrackDescriptor { EndTime = -1.0f };
        Action validateEnd = () => TrackDescriptor.Validate(in endOnly);
        validateEnd.Should().Throw<ValidationException>()
            .Where(e => e.Code == AnimationErrorCode.ValidationClipDegenerateData);
    }

    [Fact]
    public void Validate_RejectsStartAfterEnd()
    {
        TrackDescriptor descriptor = new TrackDescriptor
        {
            KeyCount = 2,
            StartTime = 1.0f,
            EndTime = 0.5f,
        };

        descriptor.Duration.Should().Be(-0.5f);

        Action validate = () => TrackDescriptor.Validate(in descriptor);
        validate.Should().Throw<ValidationException>()
            .Where(e => e.Code == AnimationErrorCode.ValidationClipDegenerateData);
    }

    [Fact]
    public void Validate_RejectsInvertedProjectionDerivedWithWith()
    {
        TrackDescriptor projected = TrackDescriptor.FromChannel(
            BoneChannel([0.0f, 1.0f], [0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f]));
        TrackDescriptor inverted = projected with { StartTime = 3.0f };

        Action validate = () => TrackDescriptor.Validate(in inverted);
        validate.Should().Throw<ValidationException>()
            .Where(e => e.Code == AnimationErrorCode.ValidationClipDegenerateData);
    }

    [Fact]
    public void WithDerivation_RoundTripsExceptTheMutatedMember()
    {
        TrackDescriptor original = TrackDescriptor.FromChannel(
            CurveChannel([0.0f, 1.5f], [0.0f, 1.0f], "trigger"));

        TrackDescriptor derived = original with { Interpolation = InterpolationMode.Step };

        derived.Should().NotBe(original);
        derived.Interpolation.Should().Be(InterpolationMode.Step);
        original.Interpolation.Should().Be(InterpolationMode.Linear);

        derived.Target.Should().Be(original.Target);
        derived.BoneIndex.Should().Be(original.BoneIndex);
        derived.CurveId.Should().Be(original.CurveId);
        derived.KeyCount.Should().Be(original.KeyCount);
        derived.StartTime.Should().Be(original.StartTime);
        derived.EndTime.Should().Be(original.EndTime);
        derived.Duration.Should().Be(original.Duration);
        derived.IsEmpty.Should().Be(original.IsEmpty);
        derived.IsBoneTrack.Should().Be(original.IsBoneTrack);
        derived.IsCurveTrack.Should().Be(original.IsCurveTrack);

        (original with { }).Should().Be(original);

        Action validateDerived = () => TrackDescriptor.Validate(in derived);
        validateDerived.Should().NotThrow();
    }

    [Fact]
    public void Describe_MirrorsRecordToString()
    {
        TrackDescriptor descriptor = TrackDescriptor.FromChannel(
            BoneChannel([0.0f, 1.0f], [0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f]));

        string described = TrackDescriptor.Describe(in descriptor);

        described.Should().Be(descriptor.ToString());
        described.Should().NotBeNullOrWhiteSpace();
        described.Should().Contain(nameof(TrackDescriptor));
        described.Should().Contain("KeyCount = 2");
        described.Should().Contain("StartTime = 0");
        described.Should().Contain("EndTime = 1");
    }

    [Fact]
    public void Enumerator_ProjectsEveryChannelIntoValidDescriptors()
    {
        AnimationChannel[] channels =
        [
            BoneChannel([0.0f, 1.0f], [0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f]),
            BoneChannel([], []),
            CurveChannel([0.0f, 0.5f, 1.0f, 2.0f], [0.0f, 0.5f, 1.0f, 2.0f], "weight"),
        ];

        var seen = new List<TrackDescriptor>();
        foreach (TrackDescriptor descriptor in TrackDescriptorEnumerator.Enumerate(channels))
        {
            Action validate = () => TrackDescriptor.Validate(in descriptor);
            validate.Should().NotThrow();
            seen.Add(descriptor);
        }

        seen.Should().HaveCount(3);
        seen[0].KeyCount.Should().Be(2);
        seen[1].IsEmpty.Should().BeTrue();
        seen[2].IsCurveTrack.Should().BeTrue();
        seen[2].CurveId.Should().Be(new CurveId("weight"));
        seen[2].Duration.Should().Be(2.0f);
    }
}
