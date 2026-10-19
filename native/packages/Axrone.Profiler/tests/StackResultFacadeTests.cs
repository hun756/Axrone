namespace Axrone.Profiler.Tests;

public sealed class StackPerfettoTests
{
    [Fact]
    public void CaptureStack_Returns_Managed_Frames()
    {
        StackFrameCapture[] frames = StackCaptureEngine.CaptureStack(depth: 32);
        frames.Should().NotBeEmpty();
        frames.Should().OnlyContain(f => !string.IsNullOrEmpty(f.Function));
        frames.Length.Should().BeLessThanOrEqualTo(32);
    }

    [Fact]
    public void CaptureStackSignature_Is_Stable()
    {
        string first = StackCaptureEngine.CaptureStackSignature();
        string second = StackCaptureEngine.CaptureStackSignature();
        first.Should().Be(second);
        first.Should().StartWith("stack-");
    }

    [Fact]
    public void CaptureStack_Rejects_Bad_Depth()
    {
        Action zero = () => StackCaptureEngine.CaptureStack(0);
        zero.Should().Throw<ArgumentException>();
    }
}

public sealed class PerfettoTests : IDisposable
{
    private readonly PerfettoSerializer _serializer = new();

    public void Dispose()
    {
        _serializer.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void RecordSample_Builds_Trace_Data()
    {
        _serializer.RecordSample("update", durationNs: 1_500_000L, category: "frame");
        _serializer.RecordSample("render", durationNs: 500_000L);

        PerfettoTraceData data = _serializer.GetPerfettoData();
        data.Version.Should().Be("2");
        data.SessionId.Should().Be(_serializer.SessionId);
        data.Events.Should().HaveCount(2);
        data.Events[0].Name.Should().Be("update");
        data.Events[0].Phase.Should().Be(PerfettoPhase.Complete);
        data.Events[0].PhaseLetter.Should().Be('X');
        data.Events[0].DurationNs.Should().Be(1_500_000L);
        data.EndedAtNs.Should().NotBeNull();
    }

    [Fact]
    public void ToJson_Contains_Events_And_Escapes_Names()
    {
        _serializer.RecordSample("up\"date", durationNs: 10L);

        string json = _serializer.ToJson();
        json.Should().Contain("\"version\":\"2\"");
        json.Should().Contain("\"ph\":\"X\"");
        json.Should().Contain("up\\\"date");
    }

    [Fact]
    public void RecordSample_Rejects_Bad_Input()
    {
        Action empty = () => _serializer.RecordSample(string.Empty);
        empty.Should().Throw<ArgumentException>();

        Action negative = () => _serializer.RecordSample("x", durationNs: -1L);
        negative.Should().Throw<ArgumentException>();
    }
}

public sealed class ProfilerFacadeTests : IDisposable
{
    private readonly Profiler _profiler = new(new ProfilerOptions(Mode: ProfilerMode.OnDemand));

