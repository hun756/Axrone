namespace Axrone.Animation.Tests;

public class TelemetryTests
{
    [Fact]
    public void Counters_IncrementAndSnapshot()
    {
        var counters = new AnimationDiagnosticCounters();
        counters.IncrementFrame();
        counters.IncrementFrame();
        counters.IncrementClip();

        AnimationMetricsSnapshot snapshot = counters.CaptureSnapshot();
        snapshot.TotalFrames.Should().Be(2);
        snapshot.TotalClips.Should().Be(1);
        snapshot.IsOperational.Should().BeTrue();

        AnimationDiagnosticCounters other = default;
        (counters == other).Should().BeFalse();
        (counters != other).Should().BeTrue();
        counters.Equals(other).Should().BeFalse();
        counters.Equals((object)other).Should().BeFalse();
        counters.Equals(counters).Should().BeTrue();
    }

    [Fact]
    public void ControllerUpdate_ReportsWithoutThrowing()
    {
        var rig = new Rig(new RigId("r"), [
            new BoneInfo { Name = "root", ParentIndex = -1 },
        ]);
        var parameters = new ParameterStore(new Dictionary<string, ParameterType>());
        using var controller = new AnimationController(rig, parameters, new Dictionary<CurveId, int>());
        var events = new List<ClipEvent>();

        Action update = () => controller.Update(0.016f, events, out _, out _);
        update.Should().NotThrow();
    }
}
