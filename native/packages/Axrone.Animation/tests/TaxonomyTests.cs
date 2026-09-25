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
}
