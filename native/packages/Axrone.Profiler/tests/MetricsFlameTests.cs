namespace Axrone.Profiler.Tests;

public sealed class MetricsFlameTests
{
    [Fact]
    public void Histogram_Reports_Mean_Min_Max()
    {
        using var histogram = new LogHistogram();
        for (int i = 1; i <= 100; i++)
        {
            histogram.Record(i);
        }

        histogram.Count.Should().Be(100);
        histogram.MinValue().Should().Be(1.0);
        histogram.MaxValue().Should().Be(100.0);

        HistogramStatistics stats = histogram.GetStatistics();
        stats.Count.Should().Be(100);
        stats.MeanMs.Should().BeApproximately(50.5, 1e-9);
        stats.MinMs.Should().Be(1.0);
        stats.MaxMs.Should().Be(100.0);
        stats.TotalDurationMs.Should().BeApproximately(5050.0, 1e-6);
    }

    [Fact]
    public void Histogram_Percentiles_Follow_Distribution()
    {
        using var histogram = new LogHistogram();
        for (int i = 1; i <= 100; i++)
        {
            histogram.Record(i);
        }

        HistogramStatistics stats = histogram.GetStatistics();
        stats.P50Ms.Should().BeInRange(32.0, 64.0);
        stats.P90Ms.Should().BeInRange(64.0, 128.0);
        stats.P99Ms.Should().BeInRange(64.0, 128.0);
        stats.P50Ms.Should().BeLessThanOrEqualTo(stats.P90Ms);
        stats.P90Ms.Should().BeLessThanOrEqualTo(stats.P99Ms);
    }

    [Fact]
    public void Histogram_Rejects_Negative_And_Empty_Stats_Are_NaN()
    {
        using var histogram = new LogHistogram();
        histogram.GetStatistics().MeanMs.Should().Be(double.NaN);

        Action negative = () => histogram.Record(-1.0);
        negative.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FlameGraph_Aggregates_Roots_And_Stacks()
    {
        using var builder = new FlameGraphBuilder();
        builder.RecordFrame("update", 100L);
        builder.RecordFrame("update", 50L);
        builder.RecordFrame("render", 200L);

        IReadOnlyList<FlameGraphNode> roots = builder.GetRoots();
        roots.Should().HaveCount(2);
        roots[0].Name.Should().Be("render");
        roots[0].DurationNs.Should().Be(200L);
        roots[1].Name.Should().Be("update");
        roots[1].DurationNs.Should().Be(150L);

        builder.RecordStack(new[] { "update", "physics", "narrowphase" }, 40L);
        FlameGraphNode update = builder.GetRoots().First(n => n.Name == "update");
        update.DurationNs.Should().Be(150L);
        update.Children.Should().ContainSingle().Which.Name.Should().Be("physics");
    }

    [Fact]
    public void FlameGraph_Begin_End_Nests_Frames()
    {
        using var builder = new FlameGraphBuilder();
        builder.BeginFrame("frame").Should().Be(1);
        builder.BeginFrame("update").Should().Be(2);
        builder.EndFrame(30L);
        builder.EndFrame(100L);

        FlameGraphNode root = builder.GetRoots().Should().ContainSingle().Subject;
        root.Name.Should().Be("frame");
        root.DurationNs.Should().Be(100L);
        root.Children.Should().ContainSingle().Which.DurationNs.Should().Be(30L);
    }

    [Fact]
    public void FlameGraph_Signatures_Are_Unique_Per_Node()
    {
        using var builder = new FlameGraphBuilder();
        builder.RecordSignature("update", "update#1");
        builder.RecordSignature("update", "update#1");
        builder.RecordSignature("update", "update#2");

        FlameGraphNode node = builder.GetRoots().Should().ContainSingle().Subject;
        node.GetUniqueSignatures().Should().BeEquivalentTo("update#1", "update#2");
    }
}
