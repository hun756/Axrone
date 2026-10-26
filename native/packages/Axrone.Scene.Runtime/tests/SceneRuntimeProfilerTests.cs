namespace Axrone.Scene.Runtime.Tests;

using Axrone.Scene;
using Axrone.Scene.Rendering;
using Axrone.Scene.Runtime;
using FluentAssertions;
using Xunit;

public sealed class SceneRuntimeProfilerTests
{
    [Fact]
    public void RecordsPhasesCountersAndFrameTiming()
    {
        using var profiler = new SceneRuntimeProfiler(new SceneRuntimeProfilerOptions
        {
            Enabled = true,
            Capacity = 16
        });

        profiler.BeginFrame(7, 0.0, 16.0);
        using (profiler.MeasurePhase(SceneRuntimePhase.Update))
        {
        }

        profiler.AccumulatePhase(SceneRuntimePhase.Render, 2.5f);
        var renderStats = new SceneRenderStats(7, 3, 12, 1, 2, 1, 0, 4);
        profiler.AttachRenderStats(in renderStats);
        profiler.AttachPhysicsStats(new ScenePhysicsStats(1.0f, 0.25f, 0.5f));
        profiler.EndFrame(16.0, fixedSteps: 1);

        SceneRuntimeFrameRecord record = profiler.LastRecord!.Value;
        record.Frame.Should().Be(7);
        record.FrameTimeMs.Should().BeApproximately(16.0, 1e-6);
        record.Fps.Should().BeApproximately(62.5, 1e-6);
        record.DeltaMs.Should().BeApproximately(16.0, 1e-6);
        record.FixedSteps.Should().Be(1);
        record.PhaseMs.RenderMs.Should().BeApproximately(2.5f, 1e-6f);
        record.PhaseMs.UpdateMs.Should().BeGreaterThanOrEqualTo(0.0f);
        record.HasRenderStats.Should().BeTrue();
        record.RenderStats.SpriteCount.Should().Be(4);
        record.HasPhysicsStats.Should().BeTrue();
        record.PhysicsStats.StepMs.Should().BeApproximately(1.0f, 1e-6f);
    }

    [Fact]
    public void RingBufferRetainsNewestRecords()
    {
        using var profiler = new SceneRuntimeProfiler(new SceneRuntimeProfilerOptions
        {
            Enabled = true,
            Capacity = 3
        });

        for (int i = 0; i < 5; i++)
        {
            profiler.BeginFrame(i, i * 10.0, 10.0);
            profiler.EndFrame(i * 10.0, 1);
        }

        var records = new SceneRuntimeFrameRecord[3];
        profiler.CopyRecords(records).Should().Be(3);
        records.Select(record => record.Frame).Should().Equal(2, 3, 4);
        profiler.Count.Should().Be(3);
    }

    [Fact]
    public void SummaryComputesAveragesAndP95()
    {
        using var profiler = new SceneRuntimeProfiler(new SceneRuntimeProfilerOptions
        {
            Enabled = true,
            Capacity = 8
        });

        double[] timestamps = [0.0, 10.0, 30.0, 60.0, 100.0];
        for (int i = 0; i < timestamps.Length; i++)
        {
            profiler.BeginFrame(i, timestamps[i], 10.0);
            profiler.EndFrame(timestamps[i], 1);
        }

        SceneRuntimeProfilerSummary summary = profiler.GetSummary(0)!.Value;

        summary.FrameCount.Should().Be(5);
        summary.AvgFrameTimeMs.Should().BeApproximately(20.0, 1e-6);
        summary.MinFrameTimeMs.Should().BeApproximately(0.0, 1e-6);
        summary.MaxFrameTimeMs.Should().BeApproximately(40.0, 1e-6);
        summary.P95FrameTimeMs.Should().BeApproximately(40.0, 1e-6);

        SceneRuntimeProfilerSummary window = profiler.GetSummary(4)!.Value;
        window.FrameCount.Should().Be(4);
        window.AvgFrameTimeMs.Should().BeApproximately(25.0, 1e-6);
        window.MinFrameTimeMs.Should().BeApproximately(10.0, 1e-6);
    }

