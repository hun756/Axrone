namespace Axrone.Animation.Tests;

using System.Collections.ObjectModel;
using System.Text.Json;

public class StreamingTests
{
    private static AnimationClip LinearClip() => new(
        new ClipId("walk"), 2.0f,
        [
            new AnimationChannel(0, ChannelTarget.Translation, InterpolationMode.Linear, [0.0f, 1.0f, 2.0f], [0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 2.0f, 0.0f, 0.0f]),
            new AnimationChannel(0, ChannelTarget.Rotation, InterpolationMode.Linear, [0.0f, 2.0f], [0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f]),
        ],
        events: [new ClipEvent(1.0f, "step", "")],
        tags: ["locomotion"]);

    [Fact]
    public void Codec_RoundTripsAndValidates()
    {
        var payload = new ChunkPayloadDto
        {
            ClipId = "walk",
            StartTime = 0.0f,
            EndTime = 1.0f,
            Duration = 2.0f,
        };
        payload.Tracks.Add(new ChunkTrackDto
        {
            BoneIndex = 0,
            Target = (byte)ChannelTarget.Translation,
            Interpolation = (byte)InterpolationMode.Linear,
            KeyTimes = [0.0f, 1.0f],
            KeyValues = [0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f],
        });

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(payload, ChunkJsonSerializerContext.Default.ChunkPayloadDto);
        ChunkPayloadDto decoded = ChunkCodec.Decode(json);
        decoded.ClipId.Should().Be("walk");
        decoded.Tracks.Count.Should().Be(1);

        Action corrupt = () => ChunkCodec.Decode("not json"u8);
        corrupt.Should().Throw<SamplingException>()
            .Where(e => e.Code == AnimationErrorCode.StreamingChunkCorrupt);

        var badVersion = new ChunkPayloadDto { Version = 99, ClipId = "walk" };
        byte[] badJson = JsonSerializer.SerializeToUtf8Bytes(badVersion, ChunkJsonSerializerContext.Default.ChunkPayloadDto);
        Action version = () => ChunkCodec.Decode(badJson);
        version.Should().Throw<SamplingException>()
            .Where(e => e.Code == AnimationErrorCode.StreamingChunkIncompatible);
    }

    [Fact]
    public void Merge_ReplacesAndAppends()
    {
        AnimationClip clip = LinearClip();
        var payload = new ChunkPayloadDto { ClipId = "walk", Duration = 2.0f };
        payload.Tracks.Add(new ChunkTrackDto
        {
            BoneIndex = 0,
            Target = (byte)ChannelTarget.Translation,
            Interpolation = (byte)InterpolationMode.Linear,
            KeyTimes = [0.0f, 2.0f],
            KeyValues = [5.0f, 0.0f, 0.0f, 6.0f, 0.0f, 0.0f],
        });
        payload.Tracks.Add(new ChunkTrackDto
        {
            BoneIndex = 1,
            Target = (byte)ChannelTarget.Scale,
            Interpolation = (byte)InterpolationMode.Linear,
            KeyTimes = [0.0f],
            KeyValues = [1.0f, 1.0f, 1.0f],
        });

        AnimationChannel[] replaced = ChunkCodec.Merge([.. clip.Channels], payload, MergeMode.ReplaceRange);
        replaced.Should().HaveCount(3);

        AnimationChannel[] all = ChunkCodec.Merge([.. clip.Channels], payload, MergeMode.ReplaceAll);
        all.Should().HaveCount(2);
    }

    [Fact]
    public void Scheduler_OrdersActiveBeforePreload()
    {
        var scheduler = new StreamingScheduler();
        var outRequests = new Collection<ChunkRequest>();
        (ClipId Clip, float Time, float Weight)[] activities = [(new ClipId("walk"), 1.0f, 1.0f)];

        scheduler.Schedule(activities, chunkDuration: 2.0f, preloadWindow: 1.5f, outRequests);
        outRequests.Should().HaveCount(2);
        outRequests[0].IsPreload.Should().BeFalse();
        outRequests[1].IsPreload.Should().BeTrue();

        var again = new Collection<ChunkRequest>();
        scheduler.Schedule(activities, chunkDuration: 2.0f, preloadWindow: 1.5f, outRequests: again);
        again.Should().BeEmpty();

        scheduler.MarkLoaded(outRequests[0].ChunkId);
        var third = new Collection<ChunkRequest>();
        scheduler.Schedule(activities, chunkDuration: 2.0f, preloadWindow: 1.5f, outRequests: third);
        third.Should().BeEmpty();
    }

    [Fact]
    public void Optimizer_DropsCollinearKeys()
    {
        AnimationClip clip = LinearClip();
        AnimationClip optimized = KeyframeOptimizer.Optimize(clip);

        int before = 0;
        foreach (AnimationChannel channel in clip.Channels)
        {
            before += channel.KeyTimes.Length;
        }

        int after = 0;
        foreach (AnimationChannel channel in optimized.Channels)
        {
            after += channel.KeyTimes.Length;
        }

        after.Should().BeLessThan(before);
        optimized.Events.Length.Should().Be(1);
        optimized.Tags.Should().Contain("locomotion");

        var frame = new AnimationFrame(1, new Dictionary<CurveId, int>());
        optimized.Sample(1.0f, frame);
        frame.ReadTranslations()[0].X.Should().BeApproximately(1.0f, 1e-4f);
    }

    [Fact]
    public void Skinning_ComposesPalette()
    {
        var rig = new Rig(new RigId("r"), [
            new BoneInfo { Name = "root", ParentIndex = -1 },
        ]);
        Span<Vector3> worldT = stackalloc Vector3[1];
        Span<Quaternion> worldR = stackalloc Quaternion[1];
        Span<Vector3> worldS = stackalloc Vector3[1];
        worldT[0] = new Vector3(1.0f, 2.0f, 3.0f);
        worldR[0] = Quaternion.Identity;
        worldS[0] = Vector3.One;

        Span<float> palette = stackalloc float[16];
        SkinningPalette.ComputeFromWorldPose(rig, worldT, worldR, worldS, palette);
        palette[3].Should().Be(1.0f);
        palette[7].Should().Be(2.0f);
        palette[11].Should().Be(3.0f);
        palette[15].Should().Be(1.0f);
    }
}