    public void Dispose()
    {
        _profiler.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Negative_BufferCapacity_Throws()
    {
        Action negative = () => _ = new Profiler(new ProfilerOptions(BufferCapacity: -1));
        negative.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Lifecycle_Rejects_Double_Start_And_Stop()
    {
        using var profiler = new Profiler();
        profiler.Start();

        Action doubleStart = () => profiler.Start();
        doubleStart.Should().Throw<ProfilerStartupException>();

        profiler.Stop();
        Action doubleStop = () => profiler.Stop();
        doubleStop.Should().Throw<ProfilerShutdownException>();
    }

    [Fact]
    public void Timers_Nest_And_Record_Hierarchy()
    {
        _profiler.Start();
        int frame = _profiler.AddTimer("frame");
        int update = _profiler.AddTimer("update");
        _profiler.EndTimer(update);
        _profiler.EndTimer(frame);
        _profiler.Stop();

        IReadOnlyList<FlameGraphNode> roots = _profiler.GetHierarchy();
        roots.Should().ContainSingle().Which.Name.Should().Be("frame");

        HistogramStatistics stats = _profiler.GetSampledMetrics();
        stats.Count.Should().Be(2);

        ProfilerResult result = _profiler.GetResult();
        result.TotalSamples.Should().Be(2);
        result.FlameGraphs.Should().ContainSingle();
        result.TimelineEvents.Should().NotBeNull();
        result.SessionId.Should().Be(_profiler.SessionId);
    }

    [Fact]
    public void Pause_Resume_And_Cancel_Do_Not_Record()
    {
        _profiler.Start();
        int timer = _profiler.AddTimer("physics");
        _profiler.PauseTimer(timer);
        _profiler.ResumeTimer(timer);
        _profiler.EndTimer(timer);

        int dropped = _profiler.AddTimer("dropped");
        _profiler.CancelTimer(dropped);
        _profiler.Stop();

        _profiler.GetSampledMetrics().Count.Should().Be(1);
        _profiler.GetOverheadMetrics().TimerCount.Should().Be(1);
    }

    [Fact]
    public void Markers_Land_In_Timeline()
    {
        _profiler.Start();
        _profiler.AddMarker("spawn");
        _profiler.AddMarker("boss", parentId: 7);
        _profiler.EndFrame();
        _profiler.Stop();

        IReadOnlyList<TimelineRecordEvent> events = _profiler.GetTimelineRecorder().GetEvents();
        events.Should().Contain(e => e.Message == "marker:spawn");
        events.Should().Contain(e => e.Message == "marker:boss parent:7");
        _profiler.GetOverheadMetrics().MarkerCount.Should().Be(2);
        _profiler.GetOverheadMetrics().OverheadNs.Should().BeGreaterThanOrEqualTo(0L);
    }

    [Fact]
    public void EndTimer_Enforces_Lifo_Order()
    {
        _profiler.Start();
        int outer = _profiler.AddTimer("outer");
        _profiler.AddTimer("inner");

        Action wrong = () => _profiler.EndTimer(outer);
        wrong.Should().Throw<ArgumentException>();
        _profiler.Stop();
    }

    [Fact]
    public void Clear_Resets_Session_State()
    {
        _profiler.Start();
        _profiler.AddTimer("frame");
        _profiler.EndTimer();
        _profiler.AddMarker("x");
        _profiler.Clear();

        _profiler.GetHierarchy().Should().BeEmpty();
        _profiler.GetSampledMetrics().Count.Should().Be(0);
        _profiler.GetTimelineRecorder().Count.Should().Be(0);
        _profiler.GetOverheadMetrics().TimerCount.Should().Be(0);
        _profiler.Stop();
    }

    [Fact]
    public void Marker_Callback_And_Frame_Hook_Run()
    {
        int frames = 0;
        using var profiler = new Profiler(new ProfilerOptions(OnFrameEnd: () => Interlocked.Increment(ref frames)));
        profiler.Start();

        int first = profiler.AddMarkerWithCallback(() => "dynamic");
        int second = profiler.AddMarkerWithCallback(() => "other");
        second.Should().Be(first + 1);
        profiler.EndFrame();

        frames.Should().Be(1);
        profiler.GetTimelineRecorder().GetEvents().Should().Contain(e => e.Message == "marker:dynamic");
        profiler.Stop();

        Action bad = () => profiler.AddMarkerWithCallback(null!);
        bad.Should().Throw<ArgumentNullException>();
    }
}

public sealed class ResultHelperTests
{
    [Fact]
    public void TryExecute_Captures_Success_And_Failure()
    {
        Result<int> ok = ProfilerResults.TryExecute(() => 41 + 1, "PROFILER_ERROR");
        ok.IsSuccess.Should().BeTrue();
        ok.Value.Should().Be(42);

        Result<int> failed = ProfilerResults.TryExecute<int>(() => throw new InvalidOperationException("boom"), "PROFILER_TICK_ERROR");
        failed.IsFailure.Should().BeTrue();
        failed.TopError.Code.Should().Be("PROFILER_TICK_ERROR");
    }

    [Fact]
    public async Task TryExecuteAsync_Captures_Failure()
    {
        async ValueTask<int> Boom()
        {
            await Task.Yield();
            throw new InvalidOperationException("async boom");
        }

        Result<int> failed = await ProfilerResults.TryExecuteAsync<int>(Boom, "PROFILER_ERROR");
        failed.IsFailure.Should().BeTrue();
    }
}