    [Fact]
    public void DisabledProfilerRecordsNothing()
    {
        using var profiler = new SceneRuntimeProfiler(new SceneRuntimeProfilerOptions
        {
            Enabled = false
        });

        profiler.BeginFrame(0, 0.0, 16.0);
        profiler.EndFrame(16.0, 1);

        profiler.Count.Should().Be(0);
        profiler.LastRecord.Should().BeNull();
        profiler.GetSummary(0).Should().BeNull();
    }

    [Fact]
    public void ListenerReceivesCompletedRecords()
    {
        using var profiler = new SceneRuntimeProfiler(new SceneRuntimeProfilerOptions
        {
            Enabled = true,
            Capacity = 4
        });

        int received = 0;
        SceneRuntimeProfilerListener listener = (in SceneRuntimeFrameRecord record) =>
        {
            if (record.Frame == 3)
            {
                received++;
            }
        };
        profiler.Subscribe(listener);

        profiler.BeginFrame(3, 0.0, 16.0);
        profiler.EndFrame(16.0, 1);

        received.Should().Be(1);
        profiler.Unsubscribe(listener).Should().BeTrue();
        profiler.Unsubscribe(listener).Should().BeFalse();
    }

    [Fact]
    public void SteadyStateRecordingDoesNotAllocate()
    {
        using var profiler = new SceneRuntimeProfiler(new SceneRuntimeProfilerOptions
        {
            Enabled = true,
            Capacity = 64,
            MemorySampleIntervalMs = 1e9
        });

        var renderStats = new SceneRenderStats(0, 1, 2, 1, 1, 0, 0, 0);

        for (int i = 0; i < 10; i++)
        {
            RecordFrame(profiler, i, in renderStats);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 500; i++)
        {
            RecordFrame(profiler, i, in renderStats);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        (after - before).Should().Be(0);
    }

    [Fact]
    public void CapturePhaseSampleStoresStackFrames()
    {
        using var profiler = new SceneRuntimeProfiler(new SceneRuntimeProfilerOptions
        {
            Enabled = true,
            Capacity = 4,
            StackSampleCapacity = 4
        });

        profiler.BeginFrame(0, 0.0, 16.0);
        profiler.CapturePhaseSample(SceneRuntimePhase.Update);
        profiler.EndFrame(16.0, 1);

        SceneRuntimeStackSample sample = profiler.GetStackSamples().Should().ContainSingle().Subject;
        sample.Phase.Should().Be(SceneRuntimePhase.Update);
        sample.Frames.Should().NotBeEmpty();

        profiler.ClearStackSamples();
        profiler.GetStackSamples().Should().BeEmpty();
    }

    [Fact]
    public void ClearResetsRecords()
    {
        using var profiler = new SceneRuntimeProfiler(new SceneRuntimeProfilerOptions
        {
            Enabled = true,
            Capacity = 4
        });

        profiler.BeginFrame(0, 0.0, 16.0);
        profiler.EndFrame(16.0, 1);
        profiler.Clear();

        profiler.Count.Should().Be(0);
        profiler.GetSummary(0).Should().BeNull();
        profiler.BeginFrame(1, 16.0, 16.0);
        profiler.EndFrame(32.0, 1);
        profiler.LastRecord!.Value.Frame.Should().Be(1);
    }

    private static void RecordFrame(
        SceneRuntimeProfiler profiler,
        int frame,
        in SceneRenderStats renderStats)
    {
        profiler.BeginFrame(frame, frame * 16.0, 16.0);

        using (profiler.MeasurePhase(SceneRuntimePhase.Update))
        {
        }

        profiler.AccumulatePhase(SceneRuntimePhase.Render, 1.0f);
        profiler.AttachRenderStats(in renderStats);
        profiler.EndFrame(frame * 16.0, 1);
    }
}
