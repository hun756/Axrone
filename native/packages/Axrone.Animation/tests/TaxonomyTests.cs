namespace Axrone.Animation.Tests;

using System.Collections.ObjectModel;
using System.Text;
using Xunit;
using FluentAssertions;

public class TaxonomyTests
{
    private static Rig TwoBone() => new(new RigId("r"), new BoneInfo[]
    {
        new() { Name = "root", ParentIndex = -1 },
        new() { Name = "child", ParentIndex = 0 },
    }.AsSpan());

    [Fact]
    public void Codec_CorruptJsonThrowsStreaming()
    {
        Action decode = () => ChunkCodec.Decode("not json"u8);
        decode.Should().Throw<StreamingException>()
            .Where(ex => ex.Code == AnimationErrorCode.StreamingChunkCorrupt);
    }

    [Fact]
    public void Codec_WrongVersionThrowsStreaming()
    {
        string json = """{"Version":99,"ClipId":"walk","MergeMode":0,"StartTime":0,"EndTime":1,"Duration":1,"Tracks":[]}""";
        Action decode = () => ChunkCodec.Decode(Encoding.UTF8.GetBytes(json));
        decode.Should().Throw<StreamingException>()
            .Where(ex => ex.Code == AnimationErrorCode.StreamingChunkIncompatible);
    }

    [Fact]
    public void Scheduler_NonPositiveDurationThrowsValidation()
    {
        var scheduler = new StreamingScheduler();
        (ClipId Clip, float Time, float Weight)[] activities = [(new ClipId("walk"), 1.0f, 1.0f)];
        Action schedule = () => scheduler.Schedule(activities, 0.0f, 1.0f, new Collection<ChunkRequest>());
        schedule.Should().Throw<ValidationException>()
            .Where(ex => ex.Code == AnimationErrorCode.ValidationInvalidArgument);
    }

    [Fact]
    public void Ik_ShortChainThrowsIk()
    {
        Rig rig = TwoBone();
        var frame = new AnimationFrame(rig.BoneCount, new Dictionary<CurveId, int>());
        frame.ResetToRest(rig);
        int[] chain = [0];

        Action fabrik = () => IkSolvers.SolveFabrik(rig, frame, chain, Vector3.Zero, Span<Vector3>.Empty);
        fabrik.Should().Throw<IkException>()
            .Where(ex => ex.Code == AnimationErrorCode.ValidationInvalidArgument);

        Action ccd = () => IkSolvers.SolveCcd(rig, frame, chain, Vector3.Zero);
        ccd.Should().Throw<IkException>()
            .Where(ex => ex.Code == AnimationErrorCode.ValidationInvalidArgument);
    }

    [Fact]
    public void Rig_ChildrenOutOfRangeThrowsValidation()
    {
        Rig rig = TwoBone();
        Action children = () => rig.GetChildren(99);
        children.Should().Throw<ValidationException>()
            .Where(ex => ex.Code == AnimationErrorCode.ValidationInvalidArgument);
    }

    private static readonly float[] s_keyTime = new float[] { 0.0f };
    private static readonly float[] s_keyTranslation = new float[] { 0, 0, 0 };
    private static readonly float[] s_keyValue = new float[] { 0 };
    private static readonly float[] s_unorderedTimes = new float[] { 1.0f, 0.0f };
    private static readonly float[] s_unorderedValues = new float[] { 0, 0, 0, 1, 0, 0 };

    [Fact]
    public void Channel_MalformedDataThrowsAtConstruction()
    {
        Action negativeBone = () => _ = new AnimationChannel(
            -1, ChannelTarget.Translation, InterpolationMode.Linear, s_keyTime, s_keyTranslation);
        negativeBone.Should().Throw<ValidationException>()
            .Where(ex => ex.Code == AnimationErrorCode.ValidationClipMismatch);

        Action unknownTarget = () => _ = new AnimationChannel(
            0, (ChannelTarget)99, InterpolationMode.Linear, s_keyTime, s_keyValue);
        unknownTarget.Should().Throw<ValidationException>()
            .Where(ex => ex.Code == AnimationErrorCode.ValidationClipMismatch);

        Action unordered = () => _ = new AnimationChannel(
            0, ChannelTarget.Translation, InterpolationMode.Linear, s_unorderedTimes, s_unorderedValues);
        unordered.Should().Throw<ValidationException>()
            .Where(ex => ex.Code == AnimationErrorCode.ValidationClipDegenerateData);
    }

    [Fact]
    public void Skinning_UndersizedInputsThrowValidation()
    {
        Action palette = () => SkinningPalette.ComputePalette(
            new float[16], ReadOnlySpan<float>.Empty, new float[16], new float[15]);
        palette.Should().Throw<ValidationException>()
            .Where(ex => ex.Code == AnimationErrorCode.ValidationInvalidArgument);
    }

    [Fact]
    public void Retarget_MismatchedFramesThrowRetargeting()
    {
        Rig rig = TwoBone();
        var profile = new RetargetProfile(rig, rig);
        var small = new AnimationFrame(1, new Dictionary<CurveId, int>());
        var frame = new AnimationFrame(rig.BoneCount, new Dictionary<CurveId, int>());

        Action retarget = () => profile.RetargetFrame(small, frame);
        retarget.Should().Throw<RetargetingException>()
            .Where(ex => ex.Code == AnimationErrorCode.RetargetingIncompatibleLayout);
    }
}
