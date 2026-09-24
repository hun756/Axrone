namespace Axrone.Animation.Tests;

public class AdvancedTests
{
    [Fact]
    public void MotionMatching_PicksNearestWithFacing()
    {
        MotionMatchCandidate[] candidates =
        [
            new(0, 0.0f, new Vector3(10.0f, 0.0f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f), new HashSet<string> { "run" }),
            new(1, 0.5f, new Vector3(1.0f, 0.0f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f), new HashSet<string> { "run" }),
            new(2, 0.0f, new Vector3(1.0f, 0.0f, 0.0f), new Vector3(-1.0f, 0.0f, 0.0f), new HashSet<string> { "run" }),
        ];
        var query = new MotionMatchQuery(new Vector3(1.0f, 0.0f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f));

        int best = MotionMatching.Search(query, candidates, out float score);
        best.Should().Be(1);
        score.Should().BeApproximately(0.0f, 1e-6f);
    }

    [Fact]
    public void MotionMatching_RespectsTagFilters()
    {
        MotionMatchCandidate[] candidates =
        [
            new(0, 0.0f, new Vector3(0.0f, 0.0f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f), new HashSet<string> { "jump" }),
            new(1, 0.0f, new Vector3(100.0f, 0.0f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f), new HashSet<string> { "run" }),
        ];
        var required = new MotionMatchQuery(
            new Vector3(0.0f, 0.0f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f),
            RequiredTags: new HashSet<string> { "run" });

        MotionMatching.Search(required, candidates, out _).Should().Be(1);

        var excluded = new MotionMatchQuery(
            new Vector3(0.0f, 0.0f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f),
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
}
