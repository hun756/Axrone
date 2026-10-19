namespace Axrone.Profiler.Tests;

public sealed class MachineMemoryTests
{
    [Fact]
    public void MachineInfo_Reports_Sane_Topology()
    {
        MachineInfo.ProcessorCount.Should().BeGreaterThan(0);
        MachineInfo.OsDescription.Should().NotBeNullOrWhiteSpace();
        MachineInfo.ClrVersion.Should().NotBeNullOrWhiteSpace();
        ThreadCpuSample.ManagedThreadId.Should().Be(Environment.CurrentManagedThreadId);
    }

    [Fact]
    public void ProcessSnapshot_Captures_Live_Counters()
    {
        ProcessSnapshot snapshot = ProcessSnapshot.Capture();
        snapshot.WorkingSetBytes.Should().BeGreaterThan(0L);
        snapshot.PrivateBytes.Should().BeGreaterThan(0L);
        snapshot.HandleCount.Should().BeGreaterThan(0);
        snapshot.ThreadCount.Should().BeGreaterThan(0);
        snapshot.TotalProcessorTime.Value.Should().BeGreaterThanOrEqualTo(0L);
    }

    [Fact]
    public void ThreadCpu_Enumerates_Os_Threads()
    {
        IReadOnlyList<ThreadCpuSample> samples = MachineInfo.CaptureThreadCpu();
        samples.Should().NotBeEmpty();
        samples.Should().OnlyContain(s => s.OsThreadId != 0);
        samples.Should().OnlyContain(s => s.TotalProcessorTime.Value >= 0L);
    }

    [Fact]
    public void GcSnapshot_Captures_Heap_After_Allocation()
    {
        byte[] keep = new byte[256 * 1024];
        GC.KeepAlive(keep);
        GC.Collect();

        GcSnapshot snapshot = GcSnapshot.Capture();
        snapshot.HeapSizeBytes.Should().BeGreaterThan(0L);
        snapshot.CommittedBytes.Should().BeGreaterThanOrEqualTo(snapshot.HeapSizeBytes);
        (snapshot.Gen0Collections + snapshot.Gen1Collections + snapshot.Gen2Collections).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void MemoryTracker_Samples_With_Named_Provider()
    {
        using var tracker = new MemoryTracker();
        tracker.GetProviderName().Should().Be("dotnet");

        tracker.StartTracking();
        MemorySample sample = tracker.Sample();
        sample.UsedHeapBytes.Should().BeGreaterThan(0L);
        sample.Source.Should().Be(MemorySource.DotNet);
        tracker.GetTotalSamples().Should().Be(1);
        tracker.StopTracking();

        Action stopped = () => tracker.Sample();
        stopped.Should().Throw<InvalidOperationException>();

        using var fallback = new MemoryTracker(new NullMemoryProvider());
        fallback.GetProviderName().Should().Be("none");
        fallback.StartTracking();
        fallback.Sample().Source.Should().Be(MemorySource.Unknown);
        fallback.StopTracking();
    }

    [Fact]
    public void MemoryTracker_Caps_Samples_And_Auto_Samples()
    {
        using var tracker = new MemoryTracker(maxSamples: 4);
        tracker.StartTracking();

        for (int i = 0; i < 6; i++)
        {
            tracker.Sample();
        }

        tracker.GetTotalSamples().Should().BeLessThanOrEqualTo(4);
        tracker.GetSamples().Should().HaveCount((int)tracker.GetTotalSamples());
        tracker.StopTracking();
    }

    [Fact]
    public void MemoryTracker_Interval_Arms_Background_Sampling()
    {
        using var tracker = new MemoryTracker();
        tracker.StartTracking(intervalMs: 5.0);
        Thread.Sleep(200);
        tracker.StopTracking();

        tracker.GetTotalSamples().Should().BeGreaterThan(0);
    }

    [Fact]
    public void Memory_Samples_Carry_Change_And_Gc_Counts()
    {
        using var tracker = new MemoryTracker();
        tracker.StartTracking();

        MemorySample first = tracker.Sample();
        first.HeapSizeChangeSinceLastSample.Should().Be(0L);
        first.GcEventsCount.Should().BeGreaterThanOrEqualTo(0);

        byte[] keep = new byte[256 * 1024];
        GC.KeepAlive(keep);
        MemorySample second = tracker.Sample();
        second.TimestampMs.Should().BeGreaterThanOrEqualTo(first.TimestampMs);
        second.AvailableHeapSizeBytes.Should().BeGreaterThanOrEqualTo(0L);
        tracker.StopTracking();
    }
}
