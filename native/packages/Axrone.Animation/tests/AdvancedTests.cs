namespace Axrone.Animation.Tests;

public class AdvancedTests
{
    [Fact]
    public void MotionMatching_PicksNearestWithFacing()
    {
        MotionMatchCandidate[] candidates =
        [
            new(0, 0.0f, new Vec3(10.0f, 0.0f, 0.0f), new Vec3(1.0f, 0.0f, 0.0f), new HashSet<string> { "run" }),
            new(1, 0.5f, new Vec3(1.0f, 0.0f, 0.0f), new Vec3(1.0f, 0.0f, 0.0f), new HashSet<string> { "run" }),
            new(2, 0.0f, new Vec3(1.0f, 0.0f, 0.0f), new Vec3(-1.0f, 0.0f, 0.0f), new HashSet<string> { "run" }),
        ];
        var query = new MotionMatchQuery(new Vec3(1.0f, 0.0f, 0.0f), new Vec3(1.0f, 0.0f, 0.0f));

        int best = MotionMatching.Search(query, candidates, out float score);
        best.Should().Be(1);
        score.Should().BeApproximately(0.0f, 1e-6f);
    }

    [Fact]
    public void MotionMatching_RespectsTagFilters()
    {
        MotionMatchCandidate[] candidates =
        [
            new(0, 0.0f, new Vec3(0.0f, 0.0f, 0.0f), new Vec3(1.0f, 0.0f, 0.0f), new HashSet<string> { "jump" }),
            new(1, 0.0f, new Vec3(100.0f, 0.0f, 0.0f), new Vec3(1.0f, 0.0f, 0.0f), new HashSet<string> { "run" }),
        ];
        var required = new MotionMatchQuery(
            new Vec3(0.0f, 0.0f, 0.0f), new Vec3(1.0f, 0.0f, 0.0f),
            RequiredTags: new HashSet<string> { "run" });

        MotionMatching.Search(required, candidates, out _).Should().Be(1);

        var excluded = new MotionMatchQuery(
            new Vec3(0.0f, 0.0f, 0.0f), new Vec3(1.0f, 0.0f, 0.0f),
            ExcludedTags: new HashSet<string> { "jump", "run" });

        MotionMatching.Search(excluded, candidates, out float score).Should().Be(-1);
        score.Should().Be(float.MaxValue);
    }

    [Fact]
    public void Grounding_AveragesContactOffsets()
    {
        FootContact[] contacts =
        [
            new(0.0f, 1.0f, 0),
            new(0.0f, 1.0f, 1),
        ];
        float[] heights = [0.1f, 0.3f];

        float offset = GroundingSystem.ComputeRootYOffset(contacts, heights, 0.0f, 0.5f);
        offset.Should().BeApproximately(-0.2f, 1e-5f);

        GroundingSystem.ComputeRootYOffset(contacts, heights, 0.0f, 5.0f).Should().Be(0.0f);
    }

    [Fact]
    public void MotionMatching_ContinuityBiasOnlyAppliesToTheCurrentClip()
    {
        MotionMatchCandidate[] candidates =
        [
            new MotionMatchCandidate(0, 0.0f, Vec3.Zero, Vec3.UnitZ, null),
            new MotionMatchCandidate(1, 0.0f, Vec3.Zero, Vec3.UnitZ, null),
        ];

        var unbiased = new MotionMatchQuery(Vec3.Zero, Vec3.UnitZ);
        MotionMatching.Search(unbiased, candidates, out _).Should().Be(0);

        var continuity = new MotionMatchQuery(
            Vec3.Zero,
            Vec3.UnitZ,
            ContinuityBias: 1.0f,
            ContinuityClipIndex: 1);
        MotionMatching.Search(continuity, candidates, out _).Should().Be(1);
    }

    [Fact]
    public void MotionMatchingDatabase_SamplesClipsAndTags()
    {
        Rig rig = new(new RigId("mm"), [new BoneInfo { Name = "root", ParentIndex = -1 }]);
        var walk = new AnimationClip(
            new ClipId("walk"), 1.0f,
            [new AnimationChannel(0, ChannelTarget.Translation, InterpolationMode.Linear, [0.0f, 1.0f], [0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f])],
            tags: ["walk"]);
        var run = new AnimationClip(
            new ClipId("run"), 1.0f,
            [new AnimationChannel(0, ChannelTarget.Translation, InterpolationMode.Linear, [0.0f, 1.0f], [0.0f, 0.0f, 0.0f, 10.0f, 0.0f, 0.0f])],
            tags: ["run"]);

        MotionMatchingDatabase database = MotionMatchingDatabase.Build(rig, [walk, run], sampleInterval: 0.5f);

        database.Candidates.Length.Should().Be(6);
        database.Candidates[0].ClipIndex.Should().Be(0);
        database.Candidates[3].ClipIndex.Should().Be(1);
        database.Candidates[0].Tags.Should().Contain("walk");
        database.Candidates[3].Tags.Should().Contain("run");

        var query = new MotionMatchQuery(new Vec3(10.0f, 0.0f, 0.0f), Vec3.UnitZ);
        int best = MotionMatching.Search(query, database.Candidates, out _);
        database.Candidates[best].ClipIndex.Should().Be(1);
    }

    [Fact]
    public void Grounding_AxisLocksKeepDisabledAxesZero()
    {
        FootContact[] contacts =
        [
            new(0.0f, 1.0f, 0),
            new(0.0f, 1.0f, 1),
        ];
        Vec3[] positions =
        [
            new Vec3(1.0f, 1.0f, 0.0f),
            new Vec3(3.0f, 2.0f, 0.0f),
        ];

        Vec3 offset = GroundingSystem.ComputeRootOffset(
            contacts,
            positions,
            Vec3.Zero,
            0.5f,
            GroundingAxes.X | GroundingAxes.Y);

        offset.X.Should().BeApproximately(-2.0f, 1e-4f);
        offset.Y.Should().BeApproximately(-1.5f, 1e-4f);
        offset.Z.Should().Be(0.0f);

        Vec3 vertical = GroundingSystem.ComputeRootOffset(
            contacts,
            positions,
            Vec3.Zero,
            0.5f);
        vertical.X.Should().Be(0.0f);
        vertical.Y.Should().BeApproximately(-1.5f, 1e-4f);
        vertical.Z.Should().Be(0.0f);
    }
}
