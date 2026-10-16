namespace Axrone.Audio.Tests;

public sealed class AudioPlaybackRuntimeTests : IDisposable
{
    private readonly CacheAlignedDiagnosticsCounters _counters = new();
    private readonly AudioObservabilityRuntime _observability;
    private readonly AudioClipStore _clips = new(16);
    private readonly AudioBusRegistry _buses;
    private readonly AudioListenerRegistry _listeners;
    private readonly AudioPlaybackRuntime _runtime;
    private readonly List<AudioClipRecord> _records = new();

    public AudioPlaybackRuntimeTests()
    {
        _observability = new AudioObservabilityRuntime(_counters);
        _buses = new AudioBusRegistry(_observability);
        _listeners = new AudioListenerRegistry(_observability);
        _runtime = new AudioPlaybackRuntime(8, _buses, _clips, _listeners, _observability);
    }

    public void Dispose()
    {
        _runtime.Dispose();
        _clips.Dispose();
        foreach (AudioClipRecord record in _records)
        {
            record.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private AudioClipId RegisterSine(float frequency = 440.0f, int sampleRate = 48000, float seconds = 1.0f)
    {
        int frames = (int)(sampleRate * seconds);
        var memory = new NativeAudioMemory(sampleRate, 1, frames);
        Span<float> span = memory.AsSpan();
        for (int i = 0; i < frames; i++)
        {
            span[i] = MathF.Sin(2.0f * MathF.PI * frequency * i / sampleRate);
        }

        var record = new AudioClipRecord(new AudioClipId((uint)(_records.Count + 1)), memory);
        _records.Add(record);
        return _clips.Register(record);
    }

    [Fact]
    public void Mixed_Block_Contains_Signal_Then_Silence()
    {
        AudioClipId clip = RegisterSine();
        AudioPlaybackHandle handle = _runtime.AllocateVoice(
            new AudioSourceId(1U), clip, AudioBusId.Master, new AudioSourcePlayRequest(),
            AudioSpatialization.Default2D, false, 1.0f, 1.0f, 0.0f);
        handle.IsValid.Should().BeTrue();

        Span<float> block = stackalloc float[512];
        _runtime.ProcessGraph(block);

        float energy = 0.0f;
        foreach (float sample in block)
        {
            energy += MathF.Abs(sample);
        }

        energy.Should().BeGreaterThan(0.0f);
        block[0].Should().BeApproximately(block[1], 1e-5f);
    }

    [Fact]
    public void Voice_Ends_AtClipEnd_WhenNotLooping()
    {
        AudioClipId clip = RegisterSine(seconds: 0.001f);
        _runtime.AllocateVoice(
            new AudioSourceId(1U), clip, AudioBusId.Master, new AudioSourcePlayRequest(),
            AudioSpatialization.Default2D, false, 1.0f, 1.0f, 0.0f);

        Span<float> block = stackalloc float[4096];
        _runtime.ProcessGraph(block);

        Span<float> tail = stackalloc float[512];
        _runtime.ProcessGraph(tail);
        tail.ToArray().Should().OnlyContain(v => v == 0.0f);
    }

    [Fact]
    public void Stop_Pause_Resume_Control_Voice()
    {
        AudioClipId clip = RegisterSine();
        AudioPlaybackHandle handle = _runtime.AllocateVoice(
            new AudioSourceId(1U), clip, AudioBusId.Master, new AudioSourcePlayRequest(),
            AudioSpatialization.Default2D, true, 1.0f, 1.0f, 0.0f);

        handle.Pause();
        Span<float> paused = stackalloc float[256];
        _runtime.ProcessGraph(paused);
        paused.ToArray().Should().OnlyContain(v => v == 0.0f);

        handle.Resume();
        Span<float> resumed = stackalloc float[256];
        _runtime.ProcessGraph(resumed);
        resumed.ToArray().Should().Contain(v => v != 0.0f);

        handle.Stop();
        Span<float> stopped = stackalloc float[256];
        _runtime.ProcessGraph(stopped);
        stopped.ToArray().Should().OnlyContain(v => v == 0.0f);
    }

    [Fact]
    public void Far_3D_Voice_Is_Quieter_Than_Near()
    {
        AudioClipId clip = RegisterSine();
        var near = AudioSpatialization.Default3D with { Position = new Vector3(0.0f, 0.0f, -2.0f) };
        var far = AudioSpatialization.Default3D with { Position = new Vector3(0.0f, 0.0f, -200.0f) };

        _runtime.AllocateVoice(new AudioSourceId(1U), clip, AudioBusId.Master, new AudioSourcePlayRequest(), near, true, 1.0f, 1.0f, 0.0f);
        Span<float> nearBlock = stackalloc float[256];
        _runtime.ProcessGraph(nearBlock);

        _runtime.Dispose();
        using var runtime2 = new AudioPlaybackRuntime(8, _buses, _clips, _listeners, _observability);
        runtime2.AllocateVoice(new AudioSourceId(2U), clip, AudioBusId.Master, new AudioSourcePlayRequest(), far, true, 1.0f, 1.0f, 0.0f);
        Span<float> farBlock = stackalloc float[256];
        runtime2.ProcessGraph(farBlock);

        float nearEnergy = 0.0f;
        float farEnergy = 0.0f;
        foreach (float s in nearBlock)
        {
            nearEnergy += MathF.Abs(s);
        }

        foreach (float s in farBlock)
        {
            farEnergy += MathF.Abs(s);
        }

        nearEnergy.Should().BeGreaterThan(farEnergy);
        farEnergy.Should().BeGreaterThan(0.0f);
    }

    [Fact]
    public void Exhausted_Voices_Evict_Quietest()
    {
        AudioClipId clip = RegisterSine();
        using var small = new AudioPlaybackRuntime(2, _buses, _clips, _listeners, _observability);
        small.AllocateVoice(new AudioSourceId(1U), clip, AudioBusId.Master, new AudioSourcePlayRequest(), AudioSpatialization.Default2D, true, 1.0f, 1.0f, 0.0f).IsValid.Should().BeTrue();
        small.AllocateVoice(new AudioSourceId(2U), clip, AudioBusId.Master, new AudioSourcePlayRequest(), AudioSpatialization.Default2D, true, 1.0f, 1.0f, 0.0f).IsValid.Should().BeTrue();
        small.AllocateVoice(new AudioSourceId(3U), clip, AudioBusId.Master, new AudioSourcePlayRequest(), AudioSpatialization.Default2D, true, 1.0f, 1.0f, 0.0f).IsValid.Should().BeTrue();
    }
}
